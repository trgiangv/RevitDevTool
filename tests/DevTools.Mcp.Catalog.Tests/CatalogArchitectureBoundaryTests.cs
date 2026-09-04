using System.Reflection;
using DevTools.Mcp.Catalog;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class CatalogArchitectureBoundaryTests
{
    [Fact]
    public void Catalog_DoesNotReferenceExecution()
    {
        var catalogReferences = Assembly.Load("DevTools.Mcp.Catalog").GetReferencedAssemblies();

        Assert.DoesNotContain(catalogReferences, reference =>
            string.Equals(reference.Name, "DevTools.Execution", StringComparison.Ordinal));
    }

    [Fact]
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
