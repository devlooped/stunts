using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Stunts.UnitTests.Castle
{
    /// <summary>
    /// Attributes that reflection does not inherit onto an override are copied
    /// onto the generated stunt. Inheritable type and method attributes are not
    /// copied a second time.
    /// </summary>
    public class AttributeTests : IRunnable
    {
        public void Run()
        {
            ClassAttributesAndMembers();
            InterfaceGuidAndParameterAttribute();
            OmittedArgumentsArriveAsTheDefault();
            ReflectionInvokeUsesTheCopiedDefault();
        }

        public void ClassAttributesAndMembers()
        {
            var stunt = Stunt.Of<Marked>(4);
            var type = stunt.GetType();

            Assert.NotNull(type.GetCustomAttribute<SerializableAttribute>());
            Assert.Equal(new Guid("22222222-2222-2222-2222-222222222222"), type.GUID);
            Assert.Null(type.GetCustomAttribute<InheritedMarkerAttribute>(inherit: false));
            Assert.Single(type.GetCustomAttributes<InheritedMarkerAttribute>(inherit: true));

            var go = type.GetMethod(nameof(Marked.Go));
            var note = go.GetCustomAttribute<NoteAttribute>();
            Assert.Equal("go", note.Text);
            Assert.True(note.Mark);
            Assert.Null(go.GetCustomAttribute<InheritedMarkerAttribute>(inherit: false));
            Assert.Single(go.GetCustomAttributes<InheritedMarkerAttribute>(inherit: true));
            Assert.Equal("id", go.GetParameters()[0].GetCustomAttribute<TagAttribute>().Name);
            Assert.Equal(2, go.GetParameters()[0].GetCustomAttribute<TagAttribute>().Count);
            Assert.NotNull(go.GetParameters()[1].GetCustomAttribute<InheritedMarkerAttribute>(inherit: false));

            var label = type.GetMethod(nameof(Marked.Label));
            Assert.Equal("ret", label.ReturnParameter.GetCustomAttribute<NoteAttribute>().Text);
            Assert.Equal("l", stunt.Label());

            var flag = type.GetMethod(nameof(Marked.Flag));
            Assert.Equal(UnmanagedType.Bool, flag.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>().Value);
            Assert.Equal(UnmanagedType.LPWStr, flag.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>().Value);

            Assert.Equal(5, type.GetMethod(nameof(Marked.Defaulted)).GetParameters()[0].DefaultValue);
            Assert.Equal(1.5m, type.GetMethod(nameof(Marked.Price)).GetParameters()[0].DefaultValue);
            Assert.Equal("none", type.GetMethod(nameof(Marked.OptionalName)).GetParameters()[0].DefaultValue);

            var meta = type.GetMethod(nameof(Marked.Meta));
            Assert.Equal(Side.Right, meta.GetCustomAttribute<KindAttribute>().Value);
            Assert.Equal(typeof(string), meta.GetCustomAttribute<TypeMarkAttribute>().Type);
            Assert.Equal(new[] { 1, 2, 3 }, meta.GetCustomAttribute<NumbersAttribute>().Values);
            Assert.NotNull(type.GetMethod(nameof(Marked.Generic)).GetCustomAttribute<GenericMarkAttribute<int>>());
            Assert.NotNull(type.GetMethod(nameof(Marked.Hidden)).GetCustomAttribute<InternalNoteAttribute>());

            var value = type.GetProperty(nameof(Marked.Value));
            Assert.Equal("value", value.GetCustomAttribute<NoteAttribute>().Text);
            Assert.NotNull(value.GetCustomAttribute<InheritedMarkerAttribute>(inherit: false));
            Assert.Equal("get", value.GetGetMethod().GetCustomAttribute<NoteAttribute>().Text);
            Assert.Equal("set", value.GetSetMethod().GetCustomAttribute<NoteAttribute>().Text);
            Assert.Equal("name", type.GetProperty(nameof(Marked.Name)).GetGetMethod().GetCustomAttribute<NoteAttribute>().Text);
            Assert.Equal("index", type.GetProperty("Item").GetIndexParameters()[0].GetCustomAttribute<TagAttribute>().Name);

            Assert.Equal("changed", type.GetEvent(nameof(Marked.Changed)).GetCustomAttribute<NoteAttribute>().Text);
            var happened = type.GetEvent(nameof(Marked.Happened));
            Assert.Equal("add", happened.AddMethod.GetCustomAttribute<NoteAttribute>().Text);
            Assert.Equal("remove", happened.RemoveMethod.GetCustomAttribute<NoteAttribute>().Text);

            Assert.Equal("ctor", type.GetConstructor(new[] { typeof(int) }).GetCustomAttribute<NoteAttribute>().Text);
            Assert.Equal("seed", type.GetConstructor(new[] { typeof(int) }).GetParameters()[0].GetCustomAttribute<TagAttribute>().Name);
            Assert.Equal(4, stunt.Seed);
        }

        public void OmittedArgumentsArriveAsTheDefault()
        {
            int number = 0;
            decimal amount = 0;
            string label = null;
            var marked = Stunt.Of<Marked>(0).AddBehavior((invocation, next) =>
            {
                switch (invocation.MethodBase.Name)
                {
                    case nameof(Marked.Defaulted):
                        number = invocation.Arguments.Get<int>("value");
                        break;
                    case nameof(Marked.Price):
                        amount = invocation.Arguments.Get<decimal>("value");
                        break;
                    case nameof(Marked.OptionalName):
                        label = invocation.Arguments.Get<string>("value");
                        break;
                }

                return next(invocation, next);
            });

            Assert.Equal(5, marked.Defaulted());
            Assert.Equal(5, number);
            Assert.Equal(1.5m, marked.Price());
            Assert.Equal(1.5m, amount);
            Assert.Equal("none", marked.OptionalName());
            Assert.Equal("none", label);

            Assert.Equal(9, marked.Defaulted(9));
            Assert.Equal(9, number);

            int echo = 0;
            string text = null;
            var optional = Stunt.Of<IOptional>().AddBehavior((invocation, next) =>
            {
                echo = invocation.Arguments.Get<int>("value");
                text = invocation.Arguments.Get<string>("text");
                return invocation.CreateValueReturn(echo + text);
            });

            Assert.Equal("7hi", optional.Echo());
            Assert.Equal(7, echo);
            Assert.Equal("hi", text);

            Assert.Equal("7other", optional.Echo(text: "other"));
            Assert.Equal("other", text);
        }

        public void ReflectionInvokeUsesTheCopiedDefault()
        {
            var marked = Stunt.Of<Marked>(0);
            var markedType = marked.GetType();

            Assert.Equal(5, InvokeOptional(markedType.GetMethod(nameof(Marked.Defaulted)), marked, Type.Missing));
            Assert.Equal(1.5m, InvokeOptional(markedType.GetMethod(nameof(Marked.Price)), marked, Type.Missing));
            Assert.Equal("none", InvokeOptional(markedType.GetMethod(nameof(Marked.OptionalName)), marked, Type.Missing));

            var optional = Stunt.Of<IOptional>().AddBehavior((invocation, next) =>
                invocation.CreateValueReturn(invocation.Arguments.Get<int>("value") + invocation.Arguments.Get<string>("text")));
            var echo = optional.GetType().GetMethod(nameof(IOptional.Echo));

            Assert.Equal("7hi", InvokeOptional(echo, optional, Type.Missing, Type.Missing));
            Assert.Equal("7other", InvokeOptional(echo, optional, Type.Missing, "other"));
        }

        static object InvokeOptional(MethodInfo method, object instance, params object[] arguments)
            => method.Invoke(
                instance,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.OptionalParamBinding,
                null,
                arguments,
                null);

        public void InterfaceGuidAndParameterAttribute()
        {
            var stunt = Stunt.Of<IMarked>();
            var type = stunt.GetType();

            Assert.Equal(new Guid("11111111-1111-1111-1111-111111111111"), type.GUID);

            var run = type.GetMethod(nameof(IMarked.Run));
            Assert.Equal("iface", run.GetCustomAttribute<NoteAttribute>().Text);
            Assert.Equal("n", run.GetParameters()[0].GetCustomAttribute<TagAttribute>().Name);
        }

        [Serializable]
        [Guid("22222222-2222-2222-2222-222222222222")]
        [InheritedMarker]
        public class Marked
        {
            [Note("ctor")]
            public Marked([Tag("seed")] int seed) => Seed = seed;

            public int Seed { get; }

            [Note("go", Mark = true)]
            [InheritedMarker]
            public virtual void Go([Tag("id", Count = 2)] int id, [InheritedMarker] string name) { }

            [return: Note("ret")]
            public virtual string Label() => "l";

            [return: MarshalAs(UnmanagedType.Bool)]
            public virtual bool Flag([MarshalAs(UnmanagedType.LPWStr)] string text) => text.Length > 0;

            public virtual int Defaulted(int value = 5) => value;

            public virtual decimal Price(decimal value = 1.5m) => value;

            public virtual string OptionalName(string value = "none") => value;

            [Kind(Side.Right)]
            [TypeMark(typeof(string))]
            [Numbers(new[] { 1, 2, 3 })]
            public virtual void Meta() { }

            [GenericMark<int>]
            public virtual void Generic() { }

            [InternalNote]
            public virtual void Hidden() { }

            [Note("value")]
            [InheritedMarker]
            public virtual int Value
            {
                [Note("get")]
                get => 0;
                [Note("set")]
                set { }
            }

            public virtual string Name
            {
                [Note("name")]
                get => "x";
            }

            public virtual int this[[Tag("index")] int index] => index;

            [Note("changed")]
            public virtual event EventHandler Changed;

            public virtual event EventHandler Happened
            {
                [Note("add")]
                add { }
                [Note("remove")]
                remove { }
            }

            public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
        }

        public interface IOptional
        {
            string Echo(int value = 7, string text = "hi");
        }

        [Guid("11111111-1111-1111-1111-111111111111")]
        public interface IMarked
        {
            [Note("iface")]
            void Run([Tag("n")] int n);
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter, Inherited = true, AllowMultiple = true)]
        public sealed class InheritedMarkerAttribute : Attribute { }

        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.ReturnValue | AttributeTargets.Event | AttributeTargets.Constructor, Inherited = false)]
        public sealed class NoteAttribute : Attribute
        {
            public NoteAttribute(string text) => Text = text;

            public string Text { get; }

            public bool Mark { get; set; }
        }

        [AttributeUsage(AttributeTargets.Parameter, Inherited = true)]
        public sealed class TagAttribute : Attribute
        {
            public TagAttribute(string name) => Name = name;

            public string Name { get; }

            public int Count { get; set; }
        }

        [AttributeUsage(AttributeTargets.Method, Inherited = false)]
        public sealed class KindAttribute : Attribute
        {
            public KindAttribute(Side value) => Value = value;

            public Side Value { get; }
        }

        [AttributeUsage(AttributeTargets.Method, Inherited = false)]
        public sealed class TypeMarkAttribute : Attribute
        {
            public TypeMarkAttribute(Type type) => Type = type;

            public Type Type { get; }
        }

        [AttributeUsage(AttributeTargets.Method, Inherited = false)]
        public sealed class NumbersAttribute : Attribute
        {
            public NumbersAttribute(int[] values) => Values = values;

            public int[] Values { get; }
        }

        [AttributeUsage(AttributeTargets.Method, Inherited = false)]
        public sealed class GenericMarkAttribute<T> : Attribute { }

        [AttributeUsage(AttributeTargets.Method, Inherited = false)]
        internal sealed class InternalNoteAttribute : Attribute { }

        public enum Side
        {
            Left = 1,
            Right = 2,
        }
    }
}
