using System.Text.Json;
using DevTools.Daemon.Mcp.Contracts;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Mcp.Tools;
using Moq;

namespace DevTools.Mcp.Server.Tests;

[TestClass]
public sealed class McpToolJsonTests
{
    [TestMethod]
    public void Options_ProvideMetadataForInvokeDynamicParameterTypes()
    {
        Assert.IsNotNull(McpToolJson.Options.GetTypeInfo(typeof(Dictionary<string, JsonElement>)));
        Assert.IsNotNull(McpToolJson.Options.GetTypeInfo(typeof(ResourceReadRequest[])));
    }

    [TestMethod]
    public void InvokeTool_Create_ResolvesDictionaryParameterMetadata()
    {
        var tool = InvokeTool.Create(Mock.Of<IProcessSessions>());
        Assert.AreEqual("invoke_dynamic", tool.ProtocolTool.Name);
    }

    [TestMethod]
    public void CatalogId_EncodeTryDecode_RoundTrips()
    {
        var id = new CatalogId(42, CatalogType.Tool, "revit_find_elements", "a1b2c3d4");
        var encoded = id.Encode();

        Assert.StartsWith("dci2.42.t.a1b2c3d4.revit_find_elements", encoded);
        Assert.IsTrue(CatalogId.TryDecode(encoded, out var decoded));
        Assert.IsNotNull(decoded);
        Assert.AreEqual(id, decoded);
    }
}
