using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif

namespace DevTools.AssemblyIsolation;

/// <summary>
/// Finds assemblies already in the default load context. Never loads from disk.
/// Host adapters Share those instances so version can be forgiven for that
/// identity only.
/// </summary>
public static class AssemblyHelper
{
    public static Assembly? Find(string simpleName)
    {
        if (string.IsNullOrWhiteSpace(simpleName))
            return null;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
                continue;
            if (!string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                continue;
#if NET
            if (AssemblyLoadContext.GetLoadContext(assembly) is { } context
                && !ReferenceEquals(context, AssemblyLoadContext.Default))
                continue;
#endif
            return assembly;
        }

        return null;
    }

    public static IEnumerable<Assembly> FindMany(IEnumerable<string> simpleNames)
    {
        if (simpleNames is null) throw new ArgumentNullException(nameof(simpleNames));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in simpleNames)
        {
            var loaded = Find(name);
            var simple = loaded?.GetName().Name;
            if (simple is null || !seen.Add(simple))
                continue;
            yield return loaded!;
        }
    }

    public static Assembly[] CaptureHostAssemblies(IEnumerable<Assembly> byType, IReadOnlyList<string> byName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assemblies = new List<Assembly>();
        foreach (var assembly in byType.Concat(FindMany(byName)))
        {
            var name = assembly.GetName().Name;
            if (name is null || !seen.Add(name))
                continue;
            assemblies.Add(assembly);
        }

        return assemblies.ToArray();
    }
}
