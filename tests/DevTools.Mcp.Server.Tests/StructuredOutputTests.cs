using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Mcp.Tools;
using DevTools.Mcp.Server.Tests.Harness;
using ModelContextProtocol.Protocol;
using Moq;

namespace DevTools.Mcp.Server.Tests;

[TestClass]
public sealed class StructuredOutputTests
{
    [TestMethod]
    public async Task ListInstances_EmitsStructuredContent()
    {
        var broker = new Mock<IProcessSessions>();
        broker.Setup(b => b.Catalog).Returns(new ProcessCatalogs());
        var scanner = new Mock<IMcpPipeScanner>();
        scanner.Setup(s => s.Discover()).Returns([]);

        var tool = ListProcessesTool.Create(broker.Object, scanner.Object);
        var result = await McpToolInvoke.Invoke(tool, "list_processes", new { });

        Assert.IsNotNull(result.StructuredContent);
        Assert.IsNull(tool.ProtocolTool.OutputSchema);
        Assert.AreEqual(0, result.StructuredContent!.Value.GetProperty("totalConnected").GetInt32());
    }
}
