using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Stunts
{
    static class DataAnnotationsCatalog
    {
        const string ValidationAttribute = "System.ComponentModel.DataAnnotations.ValidationAttribute";
        const string DataAnnotations = "System.ComponentModel.DataAnnotations";

        static readonly DiagnosticDescriptor Unsupported = new(
            "STX001",
            "Validation attribute is not enforced",
            "'{0}' is not a built-in validation attribute this generator can enforce without reflection",
            "Stunts",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        static readonly DiagnosticDescriptor Skipped = new(
            "STX002",
            "Validation attribute was skipped",
            "{0}",
            "Stunts",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
        {
            DataAnnotations + ".RequiredAttribute",
            DataAnnotations + ".StringLengthAttribute",
            DataAnnotations + ".MinLengthAttribute",
            DataAnnotations + ".MaxLengthAttribute",
            DataAnnotations + ".LengthAttribute",
            DataAnnotations + ".RangeAttribute",
            DataAnnotations + ".RegularExpressionAttribute",
            DataAnnotations + ".CompareAttribute",
            DataAnnotations + ".EmailAddressAttribute",
            DataAnnotations + ".PhoneAttribute",
            DataAnnotations + ".CreditCardAttribute",
            DataAnnotations + ".UrlAttribute",
            DataAnnotations + ".EnumDataTypeAttribute",
            DataAnnotations + ".AllowedValuesAttribute",
            DataAnnotations + ".DeniedValuesAttribute",
            DataAnnotations + ".Base64StringAttribute",
            DataAnnotations + ".CustomValidationAttribute",
            DataAnnotations + ".FileExtensionsAttribute",
        };

        static readonly Dictionary<string, string> Parsers = new(StringComparer.Ordinal)
        {
            ["System.Boolean"] = "global::System.Boolean.TryParse({0}, out {1})",
            ["System.Byte"] = "global::System.Byte.TryParse({0}, global::System.Globalization.NumberStyles.Any, {2}, out {1})",
            ["System.SByte"] = "global::System.SByte.TryParse({0}, global::System.Globalization.NumberStyles.Any, {2}, out {1})",
            ["System.Int16"] = "global::System.Int16.TryParse({0}, global::System.Globalization.NumberStyles.Any, {2}, out {1})",
            ["System.UInt16"] = "global::System.UInt16.TryParse({0}, global::System.Globalization.NumberStyles.Any, {2}, out {1})",
            ["System.Int32"] = "global::System.Int32.TryParse({0}, global::System.Globalization.NumberStyles.Any, {2}, out {1})",
            ["System.UInt32"] = "global::System.UInt32.TryParse({0}, global::System.Globalization.NumberStyles.Any, {2}, out {1})",
            ["System.Int64"] = "global::System.Int64.TryParse({0}, global::System.Globalization.NumberStyles.Any, {2}, out {1})",
            ["System.UInt64"] = "global::System.UInt64.TryParse({0}, global::System.Globalization.NumberStyles.Any, {2}, out {1})",
            ["System.Single"] = "global::System.Single.TryParse({0}, global::System.Globalization.NumberStyles.Any, {2}, out {1})",
            ["System.Double"] = "global::System.Double.TryParse({0}, global::System.Globalization.NumberStyles.Any, {2}, out {1})",
            ["System.Decimal"] = "global::System.Decimal.TryParse({0}, global::System.Globalization.NumberStyles.Any, {2}, out {1})",
            ["System.DateTime"] = "global::System.DateTime.TryParse({0}, {2}, global::System.Globalization.DateTimeStyles.None, out {1})",
            ["System.DateTimeOffset"] = "global::System.DateTimeOffset.TryParse({0}, {2}, global::System.Globalization.DateTimeStyles.None, out {1})",
            ["System.TimeSpan"] = "global::System.TimeSpan.TryParse({0}, {2}, out {1})",
            ["System.Guid"] = "global::System.Guid.TryParse({0}, out {1})",
            ["System.DateOnly"] = "global::System.DateOnly.TryParse({0}, {2}, global::System.Globalization.DateTimeStyles.None, out {1})",
            ["System.TimeOnly"] = "global::System.TimeOnly.TryParse({0}, {2}, global::System.Globalization.DateTimeStyles.None, out {1})",
        };

        public static void Emit(SourceProductionContext source, Compilation compilation)
        {
            var catalog = new Catalog(compilation);
            catalog.CollectSyntax();
            catalog.CollectSymbols();
            foreach (var diagnostic in catalog.Diagnostics)
                source.ReportDiagnostic(diagnostic);

            var text = catalog.Render();
            if (text != null)
                source.AddSource("DataAnnotationsRegistrations.g.cs", SourceText.From(text, Encoding.UTF8));
        }

        sealed class Catalog
        {
            readonly Compilation compilation;
            readonly HashSet<INamedTypeSymbol> candidates = new(SymbolEqualityComparer.Default);
            readonly HashSet<INamedTypeSymbol> stuntTargets = new(SymbolEqualityComparer.Default);
            readonly List<Diagnostic> diagnostics = new();
            readonly List<string> regexFields = new();
            readonly List<TypeModel> types = new();
            readonly HashSet<string> methodNames = new(StringComparer.Ordinal);
            readonly Dictionary<SyntaxTree, SemanticModel> models = new();
            List<SyntaxNode> annotated = new();

            public Catalog(Compilation compilation) => this.compilation = compilation;

            public List<Diagnostic> Diagnostics => diagnostics;

            public void CollectSyntax()
            {
                var invocations = new List<(InvocationExpressionSyntax Invocation, string Name)>();
                var registrations = new List<AttributeSyntax>();
                var factories = new List<SyntaxNode>();
                var annotated = new List<SyntaxNode>();

                foreach (var tree in compilation.SyntaxTrees)
                {
                    var text = tree.GetText();
                    if (text.Length > 0)
                    {
                        var head = text.ToString(new TextSpan(0, Math.Min(text.Length, 240)));
                        if (head.IndexOf("auto-generated", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var body = text.ToString();
                            if (body.IndexOf("StuntGenerator", StringComparison.Ordinal) < 0 &&
                                body.IndexOf("Validated", StringComparison.Ordinal) < 0)
                                continue;
                        }
                    }

                    foreach (var node in tree.GetRoot().DescendantNodes())
                    {
                        switch (node)
                        {
                            case InvocationExpressionSyntax invocation:
                                var name = InvocationName(invocation);
                                if (name.Length > 0)
                                    invocations.Add((invocation, name));
                                break;
                            case AttributeSyntax attribute when attribute.Parent is AttributeListSyntax { Target.Identifier.RawKind: (int)SyntaxKind.AssemblyKeyword } &&
                                attribute.Name.DescendantNodesAndSelf().OfType<GenericNameSyntax>().Any():
                                registrations.Add(attribute);
                                break;
                            case MethodDeclarationSyntax method when method.AttributeLists.Count > 0 || method.ParameterList.Parameters.Any(parameter => parameter.AttributeLists.Count > 0):
                            case LocalFunctionStatementSyntax local when local.AttributeLists.Count > 0 || local.ParameterList.Parameters.Any(parameter => parameter.AttributeLists.Count > 0):
                                if (node is MethodDeclarationSyntax { AttributeLists.Count: > 0 } || node is LocalFunctionStatementSyntax { AttributeLists.Count: > 0 })
                                    factories.Add(node);
                                annotated.Add(node);
                                break;
                            case BaseTypeDeclarationSyntax type when type.AttributeLists.Count > 0:
                            case BasePropertyDeclarationSyntax property when property.AttributeLists.Count > 0:
                            case BaseMethodDeclarationSyntax method when method.AttributeLists.Count > 0 || method.ParameterList.Parameters.Any(parameter => parameter.AttributeLists.Count > 0):
                            case ParameterSyntax parameter when parameter.AttributeLists.Count > 0:
                                annotated.Add(node);
                                break;
                        }
                    }
                }

                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var syntax in factories)
                {
                    if (Model(syntax.SyntaxTree).GetDeclaredSymbol(syntax) is IMethodSymbol method && IsFactory(method))
                        names.Add(method.Name);
                }

                AddReferencedFactoryNames(names);

                foreach (var (invocation, name) in invocations)
                {
                    if (!names.Contains(name))
                        continue;

                    if (Model(invocation.SyntaxTree).GetSymbolInfo(invocation).Symbol is IMethodSymbol method && IsFactory(method))
                        VisitFactory(method, Map(method), 0);
                }

                foreach (var attribute in registrations)
                {
                    if (Model(attribute.SyntaxTree).GetSymbolInfo(attribute).Symbol is not IMethodSymbol constructor)
                        continue;

                    if (IsStuntAttribute(constructor.ContainingType) || IsValidateAttribute(constructor.ContainingType))
                    {
                        foreach (var argument in constructor.ContainingType.TypeArguments)
                            AddStuntType(argument);
                    }
                }

                this.annotated = annotated;
            }

            public void CollectSymbols()
            {
                foreach (var node in annotated)
                {
                    var symbol = Model(node.SyntaxTree).GetDeclaredSymbol(node);
                    var type = symbol as INamedTypeSymbol ?? symbol?.ContainingType;
                    if (type != null && !DependsOnTypeParameter(type))
                        candidates.Add(type);
                }

                foreach (var type in candidates.OrderBy(candidate => candidate.ToDisplayString(), StringComparer.Ordinal))
                    AddType(type);
            }

            public string? Render()
            {
                if (types.Count == 0)
                    return null;

                var builder = new StringBuilder();
                builder.AppendLine("// <auto-generated/>");
                builder.AppendLine("#nullable enable");
                builder.AppendLine("#pragma warning disable CS8600, CS8604");
                builder.AppendLine("namespace Stunts.Generated");
                builder.AppendLine("{");
                builder.AppendLine("    [global::System.Runtime.CompilerServices.CompilerGenerated]");
                builder.AppendLine("    static class DataAnnotationsRegistrations");
                builder.AppendLine("    {");
                foreach (var field in regexFields)
                    builder.Append("        ").AppendLine(field);
                builder.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
                builder.AppendLine("        internal static void Initialize()");
                builder.AppendLine("        {");
                foreach (var type in types)
                    builder.Append("            ").Append(type.MethodName).AppendLine("();");
                builder.AppendLine("        }");
                foreach (var type in types)
                {
                    builder.AppendLine();
                    builder.Append(type.Body);
                }

                builder.AppendLine("    }");
                builder.AppendLine("}");
                return builder.ToString();
            }

            SemanticModel Model(SyntaxTree tree)
            {
                if (!models.TryGetValue(tree, out var model))
                    models.Add(tree, model = compilation.GetSemanticModel(tree));
                return model;
            }

            static string InvocationName(InvocationExpressionSyntax invocation) => invocation.Expression switch
            {
                MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic.Identifier.ValueText,
                GenericNameSyntax generic => generic.Identifier.ValueText,
                MemberAccessExpressionSyntax { Name: IdentifierNameSyntax identifier } => identifier.Identifier.ValueText,
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                _ => "",
            };

            void AddReferencedFactoryNames(HashSet<string> names)
            {
                var assemblies = new[]
                {
                    compilation.GetTypeByMetadataName("Stunts.StuntGeneratorAttribute")?.ContainingAssembly.Name,
                    compilation.GetTypeByMetadataName("Stunts.ValidatedAttribute")?.ContainingAssembly.Name,
                };
                foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
                {
                    if (!assemblies.Any(name => name != null && References(assembly, name)))
                        continue;

                    var stack = new Stack<INamespaceOrTypeSymbol>();
                    stack.Push(assembly.GlobalNamespace);
                    while (stack.Count > 0)
                    {
                        foreach (var member in stack.Pop().GetMembers())
                        {
                            switch (member)
                            {
                                case INamespaceSymbol space:
                                    stack.Push(space);
                                    break;
                                case INamedTypeSymbol type:
                                    stack.Push(type);
                                    foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
                                    {
                                        if (IsFactory(method))
                                            names.Add(method.Name);
                                    }
                                    break;
                            }
                        }
                    }
                }
            }

            bool References(IAssemblySymbol assembly, string attributeAssembly)
            {
                if (assembly.Name == attributeAssembly)
                    return true;

                switch (compilation.GetMetadataReference(assembly))
                {
                    case CompilationReference source:
                        return source.Compilation.AssemblyName == attributeAssembly ||
                            source.Compilation.ReferencedAssemblyNames.Any(identity => identity.Name == attributeAssembly);
                    case PortableExecutableReference pe:
                        return MetadataReferences(pe, attributeAssembly);
                    default:
                        return true;
                }
            }

            static bool MetadataReferences(PortableExecutableReference pe, string attributeAssembly)
            {
                if (pe.GetMetadata() is not AssemblyMetadata metadata)
                    return true;

                foreach (var module in metadata.GetModules())
                {
                    var reader = module.GetMetadataReader();
                    foreach (var handle in reader.AssemblyReferences)
                    {
                        if (reader.GetString(reader.GetAssemblyReference(handle).Name) == attributeAssembly)
                            return true;
                    }
                }

                return false;
            }

            void VisitFactory(IMethodSymbol method, ImmutableDictionary<ITypeParameterSymbol, ITypeSymbol> substitution, int depth)
            {
                if (depth > 8 || !IsFactory(method))
                    return;

                if (IsStuntFactory(method))
                {
                    foreach (var argument in method.TypeArguments)
                        AddStuntType(Substitute(argument, substitution));
                    return;
                }

                if (IsValidated(method))
                {
                    foreach (var argument in method.TypeArguments)
                        AddStuntType(Substitute(argument, substitution));
                    AddConstructed(Substitute(method.ReturnType, substitution));
                }

                foreach (var reference in method.OriginalDefinition.DeclaringSyntaxReferences)
                {
                    var syntax = reference.GetSyntax();
                    var model = compilation.GetSemanticModel(syntax.SyntaxTree);
                    foreach (var invocation in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol callee || !IsFactory(callee))
                            continue;

                        var closed = SubstituteMethod(callee, substitution);
                        if (IsStuntFactory(closed))
                        {
                            foreach (var argument in closed.TypeArguments)
                                AddStuntType(argument);
                        }
                        else
                        {
                            VisitFactory(closed, Map(closed), depth + 1);
                        }
                    }
                }
            }

            void AddConstructed(ITypeSymbol type)
            {
                AddStuntType(type);
                if (type is INamedTypeSymbol named)
                {
                    foreach (var argument in named.TypeArguments)
                        AddStuntType(argument);
                }
            }

            void AddStuntType(ITypeSymbol type)
            {
                if (type is INamedTypeSymbol named && !DependsOnTypeParameter(named))
                {
                    stuntTargets.Add(named);
                    candidates.Add(named);
                    if (named.BaseType != null)
                        AddStuntType(named.BaseType);
                    foreach (var iface in named.Interfaces)
                        AddStuntType(iface);
                }
                else if (type is IArrayTypeSymbol array)
                {
                    AddStuntType(array.ElementType);
                }
                else if (type is INamedTypeSymbol generic)
                {
                    foreach (var argument in generic.TypeArguments)
                        AddStuntType(argument);
                }
            }

            void AddType(INamedTypeSymbol type)
            {
                var report = stuntTargets.Contains(type);
                var members = new List<MemberModel>();
                foreach (var member in type.GetMembers())
                {
                    switch (member)
                    {
                        case IPropertySymbol property:
                            CollectProperty(type, property, members, report);
                            break;
                        case IMethodSymbol method when method.MethodKind is not (MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.Constructor):
                            CollectMethod(type, method, members, report);
                            break;
                    }
                }

                foreach (var constructor in type.InstanceConstructors)
                    CollectMethod(type, constructor, members, report);

                if (members.Count == 0)
                    return;

                var methodName = Unique("Register_" + Sanitize(type.ToDisplayString()));
                types.Add(new TypeModel(methodName, RenderType(type, methodName, members)));
            }

            void CollectProperty(INamedTypeSymbol type, IPropertySymbol property, List<MemberModel> members, bool report)
            {
                var setter = property.SetMethod;
                if (setter == null || !Interceptable(setter))
                    return;

                var slots = new List<SlotModel>();
                var valueIndex = setter.Parameters.Length - 1;
                for (var i = 0; i < setter.Parameters.Length; i++)
                {
                    var parameter = setter.Parameters[i];
                    var attributes = new List<AttributeData>();
                    if (i == valueIndex)
                        AddAttributes(attributes, EffectivePropertyAttributes(type, property));
                    AddAttributes(attributes, ParameterAttributes(setter, i));
                    AddSlot(slots, parameter, property, attributes, report, valueParameter: i == valueIndex);
                }

                if (slots.Count > 0)
                    members.Add(new MemberModel(setter.Name, Signature(setter), slots));
            }

            void CollectMethod(INamedTypeSymbol type, IMethodSymbol method, List<MemberModel> members, bool report)
            {
                if (!Interceptable(method))
                    return;

                var slots = new List<SlotModel>();
                for (var i = 0; i < method.Parameters.Length; i++)
                    AddSlot(slots, method.Parameters[i], method, ParameterAttributes(method, i), report, valueParameter: false);

                if (slots.Count > 0)
                    members.Add(new MemberModel(method.MethodKind == MethodKind.Constructor ? ".ctor" : method.Name, Signature(method), slots));
            }

            void AddSlot(List<SlotModel> slots, IParameterSymbol parameter, ISymbol display, List<AttributeData> attributes, bool report, bool valueParameter)
            {
                if (attributes.Count == 0)
                    return;

                if (parameter.RefKind == RefKind.Out)
                {
                    if (report)
                        Warn(attributes[0], "Validation attributes on out parameters are not enforced.");
                    return;
                }

                if (parameter.Type.IsRefLikeType || parameter.Type.TypeKind == TypeKind.Pointer)
                {
                    if (report)
                        Warn(attributes[0], "Validation attributes on ref-struct or pointer parameters are not enforced.");
                    return;
                }

                var memberName = valueParameter && display is IPropertySymbol property ? property.Name : parameter.Name;
                var displayName = DisplayName(valueParameter ? display : parameter) ?? DisplayName(parameter);
                var rules = new List<string>();
                foreach (var attribute in attributes)
                {
                    if (TryEmit(attribute, parameter.Type, display, memberName, displayName, report) is string rule)
                        rules.Add(rule);
                }

                if (rules.Count > 0)
                    slots.Add(new SlotModel(parameter.Ordinal, memberName, rules));
            }

            List<AttributeData> EffectivePropertyAttributes(INamedTypeSymbol type, IPropertySymbol property)
            {
                var attributes = new List<AttributeData>();
                AddAttributes(attributes, BuddyAttributes(type, property.Name));
                foreach (var iface in type.AllInterfaces)
                {
                    foreach (var candidate in iface.GetMembers(property.Name).OfType<IPropertySymbol>())
                    {
                        if (SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(candidate), property) ||
                            property.ExplicitInterfaceImplementations.Contains(candidate, SymbolEqualityComparer.Default))
                            AddAttributes(attributes, candidate.GetAttributes());
                    }
                }

                var chain = new List<IPropertySymbol>();
                for (var current = property; current != null; current = current.OverriddenProperty)
                    chain.Add(current);
                chain.Reverse();
                foreach (var current in chain)
                    AddAttributes(attributes, current.GetAttributes());
                return attributes;
            }

            List<AttributeData> ParameterAttributes(IMethodSymbol method, int index)
            {
                var attributes = new List<AttributeData>();
                foreach (var implemented in method.ExplicitInterfaceImplementations)
                {
                    if (index < implemented.Parameters.Length)
                        AddAttributes(attributes, implemented.Parameters[index].GetAttributes());
                }

                var chain = new List<IMethodSymbol>();
                for (var current = method; current != null; current = current.OverriddenMethod)
                    chain.Add(current);
                chain.Reverse();
                foreach (var current in chain)
                {
                    if (index < current.Parameters.Length)
                        AddAttributes(attributes, current.Parameters[index].GetAttributes());
                }

                return attributes;
            }

            IEnumerable<AttributeData> BuddyAttributes(INamedTypeSymbol type, string propertyName)
            {
                foreach (var attribute in type.GetAttributes())
                {
                    if (attribute.AttributeClass?.ToDisplayString() != DataAnnotations + ".MetadataTypeAttribute")
                        continue;
                    if (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol buddy)
                        continue;
                    foreach (var property in buddy.GetMembers(propertyName).OfType<IPropertySymbol>())
                    {
                        foreach (var buddyAttribute in property.GetAttributes())
                            yield return buddyAttribute;
                    }
                }
            }

            void AddAttributes(List<AttributeData> target, IEnumerable<AttributeData> source)
            {
                foreach (var attribute in source)
                {
                    if (!IsValidation(attribute.AttributeClass) || attribute.AttributeClass!.ToDisplayString() == DataAnnotations + ".DataTypeAttribute")
                        continue;

                    if (AllowsMultiple(attribute.AttributeClass))
                    {
                        target.Add(attribute);
                        continue;
                    }

                    var index = target.FindIndex(existing => SymbolEqualityComparer.Default.Equals(existing.AttributeClass, attribute.AttributeClass));
                    if (index >= 0)
                        target[index] = attribute;
                    else
                        target.Add(attribute);
                }
            }

            string? TryEmit(AttributeData attribute, ITypeSymbol valueType, ISymbol owner, string memberName, string? displayName, bool report)
            {
                var typeName = attribute.AttributeClass?.ToDisplayString();
                if (typeName == null || !Supported.Contains(typeName))
                {
                    if (report && typeName != null)
                        Warn(attribute, Unsupported, typeName);
                    return null;
                }

                var error = ErrorMessage(attribute, report);
                var name = Quote(memberName);
                var display = displayName == null ? "null" : Quote(displayName);
                var count = CountExpression(valueType);
                switch (typeName)
                {
                    case DataAnnotations + ".RequiredAttribute":
                        return Call("Required", "value", name, display, Bool(Named(attribute, "AllowEmptyStrings", false)), error);
                    case DataAnnotations + ".StringLengthAttribute":
                        return Call("StringLength", "value", Int(Named(attribute, "MinimumLength", 0)), Int(attribute, 0), name, display, error);
                    case DataAnnotations + ".MinLengthAttribute":
                        return Call("MinLength", "value", Int(attribute, 0), name, display, error, count);
                    case DataAnnotations + ".MaxLengthAttribute":
                        return Call("MaxLength", "value", attribute.ConstructorArguments.Length == 0 ? "-1" : Int(attribute, 0), name, display, error, count);
                    case DataAnnotations + ".LengthAttribute":
                        return Call("Length", "value", Int(attribute, 0), Int(attribute, 1), name, display, error, count);
                    case DataAnnotations + ".RangeAttribute":
                        return Range(attribute, name, display, error, report);
                    case DataAnnotations + ".RegularExpressionAttribute":
                        return Regex(attribute, name, display, error, report);
                    case DataAnnotations + ".CompareAttribute":
                        return Compare(attribute, owner, name, display, error, report);
                    case DataAnnotations + ".EmailAddressAttribute":
                        return Call("EmailAddress", "value", name, display, error);
                    case DataAnnotations + ".PhoneAttribute":
                        return Call("Phone", "value", name, display, error);
                    case DataAnnotations + ".CreditCardAttribute":
                        return Call("CreditCard", "value", name, display, error);
                    case DataAnnotations + ".UrlAttribute":
                        return Call("Url", "value", name, display, error);
                    case DataAnnotations + ".Base64StringAttribute":
                        return Call("Base64String", "value", name, display, error);
                    case DataAnnotations + ".EnumDataTypeAttribute":
                        return Enum(attribute, name, display, error, report);
                    case DataAnnotations + ".AllowedValuesAttribute":
                        return Values("Allowed", attribute, name, display, error);
                    case DataAnnotations + ".DeniedValuesAttribute":
                        return Values("Denied", attribute, name, display, error);
                    case DataAnnotations + ".FileExtensionsAttribute":
                        return Call("FileExtensions", "value", NamedStringLiteral(attribute, "Extensions"), name, display, error);
                    case DataAnnotations + ".CustomValidationAttribute":
                        return Custom(attribute, valueType, name, display, error, memberName, report);
                    default:
                        return null;
                }
            }

            string? Range(AttributeData attribute, string name, string display, string error, bool report)
            {
                var constructor = attribute.AttributeConstructor;
                if (constructor == null || constructor.Parameters.Length < 2)
                {
                    if (report)
                        Warn(attribute, "RangeAttribute is missing its bounds.");
                    return null;
                }

                var minimumExclusive = Bool(Named(attribute, "MinimumIsExclusive", false));
                var maximumExclusive = Bool(Named(attribute, "MaximumIsExclusive", false));
                var first = constructor.Parameters[0].Type;
                if (first.SpecialType is SpecialType.System_Int32 or SpecialType.System_Double)
                {
                    return "global::Stunts.DataAnnotationsValidator.Range(value, " + Primitive(attribute.ConstructorArguments[0]) + ", " + Primitive(attribute.ConstructorArguments[1]) + ", " + minimumExclusive + ", " + maximumExclusive + ", " + name + ", " + display + ", " + error + ")";
                }

                if (attribute.ConstructorArguments[0].Value is not INamedTypeSymbol operand)
                {
                    if (report)
                        Warn(attribute, "RangeAttribute requires a closed operand type.");
                    return null;
                }

                var parser = Parser(operand);
                if (parser == null)
                {
                    if (report)
                        Warn(attribute, "RangeAttribute on '" + operand.ToDisplayString() + "' needs a static TryParse method. TypeConverter bounds are not emitted.");
                    return null;
                }

                var minimum = Quote(attribute.ConstructorArguments[1].Value as string ?? "");
                var maximum = Quote(attribute.ConstructorArguments[2].Value as string ?? "");
                var type = operand.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                return "global::Stunts.DataAnnotationsValidator.Range<" + type + ">(value, " + minimum + ", " + maximum + ", " + parser + ", " +
                    Bool(Named(attribute, "ParseLimitsInInvariantCulture", false)) + ", " +
                    Bool(Named(attribute, "ConvertValueInInvariantCulture", false)) + ", " +
                    minimumExclusive + ", " + maximumExclusive + ", " + name + ", " + display + ", " + error + ")";
            }

            string? Parser(INamedTypeSymbol type)
            {
                var display = type.ToDisplayString();
                if (type.TypeKind == TypeKind.Enum)
                {
                    var enumType = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    return "static (string text, global::System.IFormatProvider? provider, out " + enumType + " result) => global::System.Enum.TryParse(text, false, out result)";
                }

                if (!Parsers.TryGetValue(display, out var pattern))
                {
                    var method = type.GetMembers("TryParse").OfType<IMethodSymbol>().FirstOrDefault(IsParser);
                    if (method == null)
                        return null;

                    var qualified = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var usesProvider = method.Parameters.Length == 3;
                    pattern = usesProvider
                        ? qualified + ".TryParse({0}, {2}, out {1})"
                        : qualified + ".TryParse({0}, out {1})";
                }

                var resultType = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var body = string.Format(CultureInfo.InvariantCulture, pattern, "text", "result", "provider");
                return "static (string text, global::System.IFormatProvider? provider, out " + resultType + " result) => " + body;
            }

            static bool IsParser(IMethodSymbol method)
            {
                if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public || method.ReturnType.SpecialType != SpecialType.System_Boolean)
                    return false;
                if (method.Parameters.Length == 2)
                    return method.Parameters[0].Type.SpecialType == SpecialType.System_String && method.Parameters[1].RefKind == RefKind.Out;
                if (method.Parameters.Length == 3)
                    return method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                        method.Parameters[1].Type.ToDisplayString() == "System.IFormatProvider" &&
                        method.Parameters[2].RefKind == RefKind.Out;
                return false;
            }

            string? Regex(AttributeData attribute, string name, string display, string error, bool report)
            {
                var pattern = attribute.ConstructorArguments.Length == 0 ? null : attribute.ConstructorArguments[0].Value as string;
                if (string.IsNullOrEmpty(pattern))
                {
                    if (report)
                        Warn(attribute, "RegularExpressionAttribute requires a pattern.");
                    return null;
                }

                var timeout = Convert.ToInt32(Named(attribute, "MatchTimeoutInMilliseconds", 2000), CultureInfo.InvariantCulture);
                if (timeout is < -1 or 0)
                {
                    if (report)
                        Warn(attribute, "RegularExpressionAttribute timeout must be -1 or a positive number of milliseconds.");
                    return null;
                }

                try
                {
                    _ = new Regex(pattern!);
                }
                catch (ArgumentException)
                {
                    if (report)
                        Warn(attribute, "RegularExpressionAttribute pattern is not a valid regular expression.");
                    return null;
                }

                var field = "regex" + regexFields.Count.ToString(CultureInfo.InvariantCulture);
                var constructed = timeout == -1
                    ? "new global::System.Text.RegularExpressions.Regex(" + Quote(pattern!) + ")"
                    : "new global::System.Text.RegularExpressions.Regex(" + Quote(pattern!) + ", global::System.Text.RegularExpressions.RegexOptions.None, global::System.TimeSpan.FromMilliseconds(" + timeout.ToString(CultureInfo.InvariantCulture) + "))";
                regexFields.Add("static readonly global::System.Text.RegularExpressions.Regex " + field + " = " + constructed + ";");
                return "global::Stunts.DataAnnotationsValidator.Pattern(value, " + field + ", " + Quote(pattern!) + ", " + name + ", " + display + ", " + error + ")";
            }

            string? Compare(AttributeData attribute, ISymbol owner, string name, string display, string error, bool report)
            {
                var otherName = attribute.ConstructorArguments.Length == 0 ? null : attribute.ConstructorArguments[0].Value as string;
                var type = owner as INamedTypeSymbol ?? owner.ContainingType;
                var other = otherName == null ? null : FindProperty(type, otherName);
                if (other?.GetMethod == null || other.Parameters.Length > 0)
                {
                    if (report)
                        Warn(attribute, "CompareAttribute could not find property '" + (otherName ?? "") + "'.");
                    return null;
                }

                var cast = other.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var otherDisplay = DisplayName(other) ?? other.Name;
                var read = "instance is " + cast + " target ? target." + other.Name + " : null";
                return "global::Stunts.DataAnnotationsValidator.Compare(value, " + read + ", " + name + ", " + display + ", " + Quote(otherDisplay) + ", " + error + ")";
            }

            string? Enum(AttributeData attribute, string name, string display, string error, bool report)
            {
                if (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol enumType || enumType.TypeKind != TypeKind.Enum)
                {
                    if (report)
                        Warn(attribute, "EnumDataTypeAttribute requires an enumeration type.");
                    return null;
                }

                var flags = enumType.GetAttributes().Any(candidate => candidate.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");
                return "global::Stunts.DataAnnotationsValidator.Enum<" + enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">(value, " + (flags ? "true" : "false") + ", " + name + ", " + display + ", " + error + ")";
            }

            string? Values(string method, AttributeData attribute, string name, string display, string error)
            {
                if (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Kind != TypedConstantKind.Array)
                    return Call(method, "value", name, display, error);
                var values = attribute.ConstructorArguments[0].Values.Select(Literal).ToList();
                values.Insert(0, error);
                values.Insert(0, display);
                values.Insert(0, name);
                values.Insert(0, "value");
                return Call(method, values.ToArray());
            }

            string? Custom(AttributeData attribute, ITypeSymbol valueType, string name, string display, string error, string memberName, bool report)
            {
                if (attribute.ConstructorArguments.Length < 2 ||
                    attribute.ConstructorArguments[0].Value is not INamedTypeSymbol validator ||
                    attribute.ConstructorArguments[1].Value is not string methodName)
                {
                    if (report)
                        Warn(attribute, "CustomValidationAttribute requires a validator type and a method name.");
                    return null;
                }

                var methods = validator.GetMembers(methodName).OfType<IMethodSymbol>()
                    .Where(method => method.IsStatic && method.DeclaredAccessibility == Accessibility.Public)
                    .ToList();
                if (methods.Count != 1)
                {
                    if (report)
                        Warn(attribute, "CustomValidationAttribute method '" + methodName + "' must be a single public static method on '" + validator.Name + "'.");
                    return null;
                }

                var method = methods[0];
                if (method.ReturnType.ToDisplayString() != DataAnnotations + ".ValidationResult" ||
                    method.Parameters.Length is < 1 or > 2 ||
                    method.Parameters[0].RefKind != RefKind.None ||
                    (method.Parameters.Length == 2 && method.Parameters[1].Type.ToDisplayString() != DataAnnotations + ".ValidationContext"))
                {
                    if (report)
                        Warn(attribute, "CustomValidationAttribute method '" + methodName + "' must return ValidationResult and take the value plus an optional ValidationContext.");
                    return null;
                }

                var expected = method.Parameters[0].Type;
                var qualified = expected.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var call = validator.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + method.Name;
                string argument;
                string guard;
                if (expected.SpecialType == SpecialType.System_Object)
                {
                    guard = "";
                    argument = "value";
                }
                else if (expected.IsValueType && expected.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
                {
                    guard = "if (value is not " + qualified + " typed) return global::Stunts.DataAnnotationsValidator.ConversionFailed(value, " + Quote(expected.ToDisplayString()) + ", " + Quote(validator.Name) + ", " + Quote(method.Name) + ", " + Quote(memberName) + ");";
                    argument = "typed";
                }
                else
                {
                    guard = "if (value is not null and not " + qualified + ") return global::Stunts.DataAnnotationsValidator.ConversionFailed(value, " + Quote(expected.ToDisplayString()) + ", " + Quote(validator.Name) + ", " + Quote(method.Name) + ", " + Quote(memberName) + ");";
                    argument = "(" + qualified + ")value";
                }

                var context = "";
                if (method.Parameters.Length == 2)
                {
                    var shown = display == "null" ? name : display;
                    context = ", new global::System.ComponentModel.DataAnnotations.ValidationContext(instance) { MemberName = " + name + ", DisplayName = " + shown + " }";
                }

                return "block:" + guard + " return global::Stunts.DataAnnotationsValidator.CustomResult(" + call + "(" + argument + context + "), " + name + ", " + display + ", " + error + ");";
            }

            string ErrorMessage(AttributeData attribute, bool report)
            {
                if (Named(attribute, "ErrorMessage") is string message)
                    return Quote(message);

                var resourceName = Named(attribute, "ErrorMessageResourceName") as string;
                var resourceType = Named(attribute, "ErrorMessageResourceType") as INamedTypeSymbol;
                if (resourceName == null && resourceType == null)
                    return "null";

                var property = resourceType?.GetMembers(resourceName ?? "").OfType<IPropertySymbol>()
                    .FirstOrDefault(candidate => candidate.IsStatic && candidate.DeclaredAccessibility == Accessibility.Public && candidate.Type.SpecialType == SpecialType.System_String);
                if (property?.GetMethod != null && resourceType != null)
                    return resourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + property.Name;

                if (report)
                    Warn(attribute, "ErrorMessageResourceName '" + (resourceName ?? "") + "' is not a public static string property.");
                return "null";
            }

            static string? CountExpression(ITypeSymbol type)
            {
                if (type.SpecialType == SpecialType.System_String || type.TypeKind is TypeKind.TypeParameter or TypeKind.Dynamic)
                    return null;
                if (type.AllInterfaces.Any(iface => iface.ToDisplayString() == "System.Collections.ICollection"))
                    return null;

                var property = type.GetMembers("Count").OfType<IPropertySymbol>().FirstOrDefault(candidate =>
                    !candidate.IsStatic &&
                    candidate.Parameters.Length == 0 &&
                    candidate.Type.SpecialType == SpecialType.System_Int32 &&
                    candidate.GetMethod?.DeclaredAccessibility == Accessibility.Public);
                if (property == null)
                    return null;

                return "static value => ((" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")value).Count";
            }

            static IPropertySymbol? FindProperty(INamedTypeSymbol type, string name)
            {
                for (var current = type; current != null; current = current.BaseType)
                {
                    var found = current.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault(property => !property.IsStatic && property.Parameters.Length == 0);
                    if (found != null)
                        return found;
                }

                foreach (var iface in type.AllInterfaces)
                {
                    var found = iface.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault(property => property.Parameters.Length == 0);
                    if (found != null)
                        return found;
                }

                return null;
            }

            static string? DisplayName(ISymbol symbol)
            {
                foreach (var attribute in symbol.GetAttributes())
                {
                    if (attribute.AttributeClass?.ToDisplayString() == DataAnnotations + ".DisplayAttribute" &&
                        Named(attribute, "Name") is string name)
                        return name;
                }

                foreach (var attribute in symbol.GetAttributes())
                {
                    if (attribute.AttributeClass?.ToDisplayString() == "System.ComponentModel.DisplayNameAttribute" &&
                        attribute.ConstructorArguments.Length > 0 &&
                        attribute.ConstructorArguments[0].Value is string name)
                        return name;
                }

                return null;
            }

            static string RenderType(INamedTypeSymbol type, string methodName, List<MemberModel> members)
            {
                var builder = new StringBuilder();
                builder.Append("        static void ").Append(methodName).AppendLine("()");
                builder.AppendLine("        {");
                var ruleNames = new List<string>();
                foreach (var member in members)
                {
                    var slots = new List<string>();
                    foreach (var slot in member.Slots)
                    {
                        var functions = new List<string>();
                        for (var i = 0; i < slot.Rules.Count; i++)
                        {
                            var function = Sanitize(member.Member + "_" + slot.Index.ToString(CultureInfo.InvariantCulture) + "_" + i.ToString(CultureInfo.InvariantCulture));
                            functions.Add(function);
                            var rule = slot.Rules[i];
                            if (rule.StartsWith("block:", StringComparison.Ordinal))
                                ruleNames.Add("            static global::System.ComponentModel.DataAnnotations.ValidationResult? " + function + "(object? value, object instance) { " + rule.Substring("block:".Length) + " }");
                            else
                                ruleNames.Add("            static global::System.ComponentModel.DataAnnotations.ValidationResult? " + function + "(object? value, object instance) => " + rule + ";");
                        }

                        slots.Add("new global::Stunts.DataAnnotationsSlot(" + slot.Index.ToString(CultureInfo.InvariantCulture) + ", " + string.Join(", ", functions) + ")");
                    }

                    builder.Append("            global::Stunts.DataAnnotationsRegistry.Register(typeof(");
                    builder.Append(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    builder.Append("), ");
                    builder.Append(Quote(member.Member));
                    builder.Append(", ");
                    builder.Append(Quote(member.Signature));
                    builder.Append(", ");
                    builder.Append(string.Join(", ", slots));
                    builder.AppendLine(");");
                }

                foreach (var rule in ruleNames)
                    builder.AppendLine(rule);
                builder.AppendLine("        }");
                return builder.ToString();
            }

            static string Signature(IMethodSymbol method)
            {
                if (method.Parameters.Length == 0)
                    return "";

                var builder = new StringBuilder();
                for (var i = 0; i < method.Parameters.Length; i++)
                {
                    if (i != 0)
                        builder.Append(',');
                    AppendType(builder, method.Parameters[i].Type);
                    if (method.Parameters[i].RefKind != RefKind.None)
                        builder.Append('&');
                }

                return builder.ToString();
            }

            static void AppendType(StringBuilder builder, ITypeSymbol type)
            {
                if (type is IArrayTypeSymbol array)
                {
                    AppendType(builder, array.ElementType);
                    builder.Append('[');
                    if (array.Rank > 1)
                        builder.Append(',', array.Rank - 1);
                    builder.Append(']');
                    return;
                }

                if (type is IPointerTypeSymbol pointer)
                {
                    AppendType(builder, pointer.PointedAtType);
                    builder.Append('*');
                    return;
                }

                if (type is ITypeParameterSymbol parameter)
                {
                    builder.Append(parameter.DeclaringMethod != null ? "!!" : "!");
                    builder.Append(parameter.Ordinal.ToString(CultureInfo.InvariantCulture));
                    return;
                }

                if (type.TypeKind == TypeKind.Dynamic)
                {
                    builder.Append("System.Object");
                    return;
                }

                if (type is not INamedTypeSymbol named)
                {
                    builder.Append(type.ToDisplayString());
                    return;
                }

                if (named.ContainingType != null)
                {
                    AppendType(builder, named.ContainingType);
                    builder.Append('.');
                }
                else if (!named.ContainingNamespace.IsGlobalNamespace)
                {
                    builder.Append(named.ContainingNamespace.ToDisplayString());
                    builder.Append('.');
                }

                builder.Append(named.Name);
                if (named.TypeArguments.Length == 0)
                    return;

                builder.Append('<');
                for (var i = 0; i < named.TypeArguments.Length; i++)
                {
                    if (i != 0)
                        builder.Append(',');
                    AppendType(builder, named.TypeArguments[i]);
                }

                builder.Append('>');
            }

            string Unique(string name)
            {
                var candidate = name;
                var suffix = 2;
                while (!methodNames.Add(candidate))
                    candidate = name + "_" + suffix++.ToString(CultureInfo.InvariantCulture);
                return candidate;
            }

            void Warn(AttributeData attribute, string message) => Warn(attribute, Skipped, message);

            void Warn(AttributeData attribute, DiagnosticDescriptor descriptor, params object[] args)
            {
                var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;
                diagnostics.Add(Diagnostic.Create(descriptor, location, args));
            }

            IMethodSymbol SubstituteMethod(IMethodSymbol method, ImmutableDictionary<ITypeParameterSymbol, ITypeSymbol> substitution)
            {
                if (method.TypeArguments.Length == 0 || substitution.IsEmpty)
                    return method;

                var arguments = method.TypeArguments.Select(argument => Substitute(argument, substitution)).ToArray();
                return method.OriginalDefinition.Construct(arguments);
            }

            ITypeSymbol Substitute(ITypeSymbol type, ImmutableDictionary<ITypeParameterSymbol, ITypeSymbol> substitution)
            {
                if (type is ITypeParameterSymbol parameter && substitution.TryGetValue(parameter, out var replaced))
                    return replaced;
                if (type is INamedTypeSymbol named && named.TypeArguments.Length > 0)
                    return named.OriginalDefinition.Construct(named.TypeArguments.Select(argument => Substitute(argument, substitution)).ToArray());
                if (type is IArrayTypeSymbol array)
                    return compilation.CreateArrayTypeSymbol(Substitute(array.ElementType, substitution), array.Rank);
                return type;
            }

            static ImmutableDictionary<ITypeParameterSymbol, ITypeSymbol> Map(IMethodSymbol method)
            {
                var builder = ImmutableDictionary.CreateBuilder<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
                var definition = method.OriginalDefinition;
                for (var i = 0; i < definition.TypeParameters.Length && i < method.TypeArguments.Length; i++)
                    builder[definition.TypeParameters[i]] = method.TypeArguments[i];
                return builder.ToImmutable();
            }

            static bool IsFactory(IMethodSymbol method) => IsGenerator(method) || IsValidated(method);

            static bool IsGenerator(IMethodSymbol method) =>
                HasAttribute(method, "StuntGeneratorAttribute");

            static bool IsValidated(IMethodSymbol method) =>
                HasAttribute(method, "ValidatedAttribute");

            static bool HasAttribute(IMethodSymbol method, string name) =>
                method.OriginalDefinition.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.Name == name &&
                    attribute.AttributeClass.ContainingNamespace.ToDisplayString() == "Stunts");

            static bool IsStuntFactory(IMethodSymbol method)
            {
                var type = method.ContainingType;
                var name = type?.ToDisplayString();
                return name is "Stunts.Stunt" or "Stunts.StuntBuilder";
            }

            static bool IsStuntAttribute(INamedTypeSymbol? type) =>
                type != null && type.Name.StartsWith("StuntAttribute", StringComparison.Ordinal) && type.ContainingNamespace.ToDisplayString() == "Stunts";

            static bool IsValidateAttribute(INamedTypeSymbol? type) =>
                type != null && type.Name == "ValidateAttribute" && type.Arity > 0 && type.ContainingNamespace.ToDisplayString() == "Stunts";

            static bool Interceptable(IMethodSymbol method)
            {
                if (method.IsStatic)
                    return false;
                if (method.MethodKind == MethodKind.Constructor)
                    return true;
                if (method.ContainingType.TypeKind == TypeKind.Interface)
                    return true;
                return method.IsVirtual || method.IsAbstract || method.IsOverride;
            }

            static bool IsValidation(INamedTypeSymbol? attribute)
            {
                for (var current = attribute; current != null; current = current.BaseType)
                {
                    if (current.ToDisplayString() == ValidationAttribute)
                        return true;
                }

                return false;
            }

            static bool AllowsMultiple(INamedTypeSymbol attribute)
            {
                foreach (var usage in attribute.GetAttributes())
                {
                    if (usage.AttributeClass?.Name != "AttributeUsageAttribute")
                        continue;
                    if (Named(usage, "AllowMultiple") is bool multiple)
                        return multiple;
                }

                return false;
            }

            static bool DependsOnTypeParameter(ITypeSymbol type)
            {
                if (type is ITypeParameterSymbol)
                    return true;
                if (type is IArrayTypeSymbol array)
                    return DependsOnTypeParameter(array.ElementType);
                if (type is not INamedTypeSymbol named)
                    return false;
                if (named.Arity > 0 && SymbolEqualityComparer.Default.Equals(named, named.ConstructedFrom) && named.TypeArguments.Any(argument => argument is ITypeParameterSymbol))
                    return true;
                foreach (var argument in named.TypeArguments)
                {
                    if (DependsOnTypeParameter(argument))
                        return true;
                }

                return named.ContainingType != null && DependsOnTypeParameter(named.ContainingType);
            }

            static string Call(string method, params string?[] arguments)
            {
                var present = arguments.Where(argument => argument != null);
                return "global::Stunts.DataAnnotationsValidator." + method + "(" + string.Join(", ", present) + ")";
            }

            static string Literal(TypedConstant constant)
            {
                if (constant.IsNull)
                    return "null";
                if (constant.Kind == TypedConstantKind.Array)
                    return "new object?[] { " + string.Join(", ", constant.Values.Select(Literal)) + " }";
                if (constant.Kind == TypedConstantKind.Type && constant.Value is ITypeSymbol type)
                    return "typeof(" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")";
                if (constant.Kind == TypedConstantKind.Enum && constant.Type is INamedTypeSymbol enumType)
                {
                    var suffix = constant.Value is ulong ? "UL" : constant.Value is long ? "L" : "";
                    return "(" + enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")" + PrimitiveValue(constant.Value) + suffix;
                }
                return PrimitiveValue(constant.Value);
            }

            static string Primitive(TypedConstant constant) => PrimitiveValue(constant.Value);

            static string PrimitiveValue(object? value) => value switch
            {
                null => "null",
                string text => Quote(text),
                bool flag => flag ? "true" : "false",
                char character => SymbolDisplay.FormatLiteral(character, quote: true),
                float number => number.ToString("R", CultureInfo.InvariantCulture) + "f",
                double number => number.ToString("R", CultureInfo.InvariantCulture) + "d",
                decimal number => number.ToString(CultureInfo.InvariantCulture) + "m",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => "null"
            };

            static string Int(AttributeData attribute, int index) =>
                Convert.ToInt32(attribute.ConstructorArguments[index].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

            static string Int(object? value) => Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

            static string Bool(object? value) => value is true ? "true" : "false";

            static string NamedStringLiteral(AttributeData attribute, string name) =>
                Named(attribute, name) is string text ? Quote(text) : "null";

            static object? Named(AttributeData attribute, string name, object? fallback = null)
            {
                foreach (var pair in attribute.NamedArguments)
                {
                    if (pair.Key == name)
                        return pair.Value.Value;
                }

                return fallback;
            }

            static string Quote(string text) => SymbolDisplay.FormatLiteral(text, quote: true);

            static string Sanitize(string text)
            {
                var builder = new StringBuilder(text.Length);
                foreach (var character in text)
                    builder.Append(char.IsLetterOrDigit(character) ? character : '_');
                if (builder.Length == 0 || !char.IsLetter(builder[0]))
                    builder.Insert(0, '_');
                return builder.ToString();
            }
        }

        sealed class TypeModel
        {
            public TypeModel(string methodName, string body)
            {
                MethodName = methodName;
                Body = body;
            }

            public string MethodName { get; }

            public string Body { get; }
        }

        sealed class MemberModel
        {
            public MemberModel(string member, string signature, List<SlotModel> slots)
            {
                Member = member;
                Signature = signature;
                Slots = slots;
            }

            public string Member { get; }

            public string Signature { get; }

            public List<SlotModel> Slots { get; }
        }

        sealed class SlotModel
        {
            public SlotModel(int index, string memberName, List<string> rules)
            {
                Index = index;
                MemberName = memberName;
                Rules = rules;
            }

            public int Index { get; }

            public string MemberName { get; }

            public List<string> Rules { get; }
        }
    }
}
