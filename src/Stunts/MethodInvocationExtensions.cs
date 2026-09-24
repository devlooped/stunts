using System.ComponentModel;

namespace Stunts
{
    /// <summary>
    /// Usability overloads for working with <see cref="IMethodInvocation"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class MethodInvocationExtensions
    {
        /// <summary>
        /// Registers a behavior in the pipeline that should be skipped during this invocation 
        /// by adding it to the <see cref="IMethodInvocation.SkipBehaviors"/> list.
        /// </summary>
        public static void SkipBehavior<TBehavior>(this IMethodInvocation invocation)
            => invocation.SkipBehaviors.Add(typeof(TBehavior));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn(this IMethodInvocation invocation)
            => invocation.CreateValueReturn(null, invocation.Arguments);

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T>(this IMethodInvocation invocation, T arg)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2>(this IMethodInvocation invocation, T1 arg1, T2 arg2)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2, T3>(this IMethodInvocation invocation, T1 arg1, T2 arg2, T3 arg3)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2, T3, T4>(this IMethodInvocation invocation, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2, T3, T4, T5>(this IMethodInvocation invocation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2, T3, T4, T5, T6>(this IMethodInvocation invocation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2, T3, T4, T5, T6, T7>(this IMethodInvocation invocation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2, T3, T4, T5, T6, T7, T8>(this IMethodInvocation invocation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this IMethodInvocation invocation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this IMethodInvocation invocation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(this IMethodInvocation invocation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(this IMethodInvocation invocation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(this IMethodInvocation invocation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(this IMethodInvocation invocation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(this IMethodInvocation invocation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation.
        /// </summary>
        public static IMethodReturn CreateReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(this IMethodInvocation invocation, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16)
            => invocation.CreateValueReturn(null, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult>(this IMethodInvocation invocation, TResult result)
            => invocation.CreateValueReturn(result, invocation.Arguments);

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T>(this IMethodInvocation invocation, TResult result, T arg)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2, T3>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2, T3 arg3)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2, T3, T4>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2, T3, T4, T5>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2, T3, T4, T5, T6>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2, T3, T4, T5, T6, T7>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2, T3, T4, T5, T6, T7, T8>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15));

        /// <summary>
        /// Creates the method invocation return that ends the current invocation for a non-void method.
        /// </summary>
        public static IMethodReturn CreateValueReturn<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(this IMethodInvocation invocation, TResult result, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16)
            => invocation.CreateValueReturn(result, ArgumentCollection.Create(invocation.MethodBase.GetParameters(), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16));
    }
}
