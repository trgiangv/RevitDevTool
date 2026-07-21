using DevTools.Ipc;
using DevTools.Mcp.Routing;
using ModelContextProtocol.Protocol;

namespace RevitDevTool.Server.Tests;

public sealed class ProtocolCompatibilityTests
{
    [Theory]
    [InlineData("4.0.0", "4.0.0", true)]
    [InlineData("4.1.0", "4.0.0", true)]
    [InlineData("3.9.9", "4.0.0", false)]
    [InlineData(null, "4.0.0", false)]
    public void IsAtLeast_compares_product_versions(string? actual, string minimum, bool expected) =>
        Assert.Equal(expected, ProtocolCompatibility.IsAtLeast(actual, minimum));

    [Fact]
    public void RequireHostProtocolVersion_accepts_advertised_capability()
    {
        var capabilities = new ServerCapabilities
        {
            Experimental = new Dictionary<string, object>
            {
                ["devtools"] = System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    protocol = new { version = ProtocolCompatibility.HostProtocolVersion }
                })
            }
        };

        var exception = Record.Exception(() =>
            ProtocolVersionValidation.RequireHostProtocolVersion(capabilities, "DevTools_Revit_2025_7"));

        Assert.Null(exception);
    }

    [Fact]
    public void RequireHostProtocolVersion_rejects_missing_capability()
    {
        var exception = Assert.Throws<ProtocolCompatibilityException>(() =>
            ProtocolVersionValidation.RequireHostProtocolVersion(new ServerCapabilities(), "DevTools_Revit_2025_7"));

        Assert.Equal("host_protocol_missing", exception.Code);
        Assert.Contains("protocol_version_mismatch", exception.Message, StringComparison.Ordinal);
    }
}
