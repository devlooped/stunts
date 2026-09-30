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
            var stunt = Stunt.For<StringReturn>();
            StringReturn value = stunt.ToObject();

            Assert.Equal("derived", value.Value());

            stunt.AddBehavior((invocation, next) => invocation.CreateValueReturn("proxy"));

            Assert.Equal("proxy", value.Value());
        }

        public void PositionalRecordConstructorAndVirtualMethod()
        {
            var stunt = Stunt.For<Person>("Ada");
            Person person = stunt.ToObject();

            Assert.Equal("Ada", person.Name);
            Assert.Equal("Hello Ada", person.Greet());

            stunt.AddBehavior((invocation, next) =>
                invocation.MethodBase.Name == nameof(Person.Greet)
                    ? invocation.CreateValueReturn("hi")
                    : next(invocation, next));

            Assert.Equal("hi", person.Greet());
            Assert.Equal("Ada", person.Name);

            var copy = person with { Name = "Bea" };

            Assert.Equal("Bea", copy.Name);
            Assert.IsType(person.GetType(), copy);
            // The synthesized record copy copies the pipeline field.
            Assert.Equal("hi", copy.Greet());
        }

        public void EmptyRecord()
        {
            Empty stunt = Stunt.Of<Empty>();

            Assert.IsAssignableFrom<Empty>(stunt);
            Assert.IsAssignableFrom<IStunt>(stunt);
        }

        public void RecordCanTakeAnAdditionalInterface()
        {
            Person stunt = Stunt.Of<Person, IDisposable>("Ada");

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
