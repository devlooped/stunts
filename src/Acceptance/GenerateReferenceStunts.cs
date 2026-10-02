#:sdk Microsoft.NET.Sdk.Web
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PreserveCompilationContext=true
#:property DeterministicSourcePaths=false
#:property ComparisonStuntsGenerator=true
#:property ManagePackageVersionsCentrally=false
#:package AGUI.Abstractions@1.0.0
#:package Aspire.Hosting.AppHost@13.5.3
#:package Autofac@4.9.1
#:package AutoFixture@4.17.0
#:package AwesomeAssertions@8.0.2
#:package Azure.Core@1.62.0
#:package Azure.Identity@1.21.0
#:package Azure.Messaging.EventHubs@5.12.2
#:package JetBrains.Annotations@2018.3.0
#:package Microsoft.Agents.AI@1.23.0
#:package Microsoft.Agents.AI.Abstractions@1.23.0
#:package Microsoft.Agents.AI.Harness@1.23.0
#:package Microsoft.Agents.AI.OpenAI@1.23.0
#:package Microsoft.Agents.AI.Workflows@1.23.0
#:package Microsoft.Agents.AI.Workflows.Declarative@1.23.0
#:package Microsoft.Agents.ObjectModel@2026.5.5
#:package Microsoft.Agents.ObjectModel.Json@2026.5.5
#:package Microsoft.Agents.ObjectModel.PowerFx@2026.5.5
#:package Microsoft.AspNetCore.Diagnostics.Middleware@10.10.0
#:package Microsoft.AspNetCore.Mvc.NewtonsoftJson@10.0.0
#:package Microsoft.AspNetCore.TestHost@10.0.12
#:package Microsoft.Bcl.AsyncInterfaces@10.0.12
#:package Microsoft.Extensions.AI@10.10.0
#:package Microsoft.Extensions.AI.Abstractions@10.10.1
#:package Microsoft.Extensions.AI.OpenAI@10.10.1
#:package Microsoft.Extensions.AmbientMetadata.Application@10.10.0
#:package Microsoft.Extensions.Compliance.Abstractions@10.10.0
#:package Microsoft.Extensions.DependencyModel@10.0.0
#:package Microsoft.Extensions.VectorData.Abstractions@10.10.0
#:package Microsoft.ML.Tokenizers@2.0.0
#:package OpenAI@2.14.0
#:package OpenTelemetry@1.18.0
#:package OpenTelemetry.Exporter.InMemory@1.18.0
#:package System.ClientModel@1.15.0

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.Extensions.DependencyModel;

if (args.Length != 0)
{
    Console.Error.WriteLine("Usage: dotnet run --file GenerateReferenceStunts.cs");
    return 1;
}

var assemblies = LoadAssemblies();
var compilationTypes = LoadCompilationTypes();
var types = assemblies
    .SelectMany(GetExportedTypes)
    .Where(type => compilationTypes.Contains(type.FullName!))
    .Where(type => !type.IsCOMObject)
    .Where(type => !RequiresPreviewFeatures(type))
    .Where(CanProxy)
    .GroupBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal)
    .Select(group => group.First())
    .OrderBy(type => type.FullName, StringComparer.Ordinal)
    .ThenBy(type => type.Assembly.GetName().Name, StringComparer.Ordinal)
    .ToArray();

var output = Path.Combine(SourceDirectory(), "Static", "Stunts.cs");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, GenerateCSharp(types).ReplaceLineEndings("\n"), new UTF8Encoding(false));
Console.WriteLine($"Generated {types.Length} Stunt.Of<T> calls from {assemblies.Count} assemblies in {output}.");
return 0;

static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
    Path.GetDirectoryName(source) ?? throw new InvalidOperationException("The generator source directory is unavailable.");

