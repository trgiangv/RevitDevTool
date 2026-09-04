using DevTools.Execution.External.Mcp.Connections;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

public class McpPipeConnectionTrackerTests
{
    [Fact]
    public void ConnectionState_TracksMcpEndpointAndClientCount()
    {
        var state = new McpConnectState(NullLogger<McpConnectState>.Instance);

        state.SetMcpEndpoint("DevToolsMcp_Revit_2025_12345");
        Assert.True(state.McpIsListening);
        Assert.False(state.McpIsConnected);
        Assert.Equal(0, state.McpClientCount);

        state.SetMcpClientCount(2);
        Assert.True(state.McpIsConnected);
        Assert.Equal(2, state.McpClientCount);

        state.ClearMcpState();
        Assert.False(state.McpIsListening);
        Assert.False(state.McpIsConnected);
        Assert.Equal(string.Empty, state.McpEndpoint);
    }
}
