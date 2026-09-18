using System.Reflection;
using System.IO;
using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.Execution.Providers.Dotnet;

/// <summary>
/// Isolation policy for dynamically discovered commands.
/// </summary>
public static class CommandIsolationPlan
{
    public static AssemblyIsolationPlan Create(
        string entryPath,
        IEnumerable<Assembly> hostAssemblies,
        IAssemblyIsolationDiagnosticSink? diagnosticSink = null)
    {
        ArgumentNullException.ThrowIfNull(hostAssemblies);

        var sourceEntryPath = Path.GetFullPath(entryPath);
        var sourceDirectory = Path.GetDirectoryName(sourceEntryPath)
            ?? throw new ArgumentException(@"The command entry path must have a directory.", nameof(entryPath));

        var loadPath = ResolveLoadPath(sourceEntryPath, hostAssemblies);
        var loadDirectory = Path.GetDirectoryName(loadPath)
            ?? throw new ArgumentException(@"The command entry path must have a directory.", nameof(entryPath));

        var plan = AssemblyIsolationPlan.Create(loadPath)
            .WithKind(AssemblyIsolationKind.Isolated);

#if NETFRAMEWORK
        // LoadFile from a temp copy so rebuilds of the same identity can override.
        plan = plan.WithDistinctFileIdentity();
#endif

#if NET
        plan = plan
            .AddManagedSource(new ResolverAssemblySource(sourceEntryPath))
            .AddManagedSource(new DirectoryAssemblySource(loadDirectory))
            .AddNativeSource(new ResolverNativeAssemblySource(sourceEntryPath));
#else
        plan = plan.AddManagedSource(new DirectoryAssemblySource(loadDirectory));
#endif
        plan = SharedSidecars.ShareFromDirectory(plan, sourceDirectory);
        plan = hostAssemblies.Aggregate(plan, (current, assembly) => current.Share(assembly));
        return diagnosticSink is null ? plan : plan.WithDiagnosticSink(diagnosticSink);
    }

    private static string ResolveLoadPath(string sourceEntryPath, IEnumerable<Assembly> hostAssemblies)
    {
        if (EntryIsShared(sourceEntryPath, hostAssemblies))
            return sourceEntryPath;

#if NETFRAMEWORK
        return CopySiblingDirectory(sourceEntryPath);
#else
        return sourceEntryPath;
#endif
    }

    private static bool EntryIsShared(string entryPath, IEnumerable<Assembly> hostAssemblies)
    {
        var requested = AssemblyName.GetAssemblyName(entryPath);
        return hostAssemblies.Any(assembly =>
            AssemblyIdentityMatcher.IsCompatible(requested, assembly.GetName(), allowVersionDrift: true));
    }

#if NETFRAMEWORK
    private static string CopySiblingDirectory(string entryPath)
    {
        if (!File.Exists(entryPath))
            throw new FileNotFoundException("Command entry assembly not found.", entryPath);

        var sourceDirectory = Path.GetDirectoryName(entryPath)
            ?? throw new ArgumentException("The command entry path must have a directory.", nameof(entryPath));
        var destinationDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            var destination = Path.Combine(destinationDirectory, Path.GetFileName(file));
            using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.Read);
            source.CopyTo(target);
        }

        return Path.Combine(destinationDirectory, Path.GetFileName(entryPath));
    }
#endif
}
