using System.Linq;
using Microsoft.CodeAnalysis;

namespace Stunts.CodeAnalysis;

static class InterfaceImplementation
{
    public static ISymbol? InaccessibleMember(INamedTypeSymbol type, Compilation compilation)
    {
        var interfaces = type.TypeKind == TypeKind.Interface
            ? type.AllInterfaces.Insert(0, type)
            : type.AllInterfaces;
        foreach (var iface in interfaces)
        {
            foreach (var member in iface.GetMembers())
            {
                if (member.IsStatic || !member.IsAbstract)
                    continue;
                var implementation = type.FindImplementationForInterfaceMember(member);
                if (implementation != null && !implementation.IsAbstract)
                    continue;

                var inaccessible = member switch
                {
                    IPropertySymbol property => new[] { property.GetMethod, property.SetMethod }
                        .FirstOrDefault(accessor => accessor != null && accessor.IsAbstract &&
                            !compilation.IsSymbolAccessibleWithin(accessor, compilation.Assembly)),
                    IEventSymbol ev => new[] { ev.AddMethod, ev.RemoveMethod }
                        .FirstOrDefault(accessor => accessor != null && accessor.IsAbstract &&
                            !compilation.IsSymbolAccessibleWithin(accessor, compilation.Assembly)),
                    IMethodSymbol method when method.MethodKind == MethodKind.Ordinary &&
                        !compilation.IsSymbolAccessibleWithin(method, compilation.Assembly) => method,
                    _ => null,
                };
                if (inaccessible != null)
                    return inaccessible;
            }
        }

        return null;
    }
}
