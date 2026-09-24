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
        public void AddAnonymousBehaviorToObject()
        {
            object stunt = new TestStunt();
            Func<string?> method = ToString;

            var actual = stunt.AddBehavior(
                (m, n) => new MethodReturn(m, "foo", null!),
                m => true,
                nameof(AddBehavior)) as IStunt;

            Assert.NotNull(actual);
            Assert.Same(stunt, actual);
            Assert.Single(actual!.Behaviors);
        }

        [Fact]
        public void AddAnonymousBehaviorToNonStuntThrows()
        {
            var stunt = new object();
            Func<string?> method = ToString;

            Assert.Throws<ArgumentException>(() => stunt.AddBehavior(
                (m, n) => new MethodReturn(m, "foo", null!),
                m => true,
                nameof(AddBehavior)));
        }

        [Fact]
        public void AddBehaviorToNonStuntThrows()
        {
            var stunt = new object();
            Func<string?> method = ToString;

            Assert.Throws<ArgumentException>(() => stunt.AddBehavior(new TestBehavior()));
        }

        [Fact]
        public void AddBehaviorToObject()
        {
            object stunt = new TestStunt();

            var actual = stunt.AddBehavior(new TestBehavior()) as IStunt;

            Assert.NotNull(actual);
            Assert.Same(stunt, actual);
            Assert.Single(actual!.Behaviors);
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
        public void InsertAnonymousBehaviorToObject()
        {
            object stunt = new TestStunt();
            Func<string?> method = ToString;

            var actual = stunt.InsertBehavior(0,
                (m, n) => new MethodReturn(m, "foo", null!),
                m => true,
                nameof(InsertAnonymousBehaviorToObject)) as IStunt;

            Assert.NotNull(actual);
            Assert.Same(stunt, actual);
            Assert.Single(actual!.Behaviors);
        }

        [Fact]
        public void InsertAnonymousBehaviorToNonStuntThrows()
        {
            var stunt = new object();
            Func<string?> method = ToString;

            Assert.Throws<ArgumentException>(() => stunt.InsertBehavior(0,
                (m, n) => new MethodReturn(m, "foo", null!),
                m => true,
                nameof(InsertAnonymousBehaviorToNonStuntThrows)));
        }

        [Fact]
        public void InsertBehaviorToNonStuntThrows()
        {
            var stunt = new object();
            Func<string?> method = ToString;

            Assert.Throws<ArgumentException>(() => stunt.InsertBehavior(0, new TestBehavior()));
        }

        [Fact]
        public void InsertBehaviorToObject()
        {
            object stunt = new TestStunt();

            var actual = stunt.InsertBehavior(0, new TestBehavior()) as IStunt;

            Assert.NotNull(actual);
            Assert.Same(stunt, actual);
            Assert.Single(actual!.Behaviors);
        }

        class TestBehavior : IStuntBehavior
        {
            public bool AppliesTo(IMethodInvocation invocation) => true;

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
                => new MethodReturn(invocation, "test", invocation.Arguments);

            public override string ToString() => nameof(TestBehavior);
        }

        class TestStunt : IStunt
        {
            public IList<IStuntBehavior> Behaviors { get; } = new BehaviorsCollection();
        }
    }
}
