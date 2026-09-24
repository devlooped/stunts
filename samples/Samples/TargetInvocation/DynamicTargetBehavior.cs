using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Stunts;
using Microsoft.CSharp.RuntimeBinder;

namespace Samples.TargetInvocation
{
    public class DynamicTargetBehavior : IStuntBehavior
    {
        readonly ConcurrentDictionary<MethodBase, Func<IMethodInvocation, IMethodReturn>> invokers = new();
        readonly HashSet<MethodBase> unsupported = new();
        readonly object target;

        public DynamicTargetBehavior(dynamic target) => this.target = target;

        public bool AppliesTo(IMethodInvocation invocation)
            => !unsupported.Contains(invocation.MethodBase) &&
               // NOTE: doing the proper binding for ref/out arguments is quite complicated 
               // with the dynamic approach, since we would need to generate variables to keep the 
               // out/ref before/after the call. The generated IL from a dynamic call for that 
               // scenario is significantly more complicated than for "normal" method calls.
               !invocation.MethodBase.GetParameters().Any(p => p.IsOut || p.ParameterType.IsByRef);

        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            var invoker = invokers.GetOrAdd(invocation.MethodBase, method => GetInvoker(method));

            try
            {
                return invoker(invocation);
            }
            catch (RuntimeBinderException)
            {
                unsupported.Add(invocation.MethodBase);
                return next.Invoke(invocation, next);
            }
        }

        Func<IMethodInvocation, IMethodReturn> GetInvoker(MethodBase method)
        {
            var parameters = method.GetParameters();
            var binder = Microsoft.CSharp.RuntimeBinder.Binder.InvokeMember(
                (method is MethodInfo mi && mi.ReturnType == typeof(void)) ? CSharpBinderFlags.ResultDiscarded : CSharpBinderFlags.None,
                method.Name,
                method.IsGenericMethod ? method.GetGenericArguments() : Array.Empty<Type>(),
                GetType(),
                new[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) }.Concat(
                    method.GetParameters().Select(p => CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, p.Name))));

            if (method is MethodInfo info && info.ReturnType == typeof(void))
            {
                var delegateType = Type.GetType("System.Action`" + (parameters.Length + 2), true)!
                    .MakeGenericType(
                        new[] { typeof(CallSite), typeof(object) }
                        .Concat(parameters.Select(p => p.ParameterType))
                        .ToArray());

                var site = typeof(CallSite<>).MakeGenericType(delegateType)
                    .InvokeMember("Create", BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod, null, null, new[] { binder });

                var target = (Delegate)site?.GetType().GetField("Target")?.GetValue(site)!;

                return invocation =>
                {
                    var args = invocation.Arguments.Select(x => x.RawValue).ToArray();
                    try
                    {
                        target.DynamicInvoke(new[] { site, this.target }.Concat(args).ToArray());
                        return invocation.CreateReturn();
                    }
                    catch (TargetInvocationException tie)
                    {
                        return invocation.CreateExceptionReturn(tie.InnerException!);
                    }
                };
            }
            else
            {
                var delegateType = Type.GetType("System.Func`" + (parameters.Length + 3), true)!
                    .MakeGenericType(
                        new[] { typeof(CallSite), typeof(object) }
                        .Concat(parameters.Select(p => p.ParameterType))
                        .Concat(new[] { typeof(object) })
                        .ToArray());

                var site = typeof(CallSite<>).MakeGenericType(delegateType)
                    .InvokeMember("Create", BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod, null, null, new[] { binder });

                var target = (Delegate)site?.GetType().GetField("Target")?.GetValue(site)!;

                return invocation =>
                {
                    var args = invocation.Arguments.Select(x => x.RawValue).ToArray();
                    try
                    {
                        var result = target.DynamicInvoke(new[] { site, this.target }.Concat(args).ToArray());
                        return invocation.CreateValueReturn(result);
                    }
                    catch (TargetInvocationException tie)
                    {
                        return invocation.CreateExceptionReturn(tie.InnerException!);
                    }
                };
            }
        }
    }
}
