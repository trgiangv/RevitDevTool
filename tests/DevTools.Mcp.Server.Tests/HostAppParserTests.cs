using DevTools.Hosting;
using DevTools.Ipc;
using DevTools.Daemon.Mcp.Utils;

namespace DevTools.Mcp.Server.Tests;

[TestClass]
public sealed class HostAppParserTests
{
    [TestMethod]
    [DataRow("DevToolsMcp_Revit_2025_123", HostApp.Revit)]
    [DataRow("DevToolsMcp_AutoCad_2026_456", HostApp.AutoCad)]
    public void FromPipeName_ParsesHostSegment(string pipeName, HostApp expected)
    {
        Assert.AreEqual(expected, HostAppParser.FromPipeName(pipeName));
    }

    [TestMethod]
    [DataRow("not-a-pipe")]
    [DataRow("DevToolsMcp_InvalidHost_2025_123")]
    public void FromPipeName_InvalidPipe_ReturnsNull(string pipeName)
    {
        Assert.IsNull(HostAppParser.FromPipeName(pipeName));
    }

    [TestMethod]
    [DataRow("revit", HostApp.Revit)]
    [DataRow("CIVIL3D", HostApp.Civil3D)]
    public void ParseHostApp_ParsesEnum(string value, HostApp expected)
    {
        Assert.AreEqual(expected, HostAppParser.ParseHostApp(value));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("unknown-host")]
    public void ParseHostApp_InvalidValue_ReturnsNull(string? value)
    {
        Assert.IsNull(HostAppParser.ParseHostApp(value));
    }
}
