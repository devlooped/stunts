using System;
using Xunit;

namespace Stunts.UnitTests.Castle
{
    /// <summary>
    /// ref, out, in, params, and array parameters from the Castle suites.
    /// </summary>
    public class ParametersTests : IRunnable
    {
        public void Run()
        {
            OutAndRefArgumentsCanBeReplaced();
            RefAndOutReachTheBaseImplementation();
            InParameterIsVisibleToTheBehavior();
            ParamsArrayIsPassedThrough();
            MultidimensionalArrayParameter();
            NullableValueAndOutDecimal();
        }

        public void OutAndRefArgumentsCanBeReplaced()
        {
            IMath stunt = Stunt.For<IMath>().AddBehavior((invocation, next) =>
            {
                var left = invocation.Arguments.Get<int>("left");
                var right = invocation.Arguments.Get<int>("right");
                return invocation.CreateValueReturn(true, left + 1, right, (left + right) * 2);
            }, invocation => !invocation.MethodBase.IsConstructor).ToObject();

            var left = 2;
            var right = 3;
            var ok = stunt.Combine(ref left, ref right, out var doubled);

            Assert.True(ok);
            Assert.Equal(3, left);
            Assert.Equal(3, right);
            Assert.Equal(10, doubled);
        }

        public void RefAndOutReachTheBaseImplementation()
        {
            MathClass stunt = Stunt.Of<MathClass>();
            var value = 2;

            Assert.True(stunt.TryDouble(ref value, out var doubled));
            Assert.Equal(4, value);
            Assert.Equal(4, doubled);
        }

        public void InParameterIsVisibleToTheBehavior()
        {
            IPoints stunt = Stunt.For<IPoints>().AddBehavior((invocation, next) =>
                invocation.CreateValueReturn(invocation.Arguments.Get<Point>("point").X),
                invocation => !invocation.MethodBase.IsConstructor).ToObject();

            var point = new Point(6);

            Assert.Equal(6, stunt.X(in point));
        }

        public void ParamsArrayIsPassedThrough()
        {
            IMath stunt = Stunt.For<IMath>().AddBehavior((invocation, next) =>
            {
                var values = invocation.Arguments.Get<int[]>("values");
                var sum = 0;
                foreach (var value in values)
                    sum += value;
                return invocation.CreateValueReturn(sum);
            }, invocation => !invocation.MethodBase.IsConstructor).ToObject();

            Assert.Equal(6, stunt.Sum(1, 2, 3));
        }

        public void MultidimensionalArrayParameter()
        {
            IMath stunt = Stunt.For<IMath>().AddBehavior((invocation, next) =>
            {
                var cells = invocation.Arguments.Get<int[,]>("cells");
                return invocation.CreateValueReturn(cells[0, 1] + cells[1, 0]);
            }, invocation => !invocation.MethodBase.IsConstructor).ToObject();

            var cells = new int[2, 2];
            cells[0, 1] = 4;
            cells[1, 0] = 5;

            Assert.Equal(9, stunt.Sum(cells));
        }

        public void NullableValueAndOutDecimal()
        {
            IMath stunt = Stunt.For<IMath>().AddBehavior((invocation, next) =>
                invocation.CreateValueReturn(true, (int?)null, 1.5m),
                invocation => !invocation.MethodBase.IsConstructor).ToObject();

            var ok = stunt.TryPrice(out int? units, out var price);

            Assert.True(ok);
            Assert.Null(units);
            Assert.Equal(1.5m, price);
        }

        public interface IMath
        {
            bool Combine(ref int left, ref int right, out int doubled);

            int Sum(params int[] values);

            int Sum(int[,] cells);

            bool TryPrice(out int? units, out decimal price);
        }

        public class MathClass
        {
            public virtual bool TryDouble(ref int value, out int doubled)
            {
                value *= 2;
                doubled = value;
                return true;
            }
        }

        public readonly struct Point
        {
            public Point(int x) => X = x;

            public int X { get; }
        }

        public interface IPoints
        {
            int X(in Point point);
        }
    }
}
