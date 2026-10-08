using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Stunts.SyntaxFactoryGenerator;

namespace Stunts.Processors
{
    /// <summary>
    /// Rewrites all members so they are implemented through 
    /// the <see cref="BehaviorPipeline"/> field added to the 
    /// class by the <see cref="CSharpStunt"/>.
    /// </summary>
    public class CSharpRewrite : ISyntaxProcessor
    {
        /// <summary>
        /// Applies to <see cref="LanguageNames.CSharp"/> only.
        /// </summary>
        public string Language { get; } = LanguageNames.CSharp;

        /// <summary>
        /// Runs in the third phase of codegen, <see cref="ProcessorPhase.Rewrite"/>.
        /// </summary>
        public ProcessorPhase Phase => ProcessorPhase.Rewrite;

        /// <summary>
        /// Rewrites all members in the document.
        /// </summary>
        public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context)
        {
            var virtualEvents = new HashSet<string>();
            var events = new EventVisitor();
            events.Visit(syntax);
            if (events.Types.Count != 0)
            {
                var semantic = context.Compilation.GetSemanticModel(syntax.SyntaxTree);

                foreach (var symbol in events.Types.Select(type => semantic
                    .GetDeclaredSymbol(type, context.CancellationToken)).OfType<INamedTypeSymbol>())
                {
                    var baseType = symbol.BaseType;
                    while (baseType != null)
                    {
                        foreach (var e in baseType.GetMembers().OfType<IEventSymbol>().Where(e => e.IsVirtual && !e.IsAbstract))
                            virtualEvents.Add(e.Name);

                        baseType = baseType.BaseType;
                    }
                }
            }

            return syntax = new CSharpRewriteVisitor(virtualEvents).Visit(syntax);
        }

        class EventVisitor : CSharpSyntaxWalker
        {
            public List<TypeDeclarationSyntax> Types { get; } = new();

            public override void VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                base.VisitClassDeclaration(node);
                if (node.Members.Any(member => member is EventDeclarationSyntax or EventFieldDeclarationSyntax))
                    Types.Add(node);
            }

