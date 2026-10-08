using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Stunts.UnitTests
{
    public class DataAnnotationsClosureTests
    {
        [Fact]
        public void AssemblyValidateRegistersAClosedTypeWithNoFactoryCall()
        {
            var source = Generate(@"
[assembly: Stunts.Validate<IOnlyValidated<int>>]
public interface IOnlyValidated<T>
{
    [System.ComponentModel.DataAnnotations.Required]
    T Value { get; set; }
}
");

            Assert.Contains("IOnlyValidated<int>", source);
            Assert.DoesNotContain("IOnlyValidated<string>", source);
        }

        [Fact]
        public void ValidatedFactoryRegistersTheClosedReturnType()
        {
            var source = Generate(@"
public interface ILabeled<T>
{
    [System.ComponentModel.DataAnnotations.Required]
    T Label { get; set; }
}
public static class Factory
{
    [Stunts.Validated]
    public static ILabeled<T> Labeled<T>() => default;
}
class Program
{
    void Use() => Factory.Labeled<int>();
}
");

            Assert.Contains("ILabeled<int>", source);
            Assert.DoesNotContain("ILabeled<string>", source);
        }

        [Fact]
        public void ValidatedWrapperClosesTheFactoryItCalls()
        {
            var source = Generate(@"
public interface ILabeled<T>
{
    [System.ComponentModel.DataAnnotations.Required]
    T Label { get; set; }
}
public static class Factory
{
    [Stunts.Validated]
    public static ILabeled<T> Labeled<T>() => default;

    [Stunts.Validated]
    public static object Make<T>() => Labeled<T>();
}
class Program
{
    void Use() => Factory.Make<string>();
}
");

            Assert.Contains("ILabeled<string>", source);
            Assert.DoesNotContain("ILabeled<int>", source);
        }

        static string Generate(string source)
        {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(System.IO.Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path))
                .Concat(new[]
                {
                    MetadataReference.CreateFromFile(typeof(RequiredAttribute).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(ValidateAttribute<>).Assembly.Location)
                });
            var compilation = CSharpCompilation.Create(
                "closure",
                new[] { CSharpSyntaxTree.ParseText(source) },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            GeneratorDriver driver = CSharpGeneratorDriver.Create(new DataAnnotationsGenerator().AsSourceGenerator());
            driver = driver.RunGenerators(compilation);
            return string.Join("\n", driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(generated => generated.SourceText.ToString()));
        }
    }
}
