using Xunit;

namespace Stunts.UnitTests
{
    public class DefaultEqualityBehaviorTests
    {
        [Fact]
        public void AppliesToGetHashCode()
        {
            var method = typeof(Foo).GetMethod(nameof(object.GetHashCode))!;
            var behavior = new DefaultEqualityBehavior();

            Assert.True(behavior.AppliesTo(MethodInvocation.Create(new Foo(), method)));
        }

        [Fact]
        public void AppliesToEquals()
        {
            var method = typeof(Foo).GetMethod(nameof(object.Equals))!;
            var behavior = new DefaultEqualityBehavior();

            Assert.True(behavior.AppliesTo(MethodInvocation.Create(new Foo(), method, new Foo())));
        }

        [Fact]
        public void GetsHashCode()
        {
            var method = typeof(Foo).GetMethod(nameof(object.GetHashCode))!;
            var behavior = new DefaultEqualityBehavior();
            var target = new Foo();

            var expected = target.GetHashCode();
            var actual = (int?)behavior.Execute(MethodInvocation.Create(target, method), null).ReturnValue;

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void EqualsSameInstance()
        {
            var method = typeof(Foo).GetMethod(nameof(object.Equals))!;
            var behavior = new DefaultEqualityBehavior();
            var target = new Foo();

            var actual = (bool?)behavior.Execute(MethodInvocation.Create(target, method, target), null!).ReturnValue;

            Assert.True(actual);
        }

        [Fact]
        public void NotEqualsDifferentInstance()
        {
            var method = typeof(Foo).GetMethod(nameof(object.Equals))!;
            var behavior = new DefaultEqualityBehavior();
            var target = new Foo();

            var actual = (bool?)behavior.Execute(MethodInvocation.Create(target, method, new Foo()), null!).ReturnValue;

            Assert.False(actual);
        }

        [Fact]
        public void InvokeNextIfNotEqualsOrGetHashCode()
        {
            var method = typeof(Foo).GetMethod(nameof(Foo.ToString))!;
            var behavior = new DefaultEqualityBehavior();
            var target = new Foo();
            var nextCalled = false;

            behavior.Execute(
                MethodInvocation.Create(target, method),
                (m, n) =>
                {
                    nextCalled = true;
                    return m.CreateReturn();
                });

            Assert.True(nextCalled);
        }

        public class Foo
        {
            public override string? ToString()
            {
                return base.ToString();
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public override bool Equals(object? obj)
            {
                return base.Equals(obj);
            }
        }
    }
}
