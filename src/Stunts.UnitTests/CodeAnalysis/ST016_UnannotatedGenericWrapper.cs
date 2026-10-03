using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Stunts.CodeAnalysis;
using Xunit;

namespace Stunts.UnitTests
{
    public class ST016_UnannotatedGenericWrapper : CodeFixVerifier
    {
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new GenericWrapperAnalyzer();

        protected override CodeFixProvider GetCSharpCodeFixProvider() => new AddGeneratorAttributeCodeFix();

        [Fact]
        public void Verify_Diagnostic()
        {
            var expected = new DiagnosticResult
            {
                Id = StuntDiagnostics.UnannotatedGenericWrapper.Id,
                Message = string.Format(StuntDiagnostics.UnannotatedGenericWrapper.MessageFormat.ToString(), "Create", "Of", "StuntGenerator"),
                Severity = DiagnosticSeverity.Error,
                Locations = new[]
                {
                    new DiagnosticResultLocation("Test0.cs", 6, 43),
                    new DiagnosticResultLocation("Test0.cs", 6, 28),
                },
            };

            VerifyCSharpDiagnostic(new[] { Source, Stunt }, expected);
        }

        [Fact]
        public void AddsGeneratorAttributeToTheWrapper()
            => VerifyCSharpFix(new[] { Source, Stunt }, Fixed);

        [Fact]
        public void AddsGeneratorAttributeToLocalFunction()
            => VerifyCSharpFix(new[] { LocalSource, Stunt }, LocalFixed);

        [Fact]
        public void AddsGeneratorAttributePreservingLineFeed()
        {
            const string source = "using System.Collections.Generic;\nusing Stunts;\n\npublic static class Factory\n{\n    public static IList<T> Create<T>() => Stunt.Of<IList<T>>();\n}\n";
            const string fixedSource = "using System.Collections.Generic;\nusing Stunts;\n\npublic static class Factory\n{\n    [StuntGenerator]\n    public static IList<T> Create<T>() => Stunt.Of<IList<T>>();\n}\n";
            VerifyCSharpFix(new[] { source, Stunt }, fixedSource);
        }

        [Fact]
        public void AnnotatedWrappersAreClosed()
            => VerifyCSharpDiagnostic(new[] { Fixed, Stunt });

        [Fact]
        public void ReportsContainingTypeParameter()
        {
            var expected = new DiagnosticResult
            {
                Id = StuntDiagnostics.ContainingTypeParameter.Id,
                Message = string.Format(StuntDiagnostics.ContainingTypeParameter.MessageFormat.ToString(), "Create", "Factory", "StuntGenerator"),
                Severity = DiagnosticSeverity.Error,
                Locations = new[]
                {
                    new DiagnosticResultLocation("Test0.cs", 7, 33)
                },
            };

            VerifyCSharpDiagnostic(new[] { """
                using System.Collections.Generic;
                using Stunts;

                public class Factory<T>
                {
                    [StuntGenerator]
                    public IList<T> Create() => Stunt.Of<IList<T>>();
                }
                """, Stunt }, expected);
        }

        [Fact]
        public void ReportsVirtualWrapper()
        {
            var expected = new DiagnosticResult
            {
                Id = StuntDiagnostics.VirtualGenericWrapper.Id,
                Message = string.Format(StuntDiagnostics.VirtualGenericWrapper.MessageFormat.ToString(), "Create"),
                Severity = DiagnosticSeverity.Error,
                Locations = new[]
                {
                    new DiagnosticResultLocation("Test0.cs", 7, 29)
                },
            };

            VerifyCSharpDiagnostic(new[] { """
                using System.Collections.Generic;
                using Stunts;

                public class Factory
                {
                    [StuntGenerator]
                    public virtual IList<T> Create<T>() => Stunt.Of<IList<T>>();
                }
                """, Stunt }, expected);
        }

        [Fact]
        public void ReportsRecursiveWrapper()
        {
            var expected = new DiagnosticResult
            {
                Id = StuntDiagnostics.UnboundedGenericWrapper.Id,
                Message = string.Format(StuntDiagnostics.UnboundedGenericWrapper.MessageFormat.ToString(), "Create",
                    "it invokes itself recursively with different type arguments"),
                Severity = DiagnosticSeverity.Error,
                Locations = new[]
                {
                    new DiagnosticResultLocation("Test0.cs", 7, 21)
                },
            };

            VerifyCSharpDiagnostic(new[] { """
                using System.Collections.Generic;
                using Stunts;

                public static class Factory
                {
                    [StuntGenerator]
                    public static T Create<T>(int depth) => depth == 0 ? Stunt.Of<T>() : (T)(object)Create<IList<T>>(depth - 1);
                }
                """, Stunt }, expected);
        }

        [Fact]
        public void ReportsTooDeepWrappers()
        {
            var code = "using Stunts;\npublic static class Factory\n{\n";
            for (var i = 0; i < StuntClosure.MaxDepth; i++)
                code += $"    [StuntGenerator] public static T Level{i}<T>() => Level{i + 1}<T>();\n";
            code += $"    [StuntGenerator] public static T Level{StuntClosure.MaxDepth}<T>() => Stunt.Of<T>();\n}}";

            var expected = new DiagnosticResult
            {
                Id = StuntDiagnostics.UnboundedGenericWrapper.Id,
                Message = string.Format(StuntDiagnostics.UnboundedGenericWrapper.MessageFormat.ToString(), "Level0",
                    $"the chain of generic wrappers is longer than {StuntClosure.MaxDepth}"),
                Severity = DiagnosticSeverity.Error,
                Locations = new[]
                {
                    new DiagnosticResultLocation("Test0.cs", 4, 38)
                },
            };

            VerifyCSharpDiagnostic(new[] { code, Stunt }, expected);
        }

        const string Source = """
            using System.Collections.Generic;
            using Stunts;

            public static class Factory
            {
                public static IList<T> Create<T>() => Stunt.Of<IList<T>>();
            }
            """;

        const string Fixed = """
            using System.Collections.Generic;
            using Stunts;

            public static class Factory
            {
                [StuntGenerator]
                public static IList<T> Create<T>() => Stunt.Of<IList<T>>();
            }
            """;

        const string LocalSource = """
            using System.Collections.Generic;
            using Stunts;

            public static class Factory
            {
                public static object Run()
                {
                    return Create<int>();

                    static IList<T> Create<T>() => Stunt.Of<IList<T>>();
                }
            }
            """;

        const string LocalFixed = """
            using System.Collections.Generic;
            using Stunts;

            public static class Factory
            {
                public static object Run()
                {
                    return Create<int>();

                    [StuntGenerator]
                    static IList<T> Create<T>() => Stunt.Of<IList<T>>();
                }
            }
            """;

        static string Stunt => File.ReadAllText(@"Stunt/Stunt.cs");
    }
}
