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
    }
}
