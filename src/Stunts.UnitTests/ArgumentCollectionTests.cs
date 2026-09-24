using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Stunts.UnitTests
{
    public class ArgumentCollectionTests
    {
        static readonly MethodInfo valueTypeMethod = typeof(ArgumentCollectionTests).GetMethod(nameof(DoValueType), BindingFlags.Static | BindingFlags.Public)!;
        static readonly MethodInfo nullableValueTypeMethod = typeof(ArgumentCollectionTests).GetMethod(nameof(DoNullableValueType), BindingFlags.Static | BindingFlags.Public)!;
        static readonly MethodInfo referenceTypeMethod = typeof(ArgumentCollectionTests).GetMethod(nameof(DoReferenceType), BindingFlags.Static | BindingFlags.Public)!;
        static readonly MethodInfo multipleArgsMethod = typeof(ArgumentCollectionTests).GetMethod(nameof(DoMultiple), BindingFlags.Static | BindingFlags.Public)!;

        [Fact]
        public void ThrowsIfMismatchValuesLength()
            => Assert.Throws<TargetParameterCountException>(() => ArgumentCollection.Create(valueTypeMethod.GetParameters(), 5, "Foo"));

        [Fact]
        public void ThrowsIfNameNotFound()
            => Assert.Throws<KeyNotFoundException>(() => ArgumentCollection.Create(valueTypeMethod.GetParameters(), 5)["foo"]);

        [Fact]
        public void ThrowsIfIndexNotFound()
            => Assert.Throws<IndexOutOfRangeException>(() => ArgumentCollection.Create(valueTypeMethod.GetParameters(), 5)[42]);

        [Fact]
        public void ThrowsIfGetValueNameNotFound()
            => Assert.Throws<KeyNotFoundException>(() => ArgumentCollection.Create(valueTypeMethod.GetParameters(), 5).GetValue("foo"));

        [Fact]
        public void ThrowsIfGetValueIndexNotFound()
            => Assert.Throws<IndexOutOfRangeException>(() => ArgumentCollection.Create(valueTypeMethod.GetParameters(), 5).GetValue(42));

        [Fact]
        public void ThrowsIfSetValueNameNotFound()
            => Assert.Throws<KeyNotFoundException>(() => ArgumentCollection.Create(valueTypeMethod.GetParameters(), 5).SetValue("foo", 42));

        [Fact]
        public void ThrowsIfSetValueIndexNotFound()
            => Assert.Throws<IndexOutOfRangeException>(() => ArgumentCollection.Create(valueTypeMethod.GetParameters(), 5).SetValue(42, 42));

        [Fact]
        public void AccessValueByIndex()
        {
            var arguments = ArgumentCollection.Create(valueTypeMethod.GetParameters(), 5);

            Assert.Equal(5, arguments.GetValue(0));
        }

        [Fact]
        public void AccessValueByName()
        {
            var arguments = ArgumentCollection.Create(valueTypeMethod.GetParameters(), 5);

            Assert.Equal(5, arguments.GetValue("value"));

            arguments.SetValue("value", 10);

            Assert.Equal(10, arguments.Get<int>("value"));
        }

        [Fact]
        public void Count()
#pragma warning disable xUnit2013 // Do not use equality check to check for collection size.
            => Assert.Equal(1, ArgumentCollection.Create(valueTypeMethod.GetParameters(), 5).Count);
#pragma warning restore xUnit2013 // Do not use equality check to check for collection size.

        [Fact]
        public void GetTyped()
        {
            Assert.Equal(5, ArgumentCollection.Create(valueTypeMethod.GetParameters(), 5).Get<int>(0));
            Assert.Equal(5, ArgumentCollection.Create(valueTypeMethod.GetParameters(), 5).Get<int>("value"));

            // If nullable annotations are ignored and we request a nullable int, it should still work.
#nullable disable
            Assert.Equal((int?)null, ArgumentCollection.Create(nullableValueTypeMethod.GetParameters(), default(int?)).Get<int?>(0));
            Assert.Equal((int?)null, ArgumentCollection.Create(nullableValueTypeMethod.GetParameters(), default(int?)).Get<int?>("value"));
#nullable restore

            Assert.Equal("foo", ArgumentCollection.Create(referenceTypeMethod.GetParameters(), "foo").Get<string>(0));
            Assert.Equal("foo", ArgumentCollection.Create(referenceTypeMethod.GetParameters(), "foo").Get<string>("value"));
        }

        [Fact]
        public void GetOptionalTyped()
        {
            Assert.Equal(5, ArgumentCollection.Create(valueTypeMethod.GetParameters(), 5).GetNullable<int>(0));
            Assert.Equal(5, ArgumentCollection.Create(valueTypeMethod.GetParameters(), 5).GetNullable<int>("value"));

            Assert.Equal((int?)null, ArgumentCollection.Create<int?>(nullableValueTypeMethod.GetParameters(), null).GetNullable<int?>(0));
            Assert.Equal((int?)null, ArgumentCollection.Create<int?>(nullableValueTypeMethod.GetParameters(), null).GetNullable<int?>("value"));

            Assert.Equal("foo", ArgumentCollection.Create(referenceTypeMethod.GetParameters(), "foo").GetNullable<string>(0));
            Assert.Equal("foo", ArgumentCollection.Create(referenceTypeMethod.GetParameters(), "foo").GetNullable<string>("value"));

            Assert.Null(ArgumentCollection.Create<string>(referenceTypeMethod.GetParameters(), null).GetNullable<string>(0));
            Assert.Null(ArgumentCollection.Create<string>(referenceTypeMethod.GetParameters(), null).GetNullable<string>("value"));
        }

        [Fact]
        public void GetTypedThrowsIfNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ArgumentCollection(new ObjectArgument(valueTypeMethod.GetParameters()[0], null)).Get<int>(0));
            Assert.Throws<ArgumentNullException>(() => new ArgumentCollection(new ObjectArgument(valueTypeMethod.GetParameters()[0], null)).Get<int>("value"));

            Assert.Throws<ArgumentNullException>(() => new ArgumentCollection(new ObjectArgument(referenceTypeMethod.GetParameters()[0], null)).Get<string>(0));
            Assert.Throws<ArgumentNullException>(() => new ArgumentCollection(new ObjectArgument(referenceTypeMethod.GetParameters()[0], null)).Get<string>("value"));
        }

        [Fact]
        public void GetTypedThrowsIfIncompatible()
        {
            Assert.Throws<ArgumentException>(() => ArgumentCollection.Create(referenceTypeMethod.GetParameters(), "foo").Get<int>(0));
            Assert.Throws<ArgumentException>(() => ArgumentCollection.Create(referenceTypeMethod.GetParameters(), "foo").Get<int>("value"));
        }

        [Fact]
        public void AddGetTyped()
        {
            var args = new ArgumentCollection(multipleArgsMethod.GetParameters())
            {
                { "message", "hello" },
                { "count", 25 },
                { "enabled", true },
            };

            Assert.Equal("hello", args.Get<string>("message"));
            Assert.Equal(25, args.Get<int>("count"));
            Assert.True(args.Get<bool>("enabled"));
        }

        public static void DoValueType(int value) { }
        public static void DoNullableValueType(int? value) { }
        public static void DoReferenceType(string value) { }
        public static void DoMultiple(string message, int count, bool enabled) { }
    }

    public class CustomType
    {
        public CustomType(int value) => Value = value;

        public int Value { get; }
    }
}