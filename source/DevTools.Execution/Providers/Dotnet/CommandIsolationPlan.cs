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
        IEnumerable<Assembly> parentBindings,
        IAssemblyIsolationDiagnosticSink? diagnosticSink = null)
    {
        if (parentBindings is null) throw new ArgumentNullException(nameof(parentBindings));

        var normalizedEntryPath = Path.GetFullPath(entryPath);
        var siblingDirectory = Path.GetDirectoryName(normalizedEntryPath)
            ?? throw new ArgumentException("The command entry path must have a directory.", nameof(entryPath));

        var plan = AssemblyIsolationPlan.Create(normalizedEntryPath)
            .WithLifecycle(
#if NET
                AssemblyIsolationLifecycle.Collectible
#else
                AssemblyIsolationLifecycle.ScopedNetFramework
#endif
            );

#if NET
        plan = plan
            .AddManagedSource(WpfSharing.SkipPrivateCopies(new DependencyResolverAssemblySource(normalizedEntryPath)))
            .AddManagedSource(WpfSharing.SkipPrivateCopies(new DirectoryAssemblySource(siblingDirectory, "command sibling directory")))
            .AddNativeSource(new DependencyResolverNativeAssemblySource(normalizedEntryPath));
#else
        plan = plan.AddManagedSource(WpfSharing.SkipPrivateCopies(
            new DirectoryAssemblySource(siblingDirectory, "command sibling directory")));
#endif

        plan = WpfSharing.BindFromDefaultContext(
            plan,
            WpfSharing.SiblingCandidatePaths(siblingDirectory));

        foreach (var assembly in parentBindings)
        {
#if NET
            plan = plan.BindToParent(assembly, ignoreRequestedVersion: true);
#else
            plan = plan.BindToParent(assembly);
#endif
        }

        return diagnosticSink is null ? plan : plan.WithDiagnosticSink(diagnosticSink);
    }
}
