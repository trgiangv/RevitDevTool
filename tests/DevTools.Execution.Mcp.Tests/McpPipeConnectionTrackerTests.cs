using DevTools.Execution.External.Mcp.Connections;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

[TestClass]
public class McpPipeConnectionTrackerTests
{
    [TestMethod]
    public void ConnectionState_TracksMcpEndpointAndClientCount()
    {
        var state = new McpConnectState(NullLogger<McpConnectState>.Instance);

        state.SetMcpEndpoint("DevToolsMcp_Revit_2025_12345");
        Assert.IsTrue(state.McpIsListening);
        Assert.IsFalse(state.McpIsConnected);
        Assert.AreEqual(0, state.McpClientCount);

        state.SetMcpClientCount(2);
        Assert.IsTrue(state.McpIsConnected);
        Assert.AreEqual(2, state.McpClientCount);

        state.ClearMcpState();
        Assert.IsFalse(state.McpIsListening);
        Assert.IsFalse(state.McpIsConnected);
        Assert.AreEqual(string.Empty, state.McpEndpoint);
    }
}
