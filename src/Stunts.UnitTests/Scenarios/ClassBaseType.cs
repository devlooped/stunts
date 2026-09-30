#pragma warning disable CS0436
using System;
using System.Collections.Generic;
using Xunit;

namespace Stunts.Scenarios.ClassBaseType
{
    public abstract class BaseType
    {
        Dictionary<(int, string), string> values = new();

        public abstract event EventHandler? AbstractBase;
        public virtual event EventHandler? TurnedOn;

        public bool IsOn { get; private set; }

        public virtual string this[int index, string key]
        {
            get => values.TryGetValue((index, key), out var value) ? value : "";
            set => values[(index, key)] = value;
        }

        public virtual PlatformID Platform { get; set; } = PlatformID.Win32NT;

        public virtual bool TryAdd(int x, int y, ref int mem, out int result)
        {
            result = x + y;
            return true;
        }

        public virtual void TurnOn()
        {
            TurnedOn?.Invoke(this, EventArgs.Empty);
            IsOn = true;
        }
    }

    public class Test : IRunnable
    {
        public void Run()
        {
            var stunt = Stunt.Of<BaseType>();
            BaseType instance = stunt;

            instance.Platform = PlatformID.MacOSX;

            Assert.Equal(PlatformID.MacOSX, instance.Platform);

            Assert.False(instance.IsOn);

            var on = false;
            instance.TurnedOn += (_, _) => on = true;
            instance.TurnOn();
            Assert.True(instance.IsOn);
            Assert.True(on);

            var x = 5;
            var y = 10;
            var mem = 42;
            Assert.True(instance.TryAdd(x, y, ref mem, out var z));
            Assert.Equal(15, z);

            instance[0, "foo"] = "bar";

            Assert.Equal("bar", instance[0, "foo"]);
        }
    }
}
