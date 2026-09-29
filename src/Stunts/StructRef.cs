using System;

namespace Stunts
{
    /// <summary>
    /// Refers to a live <c>ref struct</c> for the duration of an intercepted call.
    /// A class cannot store a <c>ref struct</c>, so this keeps the address of that stack slot.
    /// The generic <c>StructRef&lt;T&gt;</c> is emitted into the compilation that needs it.
    /// </summary>
    public unsafe class StructRef
    {
        readonly Type type;
        nint address;

        /// <summary>Refers to the value at <paramref name="ptr"/>.</summary>
        protected StructRef(Type type, void* ptr)
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));

            this.type = type;
            address = (nint)ptr;
        }

        /// <summary>
        /// Returns the address when <paramref name="checkType"/> is the type this ref was created for.
        /// </summary>
        protected void* GetPtr(Type checkType)
        {
            if (checkType != type)
                throw new AccessViolationException();

            return GetPtr();
        }

        /// <summary>Returns the address of the live <c>ref struct</c>.</summary>
        protected void* GetPtr()
        {
            var ptr = (void*)System.Threading.Volatile.Read(ref address);
            if (ptr == null)
                throw new AccessViolationException();

            return ptr;
        }

        /// <summary>
        /// Drops the address. Generated code calls this before the intercepted method returns.
        /// </summary>
        protected void Invalidate(void* checkPtr)
        {
            var ptr = (void*)System.Threading.Interlocked.CompareExchange(ref address, (nint)0, (nint)checkPtr);
            if (ptr == null || checkPtr != ptr)
                throw new AccessViolationException();
        }
    }

    /// <summary>
    /// Holds a pointer value from an intercepted signature.
    /// The pointer itself is an unmanaged value, so it is stored directly.
    /// </summary>
    public unsafe class PointerRef
    {
        /// <summary>Holds <paramref name="value"/>.</summary>
        public PointerRef(void* value) => Value = value;

        /// <summary>The pointer value.</summary>
        public void* Value { get; set; }
    }
}
