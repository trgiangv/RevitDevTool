namespace DevTools.Utilities.Tests;

public sealed class UtilitiesAssemblyBoundaryTests
{
    [Fact]
    public void Utilities_does_not_reference_ui_logging_shell_or_file_metadata()
    {
        var references = typeof(AppUtils).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("PresentationFramework", references);
        Assert.DoesNotContain("DevTools.UI", references);
        Assert.DoesNotContain("DevTools.Logging", references);
        Assert.DoesNotContain("DevTools.FileMetadata.Core", references);
        Assert.DoesNotContain("DevTools.FileMetadata.Revit", references);
        Assert.DoesNotContain("DevTools.FileMetadata.Acad", references);
        Assert.DoesNotContain("MahApps.Metro", references);
        Assert.DoesNotContain("Microsoft.WindowsAPICodePack", references);
        Assert.DoesNotContain("Microsoft.WindowsAPICodePack.Shell", references);
    }

    [Fact]
    public void Utilities_source_has_no_ui_logging_or_shell()
    {
        var utilitiesDir = Path.Combine(RepositoryRoot.Find(), "source", "DevTools.Utilities");
        var sources = Directory.GetFiles(utilitiesDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(sources);

        string[] forbidden =
        [
            "PresentationFramework",
            "DevTools.UI",
            "DevTools.Logging",
            "FileMetadata",
            "MahApps.Metro",
            "Microsoft.WindowsAPICodePack",
            "System.Windows",
        ];

        foreach (var path in sources)
        {
            var text = File.ReadAllText(path);
            foreach (var token in forbidden)
            {
                Assert.DoesNotContain(token, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Utilities_non_assemblyloading_source_has_no_autodesk_api_keywords()
    {
        var utilitiesDir = Path.Combine(RepositoryRoot.Find(), "source", "DevTools.Utilities");
        var sources = Directory.GetFiles(utilitiesDir, "*.cs", SearchOption.AllDirectories)
            .Where(static path => path.IndexOf($"{Path.DirectorySeparatorChar}AssemblyLoading{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) < 0)
            .ToArray();
        Assert.NotEmpty(sources);

        string[] forbidden = ["RevitAPI", "acmgd"];

        foreach (var path in sources)
        {
            var text = File.ReadAllText(path);
            foreach (var token in forbidden)
            {
                Assert.DoesNotContain(token, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void NUnit_Host_still_project_references_Utilities_AssemblyLoading()
    {
        var root = RepositoryRoot.Find();
        var hostCsproj = File.ReadAllText(Path.Combine(root, "source", "DevTools.NUnit.Host", "DevTools.NUnit.Host.csproj"));
        Assert.Contains("DevTools.Utilities.csproj", hostCsproj, StringComparison.Ordinal);
        Assert.Contains("DevTools.Logging.csproj", hostCsproj, StringComparison.Ordinal);

        var hostSources = Directory.GetFiles(
            Path.Combine(root, "source", "DevTools.NUnit.Host"),
            "*.cs",
            SearchOption.AllDirectories);
        Assert.Contains(
            hostSources,
            static path => File.ReadAllText(path).Contains("DevTools.Utilities.AssemblyLoading", StringComparison.Ordinal));
    }
}
