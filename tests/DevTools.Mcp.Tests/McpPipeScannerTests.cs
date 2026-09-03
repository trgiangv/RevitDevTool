using System.Diagnostics;
using DevTools.Mcp.Client;

namespace DevTools.Mcp.Tests;

public class McpPipeScannerTests
{
    [Fact]
    public void IsLiveMcpPipe_AcceptsPipeForThisProcess()
    {
        var pipe = HostPipeName.FormatMcp("Revit", "2025", Environment.ProcessId);
        Assert.True(McpPipeScanner.IsLiveMcpPipe(pipe));
    }

    [Fact]
    public void IsLiveMcpPipe_RejectsPipeForMissingProcess()
    {
        var pipe = HostPipeName.FormatMcp("Revit", "2025", int.MaxValue);
        Assert.False(McpPipeScanner.IsLiveMcpPipe(pipe));
    }

    [Fact]
    public void IsLiveMcpPipe_RejectsNonMcpNames()
    {
        Assert.False(McpPipeScanner.IsLiveMcpPipe(HostPipeName.FormatTest("Revit", "2025", Environment.ProcessId)));
        Assert.False(McpPipeScanner.IsLiveMcpPipe("not-a-pipe"));
    }
}
