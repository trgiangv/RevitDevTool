using System.Diagnostics;
using DevTools.Mcp.Client;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public class McpPipeScannerTests
{
    [TestMethod]
    public void IsLiveMcpPipe_AcceptsPipeForThisProcess()
    {
        var pipe = HostPipeName.FormatMcp("Revit", "2025", Environment.ProcessId);
        Assert.IsTrue(McpPipeScanner.IsLiveMcpPipe(pipe));
    }

    [TestMethod]
    public void IsLiveMcpPipe_RejectsPipeForMissingProcess()
    {
        var pipe = HostPipeName.FormatMcp("Revit", "2025", int.MaxValue);
        Assert.IsFalse(McpPipeScanner.IsLiveMcpPipe(pipe));
    }

    [TestMethod]
    public void IsLiveMcpPipe_RejectsNonMcpNames()
    {
        Assert.IsFalse(McpPipeScanner.IsLiveMcpPipe(HostPipeName.FormatTest("Revit", "2025", Environment.ProcessId)));
        Assert.IsFalse(McpPipeScanner.IsLiveMcpPipe("not-a-pipe"));
    }
}
