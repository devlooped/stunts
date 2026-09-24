using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Stunts.CodeAnalysis;
using Xunit;

namespace Stunts.UnitTests
{
    public class ST004_EnumType : DiagnosticVerifier
    {
        protected override DiagnosticAnalyzer? GetCSharpDiagnosticAnalyzer() => new ValidateTypesAnalyzer();

        [Fact]
        public void VerifyMultipleEnums()
        {
            VerifyCSharpDiagnostic(
                new[]
                {
                    File.ReadAllText(ThisAssembly.Constants.CodeAnalysis.ST004.Diagnostic.MultipleEnums),
                    File.ReadAllText(@"Stunt/Stunt.cs"),
                },
                new[]
                {
                    new DiagnosticResult
                    {
                        Id = StuntDiagnostics.EnumType.Id,
                        Message = string.Format(Resources.EnumType_Message, "PlatformID"),
                        Severity = DiagnosticSeverity.Error,
                        Locations = new[] {
                            new DiagnosticResultLocation("Test0.cs", 9, 25)
                        },
                    },
                    new DiagnosticResult
                    {
                        Id = StuntDiagnostics.EnumType.Id,
                        Message = string.Format(Resources.EnumType_Message, "TypeCode"),
                        Severity = DiagnosticSeverity.Error,
                        Locations = new[] {
                            new DiagnosticResultLocation("Test0.cs", 9, 25)
                        },
                    }
                });
        }

        [Theory]
        [InlineData(ThisAssembly.Constants.CodeAnalysis.ST004.Diagnostic.PublicClass, 9, 25)]
        public void Verify_Diagnostic(string path, int line, int column)
        {
            var expected = new DiagnosticResult
            {
                Id = StuntDiagnostics.EnumType.Id,
                Message = string.Format(Resources.EnumType_Message, "PlatformID"),
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
