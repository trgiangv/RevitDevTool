using DevTools.Hosting;
using DevTools.Hosting.Revit;

namespace DevTools.Hosting.Revit.Tests;

public sealed class RevitStartupDialogStrategyTests
{
    [Fact]
    public void Catalog_is_unsigned_add_in_only_with_closed_blocked_pair()
    {
        var options = new RevitStartupDialogStrategy().CreateOptions();
        Assert.Equal(["unsigned add-in"], options.DialogTitleKeywords);
        Assert.Equal(["always load"], options.PreferredButtonKeywords);
        Assert.Equal(["do not load", "load once"], options.BlockedButtonKeywords);
        Assert.DoesNotContain("questionable add-in", options.DialogTitleKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("cancel", options.BlockedButtonKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("no", options.BlockedButtonKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("#32770", options.WindowClassName);
        Assert.Equal("button", options.ButtonClassName);
    }
}

public sealed class RevitHostingBoundaryTests
{
    [Fact]
    public void Hosting_Revit_forbids_file_metadata_openmcdf_and_ui()
    {
        var root = FindRepositoryRoot();
        var csproj = File.ReadAllText(Path.Combine(root, "source", "DevTools.Hosting.Revit", "DevTools.Hosting.Revit.csproj"));
        Assert.DoesNotContain("FileMetadata", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OpenMcdf", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UseWPF", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("MahApps", csproj, StringComparison.OrdinalIgnoreCase);

        var sources = Directory.GetFiles(
            Path.Combine(root, "source", "DevTools.Hosting.Revit"), "*.cs", SearchOption.AllDirectories);
        foreach (var path in sources)
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("FileMetadata", text, StringComparison.Ordinal);
            Assert.DoesNotContain("OpenMcdf", text, StringComparison.Ordinal);
            Assert.DoesNotContain("MahApps", text, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Windows", text, StringComparison.Ordinal);
        }

        var references = typeof(RevitPathResolver).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("DevTools.FileMetadata.Revit", references);
        Assert.DoesNotContain("OpenMcdf", references);
        Assert.DoesNotContain("PresentationFramework", references);
        Assert.DoesNotContain("MahApps.Metro", references);
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

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
