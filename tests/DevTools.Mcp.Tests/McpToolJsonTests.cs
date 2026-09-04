using System.Text.Json;
using DevTools.Mcp.Core.Sessions;
using DevTools.Mcp.Server.Contracts;
using DevTools.Mcp.Server.Tools;
using Moq;

namespace DevTools.Mcp.Tests;

public sealed class McpToolJsonTests
{
    [Fact]
    public void Options_ProvideMetadataForInvokeDynamicParameterTypes()
    {
        Assert.NotNull(McpToolJson.Options.GetTypeInfo(typeof(Dictionary<string, JsonElement>)));
        Assert.NotNull(McpToolJson.Options.GetTypeInfo(typeof(ResourceReadRequest[])));
    }

    [Fact]
    public void InvokeDynamicTool_Create_ResolvesDictionaryParameterMetadata()
    {
        var tool = InvokeDynamicTool.Create(Mock.Of<IHostBroker>());
        Assert.Equal("invoke_dynamic", tool.ProtocolTool.Name);
    }

    [Fact]
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

        Assert.True(DynamicCapabilityId.TryDecode(encoded, out var decoded));
        Assert.NotNull(decoded);
        Assert.Equal(id, decoded);
    }

    [Fact]
    public void DynamicCapabilityId_TryDecode_LegacyPascalCaseToken_ReturnsFalse()
    {
        const string legacyToken =
            "dci1.eyJNYWNoaW5lSWQiOiJsZWdhY3kiLCJIb3N0SW5zdGFuY2VJZCI6MSwiS2luZCI6MCwiVGFyZ2V0IjoidCIsIkNhdGFsb2dWZXJzaW9uIjoidiIsIkZpbmdlcnByaW50IjoiZiJ9";

        Assert.False(DynamicCapabilityId.TryDecode(legacyToken, out _));
    }
}
