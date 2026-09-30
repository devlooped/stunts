using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Stunts.Processors
{
    /// <summary>
    /// Adds the constructor and <c>Invoke</c> method for a delegate stunt.
    /// The blank class carries a readonly <c>implementation</c> field whose type is the delegate.
    /// </summary>
    static class DelegateScaffold
    {
        internal static IEnumerable<MemberDeclarationSyntax> Members(TypeDeclarationSyntax declaration, SemanticModel model)
        {
            var field = declaration.Members.OfType<FieldDeclarationSyntax>()
                .FirstOrDefault(candidate => candidate.Declaration.Variables.Any(variable => variable.Identifier.ValueText == "implementation"));
            if (field == null)
                yield break;

            if (model.GetTypeInfo(field.Declaration.Type).Type is not INamedTypeSymbol delegateType ||
                delegateType.TypeKind != TypeKind.Delegate ||
                delegateType.DelegateInvokeMethod is not IMethodSymbol invoke)
                yield break;

            yield return ImplementationConstructor(declaration.Identifier.ValueText, field.Declaration.Type);
            yield return InvokeMethod(invoke, model.Compilation.Assembly, LocalPrefix(invoke));
        }

        static string LocalPrefix(IMethodSymbol invoke)
        {
            var prefix = "_";
            while (invoke.Parameters.Any(parameter => parameter.Name.StartsWith(prefix, StringComparison.Ordinal)))
                prefix += "_";
            return prefix;
        }

        static ConstructorDeclarationSyntax ImplementationConstructor(string name, TypeSyntax type)
            => ConstructorDeclaration(name)
                .WithAttributeLists(GeneratedAttribute())
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(ParameterList(SingletonSeparatedList(
                    Parameter(Identifier("implementation")).WithType(type))))
                .WithExpressionBody(ArrowExpressionClause(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        Dot(ThisExpression(), "implementation"),
                        IdentifierName("implementation"))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        static MethodDeclarationSyntax InvokeMethod(IMethodSymbol invoke, IAssemblySymbol assembly, string prefix)
        {
            var parameters = invoke.Parameters;
            var writesBack = parameters.Any(WritesBack);
            var method = prefix + "method";
            var statements = new List<StatementSyntax>
            {
                Var(method, Call(Dot(IdentifierName("MethodBase"), "GetCurrentMethod"))),
            };

            foreach (var parameter in parameters.Where(parameter => parameter.RefKind == RefKind.Out))
            {
                statements.Add(ExpressionStatement(AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(parameter.Name),
                    LiteralExpression(SyntaxKind.DefaultLiteralExpression))));
            }

            if (!writesBack)
            {
                statements.Add(IfStatement(
                    ImplementationIsNull(),
                    Block(PipelineCall(invoke, parameters, withImplementation: false, method, prefix)),
                    ElseClause(Block(PipelineCall(invoke, parameters, withImplementation: true, method, prefix)))));
            }
            else
            {
                var result = prefix + "result";
                statements.Add(Var(result, ConditionalExpression(
                    ImplementationIsNull(),
                    PipelineInvoke(parameters, withImplementation: false, invoke, method, prefix),
                    PipelineInvoke(parameters, withImplementation: true, invoke, method, prefix))));

                foreach (var parameter in parameters.Where(WritesBack))
                    statements.Add(ExpressionStatement(AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(parameter.Name),
                        Read(result, "Outputs", parameter))));

                if (!invoke.ReturnsVoid)
                {
                    statements.Add(ReturnStatement(CastExpression(
                        MemberScaffold.TypeName(invoke.ReturnType),
                        Call(
                            Dot(IdentifierName(result), nameof(MethodReturnExtensions.GetReturnValue)),
                            Argument(TypeOfExpression(MemberScaffold.TypeName(invoke.ReturnType)))))));
                }
            }

            var returnType = invoke.ReturnsVoid
                ? (TypeSyntax)PredefinedType(Token(SyntaxKind.VoidKeyword))
                : MemberScaffold.TypeName(invoke.ReturnType);

            return MethodDeclaration(returnType, "Invoke")
                .WithAttributeLists(GeneratedAttribute())
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(ParameterList(SeparatedList(parameters.Select(parameter => MemberScaffold.Parameter(parameter, assembly)))))
                .WithBody(Block(statements));
        }

        static StatementSyntax PipelineCall(IMethodSymbol invoke, IEnumerable<IParameterSymbol> parameters, bool withImplementation, string method, string prefix)
        {
            var create = CreateInvocation(parameters, withImplementation, invoke, method, prefix);
            ExpressionSyntax execute = invoke.ReturnsVoid
                ? Call(Dot(Dot(ThisExpression(), "pipeline"), "Execute"), Argument(create))
                : Call(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        Dot(ThisExpression(), "pipeline"),
                        GenericName("Execute").WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(MemberScaffold.TypeName(invoke.ReturnType))))),
                    Argument(create));

            return invoke.ReturnsVoid ? ExpressionStatement(execute) : ReturnStatement(execute);
        }

        static ExpressionSyntax PipelineInvoke(IEnumerable<IParameterSymbol> parameters, bool withImplementation, IMethodSymbol invoke, string method, string prefix)
            => Call(
                Dot(Dot(ThisExpression(), "pipeline"), "Invoke"),
                Argument(CreateInvocation(parameters, withImplementation, invoke, method, prefix)),
                Argument(LiteralExpression(SyntaxKind.TrueLiteralExpression)));

        static InvocationExpressionSyntax CreateInvocation(IEnumerable<IParameterSymbol> parameters, bool withImplementation, IMethodSymbol invoke, string method, string prefix)
        {
            var arguments = new List<ArgumentSyntax>
            {
                Argument(ThisExpression()),
                Argument(IdentifierName(method)),
            };
            if (withImplementation)
                arguments.Add(Argument(ImplementationLambda(invoke, method, prefix)));
            arguments.AddRange(parameters.Select(parameter => Argument(IdentifierName(parameter.Name))));
            return Call(Dot(IdentifierName("MethodInvocation"), "Create"), arguments.ToArray());
        }

        static LambdaExpressionSyntax ImplementationLambda(IMethodSymbol invoke, string method, string prefix)
        {
            var statements = new List<StatementSyntax>();
            var arguments = new List<ArgumentSyntax>();
            var writesBack = invoke.Parameters.Any(WritesBack);
            for (var index = 0; index < invoke.Parameters.Length; index++)
            {
                var parameter = invoke.Parameters[index];
                var local = prefix + "p" + index;
                statements.Add(Typed(ValueType(parameter.Type), local, Read("invocation", "Arguments", parameter)));

                var argument = Argument(IdentifierName(local));
                var keyword = RefKeyword(parameter.RefKind);
                if (keyword != null)
                    argument = argument.WithRefKindKeyword(keyword.Value);
                arguments.Add(argument);
            }

            var call = Call(Dot(ThisExpression(), "implementation"), arguments.ToArray());
            if (invoke.ReturnsVoid)
                statements.Add(ExpressionStatement(call));
            else if (writesBack)
                statements.Add(Typed(ValueType(invoke.ReturnType), prefix + "returned", call));

            if (invoke.ReturnsVoid && !writesBack)
            {
                statements.Add(ReturnStatement(Call(Dot(IdentifierName("invocation"), "CreateReturn"))));
            }
            else if (!writesBack)
            {
                statements.Add(ReturnStatement(Call(
                    Dot(IdentifierName("invocation"), "CreateValueReturn"),
                    Argument(call),
                    Argument(Dot(IdentifierName("invocation"), "Arguments")))));
            }
            else
            {
                var value = invoke.ReturnsVoid
                    ? (ExpressionSyntax)LiteralExpression(SyntaxKind.NullLiteralExpression)
                    : IdentifierName(prefix + "returned");
                statements.Add(ReturnStatement(Call(
                    Dot(IdentifierName("invocation"), "CreateValueReturn"),
                    Argument(value),
                    Argument(UpdatedArguments(invoke, method, prefix)))));
            }

            return ParenthesizedLambdaExpression()
                .WithParameterList(ParameterList(SeparatedList(new[]
                {
                    Parameter(Identifier("invocation")),
                    Parameter(Identifier("next")),
                })))
                .WithBody(Block(statements));
        }

        static ExpressionSyntax UpdatedArguments(IMethodSymbol invoke, string method, string prefix)
        {
            var elements = new List<ExpressionSyntax>();
            for (var index = 0; index < invoke.Parameters.Length; index++)
            {
                elements.Add(InitializerExpression(
                    SyntaxKind.ComplexElementInitializerExpression,
                    SeparatedList<ExpressionSyntax>(new ExpressionSyntax[]
                    {
                        LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(invoke.Parameters[index].Name)),
                        IdentifierName(prefix + "p" + index),
                    })));
            }

            return ObjectCreationExpression(IdentifierName("ArgumentCollection"))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(
                    Call(Dot(IdentifierName(method), "GetParameters"))))))
                .WithInitializer(InitializerExpression(
                    SyntaxKind.CollectionInitializerExpression,
                    SeparatedList(elements)));
        }

        static ExpressionSyntax Read(string target, string arguments, IParameterSymbol parameter)
            => Call(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    Dot(IdentifierName(target), arguments),
                    GenericName(IsNullableValue(parameter.Type) ? "GetNullable" : "Get")
                        .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(ValueType(parameter.Type))))),
                Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(parameter.Name))));

        static TypeSyntax ValueType(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named && IsNullableValue(type))
                return MemberScaffold.TypeName(named.TypeArguments[0]);
            return MemberScaffold.TypeName(type.WithNullableAnnotation(NullableAnnotation.None));
        }

        static bool IsNullableValue(ITypeSymbol type)
            => type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

        static bool WritesBack(IParameterSymbol parameter)
            => parameter.RefKind is RefKind.Ref or RefKind.Out;

        static SyntaxToken? RefKeyword(RefKind kind) => kind switch
        {
            RefKind.Ref => Token(SyntaxKind.RefKeyword),
            RefKind.Out => Token(SyntaxKind.OutKeyword),
            RefKind.In => Token(SyntaxKind.InKeyword),
            RefKind.RefReadOnlyParameter => Token(SyntaxKind.InKeyword),
            _ => null,
        };

        static ExpressionSyntax ImplementationIsNull()
            => BinaryExpression(
                SyntaxKind.EqualsExpression,
                Dot(ThisExpression(), "implementation"),
                LiteralExpression(SyntaxKind.NullLiteralExpression));

        static SyntaxList<AttributeListSyntax> GeneratedAttribute()
            => SingletonList(AttributeList(SingletonSeparatedList(Attribute(IdentifierName("CompilerGenerated")))));

        static MemberAccessExpressionSyntax Dot(ExpressionSyntax expression, string name)
            => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, expression, IdentifierName(name));

        static InvocationExpressionSyntax Call(ExpressionSyntax expression, params ArgumentSyntax[] arguments)
            => InvocationExpression(expression, ArgumentList(SeparatedList(arguments)));

        static LocalDeclarationStatementSyntax Var(string name, ExpressionSyntax initializer)
            => LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(name)).WithInitializer(EqualsValueClause(initializer)))));

        static LocalDeclarationStatementSyntax Typed(TypeSyntax type, string name, ExpressionSyntax initializer)
            => LocalDeclarationStatement(
                VariableDeclaration(type)
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(name)).WithInitializer(EqualsValueClause(initializer)))));
    }
}
