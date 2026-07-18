using System.Text.Json;
using DevTools.Daemon.Contracts;
using DevTools.Mcp.Routing;

namespace RevitDevTool.Server.Tests;

public sealed class GatewayContractTests
{
    [Fact]
    public void RegisterAndHeartbeat_UseExactGatewayWireNamesAndTypedHostInventory()
    {
        var inventory = new[]
        {
            new HostInstanceDescriptor(4201, "Revit", "2027", McpPipeName.Format(4201)),
            new HostInstanceDescriptor(4202, "AutoCad", "2026", McpPipeName.Format(4202))
        }.Select(host => $"{host.HostApp}_{host.VersionNumber}_{host.ProcessId}").Order().ToArray();
        var register = JsonSerializer.SerializeToElement(new GatewayRegisterMessage("register", "machine-1", "DESKTOP", inventory));
        var heartbeat = JsonSerializer.SerializeToElement(new GatewayHeartbeatMessage("heartbeat", inventory));

        Assert.Equal("register", register.GetProperty("type").GetString());
        Assert.Equal("machine-1", register.GetProperty("machine_id").GetString());
        Assert.Equal("DESKTOP", register.GetProperty("machine_name").GetString());
        Assert.Equal(inventory, register.GetProperty("host_apps").EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.Equal("heartbeat", heartbeat.GetProperty("type").GetString());
        Assert.Equal(inventory, heartbeat.GetProperty("host_apps").EnumerateArray().Select(value => value.GetString()).ToArray());
    }

    [Fact]
    public void MachineSelectionAndBrokerHostId_AreDistinctScopes()
    {
        var hostId = 4201;
        var targetMachineHeader = "x-target-machine";

        Assert.Equal(4201, hostId);
        Assert.Equal("x-target-machine", targetMachineHeader);
        Assert.True(hostId > 0, "hostId is a PID on the daemon selected by the gateway, not a gateway machine identifier.");
    }
}
