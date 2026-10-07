using System;
using System.Reflection;
using Xunit;

namespace Stunts.UnitTests
{
    public class MethodInvokerTests
    {
        static readonly MethodInfo AddMethod = typeof(Adder).GetMethod(nameof(Adder.Add))!;
        static readonly MethodInfo TryAddMethod = typeof(Adder).GetMethod(nameof(Adder.TryAdd))!;
        static readonly MethodInfo InterfaceAddMethod = typeof(IAdder).GetMethod(nameof(IAdder.Add))!;

        // An interface member has no base body. The stunt target throws; any other instance runs the member.
        static readonly MethodInvoker InterfaceAdd = (target, call) =>
        {
            if (ReferenceEquals(target, call.Target))
                throw new NotImplementedException(call.ToString());

            return call.CreateValueReturn(((IAdder)target).Add(
                call.Arguments.Get<int>("x"),
                call.Arguments.Get<int>("y")));
        };

        [Fact]
        public void StuntTargetRunsTheBaseBody()
        {
            var stunt = new StuntAdder();
            var invocation = AddCall(stunt, 2, 3);

            Assert.True(invocation.HasImplementation);
            Assert.Equal(-1, stunt.Add(2, 3));
            Assert.Equal(5, invocation.Invoke(stunt).ReturnValue);
            Assert.Equal(5, invocation.CreateInvokeReturn().ReturnValue);
        }

        [Fact]
        public void OtherTargetRunsThatInstance()
        {
            var stunt = new StuntAdder();
            var invocation = AddCall(stunt, 2, 3);

            Assert.Equal(6, invocation.Invoke(new Multiplier()).ReturnValue);
        }

        [Fact]
        public void InvokerReadsTheInvocationArguments()
        {
            var stunt = new StuntAdder();
            var invocation = AddCall(stunt, 2, 3);
            invocation.Arguments.SetValue("x", 10);

            var result = invocation.Invoke(new Multiplier());

            Assert.Equal(30, result.ReturnValue);
            Assert.Null(result.Exception);
        }

        [Fact]
        public void ReplacedArgumentsReachTheBaseBody()
        {
            var stunt = new StuntAdder();
            var invocation = AddCall(stunt, 2, 3);

            var result = invocation.CreateInvokeReturn(ArgumentCollection.Create(AddMethod.GetParameters(), 10, 4));

            Assert.Equal(14, result.ReturnValue);
            Assert.Equal(2, invocation.Arguments.Get<int>("x"));
        }

        [Fact]
        public void TryAddOnTheStuntRunsTheBaseBody()
        {
            var stunt = new StuntAdder();
            var invocation = TryAddCall(stunt, 2, 3);
            var x = 2;
            var y = 3;

            var direct = stunt.TryAdd(ref x, ref y, out var directZ);
            var result = invocation.Invoke(stunt);

            Assert.False(direct);
            Assert.Null(directZ);
            Assert.Equal(true, result.ReturnValue);
            Assert.Equal(2, result.Outputs.Get<int>("x"));
            Assert.Equal(3, result.Outputs.Get<int>("y"));
            Assert.Equal(5, result.Outputs.GetNullable<int?>("z"));
        }

        [Fact]
        public void TryAddOnAnotherInstanceReturnsRefAndOutValues()
        {
            var invocation = TryAddCall(new StuntAdder(), 2, 3);

            var result = invocation.Invoke(new Multiplier());

            Assert.Equal(true, result.ReturnValue);
            Assert.Equal(3, result.Outputs.Get<int>("x"));
            Assert.Equal(5, result.Outputs.Get<int>("y"));
            Assert.Equal(5, result.Outputs.GetNullable<int?>("z"));
        }

        [Fact]
        public void TargetExceptionPropagates()
        {
            var stunt = new StuntAdder();
            var broken = new BrokenAdder();
            var invocation = AddCall(stunt, 2, 3);

            var thrown = Assert.Throws<InvalidOperationException>(() => invocation.Invoke(broken));

            Assert.Same(broken.Error, thrown);
        }

