using System.Reflection;
using DevTools.Mcp.Catalog;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class CatalogArchitectureBoundaryTests
{
    [TestMethod]
    public void Catalog_DoesNotReferenceExecution()
    {
        var catalogReferences = Assembly.Load("DevTools.Mcp.Catalog").GetReferencedAssemblies();

        Assert.IsFalse(catalogReferences.Any(reference =>
            string.Equals(reference.Name, "DevTools.Execution", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CatalogAssembly_DoesNotReferenceUiOrMahApps()
    {
        var names = typeof(McpCatalogStore).Assembly.GetReferencedAssemblies()
            .Select(static name => name.Name)
            .ToArray();

        Assert.DoesNotContain("DevTools.UI", names);
        Assert.DoesNotContain("MahApps.Metro", names);
        Assert.DoesNotContain("DevTools.MahApps.Metro", names);
        Assert.DoesNotContain("PresentationFramework", names);
    }
}
