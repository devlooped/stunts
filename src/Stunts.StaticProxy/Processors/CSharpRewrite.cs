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
            var semantic = context.Compilation.GetSemanticModel(syntax.SyntaxTree);
            if (semantic != null)
            {
                var events = new EventVisitor();
                events.Visit(syntax);

                foreach (var symbol in events.Types.SelectMany(type => semantic
                    .LookupNamespacesAndTypes(type.Span.Start, name: type.Identifier.ValueText)
                    .OfType<INamedTypeSymbol>()).Where(x => x != null))
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
            public List<EventDeclarationSyntax> Events { get; } = new();

            public override void VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                base.VisitClassDeclaration(node);
                Types.Add(node);
            }

            public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
            {
                base.VisitRecordDeclaration(node);
                Types.Add(node);
            }

            public override void VisitEventDeclaration(EventDeclarationSyntax node)
            {
                base.VisitEventDeclaration(node);
                if (node.Modifiers.Any(SyntaxKind.OverrideKeyword))
                    Events.Add(node);
            }
        }

        class CSharpRewriteVisitor : CSharpSyntaxRewriter
        {
            readonly HashSet<string> virtualEvents;

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

                if (node.Body != null)
                    node = node.RemoveNodes(new SyntaxNode[] { node.Body }, SyntaxRemoveOptions.KeepNoTrivia)!;

                var create = CreateMethodInvocation(node.ParameterList.Parameters,
                    LambdaExpression(
                        new[]
                        {
                            Parameter("m"),
                            Parameter("n")
                        },
                        InvocationExpression(
                            "m",
                            "CreateReturn")));

                var body = InvocationExpression(
                    "pipeline",
                    nameof(BehaviorPipelineExtensions.Execute),
                    Argument(create));

                node = node.WithExpressionBody(ArrowExpressionClause(body))
                        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                        .WithSemicolon();

                return base.VisitConstructorDeclaration(node);
            }

            public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax method)
            {
                if (method.AttributeLists.HasAttribute("CompilerGenerated"))
                    return base.VisitMethodDeclaration(method);

                var baseCall = GetBaseInvocation(method);
                var prefix = "_";
                while (method.ParameterList.Parameters.Any(x => x.Identifier.ValueText.StartsWith(prefix, StringComparison.Ordinal)))
                    prefix += "_";

                if (NeedsHold(method))
                {
                    method = Hold(method, baseCall, prefix);
                    return base.VisitMethodDeclaration(method);
                }

                if (method.ParameterList.Parameters.Any(x => x.IsRefOut()))
                {
                    var body = Block(
                        // var method = MethodBase.GetCurrentMethod();
                        LocalDeclarationStatement(
                            VariableDeclaration(
                                prefix + "method",
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

                    var args = Array.Empty<ArgumentSyntax>();
                    if (baseCall == null)
                    {
                        // Simple pipeline execute without base call.
                        args = new[]
                        {
                            Argument(
                                InvocationExpression(
                                    nameof(MethodInvocation),
                                    nameof(MethodInvocation.Create),
                                    new []
                                    {
                                        Argument(ThisExpression()),
                                        Argument(prefix + "method"),
                                    }
                                    .Concat(method.ParameterList.Parameters.Select(x =>
                                            Argument(x.Identifier)))))
                        };
                    }
                    else
                    {
                        StatementSyntax InitLocal(ParameterSyntax parameter) =>
                            LocalDeclarationStatement(
                                VariableDeclaration(
                                    prefix + parameter.Identifier,
                                    InvocationExpression(
                                        MemberAccessExpression(
                                            MemberAccessExpression("m", nameof(IMethodInvocation.Arguments)),
                                            parameter.Type!.Kind() == SyntaxKind.NullableType ?
                                            GenericName(nameof(ArgumentCollectionExtensions.GetNullable), parameter.Type!) :
                                            GenericName(nameof(ArgumentCollectionExtensions.Get), parameter.Type!)),
                                        Argument(
                                            LiteralExpression(parameter.Identifier.ToString())))));

                        var captured = method.ParameterList.Parameters;
                        baseCall = baseCall.WithArguments(
                            baseCall.ArgumentList.Arguments.Select((arg, index) =>
                                arg.IsRefOut() || (index < captured.Count && captured[index].Modifiers.Any(SyntaxKind.ReadOnlyKeyword)) ?
                                // Replace original args with _ args for the base call, 
                                // since the lambda can't reference ref/out/ref readonly args from within it.
                                arg.WithExpression(
                                    IdentifierName(prefix + arg.Expression)) :
                                arg));

                        ExpressionSyntax value = method.ReturnType.IsVoid() ? NullLiteralExpression : baseCall;

                        args = new[]
                        {
                            Argument(
                                ObjectCreationExpression(
                                    nameof(MethodInvocation),
                                    new[]
                                    {
                                        Argument(ThisExpression()),
                                        Argument(prefix + "method")
                                    }
                                    .Concat(new []
                                    {
                                        Argument(
                                            // (m, n) => ...,
                                            LambdaExpression(
                                                new []
                                                {
                                                    Parameter(Identifier("m")),
                                                    Parameter(Identifier("n")),
                                                },
                                                // var _NAME = m.Arguments.Get<int>("NAME");
                                                method.ParameterList.Parameters.Where(x => x.IsRefOut()).Select(InitLocal)
                                                // If method was void, we must call base before returning
                                                .Concat(method.ReturnType.IsVoid() ?
                                                    new [] { ExpressionStatement(baseCall) } :
                                                    Array.Empty<StatementSyntax>())
                                                .Concat(new StatementSyntax[]
                                                {
                                                    // return m.CreateValueReturn(base.METHOD(_NAME, ...))
                                                    ReturnStatement(
                                                        InvocationExpression(
                                                            "m",
                                                            // We could call CreateReturn for void methods, but 
                                                            // this works too and makes the argument passing simpler
                                                            "CreateValueReturn",
                                                            Argument(value),
                                                            Argument(
                                                                //  new ArgumentCollection(method.GetParameters())
                                                                ObjectCreationExpression(
                                                                    nameof(ArgumentCollection),
                                                                    Argument(
                                                                        InvocationExpression(
                                                                            prefix + "method",
                                                                            nameof(MethodBase.GetParameters))))
                                                                .WithInitializer(
                                                                    // { { "x", _x }, ... }
                                                                    InitializerExpression(
                                                                        SyntaxKind.CollectionInitializerExpression,
                                                                        method.ParameterList.Parameters.Select(x =>
                                                                            InitializerExpression(
                                                                                SyntaxKind.ComplexElementInitializerExpression,
                                                                                LiteralExpression(x.Identifier.ToString()),
                                                                                x.IsRefOut() ?
                                                                                    IdentifierName(prefix + x.Identifier.ToString()) :
                                                                                    IdentifierName(x.Identifier))))))))
                                                }))),
                                        Argument(
                                            //  new ArgumentCollection(method.GetParameters())
                                            ObjectCreationExpression(
                                                nameof(ArgumentCollection),
                                                Argument(
                                                    InvocationExpression(
                                                        prefix + "method",
                                                        nameof(MethodBase.GetParameters))))
                                            .WithInitializer(
                                                // { { "x", x }, ... }
                                                InitializerExpression(
                                                    SyntaxKind.CollectionInitializerExpression,
                                                    method.ParameterList.Parameters.Select(x =>
                                                        InitializerExpression(
                                                            SyntaxKind.ComplexElementInitializerExpression,
                                                            LiteralExpression(x.Identifier.ToString()),
                                                            IdentifierName(x.Identifier))))))
                                    }))),
                            Argument(TrueLiteralExpression)
                        };
                    }

                    body = body.AddStatements(
                        // var _result = pipeline.Invoke(...)
                        LocalDeclarationStatement(
                            VariableDeclaration(
                                prefix + "result",
                                InvocationExpression("pipeline", "Invoke", args))));

                    body = body.AddStatements(
                        // x = _result.Outputs.GetNullable<int>("x");
                        method.ParameterList.Parameters.Where(prm => prm.IsRefOut() && !prm.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)).Select(x => ExpressionStatement(
                            AssignmentExpression(
                                x.Identifier,
                                InvocationExpression(
                                    MemberAccessExpression(
                                        MemberAccessExpression(
                                            prefix + "result",
                                            nameof(IMethodReturn.Outputs)),
                                    x.Type!.Kind() == SyntaxKind.NullableType ?
                                    GenericName(nameof(ArgumentCollectionExtensions.GetNullable), x.Type!) :
                                    GenericName(nameof(ArgumentCollectionExtensions.Get), x.Type!)),
                                    Argument(
                                        LiteralExpression(x.Identifier.ToString()))))))
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
                                                prefix + "result",
                                                GenericName("AsRef", ((RefTypeSyntax)method.ReturnType).Type)),
                                            "Value")))));
                    }
                    else if (!method.ReturnType.IsVoid())
                    {
                        method = method
                            .WithExpressionBody(null)
                            .WithSemicolonToken(default)
                            .WithBody(body.AddStatements(
                                // return (T)_result.ReturnValue;
                                ReturnStatement(
                                    CastExpression(
                                        method.ReturnType,
                                        PostfixUnaryExpression(
                                            SyntaxKind.SuppressNullableWarningExpression,
                                            MemberAccessExpression(
                                                prefix + "result",
                                                nameof(IMethodReturn.ReturnValue)))))));
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
                    var body = Execute(method.ReturnType, method.ParameterList.Parameters, baseCall, method.TypeParameterList);

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

                var getter = Accessor(node, SyntaxKind.GetAccessorDeclaration);
                var setter = Accessor(node, SyntaxKind.SetAccessorDeclaration, SyntaxKind.InitAccessorDeclaration);
                var canRead = getter != null || node.ExpressionBody != null;

                var prop = node;

                if (node.ExpressionBody != null)
                    node = node.RemoveNode(node.ExpressionBody, SyntaxRemoveOptions.KeepNoTrivia)!;

                node = node.WithAccessorList(null);

                if (canRead && setter == null)
                {
                    var baseCall = GetBaseCall(prop, SyntaxKind.GetAccessorDeclaration);
                    node = node
                        .WithExpressionBody(ArrowExpressionClause(Executed(
                            node.Type, Enumerable.Empty<ParameterSyntax>(), baseCall)))
                        .WithSemicolon();
                }
                else
                {
                    if (getter != null)
                    {
                        var baseCall = GetBaseCall(prop, SyntaxKind.GetAccessorDeclaration);
                        node = node.AddAccessorListAccessors(WithBody(getter, Executed(
                            node.Type, Enumerable.Empty<ParameterSyntax>(), baseCall)));
                    }
                    if (setter != null)
                    {
                        var baseCall = (AssignmentExpressionSyntax?)GetBaseCall(prop, setter.Kind());
                        // We must use the value in the invocation arguments received from the pipeline for the setter
                        // => base.Prop = m.Arguments.Get<T>();
                        baseCall = baseCall?.WithRight(InvocationExpression(
                            MemberAccessExpression(
                                MemberAccessExpression("m", nameof(IMethodInvocation.Arguments)),
                                node.Type.Kind() == SyntaxKind.NullableType ?
                                GenericName(nameof(ArgumentCollectionExtensions.GetNullable), node.Type) :
                                GenericName(nameof(ArgumentCollectionExtensions.Get), node.Type)),
                            Argument(
                                LiteralExpression("value"))));

                        node = node.AddAccessorListAccessors(WithBody(setter,
                            // NOTE: we always append the implicit "value" parameter for setters.
                            Execute(null, new[] { Parameter(Identifier("value")).WithType(node.Type) }, baseCall)));
                    }
                }

                return base.VisitPropertyDeclaration(node);
            }

            public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
            {
                if (node.AttributeLists.HasAttribute("CompilerGenerated"))
                    return base.VisitIndexerDeclaration(node);

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

                if (canRead && setter == null)
                {
                    return node.WithExpressionBody(
                        ArrowExpressionClause(
                            Executed(
                                node.Type, node.ParameterList.Parameters,
                                FixBaseCall(
                                    prop,
                                    (ElementAccessExpressionSyntax?)GetBaseCall(
                                        prop,
                                        SyntaxKind.GetAccessorDeclaration)))))
                        .WithSemicolon();
                }
                else
                {
                    if (getter != null)
                    {
                        node = node.AddAccessorListAccessors(WithBody(getter, Executed(
                            node.Type, node.ParameterList.Parameters,
                            FixBaseCall(
                                prop,
                                (ElementAccessExpressionSyntax?)GetBaseCall(
                                    prop,
                                    SyntaxKind.GetAccessorDeclaration)))));
                    }

                    if (setter != null)
                    {
                        var baseCall = (AssignmentExpressionSyntax?)GetBaseCall(prop, setter.Kind());
                        // Replace base indexer call args with references to pipeline invocation args
                        baseCall = baseCall?
                            .WithLeft(FixBaseCall(prop, (ElementAccessExpressionSyntax)baseCall.Left)!)
                            .WithRight(InvocationExpression(
                                MemberAccessExpression(
                                    MemberAccessExpression("m", nameof(IMethodInvocation.Arguments)),
                                    node.Type.Kind() == SyntaxKind.NullableType ?
                                    GenericName(nameof(ArgumentCollectionExtensions.GetNullable), node.Type) :
                                    GenericName(nameof(ArgumentCollectionExtensions.Get), node.Type)),
                                Argument(
                                    LiteralExpression("value"))));

                        node = node.AddAccessorListAccessors(WithBody(setter,
                            Execute(null, node.ParameterList.Parameters.Concat(new[] { Parameter(Identifier("value")).WithType(node.Type) }),
                            baseCall)));
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
                        AccessorDeclaration(SyntaxKind.AddAccessorDeclaration)
                            .WithExpressionBody(ArrowExpressionClause(Execute(null, parameters, defaultAdd)))
                            .WithSemicolon(),
                        AccessorDeclaration(SyntaxKind.RemoveAccessorDeclaration)
                            .WithExpressionBody(ArrowExpressionClause(Execute(null, parameters, defaultRemove)))
                            .WithSemicolon()
                    })));
                }
                else if (virtualEvents.Contains(node.Identifier.ValueText))
                {
                    ArrowExpressionClauseSyntax body(SyntaxKind kind)
                        => ArrowExpressionClause(
                            Execute(null, parameters!, AssignmentExpression(
                                kind,
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    BaseExpression(),
                                    IdentifierName(node.Identifier)),
                                IdentifierName("value"))));

                    var add = body(SyntaxKind.AddAssignmentExpression);
                    var remove = body(SyntaxKind.SubtractAssignmentExpression);

                    node = node.WithAccessorList(AccessorList(List(new AccessorDeclarationSyntax[]
                    {
                        AccessorDeclaration(SyntaxKind.AddAccessorDeclaration)
                            .WithExpressionBody(add)
                            .WithSemicolon(),
                        AccessorDeclaration(SyntaxKind.RemoveAccessorDeclaration)
                            .WithExpressionBody(remove)
                            .WithSemicolon()
                    })));
                }
                else
                {
                    node = node.WithAccessorList(AccessorList(List(new AccessorDeclarationSyntax[]
                    {
                        AccessorDeclaration(SyntaxKind.AddAccessorDeclaration)
                            .WithExpressionBody(
                                ArrowExpressionClause(CreatePipelineInvocation(null, parameters)))
                            .WithSemicolon(),
                        AccessorDeclaration(SyntaxKind.RemoveAccessorDeclaration)
                            .WithExpressionBody(
                                ArrowExpressionClause(CreatePipelineInvocation(null, parameters)))
                            .WithSemicolon()
                    })));
                }

                return base.VisitEventDeclaration(node);
            }

            static AccessorDeclarationSyntax? Accessor(BasePropertyDeclarationSyntax node, params SyntaxKind[] kinds)
                => node.AccessorList?.Accessors.FirstOrDefault(accessor => kinds.Contains(accessor.Kind()));

            // Keeps a narrowed accessor (protected set, internal get) on the override.
            static AccessorDeclarationSyntax WithBody(AccessorDeclarationSyntax accessor, ExpressionSyntax body)
                => AccessorDeclaration(accessor.Kind())
                    .WithModifiers(accessor.Modifiers)
                    .WithExpressionBody(ArrowExpressionClause(body))
                    .WithSemicolon();

            static ElementAccessExpressionSyntax? FixBaseCall(IndexerDeclarationSyntax indexer, ElementAccessExpressionSyntax? baseCall)
                // Replace base indexer call args with references to pipeline invocation args
                => baseCall?.WithArgumentList(
                    indexer.ParameterList.Parameters.Select(prm =>
                        Argument(
                            InvocationExpression(
                                MemberAccessExpression(
                                    MemberAccessExpression("m", nameof(IMethodInvocation.Arguments)),
                                    prm.Type!.Kind() == SyntaxKind.NullableType ?
                                    GenericName(nameof(ArgumentCollectionExtensions.GetNullable), prm.Type) :
                                    GenericName(nameof(ArgumentCollectionExtensions.Get), prm.Type!)),
                                Argument(
                                    Literal(prm.Identifier.ToString()))))));

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

            static ExpressionSyntax Execute(TypeSyntax? returnType, IEnumerable<ParameterSyntax> parameters, ExpressionSyntax? baseCall = null, TypeParameterListSyntax? typeParameters = null)
            {
                if (baseCall == null)
                    return CreatePipelineInvocation(returnType.IsVoid() ? null : returnType, parameters, typeParameters: typeParameters);

                if (!returnType.IsVoid())
                    return CreatePipelineInvocation(returnType, parameters,
                        LambdaExpression(
                            new[]
                            {
                                Parameter("m"),
                                Parameter("n")
                            },
                            InvocationExpression(
                                "m",
                                "CreateValueReturn",
                                Argument(baseCall))),
                        typeParameters);

                return CreatePipelineInvocation(null, parameters,
                        LambdaExpression(
                            new[]
                            {
                                Parameter("m"),
                                Parameter("n")
                            },
                            ExpressionStatement(baseCall),
                            ReturnStatement(
                                InvocationExpression(
                                    "m",
                                    "CreateReturn"))),
                        typeParameters);
            }

            static InvocationExpressionSyntax CreatePipelineInvocation(TypeSyntax? returnType, IEnumerable<ParameterSyntax> parameters, LambdaExpressionSyntax? target = null, TypeParameterListSyntax? typeParameters = null)
            {
                SimpleNameSyntax execute = returnType.IsVoid() ?
                    IdentifierName("Execute") :
                    returnType.IsKind(SyntaxKind.RefType) ?
                    GenericName("ExecuteRef", ((RefTypeSyntax)returnType).Type) :
                    GenericName("Execute", returnType!);

                var create = CreateMethodInvocation(parameters, target, typeParameters);

                return InvocationExpression(
                        IdentifierName("pipeline"),
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
            {
                var arguments = new List<ArgumentSyntax>
                {
                    Argument(ThisExpression()),
                    Argument(CurrentMethod(typeParameters))
                };

                if (target != null)
                    arguments.Add(Argument(target));

                arguments.AddRange(parameters.Select(parameter => Argument(parameter.Identifier)));

                return InvocationExpression(
                    nameof(MethodInvocation),
                    nameof(MethodInvocation.Create),
                    arguments);
            }

            static ExpressionSyntax Executed(TypeSyntax type, IEnumerable<ParameterSyntax> parameters, ExpressionSyntax? baseCall, TypeParameterListSyntax? typeParameters = null)
            {
                var body = Execute(type, parameters, baseCall, typeParameters);
                return type is RefTypeSyntax ? RefExpression(MemberAccessExpression(body, "Value")) : body;
            }

            static bool NeedsHold(MethodDeclarationSyntax method)
                => method.HasAnnotation(Annotations.StructRef) || method.HasAnnotation(Annotations.PointerRef)
                    || method.ParameterList.Parameters.Any(parameter =>
                        parameter.HasAnnotation(Annotations.StructRef) || parameter.HasAnnotation(Annotations.PointerRef));

            static MethodDeclarationSyntax Hold(MethodDeclarationSyntax method, InvocationExpressionSyntax? baseCall, string prefix)
            {
                var parameters = method.ParameterList.Parameters;
                var statements = new List<StatementSyntax>();
                var finallyStatements = new List<StatementSyntax>();
                foreach (var parameter in parameters)
                {
                    if (!parameter.HasAnnotation(Annotations.StructRef) && !parameter.HasAnnotation(Annotations.PointerRef))
                        continue;

                    var name = prefix + parameter.Identifier.ValueText + "Ref";
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
                var returned = prefix + "returned";
                var returnedRef = prefix + "returnedRef";
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

                if (baseCall != null)
                    baseCall = RewriteCall(baseCall, parameters, prefix);

                var create = CreateHeld(
                    parameters.Select(parameter => Argument(IdentifierName(
                        parameter.HasAnnotation(Annotations.StructRef) || parameter.HasAnnotation(Annotations.PointerRef)
                            ? prefix + parameter.Identifier.ValueText + "Ref"
                            : parameter.Identifier.ValueText))),
                    Target(baseCall, returnType, returnIsStruct, returnIsPointer, returnedRef),
                    method.TypeParameterList);

                ExpressionSyntax pipeline = returnIsStruct || returnIsPointer || returnType.IsVoid()
                    ? InvocationExpression(IdentifierName("pipeline"), IdentifierName("Execute"), Argument(create))
                    : returnType is RefTypeSyntax refReturn
                        ? InvocationExpression(IdentifierName("pipeline"), GenericName("ExecuteRef", refReturn.Type), Argument(create))
                        : InvocationExpression(IdentifierName("pipeline"), GenericName("Execute", returnType), Argument(create));

                var attempt = new List<StatementSyntax>();
                if (returnType.IsVoid())
                    attempt.Add(ExpressionStatement(pipeline));
                else if (returnIsStruct)
                {
                    attempt.Add(LocalDeclarationStatement(VariableDeclaration(prefix + "result", pipeline)));
                    attempt.Add(ExpressionStatement(AssignmentExpression(
                        IdentifierName(returned),
                        MemberAccessExpression(
                            ParenthesizedExpression(CastExpression(
                                HolderType(innerReturn),
                                PostfixUnaryExpression(
                                    SyntaxKind.SuppressNullableWarningExpression,
                                    MemberAccessExpression(prefix + "result", nameof(IMethodReturn.ReturnValue))))),
                            "Value"))));
                    attempt.Add(ReturnStatement(IdentifierName(returned)));
                }
                else if (returnIsPointer)
                {
                    attempt.Add(LocalDeclarationStatement(VariableDeclaration(prefix + "result", pipeline)));
                    attempt.Add(ReturnStatement(CastExpression(
                        innerReturn,
                        MemberAccessExpression(
                            ParenthesizedExpression(CastExpression(
                                IdentifierName("PointerRef"),
                                PostfixUnaryExpression(
                                    SyntaxKind.SuppressNullableWarningExpression,
                                    MemberAccessExpression(prefix + "result", nameof(IMethodReturn.ReturnValue))))),
                            "Value"))));
                }
                else if (returnType is RefTypeSyntax)
                    attempt.Add(ReturnStatement(RefExpression(MemberAccessExpression(pipeline, "Value"))));
                else
                    attempt.Add(ReturnStatement(pipeline));

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

            static LambdaExpressionSyntax? Target(InvocationExpressionSyntax? baseCall, TypeSyntax returnType, bool returnIsStruct, bool returnIsPointer, string returnedRef)
            {
                if (baseCall == null)
                    return null;

                var parameters = new[] { Parameter("m"), Parameter("n") };
                if (returnIsStruct)
                {
                    return LambdaExpression(
                        parameters,
                        ExpressionStatement(AssignmentExpression(
                            MemberAccessExpression(IdentifierName(returnedRef), IdentifierName("Value")),
                            baseCall)),
                        ReturnStatement(InvocationExpression("m", "CreateValueReturn", Argument(IdentifierName(returnedRef)))));
                }

                if (returnIsPointer)
                {
                    return LambdaExpression(
                        parameters,
                        ReturnStatement(InvocationExpression(
                            "m",
                            "CreateValueReturn",
                            Argument(ObjectCreationExpression(
                                IdentifierName("PointerRef"),
                                Argument(CastExpression(
                                    PointerType(PredefinedType(Token(SyntaxKind.VoidKeyword))),
                                    baseCall)))))));
                }

                if (returnType.IsVoid())
                {
                    return LambdaExpression(
                        parameters,
                        ExpressionStatement(baseCall),
                        ReturnStatement(InvocationExpression("m", "CreateReturn")));
                }

                return LambdaExpression(parameters, InvocationExpression("m", "CreateValueReturn", Argument(baseCall)));
            }

            static ExpressionSyntax CreateHeld(IEnumerable<ArgumentSyntax> values, LambdaExpressionSyntax? target, TypeParameterListSyntax? typeParameters)
            {
                var arguments = new List<ArgumentSyntax>
                {
                    Argument(ThisExpression()),
                    Argument(CurrentMethod(typeParameters)),
                };
                if (target != null)
                    arguments.Add(Argument(target));
                arguments.AddRange(values);
                return InvocationExpression(nameof(MethodInvocation), nameof(MethodInvocation.Create), arguments);
            }

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
                    var holder = IdentifierName(prefix + parameter.Identifier.ValueText + "Ref");
                    if (parameter.HasAnnotation(Annotations.PointerRef) && !arg.IsRefOut())
                    {
                        rewritten.Add(arg.WithExpression(CastExpression(
                            parameter.Type!,
                            MemberAccessExpression(holder, "Value"))));
                    }
                    else if (parameter.HasAnnotation(Annotations.StructRef))
                        rewritten.Add(arg.WithExpression(MemberAccessExpression(holder, "Value")));
                    else
                        rewritten.Add(arg);
                }

                return call.WithArgumentList(ArgumentList(SeparatedList(rewritten)));
            }
        }
    }
}