using System.Text.Json;
using DevTools.Daemon.Control;
using DevTools.Daemon.Tunnel;

namespace DevTools.Daemon.Tests;

using System.Linq;

[TestClass]
public sealed class TunnelAndControlJsonTests
{
    [TestMethod]
    public void GatewayRegisterMessage_RoundTripsWithSnakeCaseKeys()
    {
        var message = new GatewayRegisterMessage(
            "register",
            "machine-1",
            "WORKSTATION",
            ["Revit", "AutoCad"]);

        var json = JsonSerializer.Serialize(message, ControlJsonContext.Default.GatewayRegisterMessage);
        var loaded = JsonSerializer.Deserialize(json, ControlJsonContext.Default.GatewayRegisterMessage);

        Assert.IsNotNull(loaded);
        Assert.AreEqual("machine-1", loaded!.MachineId);
        Assert.AreEqual("WORKSTATION", loaded.MachineName);
        Assert.Contains("Revit", loaded.HostApps);
        Assert.Contains("machine_id", json, StringComparison.Ordinal);
    }

    [TestMethod]
    public void GatewayHeartbeatMessage_RoundTripsHostApps()
    {
        var message = new GatewayHeartbeatMessage("heartbeat", ["Revit"]);
        var json = JsonSerializer.Serialize(message, ControlJsonContext.Default.GatewayHeartbeatMessage);
        var loaded = JsonSerializer.Deserialize(json, ControlJsonContext.Default.GatewayHeartbeatMessage);

        Assert.IsNotNull(loaded);
        Assert.AreEqual("heartbeat", loaded!.Type);
        Enumerable.Single(loaded.HostApps);
    }

    [TestMethod]
    [DataRow("wss://gateway.example/tunnel", "https://gateway.example")]
    [DataRow("https://gateway.example/tunnel", "https://gateway.example")]
    public void GatewayOptions_HttpBaseUrl_StripsTunnelPath(string url, string expectedBase)
    {
        var options = new GatewayOptions { Url = url };
        Assert.AreEqual(expectedBase, options.HttpBaseUrl);
    }
}
