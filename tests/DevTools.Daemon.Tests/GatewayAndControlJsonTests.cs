using System.Text.Json;
using DevTools.Daemon.Control;
using DevTools.Daemon.Gateway;
using DevTools.Hosting;
using DevTools.Ipc;

namespace DevTools.Daemon.Tests;

using System.Linq;

[TestClass]
public sealed class GatewayAndControlJsonTests
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
    public void ControlResponses_RoundTripExpectedFields()
    {
        var auth = new AuthStateResponse(true, "user-1", "a@example.com", "Agent", "https://avatar");
        var authJson = JsonSerializer.Serialize(auth, ControlJsonContext.Default.AuthStateResponse);
        Assert.Contains("\"isAuthenticated\":true", authJson, StringComparison.Ordinal);

        var operation = new OperationResponse(false, "failed");
        var operationJson = JsonSerializer.Serialize(operation, ControlJsonContext.Default.OperationResponse);
        Assert.Contains("\"success\":false", operationJson, StringComparison.Ordinal);
        Assert.Contains("\"failed\"", operationJson, StringComparison.Ordinal);

        var error = new ErrorResponse("bad request");
        Assert.Contains("\"bad request\"", JsonSerializer.Serialize(error, ControlJsonContext.Default.ErrorResponse));

        var host = new HostInfoEntry(HostApp.Revit, "2025", 123, HostPipeName.FormatMcp("Revit", "2025", 123));
        var hostJson = JsonSerializer.Serialize(host, ControlJsonContext.Default.HostInfoEntry);
        Assert.Contains("\"hostApp\":\"Revit\"", hostJson, StringComparison.Ordinal);
        Assert.Contains("\"pid\":123", hostJson, StringComparison.Ordinal);
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
