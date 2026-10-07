#pragma warning disable IDE0079
#pragma warning disable CS0436
using System;
using Xunit;

namespace Stunts.Scenarios.ForwardedSignatures
{
    public interface IDynamicBag
    {
        dynamic ViewBag { get; }

        dynamic Read(string name);
    }

    public class Bag : IDynamicBag
    {
        public dynamic ViewBag { get; set; }

        public dynamic Read(string name) => ViewBag + name;
    }

    public interface ISpanIndexer
    {
        object this[ReadOnlySpan<int> indexes] { get; set; }

        object this[ReadOnlySpan<char> indexes] { get; }
    }

    public class Test : IRunnable
    {
        public void Run()
        {
            ForwardsADynamicPropertyAndMethod();
            RefStructIndexerStillRuns();
        }

        static void ForwardsADynamicPropertyAndMethod()
        {
            var bag = new Bag { ViewBag = "hello" };
            IDynamicBag stunt = Stunt.Builder().Forward(bag).Build<IDynamicBag>();

            Assert.Equal("hello", (string)stunt.ViewBag);
            Assert.Equal("hello!", (string)stunt.Read("!"));
        }

        static void RefStructIndexerStillRuns()
        {
            ISpanIndexer stunt = Stunt.Of<ISpanIndexer>();

            Assert.Throws<NotImplementedException>(() => stunt[new ReadOnlySpan<int>(new[] { 1, 2 })]);
            Assert.Throws<NotImplementedException>(() => stunt[new ReadOnlySpan<int>(new[] { 1, 2 })] = new object());
            Assert.Throws<NotImplementedException>(() => stunt[new ReadOnlySpan<char>(new[] { 'a' })]);
        }
    }
}
