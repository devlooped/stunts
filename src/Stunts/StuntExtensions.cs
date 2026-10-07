using System;

namespace Stunts
{
    /// <summary>
    /// Usability functions for working with stunts.
    /// </summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    public static class StuntExtensions
    {
        /// <summary>
        /// Adds a behavior to a stunt.
        /// </summary>
        /// <param name="stunt">The stunt to add the behavior to.</param>
        /// <param name="behavior">(invocation, next) => invocation.CreateValueReturn() | invocation.CreateExceptionReturn() | next().Invoke(invocation, next)</param>
        /// <param name="appliesTo">invocation => true|false</param>
        /// <param name="name">Optional friendly name for the behavior.</param>
        public static TStunt AddBehavior<TStunt>(this TStunt stunt, ExecuteHandler behavior, AppliesToHandler? appliesTo = null, string? name = null)
            where TStunt : IStunt
        {
            if (stunt == null)
                throw new ArgumentNullException(nameof(stunt));

            stunt.Behaviors.Add(new AnonymousBehavior(behavior, appliesTo, name));
            return stunt;
        }

        /// <summary>
        /// Adds a behavior to a stunt.
        /// </summary>
        /// <param name="stunt">The stunt to add the behavior to.</param>
        /// <param name="behavior">A custom behavior to apply to the stunt.</param>
        public static TStunt AddBehavior<TStunt>(this TStunt stunt, IStuntBehavior behavior)
            where TStunt : IStunt
        {
            if (stunt == null)
                throw new ArgumentNullException(nameof(stunt));

            stunt.Behaviors.Add(behavior);
            return stunt;
        }

        /// <summary>
        /// Adds a behavior factory to a stunt, so each pipeline seeded from these
        /// behaviors gets its own behavior instance instead of sharing one.
        /// </summary>
        /// <param name="stunt">The stunt to add the behavior factory to.</param>
        /// <param name="factory">Function invoked to create the behavior instance.</param>
        /// <remarks>
        /// If the stunt is not constructed yet, the factory is registered and invoked
        /// once per pipeline seeding (e.g. per <c>StuntBuilder.Build</c> call). If the
        /// stunt is already constructed, the factory is invoked immediately and the
        /// resulting instance is added to the live pipeline.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The factory function returned <see langword="null"/>.</exception>
        public static TStunt AddBehavior<TStunt>(this TStunt stunt, Func<IStuntBehavior> factory)
            where TStunt : IStunt
        {
            if (stunt == null)
                throw new ArgumentNullException(nameof(stunt));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            stunt.Behaviors.Add(CreateBehavior(stunt, factory));
            return stunt;
        }

        /// <summary>
        /// Inserts a behavior into the stunt behavior pipeline at the specified 
        /// index.
        /// </summary>
        /// <param name="stunt">The stunt to insert the behavior to.</param>
        /// <param name="index">The index to insert the behavior at.</param>
        /// <param name="behavior">(invocation, next) => invocation.CreateValueReturn() | invocation.CreateExceptionReturn() | next().Invoke(invocation, next)</param>
        /// <param name="appliesTo">invocation => true|false</param>
        /// <param name="name">Optional friendly name for the behavior.</param>
        public static TStunt InsertBehavior<TStunt>(this TStunt stunt, int index, ExecuteHandler behavior, AppliesToHandler? appliesTo = null, string? name = null)
            where TStunt : IStunt
        {
            if (stunt == null)
                throw new ArgumentNullException(nameof(stunt));

            stunt.Behaviors.Insert(index, new AnonymousBehavior(behavior, appliesTo, name));
            return stunt;
        }

        /// <summary>
        /// Inserts a behavior into the stunt behavior pipeline at the specified 
        /// index.
        /// </summary>
        /// <param name="stunt">The stunt to add the behavior to.</param>
        /// <param name="index">The index to insert the behavior at.</param>
        /// <param name="behavior">A custom behavior to apply to the stunt.</param>
        public static TStunt InsertBehavior<TStunt>(this TStunt stunt, int index, IStuntBehavior behavior)
            where TStunt : IStunt
        {
            if (stunt == null)
                throw new ArgumentNullException(nameof(stunt));

            stunt.Behaviors.Insert(index, behavior);
            return stunt;
        }

        /// <summary>
        /// Inserts a behavior factory into the stunt behavior pipeline at the specified
        /// index, so each pipeline seeded from these behaviors gets its own behavior
        /// instance instead of sharing one.
        /// </summary>
        /// <param name="stunt">The stunt to insert the behavior factory to.</param>
        /// <param name="index">The index to insert the behavior factory at.</param>
        /// <param name="factory">Function invoked to create the behavior instance.</param>
        /// <remarks>
        /// If the stunt is not constructed yet, the factory is registered and invoked
        /// once per pipeline seeding (e.g. per <c>StuntBuilder.Build</c> call). If the
        /// stunt is already constructed, the factory is invoked immediately and the
        /// resulting instance is inserted into the live pipeline.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The factory function returned <see langword="null"/>.</exception>
        public static TStunt InsertBehavior<TStunt>(this TStunt stunt, int index, Func<IStuntBehavior> factory)
            where TStunt : IStunt
        {
            if (stunt == null)
                throw new ArgumentNullException(nameof(stunt));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            stunt.Behaviors.Insert(index, CreateBehavior(stunt, factory));
            return stunt;
        }

        static IStuntBehavior CreateBehavior<TStunt>(TStunt stunt, Func<IStuntBehavior> factory)
            where TStunt : IStunt
            // A live pipeline gets the factory's instance right away; a stunt that has
            // not been constructed yet registers the factory for pipeline seeding.
            => stunt.Behaviors is BehaviorsCollection
                ? factory() ?? throw new InvalidOperationException(ThisAssembly.Strings.BehaviorFactoryReturnedNull)
                : new BehaviorFactory(factory);
    }
}
