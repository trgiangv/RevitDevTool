using DevTools.Hosting;

namespace DevTools.Hosting.Tests;

public sealed class HostingAssemblyBoundaryTests
{
    [Fact]
    public void Hosting_does_not_reference_ui_logging_or_file_metadata()
    {
        var references = typeof(HostApp).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("DevTools.UI", references);
        Assert.DoesNotContain("DevTools.Logging", references);
        Assert.DoesNotContain("DevTools.FileMetadata.Core", references);
        Assert.DoesNotContain("DevTools.FileMetadata.Revit", references);
        Assert.DoesNotContain("DevTools.FileMetadata.Acad", references);
        Assert.DoesNotContain("PresentationFramework", references);
        Assert.DoesNotContain("MahApps.Metro", references);
    }

    [Fact]
    public void Generic_Hosting_source_has_no_product_dialog_or_api_keywords()
    {
        var hostingDir = Path.Combine(RepositoryRoot.Find(), "source", "DevTools.Hosting");
        var sources = Directory.GetFiles(hostingDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(sources);

        string[] forbidden =
        [
            "unsigned add-in",
            "unsigned executable file",
            "questionable add-in",
            "#32770",
            "RevitAPI",
            "acmgd",
            "MahApps",
            "ControlzEx",
            "CommunityToolkit",
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
}
