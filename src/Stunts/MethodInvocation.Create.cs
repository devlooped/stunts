using System.Reflection;

namespace Stunts
{
    partial class MethodInvocation
    {
        #region Create(object, MethodBase, args)

        /// <summary>
        /// Creates a method invocation that has no arguments.
        /// </summary>
        /// <param name="target">The target of the invocation.</param>
        /// <param name="method">The method being invoked.</param>
        public static MethodInvocation Create(object target, MethodBase method) => new MethodInvocation(target, method);

        /// <summary>
        /// Creates a method invocation with the given typed argument value.
        /// </summary>
        /// <param name="target">The target of the invocation.</param>
        /// <param name="method">The method being invoked.</param>
        /// <param name="arg">The argument value</param>
        public static MethodInvocation Create<T>(object target, MethodBase method, T arg)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2>(object target, MethodBase method, T1 arg1, T2 arg2)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3>(object target, MethodBase method, T1 arg1, T2 arg2, T3 arg3)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4>(object target, MethodBase method, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5>(object target, MethodBase method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6>(object target, MethodBase method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7>(object target, MethodBase method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8>(object target, MethodBase method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9>(object target, MethodBase method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(object target, MethodBase method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(object target, MethodBase method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(object target, MethodBase method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(object target, MethodBase method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(object target, MethodBase method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(object target, MethodBase method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(object target, MethodBase method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16)
            => new MethodInvocation(target, method, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16));

        #endregion

        #region Create(object, MethodBase, ExecuteDelegate, args)

        /// <summary>
        /// Creates a method invocation that has no arguments.
        /// </summary>
        /// <param name="target">The target of the invocation.</param>
        /// <param name="method">The method being invoked.</param>
        /// <param name="implementation">Delegate to invoke the base method implementation for virtual methods.</param>
        public static MethodInvocation Create(object target, MethodBase method, ExecuteHandler implementation) => new MethodInvocation(target, method, implementation, new ArgumentCollection());

        /// <summary>
        /// Creates a method invocation with the given typed argument value.
        /// </summary>
        /// <param name="target">The target of the invocation.</param>
        /// <param name="method">The method being invoked.</param>
        /// <param name="implementation">Delegate to invoke the base method implementation for virtual methods.</param>
        /// <param name="arg">The argument value.</param>
        public static MethodInvocation Create<T>(object target, MethodBase method, ExecuteHandler implementation, T arg)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2, T3 arg3)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15));

        /// <summary>
        /// Creates a method invocation with the given typed argument values.
        /// </summary>
        public static MethodInvocation Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(object target, MethodBase method, ExecuteHandler implementation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16)
            => new MethodInvocation(target, method, implementation, ArgumentCollection.Create(method.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16));

        #endregion
    }
}