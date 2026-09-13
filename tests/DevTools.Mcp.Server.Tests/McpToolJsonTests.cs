using System.Text.Json;
using DevTools.Mcp.Core.Sessions;
using DevTools.Mcp.Server.Contracts;
using DevTools.Mcp.Server.Tools;
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
    public void InvokeDynamicTool_Create_ResolvesDictionaryParameterMetadata()
    {
        var tool = InvokeDynamicTool.Create(Mock.Of<IHostBroker>());
        Assert.AreEqual("invoke_dynamic", tool.ProtocolTool.Name);
    }

    [TestMethod]
    public void DynamicCapabilityId_EncodeTryDecode_RoundTrips()
    {
        var id = new DynamicCapabilityId(
            "machine-a",
            42,
            HostCatalogKind.Tool,
            "revit_find_elements",
            "catalog-version",
            "fingerprint-abc");

        var encoded = id.Encode();

        Assert.IsTrue(DynamicCapabilityId.TryDecode(encoded, out var decoded));
        Assert.IsNotNull(decoded);
        Assert.AreEqual(id, decoded);
    }

    [TestMethod]
    public void DynamicCapabilityId_TryDecode_LegacyPascalCaseToken_ReturnsFalse()
    {
        const string legacyToken =
            "dci1.eyJNYWNoaW5lSWQiOiJsZWdhY3kiLCJIb3N0SW5zdGFuY2VJZCI6MSwiS2luZCI6MCwiVGFyZ2V0IjoidCIsIkNhdGFsb2dWZXJzaW9uIjoidiIsIkZpbmdlcnByaW50IjoiZiJ9";

        Assert.IsFalse(DynamicCapabilityId.TryDecode(legacyToken, out _));
    }
}
