using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Stunts
{
    /// <summary>
    /// One parameter of a member that has data-annotation rules.
    /// </summary>
    public sealed class DataAnnotationsSlot
    {
        /// <summary>Creates a slot for the parameter at <paramref name="index"/>.</summary>
        public DataAnnotationsSlot(int index, params DataAnnotationsRule[] rules)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (rules == null || rules.Length == 0)
                throw new ArgumentException("At least one rule is required.", nameof(rules));

            Index = index;
            Rules = rules;
        }

        /// <summary>Parameter position. Property setters validate the value parameter.</summary>
        public int Index { get; }

        /// <summary>Rules in declaration order. The first failure rejects the call.</summary>
        public DataAnnotationsRule[] Rules { get; }
    }

    /// <summary>
    /// Validates one argument. Return <see langword="null"/> when the value is acceptable.
    /// <paramref name="instance"/> is the stunt the member was called on.
    /// </summary>
    public delegate System.ComponentModel.DataAnnotations.ValidationResult? DataAnnotationsRule(object? value, object instance);

    /// <summary>
    /// Compilation registrations of members and parameters that carry validation attributes.
    /// The source generator fills this from a module initializer. Lookups key a member by its
    /// declaring type, metadata name, and <see cref="DataAnnotationsSignature"/>.
    /// </summary>
    public static class DataAnnotationsRegistry
    {
        static readonly DataAnnotationsSlot[] None = Array.Empty<DataAnnotationsSlot>();
        static readonly ConcurrentDictionary<Key, DataAnnotationsSlot[]> members = new();
        static readonly ConcurrentDictionary<RuntimeMethodHandle, DataAnnotationsSlot[]> resolved = new();

        /// <summary>
        /// Registers the rules for one member. A later registration for the same member replaces the earlier one.
        /// </summary>
        /// <param name="declaringType">The interface or class that declares the member, closed over the same type arguments the stunt implements.</param>
        /// <param name="member">Metadata name, such as <c>set_Email</c>, <c>Save</c>, or <c>.ctor</c>.</param>
        /// <param name="signature"><see cref="DataAnnotationsSignature"/> of the member.</param>
        /// <param name="slots">Parameters that have rules.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Register(Type declaringType, string member, string signature, params DataAnnotationsSlot[] slots)
        {
            if (declaringType == null)
                throw new ArgumentNullException(nameof(declaringType));
            if (member == null)
                throw new ArgumentNullException(nameof(member));
            if (signature == null)
                throw new ArgumentNullException(nameof(signature));
            if (slots == null || slots.Length == 0)
                throw new ArgumentException("At least one slot is required.", nameof(slots));

            members[new Key(declaringType.TypeHandle, member, signature)] = slots;
            resolved.Clear();
        }

        internal static bool AppliesTo(MethodBase method) => TryResolve(method, out _);

        internal static Exception? Validate(IMethodInvocation invocation)
        {
            if (!TryResolve(invocation.MethodBase, out var slots))
                return null;

            foreach (var slot in slots)
            {
                if ((uint)slot.Index >= (uint)invocation.Arguments.Count)
                    continue;

                var value = invocation.Arguments.GetValue(slot.Index);
                foreach (var rule in slot.Rules)
                {
                var result = rule(value, invocation.Target);
                if (result is not null)
                    return new System.ComponentModel.DataAnnotations.ValidationException(result, validatingAttribute: null, value);
                }
            }

            return null;
        }

        static bool TryResolve(MethodBase method, out DataAnnotationsSlot[] slots)
        {
            if (!resolved.TryGetValue(method.MethodHandle, out slots!))
            {
                slots = Find(method) ?? None;
                resolved.TryAdd(method.MethodHandle, slots);
            }

            return slots.Length != 0;
        }

        static DataAnnotationsSlot[]? Find(MethodBase method)
        {
            var name = SimpleName(method.Name);
            var signature = DataAnnotationsSignature.Format(method);
            var declaring = method.DeclaringType;
            if (declaring == null)
                return null;

            foreach (var contract in Contracts(declaring))
            {
                if (members.TryGetValue(new Key(contract.TypeHandle, name, signature), out var slots))
                    return slots;
            }

            return null;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "The stunt type declares the member being invoked, so its interface map is already kept.")]
        static Type[] Contracts(Type stunt)
        {
            var count = 0;
            for (var type = stunt.BaseType; type != null && type != typeof(object); type = type.BaseType)
                count++;

            var interfaces = stunt.GetInterfaces();
            var ordered = new Type[interfaces.Length];
            Array.Copy(interfaces, ordered, interfaces.Length);
            Array.Sort(ordered, static (left, right) => InterfaceDepth(right).CompareTo(InterfaceDepth(left)));

            var contracts = new Type[count + ordered.Length];
            var index = 0;
            for (var type = stunt.BaseType; type != null && type != typeof(object); type = type.BaseType)
                contracts[index++] = type;
            Array.Copy(ordered, 0, contracts, index, ordered.Length);
            return contracts;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "The stunt type declares the member being invoked, so its interface map is already kept.")]
        static int InterfaceDepth(Type type)
        {
            var depth = 0;
            foreach (var parent in type.GetInterfaces())
                depth = Math.Max(depth, InterfaceDepth(parent) + 1);
            return depth;
        }

        static string SimpleName(string name)
        {
            var dot = name.LastIndexOf('.');
            return dot < 0 ? name : name.Substring(dot + 1);
        }

        readonly record struct Key(RuntimeTypeHandle Type, string Member, string Signature);
    }
}
