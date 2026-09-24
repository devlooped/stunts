using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Stunts.CodeAnalysis;
using Xunit;

namespace Stunts.UnitTests
{
    public class ST005_PointerMember : DiagnosticVerifier
    {
        protected override DiagnosticAnalyzer? GetCSharpDiagnosticAnalyzer() => new PointerMemberAnalyzer();

        [Theory]
        [InlineData(ThisAssembly.Constants.CodeAnalysis.ST005.Diagnostic.PublicClass, 9, 25)]
        public void Verify_Diagnostic(string path, int line, int column)
        {
            var expected = new DiagnosticResult
            {
                Id = StuntDiagnostics.PointerMember.Id,
                Message = string.Format(Resources.PointerMember_Message, "IPointers"),
                Severity = DiagnosticSeverity.Error,
                Locations = new[] {
                    new DiagnosticResultLocation("Test0.cs", line, column)
                },
            };

            VerifyCSharpDiagnostic(
                new[]
                {
                    File.ReadAllText(path),
                    File.ReadAllText(@"Stunt/Stunt.cs"),
                },
                expected);
        }
    }
}
