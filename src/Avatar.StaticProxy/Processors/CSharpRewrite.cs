using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Avatars.SyntaxFactoryGenerator;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Avatars.Processors
{
    /// <summary>
    /// Rewrites all members so they are implemented through 
    /// the <see cref="BehaviorPipeline"/> field added to the 
    /// class by the <see cref="CSharpAvatar"/>.
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
            public List<ClassDeclarationSyntax> Types { get; } = new();
            public List<EventDeclarationSyntax> Events { get; } = new();

            public override void VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                base.VisitClassDeclaration(node);
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
            {
                // Turn event fields into event declarations.
                var events = node.ChildNodes().OfType<EventFieldDeclarationSyntax>().ToArray();
                node = node.RemoveNodes(events, SyntaxRemoveOptions.KeepNoTrivia)!;

                node = node.AddMembers(events
                    .Select(x => EventDeclaration(x.Declaration.Type, x.Declaration.Variables.First().Identifier)
                        .WithModifiers(x.Modifiers))
                    .ToArray());

                return base.VisitClassDeclaration(node);
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

                if (method.ParameterList.Parameters.Any(x => x.IsRefOut()))
                {
                    var body = Block(
                        // var method = MethodBase.GetCurrentMethod();
                        LocalDeclarationStatement(
                            VariableDeclaration(
                                prefix + "method",
                                InvocationExpression(
                                    nameof(MethodBase),
                                    nameof(MethodBase.GetCurrentMethod)))));

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

                        baseCall = baseCall.WithArguments(
                            baseCall.ArgumentList.Arguments.Select(arg =>
                                arg.IsRefOut() ?
                                // Replace original args with _ args for the base call, 
                                // since the lambda can't reference ref/out args from within it.
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
                        method.ParameterList.Parameters.Where(prm => prm.IsRefOut()).Select(x => ExpressionStatement(
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
                    var body = Execute(method.ReturnType, method.ParameterList.Parameters, baseCall);

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

                var canRead = node.AccessorList?.Accessors.Any(SyntaxKind.GetAccessorDeclaration) == true;
                var canWrite = node.AccessorList?.Accessors.Any(SyntaxKind.SetAccessorDeclaration) == true;
                canRead |= node.ExpressionBody != null;

                var prop = node;

                if (node.ExpressionBody != null)
                    node = node.RemoveNode(node.ExpressionBody, SyntaxRemoveOptions.KeepNoTrivia)!;

                node = node.WithAccessorList(null);

                if (canRead && !canWrite)
                {
                    var baseCall = GetBaseCall(prop, SyntaxKind.GetAccessorDeclaration);
                    node = node
                        .WithExpressionBody(ArrowExpressionClause(Execute(
                            node.Type, Enumerable.Empty<ParameterSyntax>(), baseCall)))
                        .WithSemicolon();
                }
                else
                {
                    if (canRead)
                    {
                        var baseCall = GetBaseCall(prop, SyntaxKind.GetAccessorDeclaration);
                        node = node.AddAccessorListAccessors(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithExpressionBody(ArrowExpressionClause(Execute(
                                node.Type, Enumerable.Empty<ParameterSyntax>(), baseCall)))
                            .WithSemicolon());
                    }
                    if (canWrite)
                    {
                        var baseCall = (AssignmentExpressionSyntax?)GetBaseCall(prop, SyntaxKind.SetAccessorDeclaration);
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

                        node = node.AddAccessorListAccessors(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                            .WithExpressionBody(ArrowExpressionClause(
                                // NOTE: we always append the implicit "value" parameter for setters.
                                Execute(null, new[] { Parameter(Identifier("value")).WithType(node.Type) }, baseCall)))
                            .WithSemicolon());
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

                var canRead = node.AccessorList?.Accessors.Any(SyntaxKind.GetAccessorDeclaration) == true;
                var canWrite = node.AccessorList?.Accessors.Any(SyntaxKind.SetAccessorDeclaration) == true;
                canRead |= node.ExpressionBody != null;

                var prop = node;

                if (node.ExpressionBody != null)
                    node = node.RemoveNode(node.ExpressionBody, SyntaxRemoveOptions.KeepNoTrivia)!;

                node = node.WithAccessorList(null);

                if (canRead && !canWrite)
                {
                    return node.WithExpressionBody(
                        ArrowExpressionClause(
                            Execute(
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
                    if (canRead)
                    {
                        node = node.AddAccessorListAccessors(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithExpressionBody(ArrowExpressionClause(Execute(
                                node.Type, node.ParameterList.Parameters,
                                FixBaseCall(
                                    prop,
                                    (ElementAccessExpressionSyntax?)GetBaseCall(
                                        prop,
                                        SyntaxKind.GetAccessorDeclaration)))))
                            .WithSemicolon());
                    }

                    if (canWrite)
                    {
                        var baseCall = (AssignmentExpressionSyntax?)GetBaseCall(prop, SyntaxKind.SetAccessorDeclaration);
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

                        node = node.AddAccessorListAccessors(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                            .WithExpressionBody(ArrowExpressionClause(
                                Execute(null, node.ParameterList.Parameters.Concat(new[] { Parameter(Identifier("value")).WithType(node.Type) }),
                                baseCall)))
                            .WithSemicolon());
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

                if (virtualEvents.Contains(node.Identifier.ValueText))
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
                if (!node.Modifiers.Any(SyntaxKind.OverrideKeyword) || node.AccessorList == null)
                    return null;

                var accessor = node.DescendantNodes().OfType<AccessorDeclarationSyntax>().FirstOrDefault(x => x.IsKind(kind));
                if (accessor == null)
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
                        i.DescendantNodes().OfType<BaseExpressionSyntax>().Any());

            static ExpressionSyntax Execute(TypeSyntax? returnType, IEnumerable<ParameterSyntax> parameters, ExpressionSyntax? baseCall = null)
            {
                if (baseCall == null)
                    return CreatePipelineInvocation(returnType.IsVoid() ? null : returnType, parameters);

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
                                Argument(baseCall))));

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
                                    "CreateReturn"))));
            }

            static InvocationExpressionSyntax CreatePipelineInvocation(TypeSyntax? returnType, IEnumerable<ParameterSyntax> parameters, LambdaExpressionSyntax? target = null)
            {
                SimpleNameSyntax execute = returnType.IsVoid() ?
                    IdentifierName("Execute") :
                    returnType.IsKind(SyntaxKind.RefType) ?
                    GenericName("ExecuteRef", ((RefTypeSyntax)returnType).Type) :
                    GenericName("Execute", returnType!);

                var create = CreateMethodInvocation(parameters, target);

                return InvocationExpression(
                        IdentifierName("pipeline"),
                        execute,
                        Argument(create));
            }

            static ExpressionSyntax CreateMethodInvocation(IEnumerable<ParameterSyntax> parameters, LambdaExpressionSyntax? target = null)
            {
                var arguments = new List<ArgumentSyntax>
                {
                    Argument(ThisExpression()),
                    Argument(
                        InvocationExpression(
                        nameof(MethodBase),
                        nameof(MethodBase.GetCurrentMethod)))
                };

                if (target != null)
                    arguments.Add(Argument(target));

                arguments.AddRange(parameters.Select(parameter => Argument(parameter.Identifier)));

                return InvocationExpression(
                    nameof(MethodInvocation),
                    nameof(MethodInvocation.Create),
                    arguments);
            }
        }
    }
}