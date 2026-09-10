using DevTools.Testing.Host.NUnit;

namespace DevTools.NUnit.Host.Tests;

public sealed class HostAssemblyBoundaryTests
{
    [Fact]
    public void NUnit_provider_sources_have_no_cad_api_or_logging_references()
    {
        var csproj = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "source",
            "DevTools.Testing.Host",
            "DevTools.Testing.Host.csproj"));
        Assert.DoesNotContain("DevTools.Logging.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("ZLogger.Scintilla", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Scintilla5", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DevTools.AssemblyIsolation.csproj", csproj, StringComparison.Ordinal);

        var loadingDirectory = Path.Combine(FindRepositoryRoot(), "source", "DevTools.Testing.Host", "NUnit");
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

        Assert.Equal("DevTools.Testing.Host", typeof(NUnitTestFrameworkProvider).Assembly.GetName().Name);
    }

    [Fact]
    public void Host_uses_the_neutral_generation_manifest_without_compatibility_facades()
    {
        var directory = Path.Combine(FindRepositoryRoot(), "source", "DevTools.Testing.Host", "NUnit", "Loading");
        string[] forbiddenFiles =
        [
            "NUnitGenerationManifest.cs",
            "NUnitGenerationManifestAdapter.cs",
            "NUnitGenerationContentHash.cs",
            "NUnitGenerationPlanner.cs",
            "NUnitIsolationPlan.cs",
            "NUnitRuntimeSessionHandle.cs",
            "NUnitGenerationBuildException.cs",
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
