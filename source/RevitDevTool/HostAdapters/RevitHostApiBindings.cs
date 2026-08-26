using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif

namespace RevitDevTool.HostAdapters;

/// <summary>
/// Parent bindings for Revit API assemblies already loaded by the host process.
/// Simple names match <c>props/Revit.props</c> Nice3point compile references.
/// </summary>
internal static class RevitHostApiBindings
{
    internal static readonly string[] SimpleNames =
    [
        "RevitAPI",
        "RevitAPIUI",
        "AdWindows",
        "UIFramework",
        "UIFrameworkServices",
    ];

    internal static IEnumerable<Assembly> GetParentAssemblies()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in Enumerate())
        {
            var name = assembly.GetName().Name;
            if (name is not null && seen.Add(name))
                yield return assembly;
        }
    }

    static IEnumerable<Assembly> Enumerate()
    {
        yield return typeof(IExternalCommand).Assembly;

        var databaseAssembly = typeof(Autodesk.Revit.DB.Element).Assembly;
        if (databaseAssembly != typeof(IExternalCommand).Assembly)
            yield return databaseAssembly;

#if NET
        foreach (var simpleName in SimpleNames)
        {
            var loaded = FindInDefaultContext(simpleName);
            if (loaded is not null)
                yield return loaded;
        }
#endif
    }

#if NET
    static Assembly? FindInDefaultContext(string simpleName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
                continue;
            if (!string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (AssemblyLoadContext.GetLoadContext(assembly) is { } context
                && !ReferenceEquals(context, AssemblyLoadContext.Default))
                continue;

            return assembly;
        }

        return null;
    }
#endif
}