static IReadOnlyList<Assembly> LoadAssemblies()
{
    var context = DependencyContext.Default ??
        throw new InvalidOperationException("The file app dependency context is unavailable.");
    var assemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
    var errors = new List<Exception>();

    foreach (var name in context.RuntimeLibraries
        .Where(library => library.Type == "package")
        .SelectMany(library => library.GetDefaultAssemblyNames(context))
        .DistinctBy(name => name.FullName))
    {
        try
        {
            var assembly = Assembly.Load(name);
            assemblies.TryAdd(assembly.FullName!, assembly);
        }
        catch (Exception exception)
        {
            errors.Add(new InvalidOperationException($"Could not load package assembly {name.FullName}.", exception));
        }
    }

    var trustedPlatformAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)?
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) ?? [];
    foreach (var path in trustedPlatformAssemblies.Where(path =>
        path.Contains($"{Path.DirectorySeparatorChar}shared{Path.DirectorySeparatorChar}Microsoft.AspNetCore.App{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
    {
        try
        {
            var assembly = Assembly.LoadFrom(path);
            assemblies.TryAdd(assembly.FullName!, assembly);
        }
        catch (Exception exception)
        {
            errors.Add(new InvalidOperationException($"Could not load shared-framework assembly {path}.", exception));
        }
    }

    if (errors.Count > 0)
        throw new AggregateException("One or more comparison assemblies could not be loaded.", errors);

    return assemblies.Values.OrderBy(assembly => assembly.FullName, StringComparer.Ordinal).ToArray();
}

static HashSet<string> LoadCompilationTypes()
{
    var context = DependencyContext.Default ?? throw new InvalidOperationException("The compilation dependency context is unavailable.");
    var result = new HashSet<string>(StringComparer.Ordinal);
    foreach (var path in context.CompileLibraries.SelectMany(library => library.ResolveReferencePaths()).Distinct(StringComparer.OrdinalIgnoreCase))
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        foreach (var handle in metadata.TypeDefinitions)
        {
            var definition = metadata.GetTypeDefinition(handle);
            if ((definition.Attributes & TypeAttributes.VisibilityMask) is not (TypeAttributes.Public or TypeAttributes.NestedPublic))
                continue;
            result.Add(MetadataTypeName(metadata, handle));
        }
    }

    if (result.Count == 0)
        throw new InvalidOperationException("No compilation reference types are available.");
    return result;
}

static string MetadataTypeName(MetadataReader metadata, TypeDefinitionHandle handle)
{
    var definition = metadata.GetTypeDefinition(handle);
    var name = metadata.GetString(definition.Name);
    var parent = definition.GetDeclaringType();
    if (!parent.IsNil)
        return MetadataTypeName(metadata, parent) + "+" + name;
    var ns = metadata.GetString(definition.Namespace);
    return ns.Length == 0 ? name : ns + "." + name;
}

static IEnumerable<Type> GetExportedTypes(Assembly assembly)
{
    try
    {
        return assembly.GetExportedTypes();
    }
    catch (ReflectionTypeLoadException exception)
    {
        throw new AggregateException(
            $"Could not inspect every exported type in {assembly.FullName}.",
            exception.LoaderExceptions.Where(error => error != null)!);
    }
    catch (FileNotFoundException exception)
    {
        Console.Error.WriteLine(
            $"Skipping {assembly.FullName}: its undeclared dependency {exception.FileName} is unavailable.");
        return [];
    }
}

static bool RequiresPreviewFeatures(Type type)
{
    if (type.HasElementType)
        return RequiresPreviewFeatures(type.GetElementType()!);
    if (type.IsGenericParameter)
        return type.GetGenericParameterConstraints().Any(RequiresPreviewType);
    if (RequiresPreviewType(type))
        return true;
    if (type.IsGenericType && type.GetGenericArguments().Any(RequiresPreviewType))
        return true;

    return type
        .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(member => member is not MethodBase method || method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly)
        .Any(member => HasPreviewFeature(member.GetCustomAttributesData()) || member switch
        {
            MethodInfo method => RequiresPreviewType(method.ReturnType) || method.GetParameters().Any(parameter => RequiresPreviewType(parameter.ParameterType)),
            ConstructorInfo constructor => constructor.GetParameters().Any(parameter => RequiresPreviewType(parameter.ParameterType)),
            PropertyInfo property => RequiresPreviewType(property.PropertyType),
            EventInfo ev => ev.EventHandlerType != null && RequiresPreviewType(ev.EventHandlerType),
            _ => false,
        });
}

static bool RequiresPreviewType(Type type)
{
    if (type.HasElementType)
        return RequiresPreviewType(type.GetElementType()!);
    if (type.IsGenericParameter)
        return false;
    if (HasPreviewFeature(type.Assembly.GetCustomAttributesData()))
        return true;

    for (var current = type; current != null; current = current.DeclaringType)
    {
        if (HasPreviewFeature(current.GetCustomAttributesData()))
            return true;
    }

    return type.IsGenericType && type.GetGenericArguments().Any(RequiresPreviewType);
}

static bool HasPreviewFeature(IEnumerable<CustomAttributeData> attributes) =>
    attributes.Any(attribute =>
        attribute.AttributeType.FullName is "System.Runtime.Versioning.RequiresPreviewFeaturesAttribute" or
            "System.Diagnostics.CodeAnalysis.ExperimentalAttribute");

static bool CanProxy(Type type)
{
    if (!type.IsInterface && (!type.IsClass || type.IsSealed))
        return false;
    if (type == typeof(Array) || type == typeof(Delegate) || type == typeof(MulticastDelegate) ||
        type == typeof(Enum) || type == typeof(ValueType))
        return false;
    if (type.GetCustomAttributesData().Any(attribute => attribute.AttributeType == typeof(ObsoleteAttribute) &&
        (attribute.ConstructorArguments.Count > 1 && attribute.ConstructorArguments[1].Value is true ||
         attribute.NamedArguments.Any(argument => argument.MemberName == nameof(ObsoleteAttribute.DiagnosticId) &&
             argument.TypedValue.Value as string == "SYSLIB0011"))))
        return false;
    if (type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .Where(method => method.IsVirtual && !method.IsFinal && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly))
        .Any(method => SpecialRuntimeType(method.ReturnType) || method.GetParameters().Any(parameter => SpecialRuntimeType(parameter.ParameterType))))
        return false;
    if (type.IsInterface)
        return type.GetInterfaces().Append(type)
            .SelectMany(iface => iface.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .All(method => !method.IsAbstract || method.IsPublic && !method.IsStatic);
    if (!type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .Any(constructor => (constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly) &&
            constructor.GetParameters().All(parameter => PublicSignature(parameter.ParameterType))))
        return false;

    // An abstract internal/private-protected member cannot be implemented by a consumer.
    return !type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .Any(method => method.IsAbstract && !(method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly));
}

static bool SpecialRuntimeType(Type type)
    => type == typeof(TypedReference) || type == typeof(ArgIterator) || type == typeof(RuntimeArgumentHandle) ||
        type.IsByRef && SpecialRuntimeType(type.GetElementType()!);

static bool PublicSignature(Type type)
{
    if (type.HasElementType)
        return PublicSignature(type.GetElementType()!);
    if (type.IsGenericParameter)
        return true;
    return (type.IsPublic || type.IsNestedPublic && PublicSignature(type.DeclaringType!)) &&
        (!type.IsGenericType || type.GetGenericArguments().All(PublicSignature));
}

static string GenerateCSharp(IReadOnlyList<Type> types)
{
    var source = new StringBuilder("""
        // <auto-generated />
        #nullable enable

        namespace ComparisonPackages;

        static class GeneratedStunts
        {

        """);

    for (var index = 0; index < types.Count; index++)
    {
        var type = types[index];
        var parameters = GenericParameters(type);
        var names = parameters.Select((_, parameterIndex) => $"T{parameterIndex}").ToArray();
        source.Append("    static void Create").Append(index);
        if (names.Length > 0)
            source.Append('<').AppendJoin(", ", names).Append('>');
        source.AppendLine("()");
        foreach (var constraint in CSharpConstraints(parameters, names))
            source.Append("        ").AppendLine(constraint);
        source.Append("        => _ = global::Stunts.Stunt.Of<")
            .Append(CSharpType(type, parameters, names))
            .AppendLine(">();")
            .AppendLine();
    }

    return source.AppendLine("}").ToString();
}

static IEnumerable<string> CSharpConstraints(Type[] parameters, string[] names)
{
    for (var index = 0; index < parameters.Length; index++)
    {
        var parameter = parameters[index];
        var constraints = new List<string>();
        var attributes = parameter.GenericParameterAttributes;
        var special = attributes & GenericParameterAttributes.SpecialConstraintMask;
        var unmanaged = parameter.GetCustomAttributesData().Any(attribute =>
            attribute.AttributeType.FullName == "System.Runtime.CompilerServices.IsUnmanagedAttribute");

        if (unmanaged)
            constraints.Add("unmanaged");
        else if (special.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
            constraints.Add("struct");
        else if (special.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
            constraints.Add("class");

        constraints.AddRange(parameter.GetGenericParameterConstraints()
            .Where(constraint => constraint != typeof(ValueType))
            .Select(constraint => CSharpType(constraint, parameters, names)));

        if (!unmanaged &&
            !special.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint) &&
            special.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint))
            constraints.Add("new()");

        if (constraints.Count > 0)
            yield return $"where {names[index]} : {string.Join(", ", constraints)}";
    }
}

static string CSharpType(Type type, Type[] parameters, string[] names)
{
    if (type.IsGenericParameter)
        return GenericParameterName(type, parameters, names);
    if (type.IsArray)
    {
        var ranks = new StringBuilder();
        var element = type;
        while (element.IsArray)
        {
            ranks.Append('[').Append(',', element.GetArrayRank() - 1).Append(']');
            element = element.GetElementType()!;
        }
        return CSharpType(element, parameters, names) + ranks;
    }
    if (type.IsPointer)
        return $"{CSharpType(type.GetElementType()!, parameters, names)}*";
    if (type.IsByRef)
        return CSharpType(type.GetElementType()!, parameters, names);

    var chain = new Stack<Type>();
    for (var current = type; current != null; current = current.DeclaringType)
        chain.Push(current);

    var result = new StringBuilder("global::");
    if (!string.IsNullOrEmpty(type.Namespace))
        result.AppendJoin(".", type.Namespace.Split('.').Select(CSharpIdentifier)).Append('.');

    // DeclaringType reopens a constructed outer type; the requested type retains
    // the actual arguments for every level of the nesting chain.
    var arguments = type.GetGenericArguments();
    var consumedArguments = 0;
    var first = true;
    while (chain.Count > 0)
    {
        var current = chain.Pop();
        if (!first)
            result.Append('.');
        first = false;

        result.Append(CSharpIdentifier(WithoutArity(current.Name)));
        var declaringArguments = current.DeclaringType?.GetGenericArguments().Length ?? 0;
        var ownArguments = current.GetGenericArguments().Length - declaringArguments;
        if (ownArguments > 0)
        {
            result.Append('<');
            result.AppendJoin(", ", arguments.Skip(consumedArguments).Take(ownArguments)
                .Select(argument => CSharpType(argument, parameters, names)));
            result.Append('>');
        }
        consumedArguments += ownArguments;
    }

    return result.ToString();
}

static Type[] GenericParameters(Type type) =>
    type.ContainsGenericParameters
        ? type.GetGenericArguments()
            .Where(argument => argument.IsGenericParameter)
            .Distinct()
            .OrderBy(argument => argument.GenericParameterPosition)
            .ToArray()
        : [];

static string GenericParameterName(Type parameter, Type[] parameters, string[] names)
{
    var position = Array.IndexOf(parameters, parameter);
    if (position < 0 || position >= names.Length)
        throw new InvalidOperationException($"Generic parameter {parameter} has no matching generated parameter.");

    return names[position];
}

static string WithoutArity(string name)
{
    var tick = name.IndexOf('`');
    return tick < 0 ? name : name[..tick];
}

static string CSharpIdentifier(string identifier) =>
    Keywords.CSharp.Contains(identifier) ? $"@{identifier}" : identifier;

static class Keywords
{
    public static readonly HashSet<string> CSharp = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while"
    };
}
