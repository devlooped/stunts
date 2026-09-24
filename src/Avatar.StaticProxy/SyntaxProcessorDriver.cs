using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Avatars
{
    /// <summary>
    /// A syntax processor driver that applies the provided set of 
    /// <see cref="ISyntaxProcessor"/> to a <see cref="SyntaxNode"/>.
    /// </summary>
    public class SyntaxProcessorDriver
    {
        // Configured processors, by language, then phase.
        readonly Dictionary<string, Dictionary<ProcessorPhase, ISyntaxProcessor[]>> configuredProcessors;

        public SyntaxProcessorDriver(params ISyntaxProcessor[] processors)
            : this((IEnumerable<ISyntaxProcessor>)processors) { }

        public SyntaxProcessorDriver(IEnumerable<ISyntaxProcessor> processors)
        {
            configuredProcessors = processors
                .GroupBy(processor => processor.Language)
                .ToDictionary(
                    bylang => bylang.Key,
                    bylang => bylang
                        .GroupBy(proclang => proclang.Phase)
                        .ToDictionary(
                            byphase => byphase.Key,
                            byphase => byphase.Select(proclang => proclang).ToArray()));
        }

        /// <summary>
        /// Applies configured syntax processors to the given syntax node.
        /// </summary>
        /// <param name="syntax">The syntax root to apply processors to.</param>
        /// <param name="context">The processor context.</param>
        public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context)
        {
            if (!configuredProcessors.TryGetValue(context.Language, out var supportedProcessors))
                return syntax;

            // For each processor, we pass in an updated context with the received syntax tree added 
            // to the compilation each time. This allows us to have an up-to-date compilation with the 
            // changes from each processor, should semantic information be needed for anything in the 
            // updated syntax trees at any point.

            if (supportedProcessors.TryGetValue(ProcessorPhase.Prepare, out var prepares))
                foreach (var processor in prepares)
                    syntax = Apply(processor, syntax, context);

            if (supportedProcessors.TryGetValue(ProcessorPhase.Scaffold, out var scaffolds))
                foreach (var processor in scaffolds)
                    syntax = Apply(processor, syntax, context);

            if (supportedProcessors.TryGetValue(ProcessorPhase.Rewrite, out var rewriters))
                foreach (var processor in rewriters)
                    syntax = Apply(processor, syntax, context);

            if (supportedProcessors.TryGetValue(ProcessorPhase.Fixup, out var fixups))
                foreach (var processor in fixups)
                    syntax = Apply(processor, syntax, context);

            return syntax;
        }

        // SyntaxFactory trees use default parse options. A net10 host compilation
        // carries /features:InterceptorsNamespaces, and Roslyn refuses to mix them.
        // The node passed to the processor has to be the root of the tree that was added,
        // or GetSemanticModel throws because the original tree is not in the compilation.
        static SyntaxNode Apply(ISyntaxProcessor processor, SyntaxNode syntax, ProcessorContext context)
        {
            var tree = syntax.SyntaxTree;
            var options = context.Compilation.SyntaxTrees.FirstOrDefault()?.Options;
            if (options != null && !tree.Options.Equals(options))
            {
                tree = tree.WithRootAndOptions(tree.GetRoot(), options);
                syntax = tree.GetRoot();
            }

            var compilation = context.Compilation.AddSyntaxTrees(tree);
            return processor.Process(syntax, context with { Compilation = compilation });
        }
    }
}
