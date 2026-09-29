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

        [Fact]
        public void Verify_NoDiagnostic()
        {
            VerifyCSharpDiagnostic(
                new[]
                {
                    File.ReadAllText(ThisAssembly.Constants.CodeAnalysis.ST005.Diagnostic.PublicClass),
                    File.ReadAllText(@"Stunt/Stunt.cs"),
                });
        }
    }
}
