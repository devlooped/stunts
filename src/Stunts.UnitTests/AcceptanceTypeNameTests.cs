using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Stunts.UnitTests
{
    public class AcceptanceTypeNameTests
    {
        static readonly Lazy<Type> formatter = new(CreateFormatter);

        [Theory]
        [InlineData(typeof(System.Collections.IEnumerable), "global::System.Collections.IEnumerable")]
        [InlineData(typeof(System.Environment.SpecialFolder), "global::System.Environment.SpecialFolder")]
        [InlineData(typeof(Dictionary<,>.KeyCollection), "global::System.Collections.Generic.Dictionary<T0, T1>.KeyCollection")]
        [InlineData(typeof(Outer.Inner), "global::Stunts.UnitTests.AcceptanceTypeNameTests.Outer.Inner")]
        [InlineData(typeof(GenericOuter<>.Inner<>), "global::Stunts.UnitTests.AcceptanceTypeNameTests.GenericOuter<T0>.Inner<T1>")]
        [InlineData(typeof(GenericOuter<int>.Inner<string>), "global::Stunts.UnitTests.AcceptanceTypeNameTests.GenericOuter<global::System.Int32>.Inner<global::System.String>")]
        [InlineData(typeof(GenericOuter<int>.Leaf), "global::Stunts.UnitTests.AcceptanceTypeNameTests.GenericOuter<global::System.Int32>.Leaf")]
        [InlineData(typeof(Outer.GenericInner<>), "global::Stunts.UnitTests.AcceptanceTypeNameTests.Outer.GenericInner<T0>")]
        [InlineData(typeof(@class.@event), "global::Stunts.UnitTests.AcceptanceTypeNameTests.@class.@event")]
        [InlineData(typeof(Dictionary<string, List<int>[,][]>), "global::System.Collections.Generic.Dictionary<global::System.String, global::System.Collections.Generic.List<global::System.Int32>[,][]>")]
        [InlineData(typeof(GenericOuter<int>.Inner<string>[]), "global::Stunts.UnitTests.AcceptanceTypeNameTests.GenericOuter<global::System.Int32>.Inner<global::System.String>[]")]
        public void FormatsReflectionTypes(Type type, string expected)
            => Assert.Equal(expected, Format(type));

        [Fact]
        public void FormatsConstructedTypeContainingAnotherTypesParameter()
        {
            var parameter = typeof(GenericOuter<,>).GetGenericArguments()[1];
            var type = typeof(List<>).MakeGenericType(parameter);

            Assert.Equal("global::System.Collections.Generic.List<T0>", Format(type));
        }

        [Fact]
        public void FormatsNestedGenericConstraints()
        {
            var actual = (string)formatter.Value.GetMethod("Constraints")
                .Invoke(null, new object[] { typeof(ConstrainedOuter<>.Inner<>) });

            Assert.Equal("where T1 : global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.List<T0>[]>", actual);
        }

        [Theory]
        [InlineData(typeof(IDisposable), true)]
        [InlineData(typeof(Outer), true)]
        [InlineData(typeof(Array), false)]
        [InlineData(typeof(string), false)]
        [InlineData(typeof(int), false)]
        [InlineData(typeof(NoAccessibleConstructor), false)]
        [InlineData(typeof(InaccessibleAbstractMember), false)]
        [InlineData(typeof(IInaccessibleAccessor), false)]
        [InlineData(typeof(FieldInfo), false)]
        public void FiltersUnproxyableTypes(Type type, bool expected)
            => Assert.Equal(expected, formatter.Value.GetMethod("CanProxy").Invoke(null, new object[] { type }));

        [Fact]
        public void FiltersExperimentalSignatures()
        {
            var type = typeof(AcceptanceTypeNameTests).GetNestedType("ExperimentalType");
            Assert.Equal(true, formatter.Value.GetMethod("RequiresPreviewFeatures").Invoke(null, new object[] { type }));
            Assert.Equal(true, formatter.Value.GetMethod("RequiresPreviewFeatures").Invoke(null, new object[] { typeof(ExperimentalSignature) }));
            Assert.Equal(false, formatter.Value.GetMethod("RequiresPreviewFeatures").Invoke(null, new object[] { typeof(Outer) }));
        }

        static string Format(Type type)
            => (string)formatter.Value.GetMethod("Format").Invoke(null, new object[] { type });

        static Type CreateFormatter()
        {
            var path = Path.Combine(ThisAssembly.Project.MSBuildProjectDirectory, "..", "Acceptance", "GenerateReferenceStunts.cs");
            var source = string.Join("\n", File.ReadAllLines(path).Where(line => !line.StartsWith("#:", StringComparison.Ordinal)));
            var root = CSharpSyntaxTree.ParseText(source).GetRoot();
            var names = new HashSet<string>
            {
                "CSharpConstraints", "CSharpType",
                "GenericParameters", "GenericParameterName", "WithoutArity", "CSharpIdentifier",
                "CanProxy", "PublicSignature", "RequiresPreviewFeatures", "RequiresPreviewType", "HasPreviewFeature", "SpecialRuntimeType",
            };
            var methods = root.DescendantNodes().OfType<LocalFunctionStatementSyntax>()
                .Where(method => names.Contains(method.Identifier.ValueText))
                .Select(method => method.WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))).NormalizeWhitespace().ToFullString());
            var keywords = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Single(type => type.Identifier.ValueText == "Keywords");

            // Compile the file app's helpers without executing its package-loading entry point.
            var code = """
                using System;
                using System.Collections.Generic;
                using System.Linq;
                using System.Reflection;
                using System.Text;
                public static class TypeNames
                {
                    public static string Format(Type type)
                    {
                        var parameters = GenericParameters(type);
                        var names = parameters.Select((_, index) => $"T{index}").ToArray();
                        return CSharpType(type, parameters, names);
                    }
                    public static string Constraints(Type type)
                    {
                        var parameters = GenericParameters(type);
                        var names = parameters.Select((_, index) => $"T{index}").ToArray();
                        return string.Join("\n", CSharpConstraints(parameters, names));
                    }
                """ + string.Join("\n", methods) + "\n}\n" + keywords.ToFullString();
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create(
                nameof(AcceptanceTypeNameTests),
                new[] { CSharpSyntaxTree.ParseText(code) },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            return compilation.Emit(false).GetType("TypeNames", throwOnError: true);
        }

        public class Outer
        {
            public class Inner { }
            public class GenericInner<T> { }
        }

        public class GenericOuter<T>
        {
            public class Inner<U> { }
            public class Leaf { }
        }

        public class GenericOuter<T, U> { }

        public class ConstrainedOuter<T>
        {
            public class Inner<U> where U : IEnumerable<List<T>[]> { }
        }

        public class @class
        {
            public class @event { }
        }

        public class NoAccessibleConstructor
        {
            NoAccessibleConstructor() { }
        }

        public abstract class InaccessibleAbstractMember
        {
            internal abstract void Invoke();
        }

        public interface IInaccessibleAccessor
        {
            object Subject { get; internal set; }
        }

        [System.Diagnostics.CodeAnalysis.Experimental("STUNTS_TEST001")]
        public class ExperimentalType { }

        public class ExperimentalSignature
        {
#pragma warning disable STUNTS_TEST001
            public ExperimentalType GetValue() => null;
#pragma warning restore STUNTS_TEST001
        }
    }
}
