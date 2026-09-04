using System.Reflection;

namespace DevTools.Mcp.Tests;

public class PlatformSplitScaffoldTests
{
    [Fact]
    public void LockedDestinationAssemblies_AreReferenceable()
    {
        var core = Assembly.Load("DevTools.Mcp.Core");
        var fileMetadataCore = Assembly.Load("DevTools.FileMetadata.Core");

        Assert.NotNull(core.GetType("DevTools.Mcp.Core.McpError"));
        Assert.NotNull(fileMetadataCore.GetType("DevTools.FileMetadata.Core.FileInfoRequest"));

        foreach (var assemblyName in LockedDestinationAssemblies)
        {
            Assert.NotNull(Assembly.Load(assemblyName));
        }
    }

    [Fact]
    public void Catalog_DoesNotReferenceExecution()
    {
        var catalogReferences = Assembly.Load("DevTools.Mcp.Catalog").GetReferencedAssemblies();

        Assert.DoesNotContain(catalogReferences, reference =>
            string.Equals(reference.Name, "DevTools.Execution", StringComparison.Ordinal));
    }

    [Fact]
    public void Execution_DoesNotReferenceHostMcpAdapter()
    {
        var executionReferences = Assembly.Load("DevTools.Execution").GetReferencedAssemblies();

        Assert.DoesNotContain(executionReferences, reference =>
            string.Equals(reference.Name, "DevTools.Mcp.Adapter", StringComparison.Ordinal));
    }

    private static readonly string[] LockedDestinationAssemblies =
    [
        "DevTools.Mcp.Core",
        "DevTools.Mcp.Catalog",
        "DevTools.Mcp.Adapter",
        "DevTools.Mcp.Client",
        "DevTools.Mcp.Server",
        "DevTools.FileMetadata.Core",
        "DevTools.FileMetadata.Revit",
        "DevTools.FileMetadata.Acad"
    ];
}