            public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
            {
                base.VisitRecordDeclaration(node);
                if (node.Members.Any(member => member is EventDeclarationSyntax or EventFieldDeclarationSyntax))
                    Types.Add(node);
            }
        }

        class CSharpRewriteVisitor : CSharpSyntaxRewriter
        {
            readonly HashSet<string> virtualEvents;
            string targetName = "_target";
            string invocationName = "_invocation";
            static ExpressionSyntax Pipeline => SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression, ThisExpression(), IdentifierName("pipeline"));

            void InvocationNames(IEnumerable<ParameterSyntax> parameters)
            {
                var prefix = LocalPrefix(parameters);
                targetName = prefix + prefix + "target";
                invocationName = prefix + prefix + "invocation";
            }

            static string LocalPrefix(IEnumerable<ParameterSyntax> parameters)
            {
                var prefix = "_";
                while (parameters.Any(parameter => parameter.Identifier.ValueText.StartsWith(prefix, StringComparison.Ordinal)))
                    prefix += "_";
                return prefix;
            }

            public CSharpRewriteVisitor(HashSet<string> virtualEvents) => this.virtualEvents = virtualEvents;

            public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
                => base.VisitClassDeclaration(PromoteEventFields(node));

            public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
                => base.VisitRecordDeclaration(PromoteEventFields(node));

            static TDeclaration PromoteEventFields<TDeclaration>(TDeclaration node)
                where TDeclaration : TypeDeclarationSyntax
            {
                // Turn event fields into event declarations.
                var events = node.ChildNodes().OfType<EventFieldDeclarationSyntax>().ToArray();
                node = node.RemoveNodes(events, SyntaxRemoveOptions.KeepNoTrivia)!;

                return (TDeclaration)node.AddMembers(events
                    .Select(x => EventDeclaration(x.Declaration.Type, x.Declaration.Variables.First().Identifier)
                        .WithModifiers(x.Modifiers))
                    .ToArray());
            }

            public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                if (node.AttributeLists.HasAttribute("CompilerGenerated"))
                    return base.VisitConstructorDeclaration(node);

                InvocationNames(node.ParameterList.Parameters);
                if (node.Body != null)
                    node = node.RemoveNodes(new SyntaxNode[] { node.Body }, SyntaxRemoveOptions.KeepNoTrivia)!;

                var target = LambdaExpression(
                        new[]
                        {
                            Parameter(targetName),
                            Parameter(invocationName)
                        },
                        InvocationExpression(
                            invocationName,
                            "CreateReturn"));

                var method = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), node.Identifier)
                    .WithParameterList(node.ParameterList);
                if (NeedsHold(method))
                {
                    var held = Hold(method, null, LocalPrefix(node.ParameterList.Parameters), target, initializeOutputs: false);
                    node = node.WithBody(held.Body).WithExpressionBody(null).WithSemicolonToken(default);
                    if (held.Modifiers.Any(SyntaxKind.UnsafeKeyword))
                        node = node.AddModifiers(Token(SyntaxKind.UnsafeKeyword));
                    return base.VisitConstructorDeclaration(node);
                }

                var create = CreateMethodInvocation(node.ParameterList.Parameters, target);

                var body = InvocationExpression(
                    Pipeline,
                    nameof(BehaviorPipelineExtensions.Execute),
                    Argument(create));

                node = node.WithExpressionBody(ArrowExpressionClause(body))
                        .WithSemicolon();
                if (node.ParameterList.Parameters.Any(parameter => parameter.HasAnnotation(Annotations.PointerRef)))
                    node = node.AddModifiers(Token(SyntaxKind.UnsafeKeyword));

                return base.VisitConstructorDeclaration(node);
            }

            public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax method)
            {
                if (method.AttributeLists.HasAttribute("CompilerGenerated"))
                    return base.VisitMethodDeclaration(method);

                InvocationNames(method.ParameterList.Parameters);
                var baseCall = GetBaseInvocation(method);
                var prefix = LocalPrefix(method.ParameterList.Parameters);

                if (NeedsHold(method))
                {
                    method = Hold(method, baseCall, prefix, forwardType: ForwardType(method), inaccessible: Inaccessible(method.Modifiers));
                    return base.VisitMethodDeclaration(method);
                }

                if (method.ParameterList.Parameters.Any(x => x.IsRefOut()))
                {
                    var body = Block(
                        // var method = MethodBase.GetCurrentMethod();
                        LocalDeclarationStatement(
                            VariableDeclaration(
                                prefix + prefix + "method",
                                CurrentMethod(method.TypeParameterList))));

                    body = body.AddStatements(
                        // outParam = default;
                        method.ParameterList.Parameters
                            .Where(x => x.IsOut())
                            .Select(x => ExpressionStatement(
                                AssignmentExpression(
                                    x.Identifier,
                                    DefaultLiteralExpression)))
                            .ToArray());

                    var args = RefOutArguments(method, baseCall, prefix);

                    body = body.AddStatements(
                        // var _result = pipeline.Invoke(...)
                        LocalDeclarationStatement(
                            VariableDeclaration(
                                prefix + prefix + "result",
                                InvocationExpression(Pipeline, "Invoke", args))));

                    body = body.AddStatements(
                        // x = _result.Outputs.GetNullable<int>("x");
                        method.ParameterList.Parameters.Where(prm => prm.IsRefOut() && !prm.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)).Select(x => ExpressionStatement(
                            AssignmentExpression(
                                x.Identifier,
                                InvocationExpression(
                                    MemberAccessExpression(
                                        MemberAccessExpression(
                                            prefix + prefix + "result",
                                            nameof(IMethodReturn.Outputs)),
                                    x.Type!.Kind() == SyntaxKind.NullableType ?
                                    GenericName(nameof(ArgumentCollectionExtensions.GetNullable), x.Type!) :
                                    GenericName(nameof(ArgumentCollectionExtensions.Get), x.Type!)),
                                    Argument(
                                        LiteralExpression(x.Identifier.ValueText))))))
                        .ToArray());

                    if (method.ReturnType.IsKind(SyntaxKind.RefType))
                    {
                        method = method
                            .WithExpressionBody(null)
                            .WithSemicolonToken(default)
                            .WithBody(body.AddStatements(
                                // return ref _result.AsRef<T>().Value;
                                ReturnStatement(
                                    RefExpression(
                                        MemberAccessExpression(
                                            InvocationExpression(
                                                prefix + prefix + "result",
                                                GenericName("AsRef", ((RefTypeSyntax)method.ReturnType).Type)),
                                            "Value")))));
                    }
                    else if (!method.ReturnType.IsVoid())
                    {
                        method = method
                            .WithExpressionBody(null)
                            .WithSemicolonToken(default)
                            .WithBody(body.AddStatements(
                                // return (T)_result.GetReturnValue(typeof(T));
                                ReturnStatement(
                                    CastExpression(
                                        method.ReturnType,
                                        InvocationExpression(
                                            prefix + prefix + "result",
                                            nameof(MethodReturnExtensions.GetReturnValue),
                                            Argument(TypeOfExpression(method.ReturnType)))))));
                    }
                    else
                    {
                        method = method
                            .WithExpressionBody(null)
                            .WithSemicolonToken(default)
                            .WithBody(body);
                    }
                }
                else
                {
                    var body = Execute(method.ReturnType, method.ParameterList.Parameters, baseCall, method.TypeParameterList,
                        ForwardedCall(method, baseCall, method.Modifiers, type => SynthesizeMethod(method, type, null)));

                    if (method.ReturnType.IsKind(SyntaxKind.RefType))
                        body = RefExpression(
                            MemberAccessExpression(
                                body,
                                "Value"));

                    method = method
                        .WithBody(null)
                        .WithExpressionBody(ArrowExpressionClause(body))
                        .WithSemicolon();
                }

                return base.VisitMethodDeclaration(method);
            }

            public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
            {
                if (node.AttributeLists.HasAttribute("CompilerGenerated"))
                    return base.VisitPropertyDeclaration(node);

                InvocationNames(Array.Empty<ParameterSyntax>());
                var getter = Accessor(node, SyntaxKind.GetAccessorDeclaration);
                var setter = Accessor(node, SyntaxKind.SetAccessorDeclaration, SyntaxKind.InitAccessorDeclaration);
                var canRead = getter != null || node.ExpressionBody != null;

                if (node.HasAnnotation(Annotations.PointerRef) || node.HasAnnotation(Annotations.StructRef))
                {
                    var accessors = new List<AccessorDeclarationSyntax>();
                    foreach (var accessor in node.AccessorList!.Accessors)
                    {
                        var isGet = accessor.IsKind(SyntaxKind.GetAccessorDeclaration);
                        var method = MethodDeclaration(isGet ? node.Type : PredefinedType(Token(SyntaxKind.VoidKeyword)), "accessor")
                            .WithParameterList(ParameterList(isGet ? default : SingletonSeparatedList(
                                Parameter("value", node.Type).WithAdditionalAnnotations(
                                    node.HasAnnotation(Annotations.PointerRef) ? Annotations.PointerRef : Annotations.StructRef))));
                        if (isGet)
                            method = method.WithAdditionalAnnotations(node.HasAnnotation(Annotations.PointerRef) ? Annotations.PointerRef : Annotations.StructRef);
                        var baseCall = GetBaseCall(node, accessor.Kind());
                        if (!isGet && baseCall is AssignmentExpressionSyntax assignment)
                            baseCall = assignment.WithRight(node.HasAnnotation(Annotations.PointerRef)
                                ? CastExpression(node.Type, MemberAccessExpression(IdentifierName(HolderName("value", "_")), "Value"))
                                : MemberAccessExpression(IdentifierName(HolderName("value", "_")), "Value"));
                        var rewritten = Hold(method, baseCall, "_",
                            forwardType: ForwardType(node),
                            inaccessible: Inaccessible(EffectiveModifiers(node.Modifiers, accessor.Modifiers)));
                        accessors.Add(accessor.WithExpressionBody(null).WithSemicolonToken(default).WithBody(rewritten.Body));
                    }
                    return node.AddModifiers(Token(SyntaxKind.UnsafeKeyword)).WithAccessorList(AccessorList(List(accessors)));
                }

                var prop = node;

                if (node.ExpressionBody != null)
                    node = node.RemoveNode(node.ExpressionBody, SyntaxRemoveOptions.KeepNoTrivia)!;

                node = node.WithAccessorList(null);

                // A get-only property becomes an expression body, which cannot carry accessor attributes.
                if (canRead && setter == null && getter?.AttributeLists.Count is not > 0)
                {
                    var baseCall = GetBaseCall(prop, SyntaxKind.GetAccessorDeclaration);
                    node = node
                        .WithExpressionBody(ArrowExpressionClause(Executed(
                            node.Type, Enumerable.Empty<ParameterSyntax>(), baseCall, forwarded: ForwardedCall(
                                prop, baseCall, EffectiveModifiers(prop.Modifiers, getter?.Modifiers ?? default),
                                type => SynthesizeProperty(prop, type, null)))))
                        .WithSemicolon();
                }
                else
                {
                    if (getter != null)
                    {
                        var baseCall = GetBaseCall(prop, SyntaxKind.GetAccessorDeclaration);
                        node = node.AddAccessorListAccessors(WithBody(getter, Executed(
                            node.Type, Enumerable.Empty<ParameterSyntax>(), baseCall, forwarded: ForwardedCall(
                                prop, baseCall, EffectiveModifiers(prop.Modifiers, getter.Modifiers),
                                type => SynthesizeProperty(prop, type, null)))));
                    }
                    if (setter != null)
                    {
                        var baseCall = (AssignmentExpressionSyntax?)GetBaseCall(prop, setter.Kind());
                        // We must use the value in the invocation arguments received from the pipeline for the setter
                        // => base.Prop = invocation.Arguments.Get<T>();
                        if (baseCall != null)
                            baseCall = baseCall.WithRight(PipelineValue(node.Type, "value"));

                        node = node.AddAccessorListAccessors(WithBody(setter,
                            // NOTE: we always append the implicit "value" parameter for setters.
                            Execute(null, new[] { Parameter(Identifier("value")).WithType(node.Type) }, baseCall, forwarded: ForwardedCall(
                                prop, baseCall, EffectiveModifiers(prop.Modifiers, setter.Modifiers),
                                type => SynthesizeProperty(prop, type, PipelineValue(node.Type, "value"))))));
                    }
                }

                return base.VisitPropertyDeclaration(node);
            }

            public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
            {
                if (node.AttributeLists.HasAttribute("CompilerGenerated"))
                    return base.VisitIndexerDeclaration(node);

                InvocationNames(node.ParameterList.Parameters);
                var trivia = node.GetTrailingTrivia();

                // NOTE: Most of this code could be shared with VisitPropertyDeclaration but the mutating With* 
                // and props like ExpressionBody aren't available in the shared base BasePropertyDeclarationSyntax type :(

                var getter = Accessor(node, SyntaxKind.GetAccessorDeclaration);
                var setter = Accessor(node, SyntaxKind.SetAccessorDeclaration, SyntaxKind.InitAccessorDeclaration);
                var canRead = getter != null || node.ExpressionBody != null;

                var prop = node;

                if (node.ExpressionBody != null)
                    node = node.RemoveNode(node.ExpressionBody, SyntaxRemoveOptions.KeepNoTrivia)!;

                node = node.WithAccessorList(null);

                if (canRead && setter == null && getter?.AttributeLists.Count is not > 0)
                {
                    var baseCall = FixBaseCall(prop, (ElementAccessExpressionSyntax?)GetBaseCall(prop, SyntaxKind.GetAccessorDeclaration));
                    return node.WithExpressionBody(
                        ArrowExpressionClause(
                            Executed(
                                node.Type, node.ParameterList.Parameters, baseCall, forwarded: ForwardedCall(
                                    prop, baseCall, EffectiveModifiers(prop.Modifiers, getter?.Modifiers ?? default),
                                    type => SynthesizeIndexer(prop, type, null)))))
                        .WithSemicolon();
                }
                else
                {
                    if (getter != null)
                    {
                        var baseCall = FixBaseCall(prop, (ElementAccessExpressionSyntax?)GetBaseCall(prop, SyntaxKind.GetAccessorDeclaration));
                        node = node.AddAccessorListAccessors(WithBody(getter, Executed(
                            node.Type, node.ParameterList.Parameters, baseCall, forwarded: ForwardedCall(
                                prop, baseCall, EffectiveModifiers(prop.Modifiers, getter.Modifiers),
                                type => SynthesizeIndexer(prop, type, null)))));
                    }

                    if (setter != null)
                    {
                        var baseCall = (AssignmentExpressionSyntax?)GetBaseCall(prop, setter.Kind());
                        // Replace base indexer call args with references to pipeline invocation args
                        if (baseCall != null)
                            baseCall = baseCall
                                .WithLeft(FixBaseCall(prop, (ElementAccessExpressionSyntax)baseCall.Left)!)
                                .WithRight(PipelineValue(node.Type, "value"));

                        node = node.AddAccessorListAccessors(WithBody(setter,
                            Execute(null, node.ParameterList.Parameters.Concat(new[] { Parameter(Identifier("value")).WithType(node.Type) }),
                            baseCall, forwarded: ForwardedCall(
                                prop, baseCall, EffectiveModifiers(prop.Modifiers, setter.Modifiers),
                                type => SynthesizeIndexer(prop, type, PipelineValue(node.Type, "value"))))));
                    }
                }

                return base.VisitIndexerDeclaration(node.WithTrailingTrivia(trivia));
            }

            public override SyntaxNode? VisitEventDeclaration(EventDeclarationSyntax node)
            {
                if (node.AttributeLists.HasAttribute("CompilerGenerated"))
                    return base.VisitEventDeclaration(node);

                var value = Parameter("value", node.Type);
                var parameters = new[] { value };

                var defaultAdd = GetDefaultCall(node.AccessorList?.Accessors.FirstOrDefault(x => x.IsKind(SyntaxKind.AddAccessorDeclaration)));
                var defaultRemove = GetDefaultCall(node.AccessorList?.Accessors.FirstOrDefault(x => x.IsKind(SyntaxKind.RemoveAccessorDeclaration)));

                if (defaultAdd != null && defaultRemove != null)
                {
                    node = node.WithAccessorList(AccessorList(List(new AccessorDeclarationSyntax[]
                    {
                        EventAccessor(node.AccessorList, SyntaxKind.AddAccessorDeclaration, ArrowExpressionClause(
                            Execute(null, parameters, defaultAdd, forwarded: ForwardedCall(node, defaultAdd, node.Modifiers, null)))),
                        EventAccessor(node.AccessorList, SyntaxKind.RemoveAccessorDeclaration, ArrowExpressionClause(
                            Execute(null, parameters, defaultRemove, forwarded: ForwardedCall(node, defaultRemove, node.Modifiers, null)))),
                    })));
                }
                else if (virtualEvents.Contains(node.Identifier.ValueText))
                {
                    ArrowExpressionClauseSyntax body(SyntaxKind kind)
                    {
                        var assignment = AssignmentExpression(
                            kind,
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                BaseExpression(),
                                IdentifierName(node.Identifier)),
                            IdentifierName("value"));
                        return ArrowExpressionClause(Execute(null, parameters!, assignment,
                            forwarded: ForwardedCall(node, assignment, node.Modifiers, null)));
                    }

                    var add = body(SyntaxKind.AddAssignmentExpression);
                    var remove = body(SyntaxKind.SubtractAssignmentExpression);

                    node = node.WithAccessorList(AccessorList(List(new AccessorDeclarationSyntax[]
                    {
                        EventAccessor(node.AccessorList, SyntaxKind.AddAccessorDeclaration, add),
                        EventAccessor(node.AccessorList, SyntaxKind.RemoveAccessorDeclaration, remove),
                    })));
                }
                else
                {
                    ExpressionSyntax AccessorBody(SyntaxKind kind)
                    {
                        var forwarded = ForwardedCall(node, null, node.Modifiers, type => SynthesizeEvent(node, type, kind));
                        return forwarded == null
                            ? CreatePipelineInvocation(null, parameters)
                            : Execute(null, parameters, forwarded: forwarded);
                    }

                    node = node.WithAccessorList(AccessorList(List(new AccessorDeclarationSyntax[]
                    {
                        EventAccessor(node.AccessorList, SyntaxKind.AddAccessorDeclaration, ArrowExpressionClause(AccessorBody(SyntaxKind.AddAssignmentExpression))),
                        EventAccessor(node.AccessorList, SyntaxKind.RemoveAccessorDeclaration, ArrowExpressionClause(AccessorBody(SyntaxKind.SubtractAssignmentExpression))),
                    })));
                }

                return base.VisitEventDeclaration(node);
            }

            static AccessorDeclarationSyntax? Accessor(BasePropertyDeclarationSyntax node, params SyntaxKind[] kinds)
                => node.AccessorList?.Accessors.FirstOrDefault(accessor => kinds.Contains(accessor.Kind()));

            // Keeps a narrowed accessor (protected set, internal get) and its attributes on the override.
            static AccessorDeclarationSyntax WithBody(AccessorDeclarationSyntax accessor, ExpressionSyntax body)
                => AccessorDeclaration(accessor.Kind())
                    .WithAttributeLists(accessor.AttributeLists)
                    .WithModifiers(accessor.Modifiers)
                    .WithExpressionBody(ArrowExpressionClause(body))
                    .WithSemicolon();

            static AccessorDeclarationSyntax EventAccessor(AccessorListSyntax? list, SyntaxKind kind, ArrowExpressionClauseSyntax body)
            {
                var accessor = AccessorDeclaration(kind)
                    .WithExpressionBody(body)
                    .WithSemicolon();
                var source = list?.Accessors.FirstOrDefault(candidate => candidate.IsKind(kind));
                return source == null || source.AttributeLists.Count == 0
                    ? accessor
                    : accessor.WithAttributeLists(source.AttributeLists);
            }

            ElementAccessExpressionSyntax? FixBaseCall(IndexerDeclarationSyntax indexer, ElementAccessExpressionSyntax? baseCall)
                // Replace base indexer call args with references to pipeline invocation args
                => baseCall?.WithArgumentList(
                    indexer.ParameterList.Parameters.Select(prm =>
                        Argument(
                            InvocationExpression(
                                MemberAccessExpression(
                                    MemberAccessExpression(invocationName, nameof(IMethodInvocation.Arguments)),
                                    prm.Type!.Kind() == SyntaxKind.NullableType ?
                                    GenericName(nameof(ArgumentCollectionExtensions.GetNullable), prm.Type) :
                                    GenericName(nameof(ArgumentCollectionExtensions.Get), prm.Type!)),
                                Argument(
                                    Literal(prm.Identifier.ValueText))))));

            static ExpressionSyntax? GetBaseCall(BasePropertyDeclarationSyntax node, SyntaxKind kind)
            {
                if (node.AccessorList == null)
                    return null;

                var accessor = node.DescendantNodes().OfType<AccessorDeclarationSyntax>().FirstOrDefault(x => x.IsKind(kind));
                if (accessor == null)
                    return null;

                if (GetDefaultCall(accessor) is ExpressionSyntax defaultCall)
                    return defaultCall;

                if (!node.Modifiers.Any(SyntaxKind.OverrideKeyword))
                    return null;

                var baseCall = accessor
                    .DescendantNodes()
                    .OfType<BaseExpressionSyntax>()
                    .Select(x => x.Parent).FirstOrDefault();

                // In the setter case, we'll want the parent of the member access, that 
                // is, the entire assignment expression.
                if (kind == SyntaxKind.SetAccessorDeclaration)
                    baseCall = baseCall?.Parent;

                return (ExpressionSyntax?)baseCall;
            }

            static InvocationExpressionSyntax? GetBaseInvocation(SyntaxNode? syntax)
                => syntax?.DescendantNodes().OfType<InvocationExpressionSyntax>().FirstOrDefault(i =>
                        i.HasAnnotation(Annotations.DefaultImplementation) ||
                        i.DescendantNodes().OfType<BaseExpressionSyntax>().Any());

            // The scaffold flags calls to a default interface implementation, which 
            // proceed like base calls do for class members.
            static ExpressionSyntax? GetDefaultCall(SyntaxNode? syntax)
                => syntax?.DescendantNodes().OfType<ExpressionSyntax>().FirstOrDefault(x =>
                        x.HasAnnotation(Annotations.DefaultImplementation));

            ExpressionSyntax Execute(TypeSyntax? returnType, IEnumerable<ParameterSyntax> parameters, ExpressionSyntax? baseCall = null, TypeParameterListSyntax? typeParameters = null, ExpressionSyntax? forwarded = null)
            {
                if (baseCall == null && forwarded == null)
                    return CreatePipelineInvocation(returnType.IsVoid() ? null : returnType, parameters, typeParameters: typeParameters);

                if (forwarded == null)
                {
                    if (!returnType.IsVoid())
                        return CreatePipelineInvocation(returnType, parameters,
                            LambdaExpression(
                                new[]
                                {
                                    Parameter(targetName),
                                    Parameter(invocationName)
                                },
                                InvocationExpression(
                                    invocationName,
                                    "CreateValueReturn",
                                    Argument(baseCall!))),
                            typeParameters);

                    return CreatePipelineInvocation(null, parameters,
                            LambdaExpression(
                                new[]
                                {
                                    Parameter(targetName),
                                    Parameter(invocationName)
                                },
                                ExpressionStatement(baseCall!),
                                ReturnStatement(
                                    InvocationExpression(
                                        invocationName,
                                        "CreateReturn"))),
                            typeParameters);
                }

                return CreatePipelineInvocation(returnType.IsVoid() ? null : returnType, parameters,
                    Branch(returnType, baseCall, forwarded), typeParameters);
            }

            InvocationExpressionSyntax CreatePipelineInvocation(TypeSyntax? returnType, IEnumerable<ParameterSyntax> parameters, LambdaExpressionSyntax? target = null, TypeParameterListSyntax? typeParameters = null)
            {
                SimpleNameSyntax execute = returnType.IsVoid() ?
                    IdentifierName("Execute") :
                    returnType.IsKind(SyntaxKind.RefType) ?
                    GenericName("ExecuteRef", ((RefTypeSyntax)returnType).Type) :
                    GenericName("Execute", returnType!);

                var create = CreateMethodInvocation(parameters, target, typeParameters);

                return InvocationExpression(
                        Pipeline,
                        execute,
                        Argument(create));
            }

            // GetCurrentMethod returns the generic method definition inside a generic method,
            // whose parameter types are the open type parameters.
            // => ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(typeof(T), ...)
            static ExpressionSyntax CurrentMethod(TypeParameterListSyntax? typeParameters)
            {
                var current = InvocationExpression(
                    nameof(MethodBase),
                    nameof(MethodBase.GetCurrentMethod));

                if (typeParameters == null || typeParameters.Parameters.Count == 0)
                    return current;

                return InvocationExpression(
                    ParenthesizedExpression(CastExpression(IdentifierName(nameof(MethodInfo)), current)),
                    nameof(MethodInfo.MakeGenericMethod),
                    typeParameters.Parameters
                        .Select(x => Argument(TypeOfExpression(IdentifierName(x.Identifier))))
                        .ToArray());
            }

            static ExpressionSyntax CreateMethodInvocation(IEnumerable<ParameterSyntax> parameters, LambdaExpressionSyntax? target = null, TypeParameterListSyntax? typeParameters = null)
                => CreateInvocation(parameters.ToArray(), parameters.Select(parameter =>
                    parameter.HasAnnotation(Annotations.PointerRef) || parameter.HasAnnotation(Annotations.StructRef)
                        ? CreateHolder(parameter) : (ExpressionSyntax)IdentifierName(parameter.Identifier)).ToArray(), target, typeParameters);

            static ExpressionSyntax CreateInvocation(IReadOnlyList<ParameterSyntax> parameters, IReadOnlyList<ExpressionSyntax> values, LambdaExpressionSyntax? target, TypeParameterListSyntax? typeParameters)
            {
                var method = CurrentMethod(typeParameters);
                var arguments = new List<ArgumentSyntax>
                {
                    Argument(ThisExpression()),
                    Argument(method)
                };

                if (target != null)
                    arguments.Add(Argument(target));

                if (parameters.Count > 16)
                {
                    arguments.Add(Argument(ArgumentValues(parameters, values, method)));
                    return ObjectCreationExpression(nameof(MethodInvocation), arguments);
                }

                arguments.AddRange(values.Select(value => SyntaxFactory.Argument(value)));

                SimpleNameSyntax factory = parameters.Count == 0 ? IdentifierName(nameof(MethodInvocation.Create)) :
                    GenericName(nameof(MethodInvocation.Create), parameters.Select(parameter =>
                        parameter.HasAnnotation(Annotations.StructRef) || parameter.HasAnnotation(Annotations.PointerRef)
                            ? HolderType(parameter.Type!) : parameter.Type!));
                return InvocationExpression(IdentifierName(nameof(MethodInvocation)), factory, arguments.ToArray());
            }

            static ExpressionSyntax ArgumentValues(IReadOnlyList<ParameterSyntax> parameters, IReadOnlyList<ExpressionSyntax> values, ExpressionSyntax method)
                => ObjectCreationExpression(nameof(ArgumentCollection), Argument(InvocationExpression(method, nameof(MethodBase.GetParameters))))
                    .WithInitializer(InitializerExpression(SyntaxKind.CollectionInitializerExpression,
                        parameters.Select((parameter, index) => InitializerExpression(SyntaxKind.ComplexElementInitializerExpression,
                            LiteralExpression(parameter.Identifier.ValueText), values[index]))));

            ExpressionSyntax Executed(TypeSyntax type, IEnumerable<ParameterSyntax> parameters, ExpressionSyntax? baseCall, TypeParameterListSyntax? typeParameters = null, ExpressionSyntax? forwarded = null)
            {
                var body = Execute(type, parameters, baseCall, typeParameters, forwarded);
                return type is RefTypeSyntax ? RefExpression(MemberAccessExpression(body, "Value")) : body;
            }

            static bool NeedsHold(MethodDeclarationSyntax method)
                => method.HasAnnotation(Annotations.StructRef) || method.HasAnnotation(Annotations.PointerRef)
                    || method.ParameterList.Parameters.Any(parameter =>
                        parameter.HasAnnotation(Annotations.StructRef) || parameter.HasAnnotation(Annotations.PointerRef) ||
                        parameter.Modifiers.Any(SyntaxKind.InKeyword));

            MethodDeclarationSyntax Hold(MethodDeclarationSyntax method, ExpressionSyntax? baseCall, string prefix,
                LambdaExpressionSyntax? target = null, bool initializeOutputs = true, TypeSyntax? forwardType = null, bool inaccessible = false)
            {
                var parameters = method.ParameterList.Parameters;
                var statements = new List<StatementSyntax>();
                var finallyStatements = new List<StatementSyntax>();
                if (initializeOutputs)
                    statements.AddRange(parameters.Where(parameter => parameter.IsOut()).Select(parameter =>
                        ExpressionStatement(AssignmentExpression(IdentifierName(parameter.Identifier), DefaultLiteralExpression))));
                var hasOutputs = parameters.Any(parameter => parameter.IsRefOut() && !parameter.Modifiers.Any(SyntaxKind.ReadOnlyKeyword));
                foreach (var parameter in parameters)
                {
                    if (!parameter.HasAnnotation(Annotations.StructRef) && !parameter.HasAnnotation(Annotations.PointerRef))
                        continue;

                    var name = HolderName(parameter.Identifier.ValueText, prefix);
                    statements.Add(LocalDeclarationStatement(VariableDeclaration(name, CreateHolder(parameter))));
                    if (parameter.HasAnnotation(Annotations.StructRef))
                    {
                        finallyStatements.Add(ExpressionStatement(InvocationExpression(
                            IdentifierName(name),
                            IdentifierName("Invalidate"),
                            RefSlot(parameter))));
                    }
                }

                var returnType = method.ReturnType;
                var innerReturn = returnType is RefTypeSyntax refType ? refType.Type : returnType;
                var returnIsStruct = method.HasAnnotation(Annotations.StructRef);
                var returnIsPointer = method.HasAnnotation(Annotations.PointerRef);
                var returned = prefix + prefix + "returned";
                var returnedRef = prefix + prefix + "returnedRef";
                if (returnIsStruct)
                {
                    statements.Add(LocalDeclarationStatement(VariableDeclaration(returned, innerReturn, DefaultLiteralExpression)));
                    statements.Add(LocalDeclarationStatement(VariableDeclaration(
                        returnedRef,
                        ObjectCreationExpression(
                            HolderType(innerReturn),
                            Argument(IdentifierName(returned)).WithRefKindKeyword(Token(SyntaxKind.RefKeyword))))));
                    finallyStatements.Add(ExpressionStatement(InvocationExpression(
                        IdentifierName(returnedRef),
                        IdentifierName("Invalidate"),
                        Argument(IdentifierName(returned)).WithRefKindKeyword(Token(SyntaxKind.RefKeyword)))));
                }

                if (baseCall is InvocationExpressionSyntax invocation)
                    baseCall = RewriteCall(invocation, parameters, prefix);

                ExpressionSyntax? forwarded = null;
                if (forwardType != null && baseCall != null)
                    forwarded = inaccessible ? UnsupportedForward() : Retarget(baseCall, CastTarget(forwardType));

                var create = CreateInvocation(
                    parameters.ToArray(),
                    parameters.Select(parameter => (ExpressionSyntax)IdentifierName(
                        parameter.HasAnnotation(Annotations.StructRef) || parameter.HasAnnotation(Annotations.PointerRef)
                            ? HolderName(parameter.Identifier.ValueText, prefix)
                            : parameter.Identifier.Text)).ToArray(),
                    target ?? Target(baseCall, returnType, returnIsStruct, returnIsPointer, returnedRef, parameters.ToArray(), prefix, forwarded),
                    method.TypeParameterList);

                ExpressionSyntax pipeline = returnIsStruct || returnIsPointer || returnType.IsVoid()
                    ? InvocationExpression(Pipeline, IdentifierName("Execute"), Argument(create))
                    : returnType is RefTypeSyntax refReturn
                        ? InvocationExpression(Pipeline, GenericName("ExecuteRef", refReturn.Type), Argument(create))
                        : InvocationExpression(Pipeline, GenericName("Execute", returnType), Argument(create));

                var attempt = new List<StatementSyntax>();
                if (hasOutputs)
                {
                    attempt.Add(LocalDeclarationStatement(VariableDeclaration(prefix + prefix + "outputs",
                        InvocationExpression(Pipeline, "Invoke", Argument(create), Argument(TrueLiteralExpression)))));
                    foreach (var parameter in parameters.Where(parameter => parameter.IsRefOut() &&
                        !parameter.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) && !parameter.HasAnnotation(Annotations.StructRef)))
                    {
                        ExpressionSyntax output = parameter.HasAnnotation(Annotations.PointerRef)
                            ? CastExpression(parameter.Type!, MemberAccessExpression(
                                InvocationExpression(MemberAccessExpression(prefix + prefix + "outputs", nameof(IMethodReturn.Outputs)),
                                    GenericName(nameof(ArgumentCollectionExtensions.Get), IdentifierName("PointerRef")),
                                    Argument(LiteralExpression(parameter.Identifier.ValueText))), "Value"))
                            : InvocationExpression(MemberAccessExpression(prefix + prefix + "outputs", nameof(IMethodReturn.Outputs)),
                                GenericName(parameter.Type!.Kind() == SyntaxKind.NullableType
                                    ? nameof(ArgumentCollectionExtensions.GetNullable) : nameof(ArgumentCollectionExtensions.Get), parameter.Type!),
                                Argument(LiteralExpression(parameter.Identifier.ValueText)));
                        attempt.Add(ExpressionStatement(AssignmentExpression(IdentifierName(parameter.Identifier), output)));
                    }
                    pipeline = IdentifierName(prefix + prefix + "outputs");
                }
                if (returnType.IsVoid())
                {
                    if (!hasOutputs)
                        attempt.Add(ExpressionStatement(pipeline));
                }
                else if (returnIsStruct)
                {
                    attempt.Add(LocalDeclarationStatement(VariableDeclaration(prefix + prefix + "result", pipeline)));
                    attempt.Add(ExpressionStatement(AssignmentExpression(
                        IdentifierName(returned),
                        MemberAccessExpression(
                            ParenthesizedExpression(CastExpression(
                                HolderType(innerReturn),
                                PostfixUnaryExpression(
                                    SyntaxKind.SuppressNullableWarningExpression,
                                    InvocationExpression(
                                        prefix + prefix + "result",
                                        nameof(MethodReturnExtensions.GetReturnValue),
                                        Argument(TypeOfExpression(innerReturn)))))),
                            "Value"))));
                    attempt.Add(ReturnStatement(IdentifierName(returned)));
                }
                else if (returnIsPointer)
                {
                    attempt.Add(LocalDeclarationStatement(VariableDeclaration(prefix + prefix + "result", pipeline)));
                    attempt.Add(ReturnStatement(CastExpression(
                        innerReturn,
                        MemberAccessExpression(
                            ParenthesizedExpression(CastExpression(
                                IdentifierName("PointerRef"),
                                PostfixUnaryExpression(
                                    SyntaxKind.SuppressNullableWarningExpression,
                                    InvocationExpression(
                                        prefix + prefix + "result",
                                        nameof(MethodReturnExtensions.GetReturnValue),
                                        Argument(TypeOfExpression(innerReturn)))))),
                            "Value"))));
                }
                else if (returnType is RefTypeSyntax refReturnType)
                    attempt.Add(ReturnStatement(RefExpression(MemberAccessExpression(hasOutputs
                        ? InvocationExpression(pipeline, GenericName("AsRef", refReturnType.Type))
                        : pipeline, "Value"))));
                else
                    attempt.Add(ReturnStatement(hasOutputs
                        ? CastExpression(returnType, InvocationExpression(pipeline, nameof(MethodReturnExtensions.GetReturnValue),
                            Argument(TypeOfExpression(returnType)))) : pipeline));

                if (finallyStatements.Count == 0)
                    statements.AddRange(attempt);
                else
                    statements.Add(TryStatement(Block(attempt), default, FinallyClause(Block(finallyStatements))));

                if (returnIsPointer || parameters.Any(parameter => parameter.HasAnnotation(Annotations.PointerRef)))
                    method = method.AddModifiers(Token(SyntaxKind.UnsafeKeyword));

                return method
                    .WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithBody(Block(statements));
            }

            LambdaExpressionSyntax? Target(ExpressionSyntax? baseCall, TypeSyntax returnType, bool returnIsStruct, bool returnIsPointer, string returnedRef,
                IReadOnlyList<ParameterSyntax> signature, string prefix, ExpressionSyntax? forwarded = null)
            {
                if (baseCall == null)
                    return null;

                var body = new List<StatementSyntax>();
                foreach (var parameter in signature.Where(parameter => CapturedByRef(parameter) && !parameter.HasAnnotation(Annotations.StructRef)))
                {
                    ExpressionSyntax initial = parameter.HasAnnotation(Annotations.PointerRef)
                        ? CastExpression(parameter.Type!, MemberAccessExpression(IdentifierName(HolderName(parameter.Identifier.ValueText, prefix)), "Value"))
                        : InvocationExpression(MemberAccessExpression(invocationName, nameof(IMethodInvocation.Arguments)),
                            GenericName(parameter.Type!.Kind() == SyntaxKind.NullableType
                                ? nameof(ArgumentCollectionExtensions.GetNullable) : nameof(ArgumentCollectionExtensions.Get), parameter.Type!),
                            Argument(LiteralExpression(parameter.Identifier.ValueText)));
                    body.Add(LocalDeclarationStatement(VariableDeclaration(prefix + parameter.Identifier.ValueText, initial)));
                }

                ExpressionSyntax value = NullLiteralExpression;
                if (forwarded == null)
                {
                    if (returnIsStruct)
                    {
                        body.Add(ExpressionStatement(AssignmentExpression(MemberAccessExpression(IdentifierName(returnedRef), "Value"), baseCall)));
                        value = IdentifierName(returnedRef);
                    }
                    else if (returnType.IsVoid())
                        body.Add(ExpressionStatement(baseCall));
                    else
                    {
                        value = returnIsPointer
                            ? ObjectCreationExpression(IdentifierName("PointerRef"), Argument(CastExpression(
                                PointerType(PredefinedType(Token(SyntaxKind.VoidKeyword))), baseCall)))
                            : baseCall;
                        body.Add(LocalDeclarationStatement(VariableDeclaration(prefix + prefix + "value", value)));
                        value = IdentifierName(prefix + prefix + "value");
                    }
                }
                else
                {
                    StatementSyntax InvokeOn(ExpressionSyntax call)
                    {
                        if (IsUnsupported(call))
                            return ThrowUnsupported();
                        if (returnIsStruct)
                            return ExpressionStatement(AssignmentExpression(MemberAccessExpression(IdentifierName(returnedRef), "Value"), call));
                        if (returnType.IsVoid())
                            return ExpressionStatement(call);

                        ExpressionSyntax stored = returnIsPointer
                            ? ObjectCreationExpression(IdentifierName("PointerRef"), Argument(CastExpression(
                                PointerType(PredefinedType(Token(SyntaxKind.VoidKeyword))), call)))
                            : call;
                        return ExpressionStatement(AssignmentExpression(IdentifierName(prefix + prefix + "value"), stored));
                    }

                    if (returnIsStruct)
                        value = IdentifierName(returnedRef);
                    else if (!returnType.IsVoid())
                    {
                        TypeSyntax valueType = returnIsPointer ? IdentifierName("PointerRef") :
                            returnType is RefTypeSyntax refType ? refType.Type : returnType;
                        body.Add(LocalDeclarationStatement(VariableDeclaration(prefix + prefix + "value", valueType, null)));
                        value = IdentifierName(prefix + prefix + "value");
                    }

                    body.Add(IfStatement(IsStuntTarget(), InvokeOn(baseCall), ElseClause(InvokeOn(forwarded))));
                }

                foreach (var parameter in signature.Where(parameter => CapturedByRef(parameter) && parameter.HasAnnotation(Annotations.PointerRef)))
                    body.Add(ExpressionStatement(AssignmentExpression(
                        MemberAccessExpression(IdentifierName(HolderName(parameter.Identifier.ValueText, prefix)), "Value"),
                        CastExpression(PointerType(PredefinedType(Token(SyntaxKind.VoidKeyword))), IdentifierName(prefix + parameter.Identifier.ValueText)))));

                var outputs = ArgumentValues(signature, signature.Select(parameter =>
                    parameter.HasAnnotation(Annotations.StructRef) || parameter.HasAnnotation(Annotations.PointerRef)
                        ? (ExpressionSyntax)IdentifierName(HolderName(parameter.Identifier.ValueText, prefix))
                        : IdentifierName(CapturedByRef(parameter) ? prefix + parameter.Identifier.ValueText : parameter.Identifier.Text)).ToArray(),
                    MemberAccessExpression(invocationName, nameof(IMethodInvocation.MethodBase)));
                body.Add(ReturnStatement(InvocationExpression(invocationName, "CreateValueReturn", Argument(value), Argument(outputs))));
                return LambdaExpression(new[] { Parameter(targetName), Parameter(invocationName) }, body);
            }

            static bool CapturedByRef(ParameterSyntax parameter)
                => parameter.IsRefOut() || parameter.Modifiers.Any(SyntaxKind.InKeyword);

            static string HolderName(string parameter, string prefix)
                => prefix + prefix + "argument_" + parameter;

            static ExpressionSyntax CreateHolder(ParameterSyntax parameter)
            {
                if (parameter.HasAnnotation(Annotations.PointerRef))
                {
                    return ObjectCreationExpression(
                        IdentifierName("PointerRef"),
                        Argument(CastExpression(
                            PointerType(PredefinedType(Token(SyntaxKind.VoidKeyword))),
                            IdentifierName(parameter.Identifier))));
                }

                return ObjectCreationExpression(
                    HolderType(parameter.Type!),
                    RefSlot(parameter));
            }

            static ArgumentSyntax RefSlot(ParameterSyntax parameter)
            {
                ExpressionSyntax target = IdentifierName(parameter.Identifier);
                if (parameter.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) || parameter.Modifiers.Any(SyntaxKind.InKeyword))
                {
                    target = InvocationExpression(
                        MemberAccessExpression(ParseName("global::System.Runtime.CompilerServices.Unsafe"), IdentifierName("AsRef")),
                        Argument(target).WithRefKindKeyword(Token(SyntaxKind.InKeyword)));
                }

                return Argument(target).WithRefKindKeyword(Token(SyntaxKind.RefKeyword));
            }

            static TypeSyntax HolderType(TypeSyntax type)
            {
                var inner = type is RefTypeSyntax refType ? refType.Type : type;
                if (TrySpan(inner, "Span", out var argument))
                    return GenericName("SpanRef", argument);
                if (TrySpan(inner, "ReadOnlySpan", out argument))
                    return GenericName("ReadOnlySpanRef", argument);
                if (inner is PointerTypeSyntax || inner is FunctionPointerTypeSyntax)
                    return IdentifierName("PointerRef");
                return GenericName("StructRef", inner);
            }

            static bool TrySpan(TypeSyntax type, string name, out TypeSyntax argument)
            {
                switch (type)
                {
                    case GenericNameSyntax generic when generic.Identifier.ValueText == name && generic.TypeArgumentList.Arguments.Count == 1:
                        argument = generic.TypeArgumentList.Arguments[0];
                        return true;
                    case QualifiedNameSyntax qualified:
                        return TrySpan(qualified.Right, name, out argument);
                    case AliasQualifiedNameSyntax alias:
                        return TrySpan(alias.Name, name, out argument);
                    default:
                        argument = type;
                        return false;
                }
            }

            static InvocationExpressionSyntax RewriteCall(InvocationExpressionSyntax call, SeparatedSyntaxList<ParameterSyntax> parameters, string prefix)
            {
                var args = call.ArgumentList.Arguments;
                var rewritten = new List<ArgumentSyntax>(args.Count);
                for (var i = 0; i < args.Count; i++)
                {
                    var arg = args[i];
                    if (i >= parameters.Count)
                    {
                        rewritten.Add(arg);
                        continue;
                    }

                    var parameter = parameters[i];
                    var holder = IdentifierName(HolderName(parameter.Identifier.ValueText, prefix));
                    if (parameter.HasAnnotation(Annotations.PointerRef) && !arg.IsRefOut())
                    {
                        rewritten.Add(arg.WithExpression(CastExpression(
                            parameter.Type!,
                            MemberAccessExpression(holder, "Value"))));
                    }
                    else if (parameter.HasAnnotation(Annotations.StructRef))
                        rewritten.Add(arg.WithExpression(MemberAccessExpression(holder, "Value")));
                    else if (CapturedByRef(parameter))
                        rewritten.Add(arg.WithExpression(IdentifierName(prefix + parameter.Identifier.ValueText)));
                    else
                        rewritten.Add(arg);
                }

                return call.WithArgumentList(ArgumentList(SeparatedList(rewritten)));
            }

            const string UnsupportedForwardKind = "Stunts.UnsupportedForward";

            // The stunt target keeps the base body (or throws when there is none). Any other instance runs the same member.
            ArgumentSyntax[] RefOutArguments(MethodDeclarationSyntax method, InvocationExpressionSyntax? baseCall, string prefix)
            {
                if (baseCall != null)
                {
                    var captured = method.ParameterList.Parameters;
                    baseCall = baseCall.WithArguments(baseCall.ArgumentList.Arguments.Select((arg, index) =>
                        arg.IsRefOut() || (index < captured.Count && captured[index].Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
                            ? arg.WithExpression(IdentifierName(prefix + captured[index].Identifier.ValueText))
                            : arg));
                }

                var forwarded = ForwardedCall(method, baseCall, method.Modifiers, type => SynthesizeMethod(method, type, prefix));
                if (baseCall == null && forwarded == null)
                {
                    return new[]
                    {
                        Argument(CreateMethodInvocation(method.ParameterList.Parameters, typeParameters: method.TypeParameterList))
                    };
                }

                StatementSyntax InitLocal(ParameterSyntax parameter) =>
                    LocalDeclarationStatement(VariableDeclaration(
                        prefix + parameter.Identifier.ValueText,
                        InvocationExpression(
                            MemberAccessExpression(
                                MemberAccessExpression(invocationName, nameof(IMethodInvocation.Arguments)),
                                parameter.Type!.Kind() == SyntaxKind.NullableType
                                    ? GenericName(nameof(ArgumentCollectionExtensions.GetNullable), parameter.Type!)
                                    : GenericName(nameof(ArgumentCollectionExtensions.Get), parameter.Type!)),
                            Argument(LiteralExpression(parameter.Identifier.ValueText)))));

                var initials = method.ParameterList.Parameters.Where(parameter => parameter.IsRefOut()).Select(InitLocal);
                LambdaExpressionSyntax lambda;
                if (forwarded == null)
                {
                    ExpressionSyntax value = method.ReturnType.IsVoid() ? NullLiteralExpression : baseCall!;
                    lambda = LambdaExpression(
                        new[] { Parameter(Identifier(targetName)), Parameter(Identifier(invocationName)) },
                        initials.Concat(method.ReturnType.IsVoid()
                                ? new[] { ExpressionStatement(baseCall!) }
                                : Array.Empty<StatementSyntax>())
                            .Concat(new StatementSyntax[]
                            {
                                ReturnStatement(InvocationExpression(
                                    invocationName,
                                    "CreateValueReturn",
                                    Argument(value),
                                    Argument(RefOutOutputs(method, prefix, locals: true))))
                            }));
                }
                else
                {
                    StatementSyntax Call(ExpressionSyntax? call)
                    {
                        if (call == null)
                            return ThrowNotImplemented();
                        if (IsUnsupported(call))
                            return ThrowUnsupported();

                        var result = method.ReturnType.IsVoid() ? (ExpressionSyntax)NullLiteralExpression : call;
                        var returning = ReturnStatement(InvocationExpression(
                            invocationName,
                            "CreateValueReturn",
                            Argument(result),
                            Argument(RefOutOutputs(method, prefix, locals: true))));
                        return method.ReturnType.IsVoid() ? Block(ExpressionStatement(call), returning) : returning;
                    }

                    lambda = LambdaExpression(
                        new[] { Parameter(Identifier(targetName)), Parameter(Identifier(invocationName)) },
                        initials.Append(IfStatement(IsStuntTarget(), Call(baseCall), ElseClause(Call(forwarded)))));
                }

                return new[]
                {
                    Argument(ObjectCreationExpression(
                        nameof(MethodInvocation),
                        new[]
                        {
                            Argument(ThisExpression()),
                            Argument(prefix + prefix + "method"),
                            Argument(lambda),
                            Argument(RefOutOutputs(method, prefix, locals: false))
                        })),
                    Argument(TrueLiteralExpression)
                };
            }

            static ExpressionSyntax RefOutOutputs(MethodDeclarationSyntax method, string prefix, bool locals)
                => ObjectCreationExpression(
                    nameof(ArgumentCollection),
                    Argument(InvocationExpression(prefix + prefix + "method", nameof(MethodBase.GetParameters))))
                    .WithInitializer(InitializerExpression(
                        SyntaxKind.CollectionInitializerExpression,
                        method.ParameterList.Parameters.Select(parameter =>
                            InitializerExpression(
                                SyntaxKind.ComplexElementInitializerExpression,
                                LiteralExpression(parameter.Identifier.ValueText),
                                locals && parameter.IsRefOut()
                                    ? IdentifierName(prefix + parameter.Identifier.ValueText)
                                    : IdentifierName(parameter.Identifier)))));

            LambdaExpressionSyntax Branch(TypeSyntax? returnType, ExpressionSyntax? stuntCall, ExpressionSyntax targetCall)
            {
                StatementSyntax Stunt()
                {
                    if (stuntCall == null)
                        return ThrowNotImplemented();
                    if (returnType.IsVoid())
                        return Block(
                            ExpressionStatement(stuntCall),
                            ReturnStatement(InvocationExpression(invocationName, "CreateReturn")));

                    return ReturnStatement(InvocationExpression(invocationName, "CreateValueReturn", Argument(Returned(stuntCall))));
                }

                StatementSyntax Target()
                {
                    if (IsUnsupported(targetCall))
                        return ThrowUnsupported();
                    if (returnType.IsVoid())
                        return Block(
                            ExpressionStatement(targetCall),
                            ReturnStatement(InvocationExpression(invocationName, "CreateReturn")));

                    return ReturnStatement(InvocationExpression(invocationName, "CreateValueReturn", Argument(Returned(targetCall))));
                }

                // CreateValueReturn is an extension. A dynamic result cannot select it.
                ExpressionSyntax Returned(ExpressionSyntax call)
                    => IsDynamic(returnType)
                        ? CastExpression(PredefinedType(Token(SyntaxKind.ObjectKeyword)), call)
                        : call;

                return LambdaExpression(
                    new[] { Parameter(targetName), Parameter(invocationName) },
                    IfStatement(IsStuntTarget(), Stunt(), ElseClause(Target())));
            }

            ExpressionSyntax? ForwardedCall(SyntaxNode member, ExpressionSyntax? baseCall, SyntaxTokenList modifiers, Func<TypeSyntax, ExpressionSyntax>? synthesize)
            {
                var type = ForwardType(member);
                if (type == null)
                    return null;
                if (Inaccessible(modifiers))
                    return UnsupportedForward();
                if (baseCall != null)
                    return Retarget(baseCall, CastTarget(type));
                // Indexers read arguments back with Arguments.Get<T>. A ref struct cannot be that
                // type argument, and the invoker delegate cannot capture it. Leave the throwing invoker.
                if (synthesize == null || !CanBoxArguments(member))
                    return null;

                return synthesize.Invoke(type);
            }

            static bool IsDynamic(TypeSyntax? type)
            {
                // Roslyn 5 represents dynamic as an identifier, not SyntaxKind.DynamicKeyword.
                if (type is IdentifierNameSyntax name)
                    return name.Identifier.ValueText == "dynamic";
                return type is PredefinedTypeSyntax predefined && predefined.Keyword.ValueText == "dynamic";
            }

            static bool CanBoxArguments(SyntaxNode member)
            {
                if (member is not IndexerDeclarationSyntax indexer)
                    return true;
                if (indexer.HasAnnotation(Annotations.StructRef) || indexer.HasAnnotation(Annotations.PointerRef))
                    return false;

                return !indexer.ParameterList.Parameters.Any(parameter =>
                    parameter.HasAnnotation(Annotations.StructRef) || parameter.HasAnnotation(Annotations.PointerRef));
            }

            static TypeSyntax? ForwardType(SyntaxNode node)
            {
                var data = node.GetAnnotations(Annotations.ForwardKind).FirstOrDefault()?.Data;
                if (data == null || data.Length == 0)
                    return null;

                return ParseTypeName(data);
            }

            static bool Inaccessible(SyntaxTokenList modifiers)
                => modifiers.Any(SyntaxKind.ProtectedKeyword) || modifiers.Any(SyntaxKind.PrivateKeyword);

            // A public property can narrow one accessor. An accessor with no modifier keeps the member's.
            static SyntaxTokenList EffectiveModifiers(SyntaxTokenList member, SyntaxTokenList accessor)
                => accessor.Count == 0 ? member : accessor;

            ExpressionSyntax CastTarget(TypeSyntax type)
                => ParenthesizedExpression(CastExpression(type, IdentifierName(targetName)));

            ExpressionSyntax IsStuntTarget()
                => InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        PredefinedType(Token(SyntaxKind.ObjectKeyword)),
                        IdentifierName(nameof(ReferenceEquals))),
                    Argument(IdentifierName(targetName)),
                    Argument(ThisExpression()));

            static ExpressionSyntax UnsupportedForward()
                => ObjectCreationExpression(
                    ParseTypeName("global::System.NotSupportedException"),
                    Argument(LiteralExpression("Cannot forward a protected member.")))
                    .WithAdditionalAnnotations(new SyntaxAnnotation(UnsupportedForwardKind));

            static bool IsUnsupported(ExpressionSyntax call)
                => call.GetAnnotations(UnsupportedForwardKind).Any();

            static ThrowStatementSyntax ThrowNotImplemented()
                => ThrowStatement(ObjectCreationExpression(ParseTypeName("global::System.NotImplementedException")).WithArgumentList(ArgumentList()));

            static ThrowStatementSyntax ThrowUnsupported()
                => ThrowStatement(ObjectCreationExpression(
                    ParseTypeName("global::System.NotSupportedException"),
                    Argument(LiteralExpression("Cannot forward a protected member."))));

            static ExpressionSyntax Retarget(ExpressionSyntax call, ExpressionSyntax receiver)
                => ReplaceReceiver(call, receiver);

            static ExpressionSyntax ReplaceReceiver(ExpressionSyntax expression, ExpressionSyntax receiver)
            {
                switch (expression)
                {
                    case InvocationExpressionSyntax invocation when invocation.Expression is MemberAccessExpressionSyntax access:
                        return invocation.WithExpression(access.WithExpression(receiver));
                    case MemberAccessExpressionSyntax access:
                        return access.WithExpression(receiver);
                    case ElementAccessExpressionSyntax element:
                        return element.WithExpression(receiver);
                    case AssignmentExpressionSyntax assignment:
                        return assignment.WithLeft(ReplaceReceiver(assignment.Left, receiver));
                    case RefExpressionSyntax reference:
                        return reference.WithExpression(ReplaceReceiver(reference.Expression, receiver));
                    default:
                        return expression;
                }
            }

            ExpressionSyntax SynthesizeMethod(MethodDeclarationSyntax method, TypeSyntax type, string? localPrefix)
            {
                SimpleNameSyntax name = method.TypeParameterList == null || method.TypeParameterList.Parameters.Count == 0
                    ? IdentifierName(method.Identifier)
                    : SyntaxFactory.GenericName(method.Identifier).WithTypeArgumentList(TypeArgumentList(SeparatedList<TypeSyntax>(
                        method.TypeParameterList.Parameters.Select(parameter => IdentifierName(parameter.Identifier)))));

                var arguments = method.ParameterList.Parameters.Select(parameter =>
                {
                    var useLocal = localPrefix != null && (parameter.IsRefOut() || parameter.Modifiers.Any(SyntaxKind.ReadOnlyKeyword));
                    var argument = Argument(useLocal
                        ? IdentifierName(localPrefix + parameter.Identifier.ValueText)
                        : IdentifierName(parameter.Identifier));
                    var kind = parameter.Modifiers.FirstOrDefault(token =>
                        token.IsKind(SyntaxKind.RefKeyword) || token.IsKind(SyntaxKind.OutKeyword) || token.IsKind(SyntaxKind.InKeyword));
                    return kind.RawKind == 0 ? argument : argument.WithRefKindKeyword(Token(kind.Kind()));
                });

                return InvocationExpression(MemberAccessExpression(CastTarget(type), name), arguments);
            }

            ExpressionSyntax SynthesizeProperty(PropertyDeclarationSyntax property, TypeSyntax type, ExpressionSyntax? value)
            {
                ExpressionSyntax access = MemberAccessExpression(CastTarget(type), IdentifierName(property.Identifier));
                return value == null ? access : AssignmentExpression(access, value);
            }

            ExpressionSyntax SynthesizeIndexer(IndexerDeclarationSyntax indexer, TypeSyntax type, ExpressionSyntax? value)
            {
                ExpressionSyntax access = ElementAccessExpression(
                    CastTarget(type),
                    BracketedArgumentList(SeparatedList(indexer.ParameterList.Parameters.Select(parameter =>
                        Argument(PipelineValue(parameter.Type!, parameter.Identifier.ValueText))))));
                return value == null ? access : AssignmentExpression(access, value);
            }

            ExpressionSyntax SynthesizeEvent(EventDeclarationSyntax declaration, TypeSyntax type, SyntaxKind kind)
                => AssignmentExpression(
                    kind,
                    MemberAccessExpression(CastTarget(type), IdentifierName(declaration.Identifier)),
                    IdentifierName("value"));

            ExpressionSyntax PipelineValue(TypeSyntax type, string name)
                => InvocationExpression(
                    MemberAccessExpression(
                        MemberAccessExpression(invocationName, nameof(IMethodInvocation.Arguments)),
                        type.Kind() == SyntaxKind.NullableType
                            ? GenericName(nameof(ArgumentCollectionExtensions.GetNullable), type)
                            : GenericName(nameof(ArgumentCollectionExtensions.Get), type)),
                    Argument(LiteralExpression(name)));
        }
    }
}