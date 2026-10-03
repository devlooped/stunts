using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Stunts
{
    /// <summary>
    /// Adds <c>StructRef&lt;T&gt;</c>, <c>SpanRef&lt;T&gt;</c> and <c>ReadOnlySpanRef&lt;T&gt;</c>
    /// when compile-time stunts and unsafe blocks are enabled, the runtime supports by-ref-like generics,
    /// the compilation does not already define them, and it can reference <c>Span&lt;T&gt;</c>.
    /// </summary>
    /// <remarks>
    /// These types have to be visible in the IDE. The compile-time stunt generator
    /// is not registered for design-time builds, so this generator lives with the analyzers.
    /// <c>EnableCompileTimeStunts</c> and <c>AllowUnsafeBlocks</c> are read from the compiler-visible MSBuild properties.
    /// The source is added only when both are true.
    /// </remarks>
    [Generator(LanguageNames.CSharp)]
    public sealed class SignatureRefGenerator : IIncrementalGenerator
    {
        /// <inheritdoc/>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var supported = context.CompilationProvider
                .Combine(context.AnalyzerConfigOptionsProvider)
                .Select(static (pair, _) => ShouldEmit(pair.Left, pair.Right));

            context.RegisterSourceOutput(supported, static (source, emit) =>
            {
                if (!emit)
                    return;

                source.AddSource(
                    "SignatureRef.cs",
                    SourceText.From(ThisAssembly.Resources.SignatureRef.Text, Encoding.UTF8));
            });
        }

        static bool ShouldEmit(Compilation compilation, AnalyzerConfigOptionsProvider options)
            => BuildProperties.CompileTimeStuntsAndUnsafe(options.GlobalOptions) &&
               compilation.SupportsRuntimeCapability(RuntimeCapability.ByRefLikeGenerics) &&
               compilation.GetTypeByMetadataName("System.Span`1") is not null &&
               compilation.GetTypeByMetadataName("System.ReadOnlySpan`1") is not null &&
               compilation.GetTypeByMetadataName("Stunts.StructRef`1") is null &&
               compilation.GetTypeByMetadataName("Stunts.SpanRef`1") is null &&
               compilation.GetTypeByMetadataName("Stunts.ReadOnlySpanRef`1") is null;
    }
}
