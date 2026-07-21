using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.Registry;
using DevTools.Mcp.BuiltIn;
using DevTools.Mcp.Dispatch;
using DevTools.Mcp.Models;
using DevTools.Mcp.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RevitDevTool.Execution.Tests;

public sealed class McpExecutionDispatchTests
{
    [Fact]
    public async Task HostWrappedTool_InvokesExecutionTrackerWhenRegistered()
    {
        var tracker = new RecordingMcpExecutionTracker();
        using var services = BuildServices(tracker);
        await using var server = CreateServer(services);
        var tool = McpHostExecutionPrimitives.Wrap(
            new SuccessTool(),
            new HostContextMcpExecution(new NoOpHostContextExecutor()));

        await tool.InvokeAsync(
            CreateRequest(server, new CallToolRequestParams { Name = SuccessTool.ToolName }, RequestMethods.ToolsCall),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, tracker.BeginCount);
        Assert.Equal(1, tracker.MarkRunningCount);
        Assert.Equal(1, tracker.CompleteCount);
        Assert.Equal(1, tracker.RecordCallCount);
        Assert.Equal(SuccessTool.ToolName, tracker.LastToolName);
        Assert.Equal(ExecutionState.Completed, tracker.LastResult?.State);
    }

    [Fact]
    public async Task BuiltInGuardedTool_InvokesExecutionTrackerWhenRegistered()
    {
        var tracker = new RecordingMcpExecutionTracker();
        using var services = BuildServices(tracker);
        await using var server = CreateServer(services);
        var tool = BuiltInToolExecution.Wrap(new SuccessTool());

        await tool.InvokeAsync(
            CreateRequest(server, new CallToolRequestParams { Name = SuccessTool.ToolName }, RequestMethods.ToolsCall),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, tracker.BeginCount);
        Assert.Equal(1, tracker.MarkRunningCount);
        Assert.Equal(1, tracker.CompleteCount);
        Assert.Equal(1, tracker.RecordCallCount);
        Assert.Equal(SuccessTool.ToolName, tracker.LastToolName);
        Assert.Equal(ExecutionState.Completed, tracker.LastResult?.State);
    }

    private static ServiceProvider BuildServices(RecordingMcpExecutionTracker tracker) =>
        new ServiceCollection()
            .AddSingleton<IMcpExecutionTracker>(tracker)
            .AddSingleton<IMcpHostIdentity>(new TestHostIdentity("Revit"))
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .BuildServiceProvider();

    private static McpServer CreateServer(ServiceProvider services)
    {
        var input = new MemoryStream();
        var output = new MemoryStream();
        var transport = new StreamServerTransport(input, output);
        return McpServer.Create(transport, new McpServerOptions(), NullLoggerFactory.Instance, services);
    }

    private static RequestContext<T> CreateRequest<T>(McpServer server, T parameters, string method) =>
        new(server, new JsonRpcRequest { Id = new RequestId(Guid.NewGuid().ToString("N")), Method = method }, parameters);

    private sealed class SuccessTool : McpServerTool
    {
        public const string ToolName = "tracked_tool";

        public override Tool ProtocolTool { get; } = new() { Name = ToolName };
        public override IReadOnlyList<object> Metadata => [];

        public override ValueTask<CallToolResult> InvokeAsync(
            RequestContext<CallToolRequestParams> request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new CallToolResult
            {
                Content = [new TextContentBlock { Text = "ok" }]
            });
    }

    private sealed class NoOpHostContextExecutor : IHostContextExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<T> action, CancellationToken cancellationToken = default) =>
            Task.FromResult(action());

        public Task ExecuteAsync(Action action, CancellationToken cancellationToken = default)
        {
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMcpExecutionTracker : IMcpExecutionTracker
    {
        public int BeginCount { get; private set; }
        public int MarkRunningCount { get; private set; }
        public int CompleteCount { get; private set; }
        public int RecordCallCount { get; private set; }
        public string? LastToolName { get; private set; }
        public McpToolExecutionResult? LastResult { get; private set; }

        public IDisposable BeginExecution(string toolName)
        {
            BeginCount++;
            LastToolName = toolName;
            return new Scope();
        }

        public void MarkRunning(IDisposable scope)
        {
            if (scope is Scope)
                MarkRunningCount++;
        }

        public void Complete(IDisposable scope, McpToolExecutionResult result)
        {
            if (scope is Scope)
            {
                CompleteCount++;
                LastResult = result;
            }
        }

        public void RecordCall(string toolId, string toolName)
        {
            RecordCallCount++;
            LastToolName = toolName;
        }

        private sealed class Scope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class TestHostIdentity(string hostName) : IMcpHostIdentity
    {
        public string HostName { get; } = hostName;
    }
}
