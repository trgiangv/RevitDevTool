using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation;

/// <summary>
/// Reuses official UI sidecar identities from the default load context.
/// Extra copies pollute pack:// maps and theme dictionaries. Skipping private
/// copies is a known isolation gap: one process-wide identity even when a
/// workload ships another version. Forked identities stay private.
/// </summary>
public static class SharedSidecars
{
    private static readonly string[] Names =
    [
        "MahApps.Metro",
        "ControlzEx",
        "Microsoft.Xaml.Behaviors",
        "FSharp.Core",
    ];

    public static bool Contains(string? simpleName) =>
        simpleName is not null
        && Names.Any(name => string.Equals(name, simpleName, StringComparison.OrdinalIgnoreCase));

    public static AssemblyIsolationPlan ShareFromDirectory(AssemblyIsolationPlan plan, string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A directory is required.", nameof(directory));

        return Share(plan, Names.Select(name => AssemblyCandidate.Combine(directory, name)));
    }

    public static AssemblyIsolationPlan Share(AssemblyIsolationPlan plan, IEnumerable<string> candidatePaths)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(candidatePaths);

        var pathsByName = IndexExisting(candidatePaths);
        foreach (var simpleName in Names)
        {
            var loaded = AssemblyHelper.Find(simpleName) ?? LoadIfPresent(pathsByName, simpleName);
            if (loaded is not null)
                plan = plan.Share(loaded);
        }

        return plan;
    }

    private static Dictionary<string, string> IndexExisting(IEnumerable<string> candidatePaths)
    {
        var pathsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in candidatePaths)
        {
            if (!TryReadSharedName(path, out var name))
                continue;

            pathsByName[name] = Path.GetFullPath(path);
        }

        return pathsByName;
    }

    private static bool TryReadSharedName(string path, out string name)
    {
        name = null!;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        AssemblyName identity;
        try
        {
            identity = AssemblyName.GetAssemblyName(path);
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }

        if (identity.Name is null || !Contains(identity.Name))
            return false;

        name = identity.Name;
        return true;
    }

    private static Assembly? LoadIfPresent(Dictionary<string, string> pathsByName, string simpleName) =>
        pathsByName.TryGetValue(simpleName, out var path) ? LoadIntoDefaultContext(path) : null;

    private static Assembly LoadIntoDefaultContext(string path)
    {
#if NET
        try
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        }
        catch (FileLoadException ex)
        {
            return AssemblyHelper.Find(Path.GetFileNameWithoutExtension(path))
                ?? throw new FileLoadException(ex.Message, path, ex);
        }
#else
        return Assembly.LoadFrom(path);
#endif
    }
}
