using DevTools.Mcp.Catalog.Bridging;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class RequestFactoryTests
{
    [Fact]
    public void ToToolContext_SetsToolNameAndRequest()
    {
        var request = new CallToolRequestParams { Name = "original", Arguments = new Dictionary<string, System.Text.Json.JsonElement>() };

        var context = RequestFactory.ToToolContext("resolved_tool", request);

        Assert.Equal("resolved_tool", context.Params!.Name);
        Assert.Same(request, context.Params);
        Assert.NotNull(context.Server);
    }

    [Fact]
    public void ToResourceContext_BuildsReadResourceRequest()
    {
        var context = RequestFactory.ToResourceContext("sample://demo/status");

        Assert.Equal("sample://demo/status", context.Params!.Uri);
        Assert.NotNull(context.Server);
    }
}
