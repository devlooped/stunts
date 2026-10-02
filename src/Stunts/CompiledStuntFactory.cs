using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Stunts
{
    /// <summary>
    /// Provides a <see cref="IStuntFactory"/> that creates proxies from types 
    /// generated at compile-time that are included in the received stunt 
    /// assembly in <see cref="CreateStunt"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class CompiledStuntFactory : IStuntFactory
    {
        static readonly ConditionalWeakTable<Assembly, Template[]> templates = new();

        /// <summary>
        /// Uses the <see cref="StuntNaming.GetFullName(Type, Type[])"/> method to 
        /// determine the expected full type name of a compile-time generated stunt 
        /// and tries to locate it from <paramref name="assembly"/>. If no closed stunt
        /// exists, closes a matching generated generic template.
        /// </summary>
        /// <param name="assembly">The assembly containing the compile-time generated stunts.</param>
        /// <param name="baseType">Base type of the stunt.</param>
        /// <param name="implementedInterfaces">Additional interfaces the stunt implements.</param>
        /// <param name="constructorArguments">Optional additional constructor arguments for the stunt.</param>
        public object CreateStunt(Assembly assembly, Type baseType, Type[] implementedInterfaces, object?[] constructorArguments)
        {
            var name = StuntNaming.GetFullName(baseType, implementedInterfaces);
            ArgumentException? templateError = null;
            var type = assembly.GetType(name, false, false) ?? FindTemplate(assembly, baseType, implementedInterfaces, out templateError);
            if (type == null)
                throw new ArgumentException(ThisAssembly.Strings.StaticStuntTypeNotFoundInAssembly(name, assembly.GetName().Name), nameof(assembly), templateError);

            try
            {
                var instance = Activator.CreateInstance(type, constructorArguments);
                if (baseType.BaseType == typeof(MulticastDelegate))
                    return Delegate.CreateDelegate(baseType, instance, instance.GetType().GetMethod("Invoke"));

                return instance;
            }
            catch (TargetInvocationException tie)
            {
                var ex = ExceptionDispatchInfo.Capture(tie.GetBaseException());
                ex.Throw();
            }

            // Code will never reach this.
            throw new NotImplementedException();
        }

        static Type? FindTemplate(Assembly assembly, Type baseType, Type[] interfaces, out ArgumentException? constraintError)
        {
            constraintError = null;
            var targets = new[] { baseType }.Concat(interfaces).ToArray();
            foreach (var template in templates.GetValue(assembly, GetTemplates))
            {
                if (template.Targets.Length != targets.Length)
                    continue;

                var arguments = new Type?[template.Parameters.Length];
                if (template.Definition.DeclaringType is Type parent && parent.IsGenericType)
                {
                    var nestedTarget = targets.FirstOrDefault(target => target.IsGenericType &&
                        target.DeclaringType?.IsGenericType == true &&
                        target.DeclaringType.GetGenericTypeDefinition() == parent.GetGenericTypeDefinition());
                    if (nestedTarget != null)
                        Array.Copy(nestedTarget.GetGenericArguments(), arguments, parent.GetGenericArguments().Length);
                }
                if (MatchTargets(template, targets, arguments, 0, ref constraintError) is Type closed)
                    return closed;
            }

            return null;
        }

        static Template[] GetTemplates(Assembly assembly)
            => assembly.GetTypes()
                .Where(type => type.IsGenericTypeDefinition)
                .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(method => method.IsDefined(typeof(StuntTemplateAttribute), false))
                    .Select(method => new Template(type, method.GetParameters().Select(parameter => parameter.ParameterType).ToArray())))
                .OrderBy(template => template.Definition.FullName, StringComparer.Ordinal)
                .ToArray();

        static Type? MatchTargets(Template template, Type[] targets, Type?[] arguments, int index, ref ArgumentException? constraintError)
        {
            if (index == template.Targets.Length)
                return CloseTemplate(template, arguments, ref constraintError);

            for (var i = index; i < targets.Length; i++)
            {
                var inferred = (Type?[])arguments.Clone();
                if (!Match(template.Targets[index], targets[i], inferred))
                    continue;
                var remaining = (Type[])targets.Clone();
                (remaining[index], remaining[i]) = (remaining[i], remaining[index]);
                if (MatchTargets(template, remaining, inferred, index + 1, ref constraintError) is Type closed)
                    return closed;
            }

            return null;
        }

        static bool Match(Type pattern, Type target, Type?[] arguments)
        {
            if (pattern.IsGenericParameter)
            {
                var index = pattern.GenericParameterPosition;
                if (arguments[index] != null)
                    return arguments[index] == target;
                arguments[index] = target;
                return true;
            }
            if (pattern.IsArray)
                return target.IsArray && pattern.GetArrayRank() == target.GetArrayRank() &&
                    (pattern == pattern.GetElementType()!.MakeArrayType()) == (target == target.GetElementType()!.MakeArrayType()) &&
                    Match(pattern.GetElementType()!, target.GetElementType()!, arguments);
            if (!pattern.IsGenericType)
                return pattern == target;
            if (!target.IsGenericType || pattern.GetGenericTypeDefinition() != target.GetGenericTypeDefinition())
                return false;

            var patterns = pattern.GetGenericArguments();
            var targets = target.GetGenericArguments();
            for (var i = 0; i < patterns.Length; i++)
                if (!Match(patterns[i], targets[i], arguments))
                    return false;
            return true;
        }

        static Type? CloseTemplate(Template template, Type?[] arguments, ref ArgumentException? constraintError)
        {
            var remaining = arguments.Count(argument => argument == null);
            if (remaining == 0)
            {
                try
                {
                    return template.Definition.MakeGenericType(arguments.Select(argument => argument!).ToArray());
                }
                catch (ArgumentException error)
                {
                    // Structurally matching templates can have incompatible generic constraints.
                    constraintError = error;
                    return null;
                }
            }

            for (var i = 0; i < template.Parameters.Length; i++)
            {
                if (arguments[i] is not Type target)
                    continue;
                foreach (var constraint in template.Parameters[i].GetGenericParameterConstraints())
                {
                    foreach (var candidate in SelfAndBases(target).Concat(target.GetInterfaces()))
                    {
                        var inferred = (Type?[])arguments.Clone();
                        if (!Match(constraint, candidate, inferred) ||
                            inferred.Count(argument => argument == null) == remaining)
                            continue;
                        if (CloseTemplate(template, inferred, ref constraintError) is Type closed)
                            return closed;
                    }
                }
            }

            return null;
        }

        static IEnumerable<Type> SelfAndBases(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
                yield return current;
        }

        sealed class Template
        {
            public Template(Type definition, Type[] targets)
                => (Definition, Targets, Parameters) = (definition, targets, definition.GetGenericArguments());

            public Type Definition { get; }
            public Type[] Targets { get; }
            public Type[] Parameters { get; }
        }
    }
}
