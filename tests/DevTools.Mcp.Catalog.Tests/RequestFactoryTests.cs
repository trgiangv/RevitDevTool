using DevTools.Mcp.Catalog.Bridging;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class RequestFactoryTests
{
    [TestMethod]
    public void ToToolContext_SetsToolNameAndRequest()
    {
        var request = new CallToolRequestParams { Name = "original", Arguments = new Dictionary<string, System.Text.Json.JsonElement>() };

        var context = RequestFactory.ToToolContext("resolved_tool", request);

        Assert.AreEqual("resolved_tool", context.Params!.Name);
        Assert.AreSame(request, context.Params);
        Assert.IsNotNull(context.Server);
    }

    [TestMethod]
    public void ToResourceContext_BuildsReadResourceRequest()
    {
        var context = RequestFactory.ToResourceContext("sample://demo/status");

        Assert.AreEqual("sample://demo/status", context.Params!.Uri);
        Assert.IsNotNull(context.Server);
    }
}
