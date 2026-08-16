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
}
