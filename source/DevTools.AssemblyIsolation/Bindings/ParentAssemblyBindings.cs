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

    public static ParentAssemblyBindings Create(IEnumerable<Assembly> assemblies)
    {
        if (assemblies is null) throw new ArgumentNullException(nameof(assemblies));

        var bindings = new Dictionary<string, ParentAssemblyBinding>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in assemblies)
        {
            var binding = new ParentAssemblyBinding(assembly);
            if (bindings.TryGetValue(binding.SimpleName, out var existing))
            {
                if (!HasSameFullIdentity(existing.Identity, binding.Identity))
                    throw new AssemblyIdentityMismatchException(binding.Identity, existing.Identity);

                continue;
            }

            bindings.Add(binding.SimpleName, binding);
        }

        return new ParentAssemblyBindings(bindings);
    }

    public bool TryResolve(AssemblyName requested, out Assembly assembly)
    {
        if (requested is null) throw new ArgumentNullException(nameof(requested));

        if (requested.Name is null || !bindings.TryGetValue(requested.Name, out var binding))
        {
            assembly = null!;
            return false;
        }

        if (!AssemblyIdentityMatcher.IsCompatible(requested, binding.Identity))
            throw new AssemblyIdentityMismatchException(requested, binding.Identity);

        assembly = binding.Assembly;
        return true;
    }

    static bool HasSameFullIdentity(AssemblyName first, AssemblyName second)
        => AssemblyIdentityMatcher.IsCompatible(first, second)
           && AssemblyIdentityMatcher.IsCompatible(second, first);
}
