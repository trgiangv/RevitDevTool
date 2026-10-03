using System.Text.Json;
using DevTools.Daemon.Mcp;
using DevTools.Daemon.Mcp.Contracts;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Mcp.Core.Protocol;
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
        foreach (var name in new[] { "search_dynamic", "list_machines", "list_host_instances", "read_file_info" })
            Assert.AreEqual(McpTaskExecutionMode.Synchronous, TaskSelection.Select(CreateRequest(name)));
    }

    [TestMethod]
    public void Select_LaunchHost_IsOptional()
    {
        Assert.AreEqual(
            McpTaskExecutionMode.Optional,
            TaskSelection.Select(CreateRequest(McpSpecKeys.Tool.LaunchHost)));
    }

    [TestMethod]
    public void Select_InvokeDynamic_CatalogToolCall_IsOptional()
    {
        var id = new CatalogId(101, CatalogType.Tool, "demo_tool", "abcd1234").Encode();
        var mode = TaskSelection.Select(CreateRequest(
            McpSpecKeys.Tool.Invoke,
            new Dictionary<string, JsonElement> { [InvokeValidator.Argument.Id] = JsonSerializer.SerializeToElement(id) }));

        Assert.AreEqual(McpTaskExecutionMode.Optional, mode);
    }

    [TestMethod]
    public void Select_InvokeDynamic_ExecuteAndOpen_AreOptional()
    {
        foreach (var target in new[]
        {
            McpSpecKeys.Tool.ExecuteCSharp,
            McpSpecKeys.Tool.ExecutePython,
            McpSpecKeys.Tool.OpenDocument,
        })
        {
            var id = new CatalogId(101, CatalogType.Tool, target, "abcd1234").Encode();
            var mode = TaskSelection.Select(CreateRequest(
                McpSpecKeys.Tool.Invoke,
                new Dictionary<string, JsonElement> { [InvokeValidator.Argument.Id] = JsonSerializer.SerializeToElement(id) }));

            Assert.AreEqual(McpTaskExecutionMode.Optional, mode, target);
        }
    }

    [TestMethod]
    public void Select_InvokeDynamic_ResourceRead_IsSynchronous()
    {
        var id = new CatalogId(101, CatalogType.Resource, "revit://version", "abcd1234").Encode();
        var mode = TaskSelection.Select(CreateRequest(
            McpSpecKeys.Tool.Invoke,
            new Dictionary<string, JsonElement> { [InvokeValidator.Argument.Id] = JsonSerializer.SerializeToElement(id) }));

        Assert.AreEqual(McpTaskExecutionMode.Synchronous, mode);
    }

    [TestMethod]
    public void Select_InvokeDynamic_ReadsBatch_IsSynchronous()
    {
        var id = new CatalogId(101, CatalogType.Resource, "revit://version", "abcd1234").Encode();
        var mode = TaskSelection.Select(CreateRequest(
            McpSpecKeys.Tool.Invoke,
            new Dictionary<string, JsonElement>
            {
                [InvokeValidator.Argument.Reads] = JsonSerializer.SerializeToElement(new[]
                {
                    new { id },
                }),
            }));

        Assert.AreEqual(McpTaskExecutionMode.Synchronous, mode);
    }

    private static RequestContext<CallToolRequestParams> CreateRequest(
        string name,
        Dictionary<string, JsonElement>? arguments = null)
    {
        var server = new Mock<McpServer>();
        return new RequestContext<CallToolRequestParams>(
            server.Object,
            new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
            new CallToolRequestParams { Name = name, Arguments = arguments });
    }
}
