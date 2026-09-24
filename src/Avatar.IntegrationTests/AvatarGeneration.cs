//#define QUICK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using TypeNameFormatter;
using Xunit;

namespace Avatars.AcceptanceTests
{
    public partial class AvatarGeneration
    {
        //[InlineData(typeof(IDisposable))]
        //[Trait("LongRunning", "true")]
        //[MemberData(nameof(GetTargetTypes))]
        //[Theory]
        public void GenerateCode(params Type[] types)
        {
            var code = @"
using System;

namespace Avatars.UnitTests
{
    public class Test
    {
        public void Do()
        {
            var avatar = Avatar.Of<$$>();
            Console.WriteLine(avatar.ToString());
        }
    }
}".Replace("$$", string.Join(", ", types.Select(t =>
                     t.GetFormattedName(TypeNameFormatOptions.Namespaces))));

            var (diagnostics, compilation) = GetGeneratedOutput(code);

            Assert.Empty(diagnostics);

            var assembly = Emit(compilation);

            var name = AvatarNaming.GetFullName(types.First(), types.Skip(1).ToArray());
            var type = assembly.GetType(name, true);
            var avatar = Activator.CreateInstance(type!);

            foreach (var t in types)
            {
                Assert.IsAssignableFrom(t, avatar);
            }
        }

        static IEnumerable<object[]> GetTargetTypes() => Directory.EnumerateFiles(".", "*.dll")
            //.Select(file => Assembly.LoadFrom(file))
            .Select(file => AssemblyName.GetAssemblyName(file))
            //.GetExecutingAssembly().GetReferencedAssemblies()
            .Where(name =>
                !name.Name.StartsWith("Microsoft.CodeAnalysis") &&
                !name.Name.StartsWith("xunit") &&
                !name.Name.StartsWith("Avatar"))
            .Select(name => Assembly.Load(name))
            .SelectMany(TryGetExportedTypes)
            .Where(type => type.IsInterface && !type.IsGenericTypeDefinition && !typeof(Delegate).IsAssignableFrom(type)
                    // Hard-coded exclusions we know don't work
                    && !type.GetCustomAttributesData().Any(d =>
                        d.AttributeType == typeof(ObsoleteAttribute) || // Obsolete types could generate build errors
                        d.AttributeType == typeof(CompilerGeneratedAttribute))
                    && type.Name[0] != '_')  // These are sort of internal too...
#if QUICK
            .Take(2)
#endif
            .Where(x =>
                x.FullName == "Microsoft.DiaSymReader.ISymUnmanagedReader5"
            )
            .Select(type => new object[] { type });

        static Type[] TryGetExportedTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetExportedTypes();
            }
            catch
            {
                return new Type[0];
            }
        }
    }
}