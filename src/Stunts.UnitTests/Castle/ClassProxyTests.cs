using System;
using System.Reflection;
using Xunit;

namespace Stunts.UnitTests.Castle
{
    /// <summary>
    /// Castle class proxies: an unsealed class, virtual members intercepted,
    /// non-virtual members left alone, constructors forwarded.
    /// </summary>
    public class ClassProxyTests : IRunnable
    {
        public void Run()
        {
            VirtualCallProceedsToBase();
            MostDerivedOverrideIsTheImplementation();
            NonVirtualMembersAreNotIntercepted();
            SelfCallHitsTheOverride();
            AbstractMemberThrowsUntilABehaviorReturns();
            ProceedOnAbstractMemberThrows();
            ProtectedVirtualIsIntercepted();
            ConstructorArgumentsReachTheBase();
            ProtectedConstructorIsReachable();
            InternalConstructorInTheSameAssemblyIsReachable();
            ParamsConstructorAcceptsAnArray();
            VirtualCallDuringConstructionUsesThePipelineFactory();
            NestedClassAndCharReturn();
            InternalClassInTheSameAssembly();
            ParameterNamesAreReplicated();
            InternalAndPrivateProtectedMembersAreIntercepted();
            PrivateNestedTypeIsProxiedFromInsideItsContainer();
        }

        public void VirtualCallProceedsToBase()
        {
            var stunt = Stunt.Of<Counter>();

            Assert.Equal(1, stunt.Next());

            stunt.AddBehavior((invocation, next) =>
                invocation.MethodBase.Name == nameof(Counter.Next)
                    ? invocation.CreateValueReturn(40)
                    : next(invocation, next));

            Assert.Equal(40, stunt.Next());
        }

        public void MostDerivedOverrideIsTheImplementation()
        {
            var stunt = Stunt.Of<DerivedCounter>();

            Assert.Equal(2, stunt.Next());
        }

        public void NonVirtualMembersAreNotIntercepted()
        {
            var calls = 0;
            var stunt = Stunt.Of<Counter>().AddBehavior((invocation, next) =>
            {
                calls++;
                return next(invocation, next);
            });

            Assert.Equal(7, stunt.Fixed());
            Assert.Equal(0, calls);
        }

        public void SelfCallHitsTheOverride()
        {
            var stunt = Stunt.Of<Counter>().AddBehavior((invocation, next) =>
                invocation.MethodBase.Name == nameof(Counter.Next)
                    ? invocation.CreateValueReturn(9)
                    : next(invocation, next));

            Assert.Equal(9, stunt.ThroughSelf());
        }

        public void AbstractMemberThrowsUntilABehaviorReturns()
        {
            var stunt = Stunt.Of<AbstractCounter>();

            Assert.Throws<NotImplementedException>(() => stunt.Next());

            stunt.AddBehavior((invocation, next) => invocation.CreateValueReturn(4));

            Assert.Equal(4, stunt.Next());
        }

        public void ProceedOnAbstractMemberThrows()
        {
            var stunt = Stunt.Of<AbstractCounter>().AddBehavior((invocation, next) => next(invocation, next));

            Assert.Throws<NotImplementedException>(() => stunt.Next());
        }

        public void ProtectedVirtualIsIntercepted()
        {
            var stunt = Stunt.Of<Counter>().AddBehavior((invocation, next) =>
                invocation.MethodBase.Name == "Secret"
                    ? invocation.CreateValueReturn(11)
                    : next(invocation, next));

            Assert.Equal(11, stunt.CallSecret());
        }

        public void ConstructorArgumentsReachTheBase()
        {
            var named = Stunt.Of<Named>("Ada");
            Assert.Equal("Ada", named.Name);

            var disabled = Stunt.Of<Named>("Ada", false);
            Assert.Equal("Ada", disabled.Name);
            Assert.False(disabled.Enabled);

            var missing = Stunt.Of<Named>(new object[] { null });
            Assert.Null(missing.Name);
        }

        public void ProtectedConstructorIsReachable()
        {
            var stunt = Stunt.Of<ProtectedCtor>(5);

            Assert.Equal(5, stunt.Value);
        }

        public void InternalConstructorInTheSameAssemblyIsReachable()
        {
            var stunt = Stunt.Of<InternalCtor>();

            Assert.True(stunt.Created);
        }

        public void ParamsConstructorAcceptsAnArray()
        {
            var empty = Stunt.Of<ParamsCtor>();
            var filled = Stunt.Of<ParamsCtor>(new int[] { 1, 2, 3 });
            var head = Stunt.Of<IntAndParams>(5);

            Assert.Equal(0, empty.Count);
            Assert.Equal(3, filled.Count);
            Assert.Equal(5, head.First);
            Assert.Equal(0, head.Rest);
        }

