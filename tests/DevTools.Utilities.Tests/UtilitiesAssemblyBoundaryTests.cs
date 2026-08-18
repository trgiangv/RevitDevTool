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
        Assert.DoesNotContain("DevTools.Hosting", references);
        Assert.DoesNotContain("DevTools.Execution.Abstractions", references);
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
    public void Utilities_has_no_assembly_loading_ownership()
    {
        var utilitiesDir = Path.Combine(RepositoryRoot.Find(), "source", "DevTools.Utilities");
        var sources = Directory.GetFiles(utilitiesDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(sources);

        string[] forbidden =
        [
            "ByteAssemblyLoader",
            "DirectoryAssemblyLoader",
            "HostAssemblyResolver",
            "HostSharedAssemblies",
            "HostSharedAssemblyNames",
            "HostPackagePrefixes",
            "NUnitSharedAssemblyPolicy",
            "NetfxNUnitSharedAssemblyResolver",
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
    public void Legacy_utility_loader_files_are_absent()
    {
        var root = RepositoryRoot.Find();
        var loaderDirectory = Path.Combine(root, "source", "DevTools.Utilities", "AssemblyLoading");
        Assert.Empty(Directory.Exists(loaderDirectory)
            ? Directory.GetFiles(loaderDirectory, "*.cs", SearchOption.AllDirectories)
            : []);
        Assert.False(File.Exists(Path.Combine(root, "source", "DevTools.Utilities", "AssemblyLoader.cs")));
    }
}
