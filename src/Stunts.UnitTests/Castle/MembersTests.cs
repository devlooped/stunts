using System;
using System.Collections.Generic;
using Xunit;

namespace Stunts.UnitTests.Castle
{
    /// <summary>
    /// Properties, indexers, events, and member-name case from the Castle suites.
    /// </summary>
    public class MembersTests : IRunnable
    {
        public void Run()
        {
            PropertyRoundTripOnAClass();
            PropertyCanBeSuppliedByABehavior();
            SetterOnlyProperty();
            IndexerRoundTrip();
            VirtualEventReachesTheBaseField();
            InterfaceEventAddAndRemoveAreIntercepted();
            DifferentlyCasedMembersStayDistinct();
            NarrowedAccessorsStayNarrowAndAreIntercepted();
        }

        public void PropertyRoundTripOnAClass()
        {
            var stunt = Stunt.Of<Switch>();

            stunt.Name = "on";

            Assert.Equal("on", stunt.Name);
        }

        public void PropertyCanBeSuppliedByABehavior()
        {
            string stored = null;
            var stunt = Stunt.Of<INamed>().AddBehavior((invocation, next) =>
            {
                if (invocation.MethodBase.Name.StartsWith("set_", StringComparison.Ordinal))
                {
                    stored = (string)invocation.Arguments.GetValue(0);
                    return invocation.CreateReturn();
                }

                return invocation.CreateValueReturn(stored);
            });

            stunt.Name = "Ada";

            Assert.Equal("Ada", stunt.Name);
        }

        public void SetterOnlyProperty()
        {
            var seen = 0;
            var stunt = Stunt.Of<IWriteOnly>().AddBehavior((invocation, next) =>
            {
                seen = (int)invocation.Arguments.GetValue(0);
                return invocation.CreateReturn();
            });

            stunt.Value = 12;

            Assert.Equal(12, seen);
        }

        public void IndexerRoundTrip()
        {
            var stunt = Stunt.Of<Bag>();

            stunt[2] = "two";

            Assert.Equal("two", stunt[2]);
            Assert.Equal("", stunt[0]);
        }

        public void VirtualEventReachesTheBaseField()
        {
            var stunt = Stunt.Of<Switch>();
            var raised = false;
            stunt.Toggled += (_, _) => raised = true;

            stunt.Raise();

            Assert.True(raised);
        }

        public void InterfaceEventAddAndRemoveAreIntercepted()
        {
            var added = 0;
            var removed = 0;
            var stunt = Stunt.Of<INotify>().AddBehavior((invocation, next) =>
            {
                if (invocation.MethodBase.Name.StartsWith("add_", StringComparison.Ordinal))
                    added++;
                if (invocation.MethodBase.Name.StartsWith("remove_", StringComparison.Ordinal))
                    removed++;
                return invocation.CreateReturn();
            });

            void Handler(object sender, EventArgs args) { }

            stunt.Happened += Handler;
            stunt.Happened -= Handler;

            Assert.Equal(1, added);
            Assert.Equal(1, removed);
        }

        public void DifferentlyCasedMembersStayDistinct()
        {
            var seen = new List<string>();
            var stunt = Stunt.Of<ICased>().AddBehavior((invocation, next) =>
            {
                seen.Add(invocation.MethodBase.Name);
                return invocation.CreateValueReturn(seen.Count);
            });

            Assert.Equal(1, stunt.foo());
            Assert.Equal(2, stunt.Foo());
            Assert.Equal(new[] { "foo", "Foo" }, seen);
        }

        public void NarrowedAccessorsStayNarrowAndAreIntercepted()
        {
            var stunt = Stunt.Of<Levels>().AddBehavior((invocation, next) =>
                invocation.MethodBase.Name == "set_Name"
                    ? invocation.CreateReturn()
                    : next(invocation, next));

            stunt.Rename("Ada");

            Assert.Equal("base", stunt.Name);

            var hidden = Stunt.Of<Levels>();
            hidden.Hidden = 4;

            Assert.Equal(4, hidden.Read());

            var coded = Stunt.Of<Levels>();
            coded.SetCode("A1");

            Assert.Equal("A1", coded.Code);

            var slots = Stunt.Of<Levels>();
            slots.Put(1, "one");

            Assert.Equal("one", slots[1]);
        }

        public class Switch
        {
            public virtual string Name { get; set; }

            public virtual event EventHandler Toggled;

            public void Raise() => Toggled?.Invoke(this, EventArgs.Empty);
        }

        public interface INamed
        {
            string Name { get; set; }
        }

        public interface IWriteOnly
        {
            int Value { set; }
        }

        public class Bag
        {
            readonly Dictionary<int, string> values = new Dictionary<int, string>();

            public virtual string this[int index]
            {
                get => values.TryGetValue(index, out var value) ? value : "";
                set => values[index] = value;
            }
        }

        public interface INotify
        {
            event EventHandler Happened;
        }

        public class Levels
        {
            public virtual string Name { get; protected set; } = "base";

            public virtual int Hidden { protected get; set; }

            public virtual string Code { get; internal set; }

            readonly Dictionary<int, string> slots = new Dictionary<int, string>();

            public virtual string this[int index]
            {
                get => slots.TryGetValue(index, out var value) ? value : "";
                protected set => slots[index] = value;
            }

            public void Rename(string name) => Name = name;

            public int Read() => Hidden;

            public void SetCode(string code) => Code = code;

            public void Put(int index, string value) => this[index] = value;
        }

        public interface ICased
        {
            int foo();

            int Foo();
        }
    }
}
