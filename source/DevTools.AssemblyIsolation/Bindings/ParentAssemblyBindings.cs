using System.Reflection;
using DevTools.AssemblyIsolation.Identity;

namespace DevTools.AssemblyIsolation.Bindings;

public sealed class ParentAssemblyBindings
{
    readonly IReadOnlyDictionary<string, ParentAssemblyBinding> bindings;

    ParentAssemblyBindings(IReadOnlyDictionary<string, ParentAssemblyBinding> bindings)
    {
        this.bindings = bindings;
    }

    public static ParentAssemblyBindings Create(IEnumerable<ParentAssemblyBinding> bindings)
    {
        if (bindings is null) throw new ArgumentNullException(nameof(bindings));

        var map = new Dictionary<string, ParentAssemblyBinding>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in bindings)
        {
            AddBinding(map, binding);
        }

        return new ParentAssemblyBindings(map);
    }

    public static ParentAssemblyBindings Create(IEnumerable<Assembly> assemblies)
    {
        if (assemblies is null) throw new ArgumentNullException(nameof(assemblies));

        return Create(assemblies.Select(assembly => new ParentAssemblyBinding(assembly)));
    }

    public bool TryResolve(AssemblyName requested, out Assembly assembly)
    {
        if (requested is null) throw new ArgumentNullException(nameof(requested));

        if (requested.Name is null || !bindings.TryGetValue(requested.Name, out var binding))
        {
            assembly = null!;
            return false;
        }

        var compatible = binding.IgnoreRequestedVersion
            ? AssemblyIdentityMatcher.IsCompatibleForParentShare(requested, binding.Identity)
            : AssemblyIdentityMatcher.IsCompatible(requested, binding.Identity);
        if (!compatible)
            throw new AssemblyIdentityMismatchException(requested, binding.Identity);

        assembly = binding.Assembly;
        return true;
    }

    static void AddBinding(
        Dictionary<string, ParentAssemblyBinding> bindings,
        ParentAssemblyBinding binding)
    {
        if (bindings.TryGetValue(binding.SimpleName, out var existing))
        {
            if (!HasSameFullIdentity(existing, binding))
                throw new AssemblyIdentityMismatchException(binding.Identity, existing.Identity);

            return;
        }

        bindings.Add(binding.SimpleName, binding);
    }

    static bool HasSameFullIdentity(ParentAssemblyBinding first, ParentAssemblyBinding second)
    {
        if (first.IgnoreRequestedVersion && second.IgnoreRequestedVersion)
        {
            return AssemblyIdentityMatcher.IsCompatibleForParentShare(first.Identity, second.Identity)
                   && AssemblyIdentityMatcher.IsCompatibleForParentShare(second.Identity, first.Identity);
        }

        return AssemblyIdentityMatcher.IsCompatible(first.Identity, second.Identity)
               && AssemblyIdentityMatcher.IsCompatible(second.Identity, first.Identity);
    }
}
