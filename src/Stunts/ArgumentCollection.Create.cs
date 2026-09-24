using System.Reflection;

namespace Stunts
{
    partial class ArgumentCollection
    {
        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T>(ParameterInfo[] parameters, T arg)
            => parameters.Length != 1 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2>(ParameterInfo[] parameters, T1 arg1, T2 arg2)
            => parameters.Length != 2 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2, T3>(ParameterInfo[] parameters, T1 arg1, T2 arg2, T3 arg3)
            => parameters.Length != 3 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2),
                Argument.Create(parameters[2], arg3));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2, T3, T4>(ParameterInfo[] parameters, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => parameters.Length != 4 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2),
                Argument.Create(parameters[2], arg3),
                Argument.Create(parameters[3], arg4));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2, T3, T4, T5>(ParameterInfo[] parameters, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => parameters.Length != 5 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2),
                Argument.Create(parameters[2], arg3),
                Argument.Create(parameters[3], arg4),
                Argument.Create(parameters[4], arg5));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2, T3, T4, T5, T6>(ParameterInfo[] parameters, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            => parameters.Length != 6 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2),
                Argument.Create(parameters[2], arg3),
                Argument.Create(parameters[3], arg4),
                Argument.Create(parameters[4], arg5),
                Argument.Create(parameters[5], arg6));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2, T3, T4, T5, T6, T7>(ParameterInfo[] parameters, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            => parameters.Length != 7 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2),
                Argument.Create(parameters[2], arg3),
                Argument.Create(parameters[3], arg4),
                Argument.Create(parameters[4], arg5),
                Argument.Create(parameters[5], arg6),
                Argument.Create(parameters[6], arg7));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2, T3, T4, T5, T6, T7, T8>(ParameterInfo[] parameters, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            => parameters.Length != 8 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2),
                Argument.Create(parameters[2], arg3),
                Argument.Create(parameters[3], arg4),
                Argument.Create(parameters[4], arg5),
                Argument.Create(parameters[5], arg6),
                Argument.Create(parameters[6], arg7),
                Argument.Create(parameters[7], arg8));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ParameterInfo[] parameters, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            => parameters.Length != 9 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2),
                Argument.Create(parameters[2], arg3),
                Argument.Create(parameters[3], arg4),
                Argument.Create(parameters[4], arg5),
                Argument.Create(parameters[5], arg6),
                Argument.Create(parameters[6], arg7),
                Argument.Create(parameters[7], arg8),
                Argument.Create(parameters[8], arg9));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ParameterInfo[] parameters, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
            => parameters.Length != 10 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2),
                Argument.Create(parameters[2], arg3),
                Argument.Create(parameters[3], arg4),
                Argument.Create(parameters[4], arg5),
                Argument.Create(parameters[5], arg6),
                Argument.Create(parameters[6], arg7),
                Argument.Create(parameters[7], arg8),
                Argument.Create(parameters[8], arg9),
                Argument.Create(parameters[9], arg10));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ParameterInfo[] parameters, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11)
            => parameters.Length != 11 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2),
                Argument.Create(parameters[2], arg3),
                Argument.Create(parameters[3], arg4),
                Argument.Create(parameters[4], arg5),
                Argument.Create(parameters[5], arg6),
                Argument.Create(parameters[6], arg7),
                Argument.Create(parameters[7], arg8),
                Argument.Create(parameters[8], arg9),
                Argument.Create(parameters[9], arg10),
                Argument.Create(parameters[10], arg11));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ParameterInfo[] parameters, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12)
            => parameters.Length != 12 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2),
                Argument.Create(parameters[2], arg3),
                Argument.Create(parameters[3], arg4),
                Argument.Create(parameters[4], arg5),
                Argument.Create(parameters[5], arg6),
                Argument.Create(parameters[6], arg7),
                Argument.Create(parameters[7], arg8),
                Argument.Create(parameters[8], arg9),
                Argument.Create(parameters[9], arg10),
                Argument.Create(parameters[10], arg11),
                Argument.Create(parameters[11], arg12));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ParameterInfo[] parameters, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13)
            => parameters.Length != 13 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2),
                Argument.Create(parameters[2], arg3),
                Argument.Create(parameters[3], arg4),
                Argument.Create(parameters[4], arg5),
                Argument.Create(parameters[5], arg6),
                Argument.Create(parameters[6], arg7),
                Argument.Create(parameters[7], arg8),
                Argument.Create(parameters[8], arg9),
                Argument.Create(parameters[9], arg10),
                Argument.Create(parameters[10], arg11),
                Argument.Create(parameters[11], arg12),
                Argument.Create(parameters[12], arg13));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ParameterInfo[] parameters, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14)
            => parameters.Length != 14 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2),
                Argument.Create(parameters[2], arg3),
                Argument.Create(parameters[3], arg4),
                Argument.Create(parameters[4], arg5),
                Argument.Create(parameters[5], arg6),
                Argument.Create(parameters[6], arg7),
                Argument.Create(parameters[7], arg8),
                Argument.Create(parameters[8], arg9),
                Argument.Create(parameters[9], arg10),
                Argument.Create(parameters[10], arg11),
                Argument.Create(parameters[11], arg12),
                Argument.Create(parameters[12], arg13),
                Argument.Create(parameters[13], arg14));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ParameterInfo[] parameters, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15)
            => parameters.Length != 15 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2),
                Argument.Create(parameters[2], arg3),
                Argument.Create(parameters[3], arg4),
                Argument.Create(parameters[4], arg5),
                Argument.Create(parameters[5], arg6),
                Argument.Create(parameters[6], arg7),
                Argument.Create(parameters[7], arg8),
                Argument.Create(parameters[8], arg9),
                Argument.Create(parameters[9], arg10),
                Argument.Create(parameters[10], arg11),
                Argument.Create(parameters[11], arg12),
                Argument.Create(parameters[12], arg13),
                Argument.Create(parameters[13], arg14),
                Argument.Create(parameters[14], arg15));

        /// <summary>
        /// Creates an argument collection with the given parameter(s) and argument value(s).
        /// </summary>
        /// <exception cref="TargetParameterCountException">The <paramref name="parameters"/> does not contain 
        /// the same number of parameters for the given argument(s).</exception>
        public static ArgumentCollection Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(ParameterInfo[] parameters, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16)
            => parameters.Length != 16 ? throw new TargetParameterCountException() : new ArgumentCollection(
                Argument.Create(parameters[0], arg1),
                Argument.Create(parameters[1], arg2),
                Argument.Create(parameters[2], arg3),
                Argument.Create(parameters[3], arg4),
                Argument.Create(parameters[4], arg5),
                Argument.Create(parameters[5], arg6),
                Argument.Create(parameters[6], arg7),
                Argument.Create(parameters[7], arg8),
                Argument.Create(parameters[8], arg9),
                Argument.Create(parameters[9], arg10),
                Argument.Create(parameters[10], arg11),
                Argument.Create(parameters[11], arg12),
                Argument.Create(parameters[12], arg13),
                Argument.Create(parameters[13], arg14),
                Argument.Create(parameters[14], arg15),
                Argument.Create(parameters[15], arg16));
    }
}