        [Fact]
        public void InterfaceStuntTargetThrowsAndOtherTargetRuns()
        {
            var stunt = new object();
            var invocation = MethodInvocation.Create(stunt, InterfaceAddMethod, InterfaceAdd, 2, 3);

            var thrown = Assert.Throws<NotImplementedException>(() => invocation.Invoke(stunt));
            Assert.Contains(nameof(IAdder.Add), thrown.Message);
            Assert.Throws<NotImplementedException>(() => invocation.CreateInvokeReturn());

            var result = invocation.Invoke(new InterfaceAdder());
            Assert.Equal(5, result.ReturnValue);
            Assert.Null(result.Exception);
        }

        [Fact]
        public void MissingInvokerThrows()
        {
            var invocation = MethodInvocation.Create(new object(), AddMethod, 2, 3);

            Assert.False(invocation.HasImplementation);
            var fromInvoke = Assert.Throws<NotImplementedException>(() => invocation.Invoke(new object()));
            var fromBase = Assert.Throws<NotImplementedException>(() => invocation.CreateInvokeReturn());

            Assert.Contains(nameof(Adder.Add), fromInvoke.Message);
            Assert.Contains("does not have an implementation", fromInvoke.Message);
            Assert.Contains(nameof(Adder.Add), fromBase.Message);
        }

        [Fact]
        public void NullInvokerAndNullTargetThrow()
        {
            var stunt = new StuntAdder();
            var missing = Assert.Throws<ArgumentNullException>(() =>
                new MethodInvocation(stunt, AddMethod, (MethodInvoker)null!, ArgumentCollection.Create(AddMethod.GetParameters(), 2, 3)));
            Assert.Equal("implementation", missing.ParamName);

            var target = Assert.Throws<ArgumentNullException>(() => AddCall(stunt, 2, 3).Invoke(null!));
            Assert.Equal("target", target.ParamName);
        }

        static MethodInvocation AddCall(StuntAdder stunt, int x, int y)
            => MethodInvocation.Create(stunt, AddMethod, stunt.AddInvoker, x, y);

        static MethodInvocation TryAddCall(StuntAdder stunt, int x, int y)
            => MethodInvocation.Create(stunt, TryAddMethod, stunt.TryAddInvoker, x, y, default(int?));

        public class Adder
        {
            public virtual int Add(int x, int y) => x + y;

            public virtual bool TryAdd(ref int x, ref int y, out int? z)
            {
                z = x + y;
                return true;
            }
        }

        public class StuntAdder : Adder
        {
            public MethodInvoker AddInvoker { get; }
            public MethodInvoker TryAddInvoker { get; }

            public StuntAdder()
            {
                // The stunt target calls the base body. A virtual call on the stunt would re-enter the override.
                AddInvoker = (target, call) =>
                {
                    var x = call.Arguments.Get<int>("x");
                    var y = call.Arguments.Get<int>("y");
                    if (ReferenceEquals(target, this))
                        return call.CreateValueReturn(base.Add(x, y));

                    return call.CreateValueReturn(((Adder)target).Add(x, y));
                };

                TryAddInvoker = (target, call) =>
                {
                    var x = call.Arguments.Get<int>("x");
                    var y = call.Arguments.Get<int>("y");
                    var z = call.Arguments.GetNullable<int?>("z");
                    var added = ReferenceEquals(target, this)
                        ? base.TryAdd(ref x, ref y, out z)
                        : ((Adder)target).TryAdd(ref x, ref y, out z);
                    return call.CreateValueReturn(added, new ArgumentCollection(call.MethodBase.GetParameters())
                    {
                        { "x", x },
                        { "y", y },
                        { "z", z }
                    });
                };
            }

            public override int Add(int x, int y) => -1;

            public override bool TryAdd(ref int x, ref int y, out int? z)
            {
                z = null;
                return false;
            }
        }

        public class Multiplier : Adder
        {
            public override int Add(int x, int y) => x * y;

            public override bool TryAdd(ref int x, ref int y, out int? z)
            {
                z = x + y;
                x += 1;
                y += 2;
                return true;
            }
        }

        public class BrokenAdder : Adder
        {
            public Exception Error { get; } = new InvalidOperationException("boom");

            public override int Add(int x, int y) => throw Error;
        }

        public interface IAdder
        {
            int Add(int x, int y);
        }

        public class InterfaceAdder : IAdder
        {
            public int Add(int x, int y) => x + y;
        }
    }
}
