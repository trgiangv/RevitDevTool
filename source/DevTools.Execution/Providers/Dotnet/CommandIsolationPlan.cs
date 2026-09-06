using System.Reflection;
using System.IO;
using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.Execution.Providers.Dotnet;

/// <summary>
/// Defines the feature-owned assembly isolation policy for dynamically discovered commands.
/// </summary>
public static class CommandIsolationPlan
{
    public static AssemblyIsolationPlan Create(
        string entryPath,
        IEnumerable<Assembly> hostAssemblies,
        IAssemblyIsolationDiagnosticSink? diagnosticSink = null)
    {
        ArgumentNullException.ThrowIfNull(hostAssemblies);

        var normalizedEntryPath = Path.GetFullPath(entryPath);
        var siblingDirectory = Path.GetDirectoryName(normalizedEntryPath)
            ?? throw new ArgumentException(@"The command entry path must have a directory.", nameof(entryPath));

        var plan = AssemblyIsolationPlan.Create(normalizedEntryPath)
            .WithKind(AssemblyIsolationKind.Isolated);

#if NET
        plan = plan
            .AddManagedSource(new ResolverAssemblySource(normalizedEntryPath))
            .AddManagedSource(new DirectoryAssemblySource(siblingDirectory))
            .AddNativeSource(new ResolverNativeAssemblySource(normalizedEntryPath));
#else
        plan = plan.AddManagedSource(new DirectoryAssemblySource(siblingDirectory));
#endif
        plan = SharedSidecars.ShareFromDirectory(plan, siblingDirectory);
        plan = hostAssemblies.Aggregate(plan, (current, assembly) => current.Share(assembly));
        return diagnosticSink is null ? plan : plan.WithDiagnosticSink(diagnosticSink);
    }
}
