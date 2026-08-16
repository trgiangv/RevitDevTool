using DevTools.FileMetadata.Core;

namespace DevTools.FileMetadata.Core.Tests;

public sealed class FileMetadataCoreAssemblyBoundaryTests
{
    [Fact]
    public void FileMetadataCore_forbids_ui_logging_and_presentation_and_may_reference_hosting()
    {
        var references = typeof(FileInfoResult).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("DevTools.Hosting", references);
        Assert.DoesNotContain("DevTools.UI", references);
        Assert.DoesNotContain("DevTools.Logging", references);
        Assert.DoesNotContain("DevTools.Presentation", references);
        Assert.DoesNotContain("PresentationFramework", references);
        Assert.DoesNotContain("MahApps.Metro", references);
    }
}
