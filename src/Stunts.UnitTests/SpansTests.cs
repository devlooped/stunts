using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Stunts.UnitTests
{
    public class SpansTests
    {
        public class PointWrapper
        {
            public double X;
            public double Y;
        }

        public ref struct Point
        {
            public double X;
            public double Y;

            public Point(double x, double y)
            {
                X = x;
                Y = y;
            }

            public void Flip()
            {
                var z = X;
                Y = X;
                X = z;
            }
        }

        [Fact]
        public void SpanApi()
        {
            var span = new ReadOnlySpan<string>(new[] { "foo", "bar" });
            var owner = MemoryPool<string>.Shared.Rent();
            var arguments = new Arguments(typeof(SpanAPI).GetMethods().First().GetParameters())
            {
                { "foo", span },
                { "owner", owner },
            };

            var point = new Point { X = 5, Y = 10 };
            var point2 = default(Point);

            Calculate(ref point);
            Calculate(ref point2);
        }

        void Calculate(ref Point point)
        {
        }

        public class SpanAPI
        {
            public string GetSpan(ReadOnlySpan<string> span) => span[5];
        }

        delegate ReadOnlySpan<T> GetReadOnlySpan<T>();

        public class Arguments : IEnumerable
        {
            readonly ParameterInfo[] infos;
            readonly Dictionary<string, Delegate> getters = new Dictionary<string, Delegate>();

            public Arguments(ParameterInfo[] infos)
            {
                this.infos = infos;
            }

            public void Add<T>(string name, ReadOnlySpan<T> span)
            {
                // TODO: populate mirror state, save
            }

            public void Add<T>(string name, Span<T> span)
            {
                // TODO: populate mirror state, save
            }

            public void Add<T>(string name, Memory<T> memory)
            {
                // TODO: populate mirror state, save
            }

            public void Add<T>(string name, ReadOnlyMemory<T> memory)
            {
                // TODO: populate mirror state, save
            }

            public void Add<T>(string name, IMemoryOwner<T> owner)
            {
                // TODO: populate mirror state, save
            }

            public ReadOnlySpan<T> GetReadOnlySpan<T>(string name)
            {
                // TODO: retrieve state from mirror
                return default;
            }

            public Span<T> GetSpan<T>(string name)
            {
                // TODO: retrieve state from mirror
                return default;
            }

            public Memory<T> GetMemory<T>(string name)
            {
                // TODO: retrieve state from mirror
                return default;
            }

            public ReadOnlyMemory<T> GetReadOnlyMemory<T>(string name)
            {
                // TODO: retrieve state from mirror
                return default;
            }

            public IMemoryOwner<T> GetMemoryOwner<T>(string name)
            {
                // TODO: retrieve state from mirror
                return default;
            }




            public IEnumerator GetEnumerator()
            {
                return infos.GetEnumerator();
            }
        }
    }
}
