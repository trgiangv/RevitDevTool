using DevTools.Execution.External.Mcp.Connections;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

[TestClass]
public class McpPipeConnectionTrackerTests
{
    [TestMethod]
    public void ConnectionState_TracksMcpEndpointAndClientCount()
    {
        var state = new McpConnectTracker(NullLogger<McpConnectTracker>.Instance);

        state.SetEndpoint("DevToolsMcp_Revit_2025_12345");
        Assert.IsTrue(state.McpIsListening);
        Assert.IsFalse(state.McpIsConnected);
        Assert.AreEqual(0, state.McpClientCount);

        state.SetClientCount(2);
        Assert.IsTrue(state.McpIsConnected);
        Assert.AreEqual(2, state.McpClientCount);

        state.Reset();
        Assert.IsFalse(state.McpIsListening);
        Assert.IsFalse(state.McpIsConnected);
        Assert.AreEqual(string.Empty, state.McpEndpoint);
    }
}
