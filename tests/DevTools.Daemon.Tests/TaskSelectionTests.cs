using System.Text.Json;
using DevTools.Daemon.Mcp;
using DevTools.Daemon.Mcp.Tools;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Daemon.Tests;

[TestClass]
public sealed class TaskSelectionTests
{
    [TestMethod]
    public void Select_InfrastructureTools_AreSynchronous()
    {
        foreach (var name in new[] { "list_machines", "list_processes", "read_file_info" })
            Assert.AreEqual(McpTaskExecutionMode.Synchronous, TaskSelection.Select(CreateRequest(name)));
    }

    [TestMethod]
    public void Select_CodeMode_IsOptional()
    {
        Assert.AreEqual(McpTaskExecutionMode.Optional, TaskSelection.Select(CreateRequest(CodeModeTool.Name)));
    }

    [TestMethod]
    public void Select_LaunchHost_IsOptional()
    {
        Assert.AreEqual(
            McpTaskExecutionMode.Optional,
            TaskSelection.Select(CreateRequest("launch_host")));
    }

    private static RequestContext<CallToolRequestParams> CreateRequest(string name)
    {
        var server = new Mock<McpServer>();
        return new RequestContext<CallToolRequestParams>(
            server.Object,
            new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
            new CallToolRequestParams { Name = name });
    }
}
