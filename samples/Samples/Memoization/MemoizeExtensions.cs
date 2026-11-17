using Stunts;

namespace Samples.Memoization
{
    /// <summary>
    /// Adds a <see cref="MemoizeBehavior"/> that belongs to a single stunt.
    /// </summary>
    /// <remarks>
    /// Both methods register a factory rather than an instance. <c>BehaviorPipeline.Materialize</c>
    /// invokes it each time a pipeline is seeded: once per <see cref="StuntBuilder"/> build, once
    /// when a <see cref="StuntReference{T}"/> from <c>Stunt.For</c> constructs its stunt, or
    /// immediately for a stunt that already exists (<c>Stunt.Get</c>).
    /// The memoizer sits where it is added: add it before the behaviors whose results it remembers.
    /// </remarks>
    static class MemoizeExtensions
    {
        /// <summary>
        /// Remembers the results of the stunt's methods for each set of argument values.
        /// </summary>
        public static StuntReference<T> Memoize<T>(this StuntReference<T> stunt)
            => stunt.AddBehavior(() => new MemoizeBehavior());

        /// <summary>
        /// Gives every stunt this builder creates a memoizer of its own.
        /// </summary>
        public static StuntBuilder Memoize(this StuntBuilder builder)
            => builder.AddBehavior(() => new MemoizeBehavior());
    }
}
