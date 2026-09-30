using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Stunts.Processors
{
    /// <summary>
    /// Copies custom attributes from the proxied API onto the generated stunt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Types and methods already surface inheritable attributes through
    /// <c>GetCustomAttributes(inherit: true)</c>, so only non-inheritable ones
    /// (<see cref="SerializableAttribute"/>, <see cref="System.Runtime.InteropServices.GuidAttribute"/>)
    /// are copied there. Properties, events, parameters, return values, and
    /// constructors do not inherit attribute data onto the override, so those
    /// are copied whether or not <see cref="AttributeUsageAttribute.Inherited"/> is set.
    /// </para>
    /// <para>
    /// Compiler attributes that the signature already spells (<c>params</c>, <c>in</c>,
    /// <c>out</c>, nullable) are left out so the generated declaration does not
    /// apply them twice.
    /// </para>
    /// </remarks>
    static class AttributeReplication
    {
        static readonly SymbolDisplayFormat TypeFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        // Written by the compiler from keywords and annotations we already emit.
        static readonly HashSet<string> Skip = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.ParamArrayAttribute",
            "System.Runtime.CompilerServices.AsyncStateMachineAttribute",
            "System.Runtime.CompilerServices.CompilerGeneratedAttribute",
            "System.Runtime.CompilerServices.DynamicAttribute",
            "System.Runtime.CompilerServices.ExtensionAttribute",
            "System.Runtime.CompilerServices.ExtensionMarkerAttribute",
            "System.Runtime.CompilerServices.IsByRefLikeAttribute",
            "System.Runtime.CompilerServices.IsReadOnlyAttribute",
            "System.Runtime.CompilerServices.IsUnmanagedAttribute",
            "System.Runtime.CompilerServices.IteratorStateMachineAttribute",
            "System.Runtime.CompilerServices.NativeIntegerAttribute",
            "System.Runtime.CompilerServices.NullableAttribute",
            "System.Runtime.CompilerServices.NullableContextAttribute",
            "System.Runtime.CompilerServices.ParamCollectionAttribute",
            "System.Runtime.CompilerServices.PreserveBaseOverridesAttribute",
            "System.Runtime.CompilerServices.RefSafetyRulesAttribute",
            "System.Runtime.CompilerServices.RequiredMemberAttribute",
            "System.Runtime.CompilerServices.RequiresLocationAttribute",
            "System.Runtime.CompilerServices.ScopedRefAttribute",
            "System.Runtime.CompilerServices.SetsRequiredMembersAttribute",
            "System.Runtime.CompilerServices.TupleElementNamesAttribute",
            "System.Runtime.InteropServices.ComImportAttribute",
            "System.Runtime.InteropServices.TypeIdentifierAttribute",
        };

        public static SyntaxList<AttributeListSyntax> Replicate(ISymbol symbol, AttributeTargets target, IAssemblySymbol assembly, bool includeInherited)
        {
            var lists = new List<AttributeListSyntax>();
            foreach (var attribute in symbol.GetAttributes())
            {
                if (Replicate(attribute, symbol, target, assembly, includeInherited, targetKind: null) is AttributeListSyntax list)
                    lists.Add(list);
            }

            return List(lists);
        }

        /// <summary>
        /// Optional values are not ordinary attributes on a source symbol. The compiler
        /// stores them as <see cref="System.Runtime.InteropServices.OptionalAttribute"/> and
        /// <see cref="System.Runtime.InteropServices.DefaultParameterValueAttribute"/>
        /// (or <see cref="System.Runtime.CompilerServices.DecimalConstantAttribute"/> /
        /// <see cref="System.Runtime.CompilerServices.DateTimeConstantAttribute"/>).
        /// Spelling <c>= value</c> on an override is CS1066, so the attributes are emitted directly.
        /// </summary>
        public static SyntaxList<AttributeListSyntax> Defaults(IParameterSymbol parameter)
        {
            if (!parameter.HasExplicitDefaultValue || parameter.IsParams || HasDefaultAttribute(parameter))
                return default;

            var lists = new List<AttributeListSyntax>
            {
                AttributeList(SingletonSeparatedList(Attribute(ParseName("global::System.Runtime.InteropServices.OptionalAttribute")))),
            };
            if (ValueAttribute(parameter) is AttributeListSyntax value)
                lists.Add(value);
            return List(lists);
        }

        static bool HasDefaultAttribute(IParameterSymbol parameter)
        {
            foreach (var attribute in parameter.GetAttributes())
            {
                if (attribute.AttributeClass == null)
                    continue;
                switch (MetadataName(attribute.AttributeClass))
                {
                    case "System.Runtime.InteropServices.OptionalAttribute":
                    case "System.Runtime.InteropServices.DefaultParameterValueAttribute":
                    case "System.Runtime.CompilerServices.DecimalConstantAttribute":
                    case "System.Runtime.CompilerServices.DateTimeConstantAttribute":
                        return true;
                }
            }

            return false;
        }

        static AttributeListSyntax? ValueAttribute(IParameterSymbol parameter)
        {
            var type = parameter.Type;
            var value = parameter.ExplicitDefaultValue;
            if (type.SpecialType == SpecialType.System_Decimal && value is decimal number)
                return DecimalConstant(number);
            if (type.SpecialType == SpecialType.System_DateTime && value is DateTime time)
                return DateTimeConstant(time);
            if (Constant(value, type) is not ExpressionSyntax expression)
                return null;

            return AttributeList(SingletonSeparatedList(
                Attribute(ParseName("global::System.Runtime.InteropServices.DefaultParameterValueAttribute"))
                    .WithArgumentList(AttributeArgumentList(SingletonSeparatedList(AttributeArgument(expression))))));
        }

        static AttributeListSyntax DecimalConstant(decimal number)
        {
            var bits = decimal.GetBits(number);
            var scale = (byte)((bits[3] >> 16) & 0x7F);
            var sign = (bits[3] & int.MinValue) != 0 ? (byte)1 : (byte)0;
            return AttributeList(SingletonSeparatedList(
                Attribute(ParseName("global::System.Runtime.CompilerServices.DecimalConstantAttribute"))
                    .WithArgumentList(AttributeArgumentList(SeparatedList(new[]
                    {
                        AttributeArgument(CastExpression(PredefinedType(Token(SyntaxKind.ByteKeyword)), Number(scale))),
                        AttributeArgument(CastExpression(PredefinedType(Token(SyntaxKind.ByteKeyword)), Number(sign))),
                        AttributeArgument(Number(bits[2])),
                        AttributeArgument(Number(bits[1])),
                        AttributeArgument(Number(bits[0])),
                    })))));
        }

        static AttributeListSyntax DateTimeConstant(DateTime time)
            => AttributeList(SingletonSeparatedList(
                Attribute(ParseName("global::System.Runtime.CompilerServices.DateTimeConstantAttribute"))
                    .WithArgumentList(AttributeArgumentList(SingletonSeparatedList(
                        AttributeArgument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(time.Ticks))))))));

        static ExpressionSyntax? Constant(object? value, ITypeSymbol type)
        {
            if (value == null)
                return CastExpression(TypeName(type), LiteralExpression(SyntaxKind.NullLiteralExpression));
            if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumType && enumType.EnumUnderlyingType != null)
            {
                var literal = Primitive(value, enumType.EnumUnderlyingType);
                if (literal == null)
                    return null;
                return CastExpression(TypeName(enumType), literal is LiteralExpressionSyntax ? literal : ParenthesizedExpression(literal));
            }

            if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && named.TypeArguments.Length == 1)
                type = named.TypeArguments[0];
            return Primitive(value, type);
        }

        public static SyntaxList<AttributeListSyntax> ReplicateReturn(IMethodSymbol method, IAssemblySymbol assembly)
        {
            var lists = new List<AttributeListSyntax>();
            foreach (var attribute in method.GetReturnTypeAttributes())
            {
                if (Replicate(attribute, method, AttributeTargets.ReturnValue, assembly, includeInherited: true, SyntaxKind.ReturnKeyword) is AttributeListSyntax list)
                    lists.Add(list);
            }

            return List(lists);
        }

        static AttributeListSyntax? Replicate(AttributeData attribute, ISymbol owner, AttributeTargets target, IAssemblySymbol assembly, bool includeInherited, SyntaxKind? targetKind)
        {
            if (attribute.AttributeClass is not INamedTypeSymbol attributeClass || attributeClass.TypeKind == TypeKind.Error)
                return null;
            if (attribute.AttributeConstructor == null)
                return null;
            if (!includeInherited && Inherited(attributeClass))
                return null;
            if ((Targets(attributeClass) & target) == 0)
                return null;
            if (Skip.Contains(MetadataName(attributeClass)) || IsSecurityAttribute(attributeClass))
                return null;
            if (owner is IParameterSymbol parameter && ImpliedBySignature(parameter, MetadataName(attributeClass)))
                return null;
            if (!Accessible(attributeClass, assembly) || !Accessible(attribute.AttributeConstructor, assembly))
                return null;

            var arguments = new List<AttributeArgumentSyntax>();
            foreach (var constant in attribute.ConstructorArguments)
            {
                if (Expression(constant) is not ExpressionSyntax value)
                    return null;
                arguments.Add(AttributeArgument(value));
            }

            foreach (var named in attribute.NamedArguments)
            {
                if (!NamedAccessible(attributeClass, named.Key, assembly))
                    return null;
                if (Expression(named.Value) is not ExpressionSyntax value)
                    return null;
                arguments.Add(AttributeArgument(NameEquals(IdentifierName(named.Key)), null, value));
            }

            var syntax = Attribute(ParseName(attributeClass.ToDisplayString(TypeFormat)));
            if (arguments.Count > 0)
                syntax = syntax.WithArgumentList(AttributeArgumentList(SeparatedList(arguments)));

            var list = AttributeList(SingletonSeparatedList(syntax));
            return targetKind == null
                ? list
                : list.WithTarget(AttributeTargetSpecifier(Token(targetKind.Value)));
        }

        // `in`, `out`, and `params` already emit these. Copying them again is CS0579.
        static bool ImpliedBySignature(IParameterSymbol parameter, string metadataName)
        {
            if (metadataName == "System.ParamArrayAttribute")
                return true;
            if (metadataName == "System.Runtime.InteropServices.InAttribute" && parameter.RefKind == RefKind.In)
                return true;
            if (metadataName == "System.Runtime.InteropServices.OutAttribute" && parameter.RefKind == RefKind.Out)
                return true;
            return false;
        }

        static bool Inherited(INamedTypeSymbol attributeClass)
        {
            var usage = Usage(attributeClass);
            if (usage == null)
                return true;

            foreach (var named in usage.NamedArguments)
            {
                if (named.Key == nameof(AttributeUsageAttribute.Inherited) && named.Value.Value is bool inherited)
                    return inherited;
            }

            return true;
        }

        static AttributeTargets Targets(INamedTypeSymbol attributeClass)
        {
            var usage = Usage(attributeClass);
            if (usage == null || usage.ConstructorArguments.Length == 0 || usage.ConstructorArguments[0].Value == null)
                return AttributeTargets.All;

            return (AttributeTargets)Convert.ToInt32(usage.ConstructorArguments[0].Value);
        }

        static AttributeData? Usage(INamedTypeSymbol attributeClass)
        {
            for (var type = attributeClass; type != null; type = type.BaseType)
            {
                var usage = type.GetAttributes().FirstOrDefault(attribute =>
                    attribute.AttributeClass != null && MetadataName(attribute.AttributeClass) == "System.AttributeUsageAttribute");
                if (usage != null)
                    return usage;
            }

            return null;
        }

        static bool IsSecurityAttribute(INamedTypeSymbol attributeClass)
        {
            for (var type = attributeClass; type != null; type = type.BaseType)
            {
                if (MetadataName(type) == "System.Security.Permissions.SecurityAttribute")
                    return true;
            }

            return false;
        }

        static bool NamedAccessible(INamedTypeSymbol attributeClass, string name, IAssemblySymbol assembly)
        {
            for (var type = attributeClass; type != null; type = type.BaseType)
            {
                var member = type.GetMembers(name).FirstOrDefault(candidate => candidate is IPropertySymbol or IFieldSymbol);
                if (member is IPropertySymbol property)
                    return property.SetMethod != null && Accessible(property.SetMethod, assembly);
                if (member is IFieldSymbol field)
                    return Accessible(field, assembly);
            }

            return false;
        }

        static bool Accessible(ISymbol symbol, IAssemblySymbol from)
        {
            for (ISymbol? current = symbol; current != null && current is not INamespaceSymbol; current = current.ContainingSymbol)
            {
                switch (current.DeclaredAccessibility)
                {
                    case Accessibility.Public:
                        continue;
                    case Accessibility.Internal:
                    case Accessibility.ProtectedOrInternal:
                        if (SymbolEqualityComparer.Default.Equals(current.ContainingAssembly, from) ||
                            current.ContainingAssembly.GivesAccessTo(from))
                            continue;
                        return false;
                    default:
                        return false;
                }
            }

            return true;
        }

        static string MetadataName(INamedTypeSymbol type)
        {
            var name = type.ContainingType != null ? MetadataName(type.ContainingType) + "+" + type.Name : type.Name;
            var ns = type.ContainingNamespace;
            if (type.ContainingType != null || ns == null || ns.IsGlobalNamespace)
                return name;
            return ns.ToDisplayString() + "." + name;
        }

        static ExpressionSyntax? Expression(TypedConstant constant)
        {
            if (constant.Type == null || constant.Type.TypeKind == TypeKind.Error || constant.Kind == TypedConstantKind.Error)
                return null;
            if (constant.IsNull)
                return CastExpression(TypeName(constant.Type), LiteralExpression(SyntaxKind.NullLiteralExpression));

            switch (constant.Kind)
            {
                case TypedConstantKind.Primitive:
                    return Primitive(constant.Value, constant.Type);
                case TypedConstantKind.Enum:
                    if (constant.Type is not INamedTypeSymbol enumType || enumType.EnumUnderlyingType == null)
                        return null;
                    var literal = Primitive(constant.Value, enumType.EnumUnderlyingType);
                    if (literal == null)
                        return null;
                    return CastExpression(TypeName(enumType), literal is LiteralExpressionSyntax ? literal : ParenthesizedExpression(literal));
                case TypedConstantKind.Type:
                    return constant.Value is ITypeSymbol type && type.TypeKind != TypeKind.Error
                        ? TypeOfExpression(TypeName(type))
                        : null;
                case TypedConstantKind.Array:
                    return constant.Type is IArrayTypeSymbol array ? Array(array, constant.Values) : null;
                default:
                    return null;
            }
        }

        static ExpressionSyntax? Array(IArrayTypeSymbol array, ImmutableArray<TypedConstant> values)
        {
            var elements = new List<ExpressionSyntax>();
            foreach (var value in values)
            {
                if (Expression(value) is not ExpressionSyntax element)
                    return null;
                elements.Add(element);
            }

            return ArrayCreationExpression(
                ArrayType(TypeName(array.ElementType)).WithRankSpecifiers(
                    SingletonList(ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(OmittedArraySizeExpression())))),
                InitializerExpression(SyntaxKind.ArrayInitializerExpression, SeparatedList(elements)));
        }

        static ExpressionSyntax? Primitive(object? value, ITypeSymbol type)
        {
            if (value == null)
                return null;

            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return LiteralExpression(Convert.ToBoolean(value) ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression);
                case SpecialType.System_String:
                    return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal((string)value));
                case SpecialType.System_Char:
                    return LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal(Convert.ToChar(value)));
                case SpecialType.System_SByte:
                    return CastExpression(PredefinedType(Token(SyntaxKind.SByteKeyword)), Number(Convert.ToInt32(value)));
                case SpecialType.System_Byte:
                    return CastExpression(PredefinedType(Token(SyntaxKind.ByteKeyword)), Number(Convert.ToInt32(value)));
                case SpecialType.System_Int16:
                    return CastExpression(PredefinedType(Token(SyntaxKind.ShortKeyword)), Number(Convert.ToInt32(value)));
                case SpecialType.System_UInt16:
                    return CastExpression(PredefinedType(Token(SyntaxKind.UShortKeyword)), Number(Convert.ToInt32(value)));
                case SpecialType.System_Int32:
                    return Number(Convert.ToInt32(value));
                case SpecialType.System_UInt32:
                    return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(Convert.ToUInt32(value)));
                case SpecialType.System_Int64:
                    return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(Convert.ToInt64(value)));
                case SpecialType.System_UInt64:
                    return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(Convert.ToUInt64(value)));
                case SpecialType.System_Single:
                    var single = Convert.ToSingle(value);
                    if (float.IsNaN(single))
                        return Member(nameof(System.Single), nameof(float.NaN));
                    if (float.IsPositiveInfinity(single))
                        return Member(nameof(System.Single), nameof(float.PositiveInfinity));
                    if (float.IsNegativeInfinity(single))
                        return Member(nameof(System.Single), nameof(float.NegativeInfinity));
                    return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(single));
                case SpecialType.System_Double:
                    var real = Convert.ToDouble(value);
                    if (double.IsNaN(real))
                        return Member(nameof(System.Double), nameof(double.NaN));
                    if (double.IsPositiveInfinity(real))
                        return Member(nameof(System.Double), nameof(double.PositiveInfinity));
                    if (double.IsNegativeInfinity(real))
                        return Member(nameof(System.Double), nameof(double.NegativeInfinity));
                    return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(real));
                case SpecialType.System_Decimal:
                    return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(Convert.ToDecimal(value)));
                default:
                    return null;
            }
        }

        static LiteralExpressionSyntax Number(int value)
            => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));

        static ExpressionSyntax Member(string type, string member)
            => MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                ParseName("global::System." + type),
                IdentifierName(member));

        static TypeSyntax TypeName(ITypeSymbol type) => ParseTypeName(type.ToDisplayString(TypeFormat));
    }
}
