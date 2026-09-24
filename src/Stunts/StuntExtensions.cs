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
        public static IStunt AddBehavior(this IStunt stunt, ExecuteHandler behavior, AppliesToHandler? appliesTo = null, string? name = null)
        {
            stunt.Behaviors.Add(new AnonymousBehavior(behavior, appliesTo, name));
            return stunt;
        }

        /// <summary>
        /// Adds a behavior to a stunt.
        /// </summary>
        /// <param name="stunt">The stunt to add the behavior to.</param>
        /// <param name="behavior">A custom behavior to apply to the stunt.</param>
        public static IStunt AddBehavior(this IStunt stunt, IStuntBehavior behavior)
        {
            stunt.Behaviors.Add(behavior);
            return stunt;
        }

        /// <summary>
        /// Adds a behavior to a stunt.
        /// </summary>
        /// <param name="stunt">The stunt to add the behavior to.</param>
        /// <param name="behavior">(invocation, next) => invocation.CreateValueReturn() | invocation.CreateExceptionReturn() | next().Invoke(invocation, next)</param>
        /// <param name="appliesTo">invocation => true|false</param>
        /// <param name="name">Optional friendly name for the behavior.</param>
        //[EditorBrowsable(EditorBrowsableState.Advanced)]
        public static TStunt AddBehavior<TStunt>(this TStunt stunt, ExecuteHandler behavior, AppliesToHandler? appliesTo = null, string? name = null)
        {
            // We can't just add a constraint to the method signature, because 
            // proxies are typically generated and don't expose the IProxy interface directly.
            if (stunt is IStunt target)
                target.Behaviors.Add(new AnonymousBehavior(behavior, appliesTo, name));
            else
                throw new ArgumentException(nameof(stunt));

            return stunt;
        }

        /// <summary>
        /// Adds a behavior to a stunt.
        /// </summary>
        /// <param name="stunt">The stunt to add the behavior to.</param>
        /// <param name="behavior">A custom behavior to apply to the stunt.</param>
        //[EditorBrowsable(EditorBrowsableState.Advanced)]
        public static TStunt AddBehavior<TStunt>(this TStunt stunt, IStuntBehavior behavior)
        {
            if (stunt is IStunt target)
                target.Behaviors.Add(behavior);
            else
                throw new ArgumentException(nameof(stunt));

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
        public static IStunt InsertBehavior(this IStunt stunt, int index, ExecuteHandler behavior, AppliesToHandler? appliesTo = null, string? name = null)
        {
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
        public static IStunt InsertBehavior(this IStunt stunt, int index, IStuntBehavior behavior)
        {
            stunt.Behaviors.Insert(index, behavior);
            return stunt;
        }

        /// <summary>
        /// Inserts a behavior into the stunt behavior pipeline at the specified
        /// index.
        /// </summary>
        /// <param name="stunt">The stunt to add the behavior to.</param>
        /// <param name="index">The index to insert the behavior at.</param>
        /// <param name="behavior">(invocation, next) => invocation.CreateValueReturn() | invocation.CreateExceptionReturn() | next().Invoke(invocation, next)</param>
        /// <param name="appliesTo">invocation => true|false</param>
        /// <param name="name">Optional friendly name for the behavior.</param>
        //[EditorBrowsable(EditorBrowsableState.Advanced)]
        public static TStunt InsertBehavior<TStunt>(this TStunt stunt, int index, ExecuteHandler behavior, AppliesToHandler? appliesTo = null, string? name = null)
        {
            if (stunt is IStunt target)
                target.Behaviors.Insert(index, new AnonymousBehavior(behavior, appliesTo, name));
            else
                throw new ArgumentException(nameof(stunt));

            return stunt;
        }

        /// <summary>
        /// Inserts a behavior into the stunt behavior pipeline at the specified
        /// index.
        /// </summary>
        /// <param name="stunt">The stunt to add the behavior to.</param>
        /// <param name="index">The index to insert the behavior at.</param>
        /// <param name="behavior">A custom behavior to apply to the stunt.</param>
        //[EditorBrowsable(EditorBrowsableState.Advanced)]
        public static TStunt InsertBehavior<TStunt>(this TStunt stunt, int index, IStuntBehavior behavior)
        {
            if (stunt is IStunt target)
                target.Behaviors.Insert(index, behavior);
            else
                throw new ArgumentException(nameof(stunt));

            return stunt;
        }
    }
}
