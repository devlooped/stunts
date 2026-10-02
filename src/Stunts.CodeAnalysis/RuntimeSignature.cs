using System.Linq;
using Microsoft.CodeAnalysis;

namespace Stunts.CodeAnalysis;

static class RuntimeSignature
{
    public static ISymbol? UnsupportedMember(INamedTypeSymbol type, Compilation compilation)
    {
        var types = type.TypeKind == TypeKind.Interface
            ? type.AllInterfaces.Insert(0, type)
            : type.AllInterfaces.Add(type);
        foreach (var current in types)
        {
            for (var declaring = current; declaring != null; declaring = declaring.BaseType)
            {
                foreach (var member in declaring.GetMembers())
                {
                    if (member.IsStatic || member.IsSealed ||
                        !(member.IsVirtual || member.IsAbstract || member.IsOverride ||
                            member is IMethodSymbol { MethodKind: MethodKind.Constructor }) ||
                        member.DeclaredAccessibility is Accessibility.Private or Accessibility.ProtectedAndInternal ||
                        member.DeclaredAccessibility == Accessibility.Internal && !declaring.ContainingAssembly.GivesAccessTo(compilation.Assembly))
                        continue;
                    if (member is IMethodSymbol method &&
                        (Unsupported(method.ReturnType) || method.Parameters.Any(parameter => Unsupported(parameter.Type))))
                        return member;
                }
            }
        }

        return null;
    }

    static bool Unsupported(ITypeSymbol type)
        => type.SpecialType is SpecialType.System_TypedReference or SpecialType.System_ArgIterator or SpecialType.System_RuntimeArgumentHandle;
}
