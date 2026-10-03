using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Stunts
{
    /// <summary>
    /// Provides a <see cref="IStuntFactory"/> that creates the stunts generated at 
    /// compile-time, which register themselves when their assembly is loaded.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class CompiledStuntFactory : IStuntFactory
    {
        static readonly ConditionalWeakTable<Assembly, Registrations> byAssembly = new();
        static readonly Dictionary<string, Registrations> byName = new(StringComparer.Ordinal);

        /// <summary>
        /// Registers a generated stunt for the given <paramref name="types"/>, requested
        /// at run time by the <paramref name="assembly"/> that declares it.
        /// </summary>
        /// <param name="assembly">The assembly that requests the stunt (and declares it).</param>
        /// <param name="stuntType">The generated stunt type.</param>
        /// <param name="types">The base type and additional interfaces the stunt implements.</param>
        /// <param name="constructors">The constructors of the stunt.</param>
        public static void Register(Assembly assembly, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type stuntType, Type[] types, params StuntConstructor[] constructors)
            => byAssembly.GetValue(assembly, _ => new Registrations()).Add(stuntType, types, constructors);

        /// <summary>
        /// Registers a generated stunt for the given <paramref name="types"/>, requested
        /// at run time by a referenced assembly, such as a generic factory method in a 
        /// library invoked with concrete types from the generating project.
        /// </summary>
        /// <param name="assembly">The name of the assembly that requests the stunt.</param>
        /// <param name="stuntType">The generated stunt type.</param>
        /// <param name="types">The base type and additional interfaces the stunt implements.</param>
        /// <param name="constructors">The constructors of the stunt.</param>
        public static void Register(string assembly, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type stuntType, Type[] types, params StuntConstructor[] constructors)
        {
            lock (byName)
            {
                if (!byName.TryGetValue(assembly, out var registrations))
                    byName.Add(assembly, registrations = new Registrations());

                registrations.Add(stuntType, types, constructors);
            }
        }

        /// <summary>
        /// Creates the stunt registered for <paramref name="baseType"/> and 
        /// <paramref name="implementedInterfaces"/> by the generated code for 
        /// <paramref name="assembly"/>.
        /// </summary>
        /// <param name="assembly">The assembly requesting the stunt.</param>
        /// <param name="baseType">Base type of the stunt.</param>
        /// <param name="implementedInterfaces">Additional interfaces the stunt implements.</param>
        /// <param name="constructorArguments">Optional additional constructor arguments for the stunt.</param>
        public object CreateStunt(Assembly assembly, Type baseType, Type[] implementedInterfaces, object?[] constructorArguments)
        {
            var key = new TypeSet(baseType, implementedInterfaces);
            var registration = Find(assembly, key);
            if (registration == null)
            {
                // Registrations run from the module initializer, which may not have run yet
                // if nothing else from the assembly was used.
                RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
                registration = Find(assembly, key);
            }

            if (registration == null)
            {
                var type = StuntTypeName(baseType, implementedInterfaces);
                throw new NotSupportedException(ThisAssembly.Strings.StuntNotRegistered(type, assembly.GetName().Name));
            }

            return registration.Create(constructorArguments ?? Array.Empty<object?>(), baseType, implementedInterfaces);
        }

        static Registration? Find(Assembly assembly, TypeSet key)
        {
            if (byAssembly.TryGetValue(assembly, out var registrations) &&
                registrations.TryGet(key) is Registration registration)
                return registration;

            if (assembly.GetName().Name is string name)
            {
                lock (byName)
                {
                    if (byName.TryGetValue(name, out registrations))
                        return registrations.TryGet(key);
                }
            }

            return null;
        }

        static string StuntTypeName(Type baseType, Type[] interfaces)
            => string.Join(", ", new[] { baseType }.Concat(interfaces).Select(CSharpTypeName.Format));

        sealed class Registrations
        {
            readonly Dictionary<TypeSet, Registration> registrations = new();

            public void Add(Type stuntType, Type[] types, StuntConstructor[] constructors)
            {
                lock (registrations)
                    registrations[new TypeSet(types[0], types.Skip(1).ToArray())] = new Registration(stuntType, constructors);
            }

            public Registration? TryGet(TypeSet key)
            {
                lock (registrations)
                    return registrations.TryGetValue(key, out var registration) ? registration : null;
            }
        }

        sealed class Registration
        {
            public Registration(Type stuntType, StuntConstructor[] constructors)
                => (StuntType, Constructors) = (stuntType, constructors);

            public Type StuntType { get; }

            public StuntConstructor[] Constructors { get; }

            public object Create(object?[] arguments, Type baseType, Type[] interfaces)
            {
                var candidates = Constructors.Where(constructor => constructor.Accepts(arguments)).ToArray();
                var best = candidates.Where(candidate => candidates.All(other =>
                    other == candidate || candidate.IsMoreSpecificThan(other, arguments))).ToArray();

                if (candidates.Length == 0)
                    throw new MissingMethodException(ThisAssembly.Strings.StuntConstructorNotFound(StuntTypeName(baseType, interfaces)));
                if (best.Length != 1)
                    throw new AmbiguousMatchException(ThisAssembly.Strings.AmbiguousStuntConstructor(StuntTypeName(baseType, interfaces)));

                return best[0].Create(arguments);
            }
        }

        // The set of types implemented by a stunt is order-insensitive, which matches the naming 
        // convention of generated stunts, that sorts the additional interfaces.
        readonly struct TypeSet : IEquatable<TypeSet>
        {
            readonly Type[] types;
            readonly int hashCode;

            public TypeSet(Type baseType, Type[] interfaces)
            {
                types = new[] { baseType }.Concat(interfaces).Distinct().ToArray();
                hashCode = types.Aggregate(types.Length, (hash, type) => hash ^ type.GetHashCode());
            }

            public bool Equals(TypeSet other)
                => types.Length == other.types.Length && types.All(type => Array.IndexOf(other.types, type) >= 0);

            public override bool Equals(object? obj) => obj is TypeSet other && Equals(other);

            public override int GetHashCode() => hashCode;
        }
    }
}
