using System.IO;
using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Sources;
#if NET
using System.Runtime.Loader;
#endif

namespace DevTools.Execution.Providers;

/// <summary>
/// Reuses official third-party WPF libraries from the default load context.
/// Loading extra copies of those packages pollutes <c>pack://</c> and theme
/// dictionaries and can conflict styles in a single host process. Skipping
/// private copies is a known isolation gap: the process keeps one identity
/// even when a workload ships a different version. That is acceptable in
/// practice because these libraries are mature and rarely change.
/// DevTools forks use different assembly names and are not shared here.
/// </summary>
static class WpfSharing
{
    static readonly string[] SimpleNames =
    [
        "MahApps.Metro",
        "ControlzEx",
        "Microsoft.Xaml.Behaviors",
    ];

    internal static bool IsShared(string? simpleName)
    {
        if (simpleName is null)
            return false;

        foreach (var name in SimpleNames)
        {
            if (string.Equals(name, simpleName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    internal static IManagedAssemblySource SkipPrivateCopies(IManagedAssemblySource source)
        => new SkippingSource(source);

    internal static IEnumerable<string> SiblingCandidatePaths(string directory)
    {
        foreach (var name in SimpleNames)
            yield return Path.Combine(directory, name + ".dll");
    }

    internal static AssemblyIsolationPlan BindFromDefaultContext(
        AssemblyIsolationPlan plan,
        IEnumerable<string> candidatePaths)
    {
        if (plan is null) throw new ArgumentNullException(nameof(plan));
        if (candidatePaths is null) throw new ArgumentNullException(nameof(candidatePaths));

        var pathsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in candidatePaths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            AssemblyName identity;
            try
            {
                identity = AssemblyName.GetAssemblyName(path);
            }
            catch (BadImageFormatException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            if (!IsShared(identity.Name) || identity.Name is null)
                continue;

            pathsByName[identity.Name] = Path.GetFullPath(path);
        }

        foreach (var simpleName in SimpleNames)
        {
            var loaded = FindInDefaultContext(simpleName);
            if (loaded is null && pathsByName.TryGetValue(simpleName, out var path))
                loaded = LoadIntoDefaultContext(path);
            if (loaded is not null)
                plan = plan.BindToParent(loaded);
        }

        return plan;
    }

    static Assembly? FindInDefaultContext(string simpleName)
    {
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

    static Assembly LoadIntoDefaultContext(string path)
    {
#if NET
        try
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        }
        catch (FileLoadException ex)
        {
            return FindInDefaultContext(Path.GetFileNameWithoutExtension(path))
                ?? throw new FileLoadException(ex.Message, path, ex);
        }
#else
        return Assembly.LoadFrom(path);
#endif
    }

    sealed class SkippingSource(IManagedAssemblySource inner) : IManagedAssemblySource
    {
        public AssemblyCandidate? Resolve(AssemblyName requested)
            => IsShared(requested.Name) ? null : inner.Resolve(requested);
    }
}
