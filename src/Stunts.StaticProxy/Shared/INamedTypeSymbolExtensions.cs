using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.CodeAnalysis.Shared.Extensions;

/// <summary>
/// The overridable-member query Stunt used to reflect out of the Workspaces assembly.
/// Same namespace and name, implemented with the public symbol API only.
/// </summary>
public static class INamedTypeSymbolExtensions
{
    /// <summary>
    /// Overridable members from <see cref="object"/> toward <paramref name="containingType"/>,
    /// skipping members that type already overrides. Furthest base first.
    /// </summary>
    public static ImmutableArray<ISymbol> GetOverridableMembers(this INamedTypeSymbol containingType, System.Threading.CancellationToken cancellationToken = default)
    {
        if (containingType.TypeKind != TypeKind.Class && containingType.TypeKind != TypeKind.Struct || containingType.IsStatic)
            return ImmutableArray<ISymbol>.Empty;

        var ordered = new List<ISymbol>();
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var bases = BaseTypes(containingType);
        for (var i = bases.Count - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var type = bases[i];
            RemoveOverridden(ordered, seen, type);
            foreach (var member in type.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsOverridable(member) || !seen.Add(member))
                    continue;
                ordered.Add(member);
            }
        }

        RemoveOverridden(ordered, seen, containingType);
        RemoveCollisions(ordered, seen, containingType);
        return ordered.ToImmutableArray();
    }

    /// <summary>
    /// Interface members the type has not implemented yet. This is the other half of
    /// what the Implement Interface code fix used to discover.
    /// </summary>
    public static ImmutableArray<ISymbol> GetUnimplementedInterfaceMembers(this INamedTypeSymbol containingType, System.Threading.CancellationToken cancellationToken = default)
    {
        var result = new List<ISymbol>();
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        // Most-derived interfaces first so a colliding member (GetEnumerator) keeps
        // the specific signature public and the base one can be explicit.
        var interfaces = containingType.AllInterfaces.OrderByDescending(iface => iface.AllInterfaces.Length);
        foreach (var iface in interfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var member in iface.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (member.IsStatic || member.IsImplicitlyDeclared || IsExplicitImplementation(member))
                    continue;
                if (member is IMethodSymbol method && method.MethodKind != MethodKind.Ordinary)
                    continue;
                if (containingType.FindImplementationForInterfaceMember(member) != null)
                    continue;
                if (seen.Add(member))
                    result.Add(member);
            }
        }

        return result.ToImmutableArray();
    }

    /// <summary>
    /// Interface members whose implementation is a default provided by an interface, 
    /// together with the interface that provides it (the member's own interface, or 
    /// a derived one that supplies a more specific default).
    /// </summary>
    /// <remarks>
    /// Only interfaces the type declares (directly or through interface inheritance) are 
    /// considered. Interfaces inherited from a base class keep that class' mapping, since 
    /// a member declared in the derived type does not re-implement them.
    /// </remarks>
    public static ImmutableArray<(ISymbol Member, INamedTypeSymbol Provider)> GetDefaultImplementedInterfaceMembers(this INamedTypeSymbol containingType, System.Threading.CancellationToken cancellationToken = default)
    {
        var result = new List<(ISymbol, INamedTypeSymbol)>();
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var interfaces = containingType.Interfaces
            .SelectMany(iface => new[] { iface }.Concat(iface.AllInterfaces))
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .OrderByDescending(iface => iface.AllInterfaces.Length);

        foreach (var iface in interfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var member in iface.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (member.IsStatic || member.IsImplicitlyDeclared || member.DeclaredAccessibility != Accessibility.Public)
                    continue;
                // Sealed interface members cannot be implemented.
                if (!member.IsVirtual && !member.IsAbstract)
                    continue;
                if (member is IMethodSymbol method && method.MethodKind != MethodKind.Ordinary)
                    continue;
                if (containingType.FindImplementationForInterfaceMember(member) is not { ContainingType: { TypeKind: TypeKind.Interface } provider })
                    continue;
                if (seen.Add(member))
                    result.Add((member, provider));
            }
        }

        return result.ToImmutableArray();
    }

    // A derived interface providing a default for a base interface member (int IFoo.Value => 6).
    static bool IsExplicitImplementation(ISymbol member) => member switch
    {
        IMethodSymbol method => method.ExplicitInterfaceImplementations.Length > 0,
        IPropertySymbol property => property.ExplicitInterfaceImplementations.Length > 0,
        IEventSymbol ev => ev.ExplicitInterfaceImplementations.Length > 0,
        _ => false,
    };

    static bool IsOverridable(ISymbol member)
    {
        if (member.IsStatic || member.IsSealed)
            return false;
        if (!member.IsAbstract && !member.IsVirtual && !member.IsOverride)
            return false;
        if (member.DeclaredAccessibility != Accessibility.Public &&
            member.DeclaredAccessibility != Accessibility.Protected &&
            member.DeclaredAccessibility != Accessibility.ProtectedOrInternal)
            return false;

        if (member is IMethodSymbol method)
            return method.MethodKind == MethodKind.Ordinary && method.CanBeReferencedByName;
        return member is IPropertySymbol || member is IEventSymbol;
    }

    static void RemoveOverridden(List<ISymbol> ordered, HashSet<ISymbol> seen, INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            var overridden = Overridden(member);
            if (overridden != null && seen.Remove(overridden))
                ordered.Remove(overridden);
        }
    }

    static void RemoveCollisions(List<ISymbol> ordered, HashSet<ISymbol> seen, INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member.IsImplicitlyDeclared)
                continue;
            foreach (var candidate in ordered.ToArray())
            {
                if (SameSignature(member, candidate) && seen.Remove(candidate))
                    ordered.Remove(candidate);
            }
        }
    }

    static bool SameSignature(ISymbol left, ISymbol right)
    {
        if (left.Name != right.Name || left.Kind != right.Kind)
            return false;
        if (left is not IMethodSymbol leftMethod || right is not IMethodSymbol rightMethod)
            return true;
        if (leftMethod.Parameters.Length != rightMethod.Parameters.Length || leftMethod.TypeParameters.Length != rightMethod.TypeParameters.Length)
            return false;
        for (var i = 0; i < leftMethod.Parameters.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(leftMethod.Parameters[i].Type, rightMethod.Parameters[i].Type))
                return false;
        }

        return true;
    }

    static ISymbol? Overridden(ISymbol member)
    {
        if (member is IMethodSymbol method)
            return method.OverriddenMethod;
        if (member is IPropertySymbol property)
            return property.OverriddenProperty;
        if (member is IEventSymbol ev)
            return ev.OverriddenEvent;
        return null;
    }

    static List<INamedTypeSymbol> BaseTypes(INamedTypeSymbol type)
    {
        var chain = new List<INamedTypeSymbol>();
        for (var current = type.BaseType; current != null; current = current.BaseType)
            chain.Add(current);
        return chain;
    }
}
