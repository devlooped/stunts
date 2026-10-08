using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Stunts
{
    /// <summary>
    /// Marks an <see cref="ISyntaxProcessor"/> that rewrites syntax without binding it.
    /// The driver does not add the generated tree to the compilation for these processors.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    sealed class SyntaxOnlyProcessorAttribute : Attribute
    {
    }

    /// <summary>
    /// A syntax processor driver that applies the provided set of 
    /// <see cref="ISyntaxProcessor"/> to a <see cref="SyntaxNode"/>.
    /// </summary>
    public class SyntaxProcessorDriver
    {
        // Configured processors, by language, then phase.
        readonly Dictionary<string, Dictionary<ProcessorPhase, ISyntaxProcessor[]>> configuredProcessors;
        readonly HashSet<Type> syntaxOnlyProcessors;

        public SyntaxProcessorDriver(params ISyntaxProcessor[] processors)
            : this((IEnumerable<ISyntaxProcessor>)processors) { }

        public SyntaxProcessorDriver(IEnumerable<ISyntaxProcessor> processors)
        {
            syntaxOnlyProcessors = new HashSet<Type>();
            configuredProcessors = processors
                .GroupBy(processor => processor.Language)
                .ToDictionary(
                    bylang => bylang.Key,
                    bylang => bylang
                        .GroupBy(proclang => proclang.Phase)
                        .ToDictionary(
                            byphase => byphase.Key,
                            byphase =>
                            {
                                var list = byphase.ToArray();
                                foreach (var processor in list)
                                {
                                    if (processor.GetType().IsDefined(typeof(SyntaxOnlyProcessorAttribute), inherit: false))
                                        syntaxOnlyProcessors.Add(processor.GetType());
                                }

                                return list;
                            }));
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

            // Binding processors receive a compilation that contains the syntax they are about
            // to rewrite. Syntax-only processors leave that compilation unchanged.

            syntax = Run(supportedProcessors, ProcessorPhase.Prepare, syntax, ref context);
            syntax = Run(supportedProcessors, ProcessorPhase.Scaffold, syntax, ref context);
            syntax = Run(supportedProcessors, ProcessorPhase.Rewrite, syntax, ref context);
            return Run(supportedProcessors, ProcessorPhase.Fixup, syntax, ref context);
        }

        SyntaxNode Run(Dictionary<ProcessorPhase, ISyntaxProcessor[]> supportedProcessors, ProcessorPhase phase, SyntaxNode syntax, ref ProcessorContext context)
        {
            if (!supportedProcessors.TryGetValue(phase, out var processors))
                return syntax;

            foreach (var processor in processors)
                syntax = Apply(processor, syntax, ref context);
            return syntax;
        }

        // SyntaxFactory trees use default parse options. A net10 host compilation
        // carries /features:InterceptorsNamespaces, and Roslyn refuses to mix them.
        // The node passed to a binding processor has to be the root of the tree that was added,
        // or GetSemanticModel throws because the original tree is not in the compilation.
        // Processors that only rewrite syntax skip that compilation update.
        SyntaxNode Apply(ISyntaxProcessor processor, SyntaxNode syntax, ref ProcessorContext context)
        {
            var attach = GenProfile.Enabled ? Stopwatch.GetTimestamp() : 0;
            if (!syntaxOnlyProcessors.Contains(processor.GetType()))
                syntax = Attach(syntax, ref context);
            if (GenProfile.Enabled)
                GenProfile.Step("attach." + processor.GetType().Name, Stopwatch.GetTimestamp() - attach);

            var start = GenProfile.Enabled ? Stopwatch.GetTimestamp() : 0;
            var updated = processor.Process(syntax, context);
            if (GenProfile.Enabled)
                GenProfile.Step(processor.GetType().Name, Stopwatch.GetTimestamp() - start);
            return updated;
        }

        static SyntaxNode Attach(SyntaxNode syntax, ref ProcessorContext context)
        {
            var tree = syntax.SyntaxTree;
            var options = context.MetadataScaffold == null
                ? context.Compilation.SyntaxTrees.FirstOrDefault()?.Options ?? context.ParseOptions
                : context.MetadataScaffold.SyntaxTrees.FirstOrDefault()?.Options
                    ?? context.Compilation.SyntaxTrees.FirstOrDefault()?.Options
                    ?? context.ParseOptions;
            if (options != null && !tree.Options.Equals(options))
            {
                tree = tree.WithRootAndOptions(tree.GetRoot(), options);
                syntax = tree.GetRoot();
            }

            if (context.MetadataScaffold != null)
            {
                var compilation = context.MetadataScaffold;
                if (!compilation.ContainsSyntaxTree(tree))
                    compilation = compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(tree);

                context = context with { Compilation = compilation, MetadataScaffold = compilation };
                return syntax;
            }

            if (!context.Compilation.ContainsSyntaxTree(tree))
                context = context with { Compilation = context.Compilation.AddSyntaxTrees(tree) };
            return syntax;
        }
    }
}
