using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Stunts
{
    /// <summary>
    /// An <see cref="IStuntBehavior"/> that raises <see cref="INotifyPropertyChanging.PropertyChanging"/>
    /// and <see cref="INotifyPropertyChanged.PropertyChanged"/> from property setters.
    /// </summary>
    /// <remarks>
    /// The behavior intercepts the <c>set_</c> accessors of properties as well as the
    /// <c>add_</c>/<c>remove_</c> accessors of the notify events (the generated event
    /// accessors are empty for interface stunts, so the behavior owns subscriber
    /// storage and event dispatch end-to-end).
    /// <para>
    /// The incoming value is compared against the last value written through the
    /// pipeline using <see cref="object.Equals(object, object)"/>; no property getter
    /// is ever invoked, keeping the behavior Native AOT-friendly. Assigning an
    /// unchanged value raises neither event. By default the setter still runs;
    /// pass <c>true</c> for <c>shortCircuitUnchanged</c> to skip the
    /// setter entirely for unchanged values.
    /// </para>
    /// <para>
    /// <see cref="INotifyPropertyChanging.PropertyChanging"/> is raised before the
    /// setter runs; <see cref="INotifyPropertyChanged.PropertyChanged"/> is raised
    /// after a successful set. A throwing setter (or an exception-return) records no
    /// value and raises no <c>PropertyChanged</c>.
    /// </para>
    /// <para>
    /// State is held in instance fields, so each stunt needs its own behavior
    /// instance. When using <c>StuntBuilder</c>, register via
    /// <c>AddBehavior&lt;NotifyPropertyChangeBehavior&gt;()</c> (or a factory) so every
    /// built stunt gets a fresh instance; the behavior also implements
    /// <see cref="ICloneable"/>, so plain instance registrations are cloned per stunt.
    /// </para>
    /// </remarks>
    public class NotifyPropertyChangeBehavior : IStuntBehavior, ICloneable
    {
        readonly object gate = new object();
        readonly Dictionary<string, object?> lastValues = new Dictionary<string, object?>(StringComparer.Ordinal);
        readonly bool shortCircuitUnchanged;
        PropertyChangingEventHandler? changing;
        PropertyChangedEventHandler? changed;

        /// <summary>
        /// Initializes the behavior with <c>shortCircuitUnchanged: false</c>.
        /// </summary>
        public NotifyPropertyChangeBehavior() : this(false) { }

        /// <summary>
        /// Initializes the behavior.
        /// </summary>
        /// <param name="shortCircuitUnchanged">
        /// When <see langword="true"/>, assigning a value equal to the last written
        /// value skips the setter entirely (no events, no <c>next</c> invocation).
        /// When <see langword="false"/> (default), the setter still runs but no
        /// events are raised, so the behavior introduces no behavioral change.
        /// </param>
        public NotifyPropertyChangeBehavior(bool shortCircuitUnchanged)
            => this.shortCircuitUnchanged = shortCircuitUnchanged;

        /// <summary>
        /// Returns a new, empty <see cref="NotifyPropertyChangeBehavior"/> carrying
        /// over the <c>shortCircuitUnchanged</c> setting, so each stunt gets
        /// independent notification state.
        /// </summary>
        public object Clone() => new NotifyPropertyChangeBehavior(shortCircuitUnchanged);

        /// <summary>
        /// Applies to property setters (<c>set_*</c>) and to the
        /// <c>add_</c>/<c>remove_</c> accessors of <c>PropertyChanged</c> and
        /// <c>PropertyChanging</c>.
        /// </summary>
        public bool AppliesTo(IMethodInvocation invocation)
        {
            var name = invocation.MethodBase.Name;
            return name.StartsWith("set_", StringComparison.Ordinal)
                || name == "add_PropertyChanged" || name == "remove_PropertyChanged"
                || name == "add_PropertyChanging" || name == "remove_PropertyChanging";
        }

        /// <summary>
        /// Handles event subscriptions and property sets as described in the
        /// class remarks.
        /// </summary>
        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            switch (invocation.MethodBase.Name)
            {
                case "add_PropertyChanged":
                    Subscribe(ref changed, (PropertyChangedEventHandler)invocation.Arguments[0].RawValue!);
                    return invocation.CreateReturn();
                case "remove_PropertyChanged":
                    Unsubscribe(ref changed, (PropertyChangedEventHandler)invocation.Arguments[0].RawValue!);
                    return invocation.CreateReturn();
                case "add_PropertyChanging":
                    Subscribe(ref changing, (PropertyChangingEventHandler)invocation.Arguments[0].RawValue!);
                    return invocation.CreateReturn();
                case "remove_PropertyChanging":
                    Unsubscribe(ref changing, (PropertyChangingEventHandler)invocation.Arguments[0].RawValue!);
                    return invocation.CreateReturn();
                default:
                    return ExecuteSet(invocation, next);
            }
        }

        IMethodReturn ExecuteSet(IMethodInvocation invocation, ExecuteHandler next)
        {
            var property = invocation.MethodBase.Name.Substring("set_".Length);
            var incoming = invocation.Arguments[0].RawValue;

            bool unchanged;
            lock (gate)
                unchanged = lastValues.TryGetValue(property, out var current) && Equals(current, incoming);

            if (unchanged)
            {
                // No events for an unchanged value. By default the setter still runs;
                // opt-in short-circuit skips it entirely.
                if (shortCircuitUnchanged)
                    return invocation.CreateReturn();
                return next(invocation, next);
            }

            PropertyChangingEventHandler? changingSnapshot;
            lock (gate)
                changingSnapshot = changing;
            changingSnapshot?.Invoke(invocation.Target, new PropertyChangingEventArgs(property));

            var result = next(invocation, next);
            if (result.Exception != null)
                return result;

            // A thrown exception propagates from next() above, so reaching here
            // means the set succeeded: record the value and notify.
            PropertyChangedEventHandler? changedSnapshot;
            lock (gate)
            {
                lastValues[property] = incoming;
                changedSnapshot = changed;
            }
            changedSnapshot?.Invoke(invocation.Target, new PropertyChangedEventArgs(property));
            return result;
        }

        void Subscribe(ref PropertyChangedEventHandler? handlers, PropertyChangedEventHandler handler)
        {
            lock (gate)
                handlers += handler;
        }

        void Unsubscribe(ref PropertyChangedEventHandler? handlers, PropertyChangedEventHandler handler)
        {
            lock (gate)
                handlers -= handler;
        }

        void Subscribe(ref PropertyChangingEventHandler? handlers, PropertyChangingEventHandler handler)
        {
            lock (gate)
                handlers += handler;
        }

        void Unsubscribe(ref PropertyChangingEventHandler? handlers, PropertyChangingEventHandler handler)
        {
            lock (gate)
                handlers -= handler;
        }
    }
}
