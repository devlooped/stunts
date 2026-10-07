using System;

namespace Stunts
{
    /// <summary>
    /// Registers a behavior factory so that each pipeline seeded from the
    /// registration gets its own behavior instance, instead of sharing one
    /// across all stunts.
    /// </summary>
    /// <remarks>
    /// This is an implementation detail: the public API is the
    /// <see cref="StuntExtensions"/> overloads accepting a <see cref="Func{TResult}"/>.
    /// The factory is invoked once per pipeline seeding (e.g. per
    /// <c>StuntBuilder.Build</c> call) by <see cref="BehaviorPipeline.Materialize"/>.
    /// <para>
    /// A factory that reaches a live pipeline was registered too late: factories
    /// are only unwrapped when the pipeline is seeded. Invoking it throws
    /// <see cref="NotSupportedException"/>.
    /// </para>
    /// </remarks>
    sealed class BehaviorFactory : IStuntBehavior
    {
        readonly Func<IStuntBehavior> factory;

        /// <summary>
        /// Initializes the factory registration with the behavior factory function.
        /// </summary>
        /// <param name="factory">Function invoked once per pipeline seeding to create the behavior instance.</param>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
        public BehaviorFactory(Func<IStuntBehavior> factory)
            => this.factory = factory ?? throw new ArgumentNullException(nameof(factory));

        /// <summary>
        /// Creates the behavior instance for one pipeline seeding.
        /// </summary>
        /// <returns>The behavior instance created by the factory function.</returns>
        /// <exception cref="InvalidOperationException">The factory function returned <see langword="null"/>.</exception>
        public IStuntBehavior Create()
            => factory() ?? throw new InvalidOperationException(ThisAssembly.Strings.BehaviorFactoryReturnedNull);

        /// <summary>
        /// Always returns <see langword="true"/> so that a factory mistakenly placed in a
        /// live pipeline fails fast on execution instead of being silently ignored.
        /// </summary>
        bool IStuntBehavior.AppliesTo(IMethodInvocation invocation) => true;

        /// <summary>
        /// Always throws: factories are unwrapped when the pipeline is seeded and must
        /// never execute as pipeline behaviors.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        IMethodReturn IStuntBehavior.Execute(IMethodInvocation invocation, ExecuteHandler next)
            => throw new NotSupportedException(ThisAssembly.Strings.BehaviorFactoryLivePipeline);

        /// <inheritdoc/>
        public override string ToString() => nameof(BehaviorFactory);
    }
}