        public void VirtualCallDuringConstructionUsesThePipelineFactory()
        {
            var previous = BehaviorPipelineFactory.LocalDefault;
            BehaviorPipelineFactory.LocalDefault = new CtorBehaviorFactory();
            try
            {
                var stunt = Stunt.Of<CtorCaller>();

                Assert.Equal("proxy", stunt.Seen);
            }
            finally
            {
                BehaviorPipelineFactory.LocalDefault = previous;
            }
        }

        public void NestedClassAndCharReturn()
        {
            var stunt = Stunt.Of<Outer.Inner>();

            Assert.Equal('a', stunt.Letter());
        }

        public void InternalClassInTheSameAssembly()
        {
            var stunt = Stunt.Of<InternalCounter>();

            Assert.Equal(1, stunt.Next());
        }

        public void InternalAndPrivateProtectedMembersAreIntercepted()
        {
            var stunt = Stunt.Of<Gate>();

            Assert.True(stunt.Opened);
            Assert.Equal(1, stunt.Next());
            Assert.Equal(2, stunt.CallSecret());

            stunt.AddBehavior((invocation, next) =>
                invocation.MethodBase.Name == "Secret"
                    ? invocation.CreateValueReturn(9)
                    : next(invocation, next));

            Assert.Equal(9, stunt.CallSecret());
            Assert.Equal(1, stunt.Next());
        }

        public void PrivateNestedTypeIsProxiedFromInsideItsContainer()
        {
            Shelter.Run();
        }

        public void ParameterNamesAreReplicated()
        {
            var method = Stunt.Of<Counter>().GetType().GetMethod(nameof(Counter.Add));
            var parameters = method.GetParameters();

            Assert.Equal("left", parameters[0].Name);
            Assert.Equal("right", parameters[1].Name);
        }

        class CtorBehaviorFactory : IBehaviorPipelineFactory
        {
            public BehaviorPipeline CreatePipeline<TStunt>() => new BehaviorPipeline(new NameBehavior());

            class NameBehavior : IStuntBehavior
            {
                public bool AppliesTo(IMethodInvocation invocation) => true;

                public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
                    => invocation.MethodBase.Name == nameof(CtorCaller.Name)
                        ? invocation.CreateValueReturn("proxy")
                        : next(invocation, next);
            }
        }

        public class Counter
        {
            public virtual int Next() => 1;

            public int Fixed() => 7;

            public int ThroughSelf() => Next();

            protected virtual int Secret() => 3;

            public int CallSecret() => Secret();

            public virtual int Add(int left, int right) => left + right;
        }

        public class DerivedCounter : Counter
        {
            public override int Next() => 2;
        }

        public abstract class AbstractCounter
        {
            public abstract int Next();
        }

        public class Named
        {
            public Named(string name) : this(name, true) { }

            protected Named(string name, bool enabled)
                => (Name, Enabled) = (name, enabled);

            public string Name { get; }

            public bool Enabled { get; }
        }

        public class ProtectedCtor
        {
            protected ProtectedCtor(int value) => Value = value;

            public int Value { get; }
        }

        public class Gate
        {
            private protected Gate() => Opened = true;

            public bool Opened { get; }

            internal virtual int Next() => 1;

            private protected virtual int Secret() => 2;

            public int CallSecret() => Secret();
        }

        public class InternalCtor
        {
            internal InternalCtor() => Created = true;

            public bool Created { get; }
        }

        public class ParamsCtor
        {
            public ParamsCtor(params int[] values) => Count = values.Length;

            public int Count { get; }
        }

        public class IntAndParams
        {
            public IntAndParams(int first, params int[] rest)
            {
                First = first;
                Rest = rest.Length;
            }

            public int First { get; }

            public int Rest { get; }
        }

        public class CtorCaller
        {
            public CtorCaller() => Seen = Name();

            public string Seen { get; }

            public virtual string Name() => "base";
        }

        public class Outer
        {
            public class Inner
            {
                public virtual char Letter() => 'a';
            }
        }
    }

    public partial class Shelter
    {
        public static void Run()
        {
            var stunt = Stunt.Of<Hidden>();

            Assert.Equal(1, stunt.Next());
            Assert.Equal(3, stunt.CallSecret());

            stunt.AddBehavior((invocation, next) =>
                invocation.MethodBase.Name == "Secret"
                    ? invocation.CreateValueReturn(9)
                    : next(invocation, next));

            Assert.Equal(9, stunt.CallSecret());
        }

        class Hidden
        {
            public virtual int Next() => 1;

            private protected virtual int Secret() => 3;

            public int CallSecret() => Secret();
        }
    }

    class InternalCounter
    {
        public virtual int Next() => 1;
    }
}
