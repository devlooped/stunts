#pragma warning disable CS8500
using System;
using System.Runtime.CompilerServices;

namespace Stunts
{
    /// <summary>Refers to a live <typeparamref name="T"/> ref struct.</summary>
    public unsafe class StructRef<T> : StructRef
        where T : allows ref struct
    {
        /// <summary>Refers to <paramref name="value"/>.</summary>
        public StructRef(ref T value)
            : base(typeof(T), Unsafe.AsPointer(ref value))
        {
        }

        /// <summary>The ref struct this ref points at.</summary>
        public ref T Value => ref Unsafe.AsRef<T>(GetPtr(typeof(T)));

        /// <summary>Drops the address when it still points at <paramref name="value"/>.</summary>
        public void Invalidate(ref T value) => Invalidate(Unsafe.AsPointer(ref value));
    }

    /// <summary>Refers to a live <see cref="Span{T}"/>.</summary>
    public class SpanRef<T> : StructRef<Span<T>>
    {
        /// <summary>Refers to <paramref name="value"/>.</summary>
        public SpanRef(ref Span<T> value) : base(ref value) { }
    }

    /// <summary>Refers to a live <see cref="ReadOnlySpan{T}"/>.</summary>
    public class ReadOnlySpanRef<T> : StructRef<ReadOnlySpan<T>>
    {
        /// <summary>Refers to <paramref name="value"/>.</summary>
        public ReadOnlySpanRef(ref ReadOnlySpan<T> value) : base(ref value) { }
    }
}
