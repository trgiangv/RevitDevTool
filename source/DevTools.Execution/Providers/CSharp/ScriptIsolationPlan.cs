using System.Reflection;
using System.IO;
using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.Execution.Providers.CSharp;

/// <summary>
/// Defines the feature-owned assembly isolation policy for compiled C# scripts.
/// </summary>
public static class ScriptIsolationPlan
{
    public static AssemblyIsolationPlan Create(
        string compiledEntryName,
        IEnumerable<string> nugetPaths,
        IEnumerable<Assembly> parentBindings,
        IAssemblyIsolationDiagnosticSink? diagnosticSink = null)
    {
        if (string.IsNullOrWhiteSpace(compiledEntryName))
            throw new ArgumentException("A compiled script entry name is required.", nameof(compiledEntryName));
        if (nugetPaths is null) throw new ArgumentNullException(nameof(nugetPaths));
        if (parentBindings is null) throw new ArgumentNullException(nameof(parentBindings));

        var manifest = new List<AssemblyCandidate>();
        foreach (var path in nugetPaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var normalizedPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(normalizedPath);
            if (directory is null)
                continue;

            try
            {
                _ = AssemblyName.GetAssemblyName(normalizedPath);
                manifest.Add(new AssemblyCandidate(normalizedPath, "selected NuGet assembly", directory));
            }
            catch (BadImageFormatException)
            {
                diagnosticSink?.Publish(new AssemblyIsolationDiagnostic(
                    "script-manifest-entry-skipped",
                    $"Selected NuGet path '{normalizedPath}' is not a managed assembly."));
            }
            catch (IOException)
            {
                diagnosticSink?.Publish(new AssemblyIsolationDiagnostic(
                    "script-manifest-entry-skipped",
                    $"Selected NuGet path '{normalizedPath}' could not be read."));
            }
            catch (UnauthorizedAccessException)
            {
                diagnosticSink?.Publish(new AssemblyIsolationDiagnostic(
                    "script-manifest-entry-skipped",
                    $"Selected NuGet path '{normalizedPath}' could not be accessed."));
            }
        }

        var entryPath = Path.Combine(Path.GetTempPath(), "DevTools.Execution", compiledEntryName + ".dll");
        var plan = AssemblyIsolationPlan.Create(entryPath)
            .WithLifecycle(AssemblyIsolationLifecycle.Collectible)
            .AddManagedSource(new ManifestAssemblySource(manifest));

        foreach (var assembly in parentBindings)
            plan = plan.BindToParent(assembly);

        return diagnosticSink is null ? plan : plan.WithDiagnosticSink(diagnosticSink);
    }
}
