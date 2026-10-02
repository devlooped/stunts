using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Stunts.CodeAnalysis;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Stunts
{
    sealed class GenericStuntTemplate
    {
        static readonly SymbolDisplayFormat Format = new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        readonly Dictionary<ITypeParameterSymbol, string> names = new(SymbolEqualityComparer.Default);
        readonly List<ITypeParameterSymbol> parameters = new();
        readonly HashSet<string> reserved = new();
        readonly INamedTypeSymbol[] types;

        GenericStuntTemplate(INamedTypeSymbol[] types)
        {
            this.types = types;
            foreach (var type in types)
                Collect(type);
            for (var i = 0; i < parameters.Count; i++)
                foreach (var constraint in parameters[i].ConstraintTypes)
                    Collect(constraint);
            if (parameters.Count == 0)
                return;

            foreach (var type in types)
            {
                for (var outer = type.ContainingType; outer != null; outer = outer.ContainingType)
                    foreach (var parameter in outer.TypeParameters)
                        reserved.Add(parameter.Name);
                for (var current = type; current != null; current = current.BaseType)
                    ReserveMethods(current);
                foreach (var iface in type.AllInterfaces)
                    ReserveMethods(iface);
            }
            for (var i = 0; i < parameters.Count; i++)
            {
                var name = "T" + i;
                while (!reserved.Add(name))
                    name += "_";
                names[parameters[i]] = name;
            }
        }

        public IReadOnlyList<ITypeParameterSymbol> Parameters => parameters;

        public static GenericStuntTemplate? Create(params INamedTypeSymbol[] types)
        {
            var template = new GenericStuntTemplate(types);
            return template.parameters.Count == 0 ? null : template;
        }

        void ReserveMethods(INamedTypeSymbol type)
        {
            foreach (var parameter in type.GetMembers().OfType<IMethodSymbol>().SelectMany(method => method.TypeParameters))
                reserved.Add(parameter.Name);
        }

        void Collect(ITypeSymbol type)
        {
            if (type is ITypeParameterSymbol parameter)
            {
                if (!names.ContainsKey(parameter))
                {
                    names.Add(parameter, "");
                    parameters.Add(parameter);
                }
            }
            else if (type is IArrayTypeSymbol array)
                Collect(array.ElementType);
            else if (type is IPointerTypeSymbol pointer)
                Collect(pointer.PointedAtType);
            else if (type is INamedTypeSymbol named)
            {
                if (named.ContainingType != null)
                    Collect(named.ContainingType);
                foreach (var argument in named.TypeArguments)
                    Collect(argument);
            }
        }

        public string Display(ITypeSymbol type)
            => string.Concat(type.ToDisplayParts(Format).Select(part =>
                part.Symbol is ITypeParameterSymbol parameter && names.TryGetValue(parameter, out var name)
                    ? name : part.ToString()));

        public string GetName(NamingConvention naming, string prefix = "Generic")
        {
            var signature = string.Join(";", types.Select(Display)) +
                Declaration(ClassDeclaration("Template")).NormalizeWhitespace().ToFullString();
            using var hash = SHA256.Create();
            var suffix = string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(signature)).Select(value => value.ToString("x2")));
            return prefix + types[0].Name + naming.NameSuffix + suffix;
        }

        public TypeDeclarationSyntax Declaration(TypeDeclarationSyntax declaration)
        {
            declaration = declaration.WithTypeParameterList(TypeParameterList(SeparatedList(
                parameters.Select(parameter => TypeParameter(names[parameter])))));
            var clauses = new List<TypeParameterConstraintClauseSyntax>();
            foreach (var parameter in parameters)
            {
                var constraints = new List<TypeParameterConstraintSyntax>();
                if (parameter.HasUnmanagedTypeConstraint)
                    constraints.Add(TypeConstraint(IdentifierName("unmanaged")));
                else if (parameter.HasValueTypeConstraint)
                    constraints.Add(ClassOrStructConstraint(SyntaxKind.StructConstraint));
                else if (parameter.HasReferenceTypeConstraint)
                {
                    var constraint = ClassOrStructConstraint(SyntaxKind.ClassConstraint);
                    if (parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated)
                        constraint = constraint.WithQuestionToken(Token(SyntaxKind.QuestionToken));
                    constraints.Add(constraint);
                }
                else if (parameter.HasNotNullConstraint)
                    constraints.Add(TypeConstraint(IdentifierName("notnull")));
                foreach (var constraint in parameter.ConstraintTypes)
                    constraints.Add(TypeConstraint(ParseTypeName(Display(constraint))));
                if (parameter.HasConstructorConstraint)
                    constraints.Add(ConstructorConstraint());
                if (constraints.Count > 0)
                    clauses.Add(TypeParameterConstraintClause(names[parameter]).WithConstraints(SeparatedList(constraints)));
            }

            return declaration.WithConstraintClauses(List(clauses));
        }
    }
}
