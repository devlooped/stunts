using System;
using System.Collections.Generic;
using Xunit;

namespace Stunts.UnitTests
{
    public class StuntExtensionsTests
    {
        [Fact]
        public void AddAnonymousBehavior()
        {
            IStunt stunt = new TestStunt();
            Func<string?> method = ToString;

            var actual = stunt.AddBehavior(
                (m, n) => new MethodReturn(m, "foo", m.Arguments),
                m => true,
                nameof(AddBehavior));

            Assert.Same(stunt, actual);
            Assert.Single(stunt.Behaviors);
            Assert.Equal(nameof(AddBehavior), actual.Behaviors[0].ToString());
            Assert.True(actual.Behaviors[0].AppliesTo(null!));
            Assert.Equal("foo", (string?)actual.Behaviors[0].Execute(new MethodInvocation(this, method.Method), null!).ReturnValue);
        }

        [Fact]
        public void AddBehavior()
        {
            IStunt stunt = new TestStunt();
            Func<string?> method = ToString;

            var actual = stunt.AddBehavior(new TestBehavior());

            Assert.Same(stunt, actual);
            Assert.Single(stunt.Behaviors);
            Assert.Equal(nameof(TestBehavior), actual.Behaviors[0].ToString());
            Assert.True(actual.Behaviors[0].AppliesTo(null!));
            Assert.Equal("test", (string?)actual.Behaviors[0].Execute(new MethodInvocation(this, method.Method), null!).ReturnValue);
        }

        [Fact]
        public void AddBehaviorPreservesTheStuntType()
        {
            var stunt = new StuntReference<ITestStunt>(new TestStunt());

            // The concrete reference type flows through, so behaviors can keep being configured.
            StuntReference<ITestStunt> actual = stunt.AddBehavior(new TestBehavior());
            ITestStunt value = actual.ToObject();

            Assert.Same(stunt, actual);
            Assert.Same(stunt.ToObject(), value);
            Assert.Single(stunt.Behaviors);
        }

        [Fact]
        public void AddBehaviorToNullStuntThrows()
        {
            IStunt stunt = null!;

            Assert.Throws<ArgumentNullException>(() => stunt.AddBehavior(new TestBehavior()));
        }

        [Fact]
        public void AddBehaviorFollowsDelegateTarget()
        {
            var target = new TestStunt();
            var action = (Action)Delegate.CreateDelegate(typeof(Action), target, typeof(TestStunt).GetMethod(nameof(TestStunt.Do))!);
            var stunt = new StuntReference<Action>(action);

            var actual = stunt.AddBehavior(new TestBehavior());

            Assert.Same(stunt, actual);
            Assert.Single(target.Behaviors);
        }

        [Fact]
        public void AddBehaviorOnCombinedDelegateThrows()
        {
            var target = new TestStunt();
            var action = (Action)Delegate.CreateDelegate(typeof(Action), target, typeof(TestStunt).GetMethod(nameof(TestStunt.Do))!);
            Action other = () => { };
            var combined = (Action)Delegate.Combine(action, other);

            Assert.Throws<ArgumentException>(() => new StuntReference<Action>(combined));
        }

        [Fact]
        public void InsertAnonymousBehavior()
        {
            IStunt stunt = new TestStunt();
            Func<string?> method = ToString;

            var actual = stunt.InsertBehavior(0,
                (m, n) => new MethodReturn(m, "foo", m.Arguments),
                m => true,
                nameof(InsertAnonymousBehavior));

            Assert.Same(stunt, actual);
            Assert.Single(stunt.Behaviors);
            Assert.Equal(nameof(InsertAnonymousBehavior), actual.Behaviors[0].ToString());
            Assert.True(actual.Behaviors[0].AppliesTo(null!));
            Assert.Equal("foo", (string?)actual.Behaviors[0].Execute(new MethodInvocation(this, method.Method), null!).ReturnValue);
        }

        [Fact]
        public void InsertBehavior()
        {
            IStunt stunt = new TestStunt();
            Func<string?> method = ToString;

            var actual = stunt.InsertBehavior(0, new TestBehavior());

            Assert.Same(stunt, actual);
            Assert.Single(stunt.Behaviors);
            Assert.Equal(nameof(TestBehavior), actual.Behaviors[0].ToString());
            Assert.True(actual.Behaviors[0].AppliesTo(null!));
            Assert.Equal("test", (string?)actual.Behaviors[0].Execute(new MethodInvocation(this, method.Method), null!).ReturnValue);
        }

        [Fact]
        public void InsertBehaviorPreservesTheStuntType()
        {
            var stunt = new StuntReference<ITestStunt>(new TestStunt());

            StuntReference<ITestStunt> actual = stunt.InsertBehavior(0, new TestBehavior());

            Assert.Same(stunt, actual);
            Assert.Single(stunt.Behaviors);
        }

        [Fact]
        public void InsertBehaviorToNullStuntThrows()
        {
            IStunt stunt = null!;

            Assert.Throws<ArgumentNullException>(() => stunt.InsertBehavior(0, new TestBehavior()));
        }

        [Fact]
        public void StuntOfNonStuntThrows()
            => Assert.Throws<ArgumentException>(() => new StuntReference<object>(new object()));

        class TestBehavior : IStuntBehavior
        {
            public bool AppliesTo(IMethodInvocation invocation) => true;

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
                => new MethodReturn(invocation, "test", invocation.Arguments);

            public override string ToString() => nameof(TestBehavior);
        }

        public interface ITestStunt
        {
            void Do();
        }

        class TestStunt : ITestStunt, IStunt
        {
            public IList<IStuntBehavior> Behaviors { get; } = new BehaviorsCollection();

            public void Do() { }
        }
    }
}
