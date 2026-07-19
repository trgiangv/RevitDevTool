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
}
