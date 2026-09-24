using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stunts;

namespace Samples
{
    /// <summary>
    /// Forwards calls whose signature match to static methods on 
    /// another type.
    /// </summary>
    public class InterfaceStaticAdapterBehavior : IStuntBehavior
    {
        readonly Type targetType;
        readonly Dictionary<int, MethodInfo> targetMethods;

        public InterfaceStaticAdapterBehavior(Type targetType)
        {
            this.targetType = targetType;
            targetMethods = targetType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToDictionary(GetHashCode);
        }

        public bool AppliesTo(IMethodInvocation invocation) => targetMethods.ContainsKey(GetHashCode(invocation.MethodBase as MethodInfo));

        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            var method = targetMethods[GetHashCode(invocation.MethodBase as MethodInfo)];
            var arguments = invocation.Arguments.ToArray();

            try
            {
                var result = method.Invoke(null, arguments);
                return invocation.CreateValueReturn(result, arguments);
            }
            catch (TargetInvocationException tie)
            {
                return invocation.CreateExceptionReturn(tie.InnerException!);
            }
        }

        int GetHashCode(MethodInfo? method)
        {
            if (method == null)
                return 0;

            var hash = new HashCode();
            hash.Add(method.ReturnType);
            hash.Add(method.Name);

            foreach (var type in method.GetGenericArguments())
            {
                hash.Add(type);
            }

            foreach (var parameter in method.GetParameters())
            {
                hash.Add(parameter.ParameterType);
            }

            return hash.ToHashCode();
        }
    }
}
