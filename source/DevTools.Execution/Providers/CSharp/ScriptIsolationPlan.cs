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
        IReadOnlyList<string> nugetPaths,
        IEnumerable<Assembly> hostAssemblies,
        IAssemblyIsolationDiagnosticSink? diagnosticSink = null)
    {
        if (string.IsNullOrWhiteSpace(compiledEntryName))
            throw new ArgumentException(@"A compiled script entry name is required.", nameof(compiledEntryName));
        if (nugetPaths is null) throw new ArgumentNullException(nameof(nugetPaths));
        if (hostAssemblies is null) throw new ArgumentNullException(nameof(hostAssemblies));

        var manifest = new List<AssemblyCandidate>();
        foreach (var path in nugetPaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            TryAddManifestEntry(path, manifest, diagnosticSink);

        var entryPath = Path.Combine(Path.GetTempPath(), "DevTools.Execution", compiledEntryName + ".dll");
        var plan = AssemblyIsolationPlan.Create(entryPath)
            .WithKind(AssemblyIsolationKind.Isolated)
            .AddManagedSource(new ManifestAssemblySource(manifest));

        plan = SharedSidecars.Share(plan, nugetPaths);

        foreach (var assembly in hostAssemblies)
            plan = plan.Share(assembly);

        return diagnosticSink is null ? plan : plan.WithDiagnosticSink(diagnosticSink);
    }

    private static void TryAddManifestEntry(
        string path,
        List<AssemblyCandidate> manifest,
        IAssemblyIsolationDiagnosticSink? diagnosticSink)
    {
        var normalizedPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(normalizedPath);
        if (directory is null)
            return;

        try
        {
            var identity = AssemblyName.GetAssemblyName(normalizedPath);
            if (SharedSidecars.Contains(identity.Name))
                return;

            manifest.Add(new AssemblyCandidate(normalizedPath, directory));
        }
        catch (BadImageFormatException)
        {
            Skip(diagnosticSink, normalizedPath, "is not a managed assembly.");
        }
        catch (IOException)
        {
            Skip(diagnosticSink, normalizedPath, "could not be read.");
        }
        catch (UnauthorizedAccessException)
        {
            Skip(diagnosticSink, normalizedPath, "could not be accessed.");
        }
    }

    private static void Skip(IAssemblyIsolationDiagnosticSink? diagnosticSink, string path, string reason) =>
        diagnosticSink?.Publish(new AssemblyIsolationDiagnostic(
            "script-manifest-entry-skipped",
            $"Selected NuGet path '{path}' {reason}"));
}
