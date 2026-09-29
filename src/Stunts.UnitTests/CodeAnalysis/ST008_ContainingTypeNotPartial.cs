using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Stunts.CodeAnalysis;
using Xunit;

namespace Stunts.UnitTests
{
    public class ST008_ContainingTypeNotPartial : CodeFixVerifier
    {
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new ValidateTypesAnalyzer();

        protected override CodeFixProvider GetCSharpCodeFixProvider() => new AddPartialCodeFix();

        [Fact]
        public void Verify_Diagnostic()
        {
            var expected = new DiagnosticResult
            {
                Id = StuntDiagnostics.ContainingTypeNotPartial.Id,
                Message = string.Format(StuntDiagnostics.ContainingTypeNotPartial.MessageFormat.ToString(), "Hidden", "Outer"),
                Severity = DiagnosticSeverity.Error,
                Locations = new[]
                {
                    new DiagnosticResultLocation("Test0.cs", 12, 9)
                },
            };

            VerifyCSharpDiagnostic(new[] { Source, Stunt }, expected);
        }

        [Fact]
        public void AddsPartialToTheContainingType()
        {
            VerifyCSharpFix(new[] { Source, Stunt }, Fixed);
        }

        const string Source = """
            using Stunts;

            public class Outer
            {
                class Hidden
                {
                    public virtual int Next() => 1;
                }

                public void Run()
                {
                    Stunt.Of<Hidden>();
                }
            }
            """;

        const string Fixed = """
            using Stunts;

            public partial class Outer
            {
                class Hidden
                {
                    public virtual int Next() => 1;
                }

                public void Run()
                {
                    Stunt.Of<Hidden>();
                }
            }
            """;

        static string Stunt => File.ReadAllText(@"Stunt/Stunt.cs");
    }
}
