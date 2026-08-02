using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

/// <summary>Live host-pipe checks (requires deployed host with tasks/get handlers).</summary>
public sealed class HostTasksLiveIntegrationTests
{
    [Fact]
    public async Task LiveHost_ExecuteCsharp_SyncAndTasksOptIn()
    {
        var pipeName = DiscoverRevitMcpPipe();
        if (pipeName is null)
        {
            Assert.Fail("No DevToolsMcp_Revit_* pipe found. Launch Revit with DevTools loaded.");
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(90));

        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(cts.Token);

        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(pipe, pipe, NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: cts.Token);

        const string code =
            "using System;\nusing Autodesk.Revit.UI;\nusing Autodesk.Revit.DB;\nusing Autodesk.Revit.Attributes;\n\n[Transaction(TransactionMode.ReadOnly)]\npublic class TasksLiveTest : IExternalCommand\n{\n    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)\n    {\n        message = \"tasks-live-ok\";\n        return Result.Succeeded;\n    }\n}";

        var arguments = new Dictionary<string, JsonElement>
        {
            ["code"] = JsonSerializer.SerializeToElement(code)
        };

        var request = new CallToolRequestParams { Name = "execute_csharp_code", Arguments = arguments };

        var syncResult = await client.CallToolAsync(request, cancellationToken: cts.Token);
        Assert.NotEqual(true, syncResult.IsError);
        Assert.Contains("tasks-live-ok", Text(syncResult), StringComparison.Ordinal);

        var taskOrResult = await client.CallToolAsTaskAsync(request, cancellationToken: cts.Token);

        if (taskOrResult.IsTask)
        {
            var polled = await client.CallToolWithPollingAsync(request, cancellationToken: cts.Token);
            Assert.NotEqual(true, polled.IsError);
            Assert.Contains("tasks-live-ok", Text(polled), StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("tasks-live-ok", Text(taskOrResult.Result!), StringComparison.Ordinal);
        }
    }

    private static string? DiscoverRevitMcpPipe()
    {
        foreach (var pipe in Directory.GetFiles(@"\\.\pipe\"))
        {
            var name = Path.GetFileName(pipe);
            if (name.StartsWith("DevToolsMcp_Revit_", StringComparison.OrdinalIgnoreCase))
                return name;
        }

        return null;
    }

    private static string Text(CallToolResult result) =>
        string.Join('\n', result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
