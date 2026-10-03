using System.Reflection;

namespace DevTools.Mcp.Adapter.Tests;

[TestClass]
public sealed class AdapterArchitectureBoundaryTests
{
    [TestMethod]
    public void Catalog_DoesNotReferenceExecution()
    {
        var catalogReferences = typeof(DevTools.Mcp.McpCatalogStore).Assembly.GetReferencedAssemblies();

        Assert.IsFalse(catalogReferences.Any(reference =>
            string.Equals(reference.Name, "DevTools.Execution", StringComparison.Ordinal)));
    }
}
