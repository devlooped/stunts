using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Stunts
{
    /// <summary>
    /// Provides a <see cref="IStuntFactory"/> that creates proxies from types 
    /// generated at compile-time that are included in the received stunt 
    /// assembly in <see cref="CreateStunt"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class StaticStuntFactory : IStuntFactory
    {
        /// <summary>
        /// Uses the <see cref="StuntNaming.GetFullName(Type, Type[])"/> method to 
        /// determine the expected full type name of a compile-time generated stunt 
        /// and tries to locate it from <paramref name="assembly"/>.
        /// </summary>
        /// <param name="assembly">The assembly containing the compile-time generated stunts.</param>
        /// <param name="baseType">Base type of the stunt.</param>
        /// <param name="implementedInterfaces">Additional interfaces the stunt implements.</param>
        /// <param name="constructorArguments">Optional additional constructor arguments for the stunt.</param>
        public object CreateStunt(Assembly assembly, Type baseType, Type[] implementedInterfaces, object?[] constructorArguments)
        {
            var name = StuntNaming.GetFullName(baseType, implementedInterfaces);
            var type = assembly.GetType(name, false, false);
            if (type == null)
                throw new ArgumentException(ThisAssembly.Strings.StaticStuntTypeNotFoundInAssembly(name, assembly.GetName().Name), nameof(assembly));

            try
            {
                return Activator.CreateInstance(type, constructorArguments);
            }
            catch (TargetInvocationException tie)
            {
                var ex = ExceptionDispatchInfo.Capture(tie.GetBaseException());
                ex.Throw();
            }

            // Code will never reach this.
            throw new NotImplementedException();
        }
    }
}
