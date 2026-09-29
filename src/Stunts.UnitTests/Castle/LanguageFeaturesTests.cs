using System;
using Xunit;

namespace Stunts.UnitTests.Castle
{
    /// <summary>
    /// Covariant returns and records.
    /// </summary>
    public class LanguageFeaturesTests : IRunnable
    {
        public void Run()
        {
            CovariantReturnUsesTheDerivedSignature();
            PositionalRecordConstructorAndVirtualMethod();
            EmptyRecord();
            RecordCanTakeAnAdditionalInterface();
        }

        public void CovariantReturnUsesTheDerivedSignature()
        {
            var stunt = Stunt.Of<StringReturn>();

            Assert.Equal("derived", stunt.Value());

            stunt.AddBehavior((invocation, next) => invocation.CreateValueReturn("proxy"));

            Assert.Equal("proxy", stunt.Value());
        }

        public void PositionalRecordConstructorAndVirtualMethod()
        {
            var stunt = Stunt.Of<Person>("Ada");

            Assert.Equal("Ada", stunt.Name);
            Assert.Equal("Hello Ada", stunt.Greet());

            stunt.AddBehavior((invocation, next) =>
                invocation.MethodBase.Name == nameof(Person.Greet)
                    ? invocation.CreateValueReturn("hi")
                    : next(invocation, next));

            Assert.Equal("hi", stunt.Greet());
            Assert.Equal("Ada", stunt.Name);

            var copy = stunt with { Name = "Bea" };

            Assert.Equal("Bea", copy.Name);
            Assert.IsType(stunt.GetType(), copy);
            // The synthesized record copy copies the pipeline field.
            Assert.Equal("hi", copy.Greet());
        }

        public void EmptyRecord()
        {
            var stunt = Stunt.Of<Empty>();

            Assert.IsAssignableFrom<Empty>(stunt);
            Assert.IsAssignableFrom<IStunt>(stunt);
        }

        public void RecordCanTakeAnAdditionalInterface()
        {
            var stunt = Stunt.Of<Person, IDisposable>("Ada");

            Assert.IsAssignableFrom<Person>(stunt);
            Assert.IsAssignableFrom<IDisposable>(stunt);
            Assert.Equal("Ada", stunt.Name);
        }

        public class ObjectReturn
        {
            public virtual object Value() => "base";
        }

        public class StringReturn : ObjectReturn
        {
            public override string Value() => "derived";
        }

        public record Person(string Name)
        {
            public virtual string Greet() => "Hello " + Name;
        }

        public record Empty;
    }
}
