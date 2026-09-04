using DevTools.Hosting;
using DevTools.Ipc;
using DevTools.Mcp.Server.Utils;

namespace DevTools.Mcp.Server.Tests;

public sealed class HostAppParsingTests
{
    [Theory]
    [InlineData("DevToolsMcp_Revit_2025_123", HostApp.Revit)]
    [InlineData("DevToolsMcp_AutoCad_2026_456", HostApp.AutoCad)]
    public void FromPipeName_ParsesHostSegment(string pipeName, HostApp expected)
    {
        Assert.Equal(expected, HostAppParsing.FromPipeName(pipeName));
    }

    [Theory]
    [InlineData("not-a-pipe")]
    [InlineData("DevToolsMcp_InvalidHost_2025_123")]
    public void FromPipeName_InvalidPipe_ReturnsNull(string pipeName)
    {
        Assert.Null(HostAppParsing.FromPipeName(pipeName));
    }

    [Theory]
    [InlineData("revit", HostApp.Revit)]
    [InlineData("CIVIL3D", HostApp.Civil3D)]
    public void ParseHostApp_ParsesEnum(string value, HostApp expected)
    {
        Assert.Equal(expected, HostAppParsing.ParseHostApp(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown-host")]
    public void ParseHostApp_InvalidValue_ReturnsNull(string? value)
    {
        Assert.Null(HostAppParsing.ParseHostApp(value));
    }
}
