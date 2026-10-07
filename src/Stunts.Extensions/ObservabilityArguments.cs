using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Stunts
{
    /// <summary>
    /// Display projection of an invocation's arguments. Changes here are what
    /// <see cref="ObservabilityBehavior"/> logs and tags. The target still receives
    /// the original arguments.
    /// </summary>
    public sealed class ObservabilityArguments
    {
        readonly List<Slot> slots;

        internal ObservabilityArguments(IMethodInvocation invocation)
        {
            slots = new List<Slot>(invocation.Arguments.Count);
            foreach (Argument argument in invocation.Arguments)
                slots.Add(new Slot(argument.Name, argument.RawValue));
        }

        /// <summary>
        /// Omits the argument from the log and from activity tags.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <exception cref="ArgumentOutOfRangeException">No argument has <paramref name="name"/>.</exception>
        public void Drop(string name)
        {
            Find(name).Dropped = true;
        }

        /// <summary>
        /// Sets the value written for the argument. A previous <see cref="Drop"/> of the same name is cleared.
        /// The display value is not type-checked against the parameter.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="display">The value to log and tag.</param>
        /// <exception cref="ArgumentOutOfRangeException">No argument has <paramref name="name"/>.</exception>
        public void Replace(string name, object? display)
        {
            var slot = Find(name);
            slot.Dropped = false;
            slot.Display = display;
        }

        internal string Format()
        {
            var builder = new StringBuilder();
            foreach (var slot in slots)
            {
                if (slot.Dropped)
                    continue;

                if (builder.Length > 0)
                    builder.Append(", ");

                builder.Append(slot.Name).Append('=').Append(Format(slot.Display));
            }

            return builder.ToString();
        }

        internal void Tag(Activity activity)
        {
            foreach (var slot in slots)
            {
                if (!slot.Dropped)
                    activity.SetTag("arg." + slot.Name, slot.Display);
            }
        }

        Slot Find(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            foreach (var slot in slots)
            {
                if (slot.Name == name)
                    return slot;
            }

            throw new ArgumentOutOfRangeException(nameof(name), name, "No argument has this name.");
        }

        static string Format(object? display)
            => display switch
            {
                null => "null",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "null",
                _ => display.ToString() ?? "null"
            };

        sealed class Slot(string name, object? display)
        {
            public string Name => name;

            public object? Display { get; set; } = display;

            public bool Dropped { get; set; }
        }
    }
}
