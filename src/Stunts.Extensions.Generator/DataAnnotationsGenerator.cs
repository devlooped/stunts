using Microsoft.CodeAnalysis;

namespace Stunts
{
    /// <summary>
    /// Emits a module initializer that registers data-annotation rules for interceptable members.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class DataAnnotationsGenerator : IIncrementalGenerator
    {
        /// <inheritdoc/>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(context.CompilationProvider, static (source, compilation) =>
            {
                if (compilation.GetTypeByMetadataName("Stunts.DataAnnotationsRegistry") == null)
                    return;

                DataAnnotationsCatalog.Emit(source, compilation);
            });
        }
    }
}
