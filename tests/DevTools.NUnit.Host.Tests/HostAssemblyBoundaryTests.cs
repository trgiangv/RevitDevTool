using DevTools.NUnit.Host;

namespace DevTools.NUnit.Host.Tests;

public sealed class HostAssemblyBoundaryTests
{
    [Fact]
    public void Host_references_only_testing_and_isolation_infrastructure()
    {
        var csproj = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "source",
            "DevTools.NUnit.Host",
            "DevTools.NUnit.Host.csproj"));
        Assert.DoesNotContain("DevTools.Logging.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.Hosting.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.Execution.Abstractions.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.Ipc.csproj", csproj, StringComparison.Ordinal);

        var loadingDirectory = Path.Combine(FindRepositoryRoot(), "source", "DevTools.NUnit.Host");
        string[] forbiddenHostApiNames =
        [
            "RevitDBAPI",
            "RevitAPI.dll",
            "RevitAPIUI",
            "accoremgd",
            "Acdbmgd",
            "acmgd.dll",
        ];
        var hostApiNameHits = Directory
            .GetFiles(loadingDirectory, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => forbiddenHostApiNames
                .Where(name => File.ReadAllText(path).Contains(name, StringComparison.OrdinalIgnoreCase))
                .Select(name => $"{Path.GetRelativePath(FindRepositoryRoot(), path)} contains {name}"))
            .ToList();
        Assert.True(hostApiNameHits.Count == 0, string.Join(Environment.NewLine, hostApiNameHits));

        var references = typeof(NUnitHostTestFrameworkProvider).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("DevTools.Hosting", references);
        Assert.DoesNotContain("DevTools.Presentation", references);
        Assert.DoesNotContain("DevTools.UI", references);
        Assert.DoesNotContain("ZLogger.Scintilla", references);
        Assert.DoesNotContain("PresentationFramework", references);
    }

    [Fact]
    public void Host_uses_the_neutral_generation_manifest_without_compatibility_facades()
    {
        var directory = Path.Combine(FindRepositoryRoot(), "source", "DevTools.NUnit.Host", "Loading");
        string[] forbiddenFiles =
        [
            "NUnitGenerationManifest.cs",
            "NUnitGenerationManifestAdapter.cs",
            "NUnitGenerationContentHash.cs",
            "NUnitGenerationLoadException.cs",
            "NUnitGenerationPaths.cs",
            "NUnitRuntimeDiagnostic.cs",
            "NUnitRuntimeUnloadVerifier.cs",
        ];

        var existing = forbiddenFiles.Where(file => File.Exists(Path.Combine(directory, file))).ToList();
        Assert.Empty(existing);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output.");
    }
}
