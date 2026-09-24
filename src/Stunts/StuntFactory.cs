using System;
using System.Reflection;
using System.Threading;

namespace Stunts
{
    /// <summary>
    /// Allows accessing the default <see cref="IStuntFactory"/> to use to 
    /// create stunts.
    /// </summary>
    public class StuntFactory : IStuntFactory
    {
        static readonly AsyncLocal<IStuntFactory?> localFactory = new();
        static readonly IStuntFactory nullFactory = new StuntFactory();
        static IStuntFactory defaultFactory = nullFactory;

        /// <summary>
        /// Gets or sets the default <see cref="IStuntFactory"/> to use 
        /// to create stunts. Defaults to the <see cref="NotImplemented"/> factory.
        /// </summary>
        /// <remarks>
        /// A <see cref="LocalDefault"/> can override the value of this global 
        /// default, if assigned to a non-null value.
        /// </remarks>
        public static IStuntFactory Default
        {
            get => localFactory.Value ?? defaultFactory;
            set => defaultFactory = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="IStuntFactory"/> to use 
        /// in the current (async) flow, so it does not affect other threads/flows.
        /// </summary>
        public static IStuntFactory? LocalDefault
        {
            get => localFactory.Value;
            set => localFactory.Value = value;
        }

        /// <summary>
        /// A factory that throws <see cref="NotImplementedException"/>.
        /// </summary>
        public static IStuntFactory NotImplemented { get; } = nullFactory;

        StuntFactory() { }

        /// <summary>
        /// See <see cref="IStuntFactory.CreateStunt(Assembly, Type, Type[], object[])"/>
        /// </summary>
        public object CreateStunt(Assembly assembly, Type baseType, Type[] implementedInterfaces, object?[] constructorArguments)
            => throw new NotImplementedException(ThisAssembly.Strings.StuntFactoryNotImplemented);
    }
}