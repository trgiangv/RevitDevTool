using DevTools.Mcp.Routing.Broker;
using DevTools.Mcp.Routing.Catalog;

namespace RevitDevTool.Server.Tests;

public sealed class CatalogGenerationTests
{
    [Fact]
    public void ReconnectIdentity_DiffersWhenPipeAndPidAreReused()
    {
        var first = new HostCatalogIdentity("DevTools_Revit_2025_41100", 1);
        var second = new HostCatalogIdentity("DevTools_Revit_2025_41100", 2);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void BrokerSearchResponse_CanExposeCatalogReadiness()
    {
        var status = new HostCatalogStatus(41100, HostCatalogState.Refreshing, null, null, null);
        var response = new BrokerSearchResponse("rev", [], [], false, [status]);

        Assert.Equal(HostCatalogState.Refreshing, Assert.Single(response.Catalogs!).State);
    }

    [Fact]
    public void BrokerSearch_IncludesUnavailablePublicationsWithoutPrimitiveEntries()
    {
        var instance = new HostInstanceDescriptor(41101, "Revit", "2025", "DevTools_Revit_2025_41101");
        var publication = new HostCatalogPublication(
            new HostCatalogIdentity(instance.PipeName, 2),
            instance,
            HostCatalogState.Unavailable,
            null,
            null,
            null,
            "catalog_fetch_failed");
        var catalog = new BrokerCatalogIndex();

        catalog.ReplacePublications([publication]);
        var response = catalog.Search(new BrokerSearchRequest(null, null, null));

        Assert.Empty(response.Items);
        var status = Assert.Single(response.Catalogs!);
        Assert.Equal(41101, status.HostId);
        Assert.Equal(HostCatalogState.Unavailable, status.State);
        Assert.Equal("catalog_fetch_failed", status.LastErrorCode);
    }
}
