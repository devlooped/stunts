using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Stunts
{
    static class SyntaxFactoryGenerator
    {
        public static LiteralExpressionSyntax NullLiteralExpression => SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);

        public static LiteralExpressionSyntax TrueLiteralExpression => SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);

        public static LiteralExpressionSyntax FalseLiteralExpression => SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);

        public static LiteralExpressionSyntax DefaultLiteralExpression => SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression, Token(SyntaxKind.DefaultKeyword));

        public static ArgumentSyntax Argument(SyntaxToken identifier) => SyntaxFactory.Argument(IdentifierName(identifier.ToString()));

        public static ArgumentSyntax Argument(string identifier) => SyntaxFactory.Argument(IdentifierName(identifier));

        /// <summary>
        /// Creates an assignment with <see cref="SyntaxKind.SimpleAssignmentExpression"/>.
        /// </summary>
        public static AssignmentExpressionSyntax AssignmentExpression(ExpressionSyntax left, ExpressionSyntax right)
            => SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, left, right);

        /// <summary>
        /// Creates an assignment with <see cref="SyntaxKind.SimpleAssignmentExpression"/> that assigns
        /// the given <paramref name="identifier"/> to the <paramref name="assignment"/>.
        /// </summary>
        public static AssignmentExpressionSyntax AssignmentExpression(string identifier, ExpressionSyntax assignment)
            => SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(identifier), assignment);

        /// <summary>
        /// Creates an assignment with <see cref="SyntaxKind.SimpleAssignmentExpression"/> that assigns
        /// the given <paramref name="identifier"/> to the <paramref name="assignment"/>.
        /// </summary>
        public static AssignmentExpressionSyntax AssignmentExpression(SyntaxToken identifier, ExpressionSyntax assignment)
            => SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(identifier), assignment);

        public static GenericNameSyntax GenericName(string identifier, params TypeSyntax[] typeArguments)
            => GenericName(identifier, (IEnumerable<TypeSyntax>)typeArguments);

        public static GenericNameSyntax GenericName(string identifier, IEnumerable<TypeSyntax> typeArguments)
            => SyntaxFactory.GenericName(Identifier(identifier), TypeArgumentList(SeparatedList(typeArguments)));

        public static InitializerExpressionSyntax InitializerExpression(SyntaxKind kind, params ExpressionSyntax[] expressions)
            => InitializerExpression(kind, (IEnumerable<ExpressionSyntax>)expressions);

        public static InitializerExpressionSyntax InitializerExpression(SyntaxKind kind, IEnumerable<ExpressionSyntax> expressions)
            => SyntaxFactory.InitializerExpression(kind, SeparatedList(expressions));

        public static InvocationExpressionSyntax InvocationExpression(string identifier, string memberName)
            => InvocationExpression(MemberAccessExpression(identifier, memberName), Array.Empty<ArgumentSyntax>());

        public static InvocationExpressionSyntax InvocationExpression(string identifier, string memberName, IEnumerable<ArgumentSyntax> arguments)
            => InvocationExpression(MemberAccessExpression(identifier, memberName), arguments);

        public static InvocationExpressionSyntax InvocationExpression(string identifier, string memberName, params ArgumentSyntax[] arguments)
            => InvocationExpression(MemberAccessExpression(identifier, memberName), (IEnumerable<ArgumentSyntax>)arguments);

        public static InvocationExpressionSyntax InvocationExpression(ExpressionSyntax expression, string memberName, params ArgumentSyntax[] arguments)
            => InvocationExpression(MemberAccessExpression(expression, memberName), (IEnumerable<ArgumentSyntax>)arguments);

        public static InvocationExpressionSyntax InvocationExpression(string identifier, SimpleNameSyntax name, params ArgumentSyntax[] arguments)
            => InvocationExpression(IdentifierName(identifier), name, arguments);

        public static InvocationExpressionSyntax InvocationExpression(ExpressionSyntax expression, SimpleNameSyntax name, params ArgumentSyntax[] arguments)
            => InvocationExpression(MemberAccessExpression(expression, name), (IEnumerable<ArgumentSyntax>)arguments);

        public static InvocationExpressionSyntax InvocationExpression(ExpressionSyntax expression, params ArgumentSyntax[] arguments)
            => InvocationExpression(expression, (IEnumerable<ArgumentSyntax>)arguments);

        public static InvocationExpressionSyntax InvocationExpression(ExpressionSyntax expression, IEnumerable<ArgumentSyntax> arguments)
            => SyntaxFactory.InvocationExpression(expression, ArgumentList(SeparatedList(arguments)));

        public static LambdaExpressionSyntax LambdaExpression(IEnumerable<ParameterSyntax> parameters, ExpressionSyntax expression)
            => ParenthesizedLambdaExpression()
                .WithParameterList(ParameterList(SeparatedList(parameters)))
                .WithExpressionBody(expression);

        public static LambdaExpressionSyntax LambdaExpression(ParameterSyntax[] parameters, ExpressionSyntax expression)
            => LambdaExpression((IEnumerable<ParameterSyntax>)parameters, expression);

        public static LambdaExpressionSyntax LambdaExpression(ParameterSyntax parameter, ExpressionSyntax expression)
            => SimpleLambdaExpression(parameter).WithExpressionBody(expression);

        public static LambdaExpressionSyntax LambdaExpression(ParameterSyntax parameter, IEnumerable<StatementSyntax> statements)
            => LambdaExpression(parameter, statements.ToArray());

        public static LambdaExpressionSyntax LambdaExpression(ParameterSyntax parameter, params StatementSyntax[] statements)
        {
            if (statements.Length == 1 && statements[0] is ExpressionStatementSyntax statement)
                return LambdaExpression(parameter, statement.Expression);

            return SimpleLambdaExpression(parameter).WithBody(Block(statements));
        }

        public static LambdaExpressionSyntax LambdaExpression(IEnumerable<ParameterSyntax> parameters, params StatementSyntax[] statements)
            => LambdaExpression(parameters.ToArray(), statements);

        public static LambdaExpressionSyntax LambdaExpression(IEnumerable<ParameterSyntax> parameters, IEnumerable<StatementSyntax> statements)
            => LambdaExpression(parameters.ToArray(), statements.ToArray());

        public static LambdaExpressionSyntax LambdaExpression(ParameterSyntax[] parameters, params StatementSyntax[] statements)
        {
            if (statements.Length == 1 && statements[0] is ExpressionStatementSyntax statement)
                return LambdaExpression(parameters, statement.Expression);

            return ParenthesizedLambdaExpression()
                .WithParameterList(ParameterList(SeparatedList(parameters)))
                .WithBody(Block(statements));
        }

        public static MemberAccessExpressionSyntax MemberAccessExpression(string identifier, string memberName)
            => MemberAccessExpression(IdentifierName(identifier), IdentifierName(memberName));

        public static MemberAccessExpressionSyntax MemberAccessExpression(ExpressionSyntax expression, string memberName)
            => MemberAccessExpression(expression, IdentifierName(memberName));

        public static MemberAccessExpressionSyntax MemberAccessExpression(ExpressionSyntax expression, SimpleNameSyntax name)
            => SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, expression, name);

        public static ObjectCreationExpressionSyntax ObjectCreationExpression(string identifier, params ArgumentSyntax[] arguments)
            => ObjectCreationExpression(identifier, (IEnumerable<ArgumentSyntax>)arguments);

        public static ObjectCreationExpressionSyntax ObjectCreationExpression(string identifier, IEnumerable<ArgumentSyntax> arguments)
            => ObjectCreationExpression(IdentifierName(identifier), arguments);

        public static ObjectCreationExpressionSyntax ObjectCreationExpression(TypeSyntax type, params ArgumentSyntax[] arguments)
            => ObjectCreationExpression(type, (IEnumerable<ArgumentSyntax>)arguments);

        public static ObjectCreationExpressionSyntax ObjectCreationExpression(TypeSyntax type, IEnumerable<ArgumentSyntax> arguments)
            => SyntaxFactory.ObjectCreationExpression(type, ArgumentList(SeparatedList(arguments)), null);

        public static ParameterSyntax Parameter(string identifier)
            => SyntaxFactory.Parameter(Identifier(identifier));

        public static ParameterSyntax Parameter(string identifier, TypeSyntax type)
            => SyntaxFactory.Parameter(Identifier(identifier)).WithType(type);

        public static VariableDeclarationSyntax VariableDeclaration(string identifier, TypeSyntax type, ExpressionSyntax? initializer)
            => SyntaxFactory.VariableDeclaration(
                type,
                SeparatedList(
                    SingletonList(
                        VariableDeclarator(identifier)
                        .WithInitializer(initializer == null ? null :
                            EqualsValueClause(initializer)))));

        /// <summary>
        /// Declares an implicitly typed variable (<c>var</c>) with the given initializer.
        /// </summary>
        public static VariableDeclarationSyntax VariableDeclaration(string identifier, ExpressionSyntax initializer)
            => SyntaxFactory.VariableDeclaration(
                IdentifierName("var"),
                SeparatedList(
                    SingletonList(
                        VariableDeclarator(identifier)
                        .WithInitializer(
                            EqualsValueClause(initializer)))));

        public static LiteralExpressionSyntax LiteralExpression(int value)
            => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));

        public static LiteralExpressionSyntax LiteralExpression(uint value)
            => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));

        public static LiteralExpressionSyntax LiteralExpression(long value)
            => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));

        public static LiteralExpressionSyntax LiteralExpression(ulong value)
            => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));

        public static LiteralExpressionSyntax LiteralExpression(float value)
            => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));

        public static LiteralExpressionSyntax LiteralExpression(double value)
            => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));

        public static LiteralExpressionSyntax LiteralExpression(decimal value)
            => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));

        public static LiteralExpressionSyntax LiteralExpression(string value)
            => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(value));

        public static LiteralExpressionSyntax LiteralExpression(char value)
            => SyntaxFactory.LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal(value));
    }
}
