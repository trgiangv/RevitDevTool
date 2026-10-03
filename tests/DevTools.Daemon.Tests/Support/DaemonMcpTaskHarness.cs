#pragma warning disable MCPEXP001

using System.IO.Pipelines;
using System.Text.Json;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Gateway;
using DevTools.Daemon.Mcp;
using DevTools.Daemon.Mcp.Hosting;
using DevTools.Daemon.Mcp.Processes;
using DevTools.FileMetadata.Core;
using DevTools.Hosting;
using DevTools.Ipc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Daemon.Tests.Support;

internal static class DaemonTaskTestTools
{
    public const string ThrowsProtocolName = "daemon_task_protocol_fault";

    public static McpServerTool ThrowsProtocol() => McpServerTool.Create(
        ThrowProtocolFault,
        new McpServerToolCreateOptions
        {
            Name = ThrowsProtocolName,
            Description = "Test-only tool that throws McpProtocolException for task failure coverage.",
        });

    private static CallToolResult ThrowProtocolFault(CancellationToken cancellationToken) =>
        throw new McpProtocolException("daemon-task-protocol-fault", McpErrorCode.InvalidParams);
}

internal sealed class TaskTestProcessSessions : IProcessSessions
{
    public const int DefaultProcessId = 101;
    public const string DefaultToolName = "task_demo_tool";
    public const string SlowToolName = "task_slow_tool";
    public const string ErrorToolName = "task_error_tool";

    private readonly TaskTestProcessSession _session;

    public TaskTestProcessSessions()
    {
        _session = new TaskTestProcessSession(this, DefaultProcessId);
    }

    public ProcessCatalogs Catalog { get; } = new();
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public event Action? Changed { add { } remove { } }

    public void SeedDefaultCatalog()
    {
        Catalog.Replace(new ProcessCatalog
        {
            ProcessId = DefaultProcessId,
            Instance = _session.Info,
            PipeName = _session.PipeName,
            Tools =
            [
                new Tool { Name = DefaultToolName, Description = "demo", InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }) },
                new Tool { Name = SlowToolName, Description = "slow", InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }) },
                new Tool { Name = ErrorToolName, Description = "error", InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }) },
            ],
            Resources = [],
            ResourceTemplates = [],
        });
    }

    public IProcessSession? GetByProcessId(int processId) =>
        processId == DefaultProcessId ? _session : null;

    public Task RunAsync(CancellationToken ct) => Task.CompletedTask;

    internal Task<Result> InvokeHostToolAsync(string toolName, CancellationToken ct)
    {
        if (toolName == SlowToolName)
            return SlowAsync(ct);

        if (toolName == ErrorToolName)
        {
            return Task.FromResult<Result>(new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = "host tool failed" }],
            });
        }

        return Task.FromResult<Result>(new CallToolResult
        {
            Content = [new TextContentBlock { Text = $"called:{toolName}" }],
        });
    }

    private async Task<Result> SlowAsync(CancellationToken ct)
    {
        Started.TrySetResult();
        try
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = "should-not-complete" }],
            };
        }
        catch (OperationCanceledException)
        {
            CancellationObserved.TrySetResult();
            throw;
        }
    }
}

internal sealed class TaskTestProcessSession(TaskTestProcessSessions parent, int processId) : IProcessSession
{
    public int ProcessId => processId;
    public string PipeName { get; } = $"DevToolsMcp_Revit_2025_{processId}";
    public InstanceInfo Info { get; } = new() { HostApp = "Revit", ProcessId = processId, VersionNumber = "2025" };
    public bool IsConnected => true;

    public Task<Result> CallToolPassthroughAsync(CallToolRequestParams parameters, CancellationToken ct = default) =>
        parent.InvokeHostToolAsync(parameters.Name, ct);

    public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct = default) =>
        Task.FromResult(new ReadResourceResult { Contents = [new TextResourceContents { Uri = uri, Text = "ok" }] });

    public Task<ReadResourceResult> ReadResourceAsync(
        string uriTemplate,
        IDictionary<string, JsonElement> arguments,
        CancellationToken ct = default) =>
        Task.FromResult(new ReadResourceResult
        {
            Contents = [new TextResourceContents { Uri = uriTemplate, Text = "template" }],
        });

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class DaemonMcpTaskHarness : IAsyncDisposable
{
    private readonly CancellationTokenSource _serverCts;
    private readonly Pipe _clientToServer;
    private readonly Pipe _serverToClient;
    private readonly Task _serverTask;
    private readonly McpServer _server;
    private readonly ServiceProvider _serviceProvider;

    private DaemonMcpTaskHarness(
        ServiceProvider serviceProvider,
        McpClient client,
        McpServer server,
        Task serverTask,
        CancellationTokenSource serverCts,
        Pipe clientToServer,
        Pipe serverToClient,
        TaskTestProcessSessions sessions)
    {
        _serviceProvider = serviceProvider;
        Client = client;
        _server = server;
        _serverTask = serverTask;
        _serverCts = serverCts;
        _clientToServer = clientToServer;
        _serverToClient = serverToClient;
        Sessions = sessions;
        TaskStore = serviceProvider.GetRequiredService<IMcpTaskStore>();
    }

    public McpClient Client { get; }
    public IMcpTaskStore TaskStore { get; }
    public TaskTestProcessSessions Sessions { get; }

    public static async Task<DaemonMcpTaskHarness> StartAsync(
        CancellationToken cancellationToken,
        McpClientOptions? clientOptions = null,
        bool includeProtocolFaultTool = false)
    {
        var sessions = new TaskTestProcessSessions();
        sessions.SeedDefaultCatalog();

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        RegisterTasks(services, includeProtocolFaultTool);
        services.AddSingleton<IProcessSessions>(sessions);
        services.AddSingleton(Mock.Of<IMcpPipeScanner>());
        services.AddSingleton(Mock.Of<IHostLaunchService>());
        services.AddSingleton(Mock.Of<IAuthService>());
        services.AddSingleton(Options.Create(new GatewayOptions()));
        services.AddSingleton(Mock.Of<IFileReaderCatalog>());
        services.AddSingleton<McpEngine>();

        var serviceProvider = services.BuildServiceProvider();
        var engine = serviceProvider.GetRequiredService<McpEngine>();

        var tools = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var tool in engine.LocalTools)
            tools.TryAdd(tool);
        if (includeProtocolFaultTool)
            tools.TryAdd(DaemonTaskTestTools.ThrowsProtocol());

        var options = McpServerFactory.CreateOptions(tools, engine.PromptCollection, serviceProvider);
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        var transport = new StreamServerTransport(
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream(),
            "daemon-task-test",
            NullLoggerFactory.Instance);

        var serverCts = new CancellationTokenSource();
        var server = McpServer.Create(transport, options, NullLoggerFactory.Instance, serviceProvider);
        var serverTask = server.RunAsync(serverCts.Token);

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServer.Writer.AsStream(),
                serverToClient.Reader.AsStream(),
                NullLoggerFactory.Instance),
            clientOptions,
            NullLoggerFactory.Instance,
            cancellationToken);

        return new DaemonMcpTaskHarness(
            serviceProvider,
            client,
            server,
            serverTask,
            serverCts,
            clientToServer,
            serverToClient,
            sessions);
    }

    public async Task<string> GetToolIdAsync(string toolName)
    {
        var result = await Client.CallToolAsync(
            "search_dynamic",
            new Dictionary<string, object?> { ["query"] = toolName },
            cancellationToken: CancellationToken.None);

        var payload = JsonDocument.Parse(Text(result)).RootElement;
        return payload.GetProperty("items")[0].GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Missing id.");
    }

    public static string Text(CallToolResult result) =>
        string.Join('\n', result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    public static async Task<CallToolResult> PollCallToolResultAsync(McpClient client, string taskId, CancellationToken ct)
    {
        long pollIntervalMs = 1000;
        while (true)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(pollIntervalMs), ct).ConfigureAwait(false);
            var state = await client.GetTaskAsync(taskId, ct).ConfigureAwait(false);
            if (state.PollIntervalMs is { } updated)
                pollIntervalMs = updated;

            switch (state)
            {
                case CompletedTaskResult completed:
                    return completed.Result.Deserialize<CallToolResult>()
                        ?? throw new InvalidOperationException("Task completed without a CallToolResult.");
                case FailedTaskResult failed:
                    throw new InvalidOperationException($"Task failed: {failed.Error}");
                case CancelledTaskResult:
                    throw new InvalidOperationException("Task was cancelled.");
                case WorkingTaskResult:
                case InputRequiredTaskResult:
                    continue;
                default:
                    throw new InvalidOperationException($"Unexpected task state: {state.GetType().Name}");
            }
        }
    }

    public static async Task<GetTaskResult> PollTaskStateAsync(McpClient client, string taskId, CancellationToken ct)
    {
        long pollIntervalMs = 1000;
        while (true)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(pollIntervalMs), ct).ConfigureAwait(false);
            var state = await client.GetTaskAsync(taskId, ct).ConfigureAwait(false);
            if (state.PollIntervalMs is { } updated)
                pollIntervalMs = updated;

            if (state is WorkingTaskResult or InputRequiredTaskResult)
                continue;

            return state;
        }
    }

    private static void RegisterTasks(ServiceCollection services, bool includeProtocolFaultTool)
    {
        var taskStore = new InMemoryMcpTaskStore
        {
            DefaultTimeToLive = TimeSpan.FromMinutes(30),
            DefaultPollIntervalMs = 1000,
        };
        services.AddSingleton<IMcpTaskStore>(taskStore);
        services.AddMcpServer().WithTasks(taskStore, options =>
        {
            options.ExecutionModeSelector = request =>
            {
                if (includeProtocolFaultTool && request.Params?.Name == DaemonTaskTestTools.ThrowsProtocolName)
                    return McpTaskExecutionMode.Optional;
                return TaskSelection.Select(request);
            };
        });
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync().ConfigureAwait(false);
        await _serverCts.CancelAsync().ConfigureAwait(false);
        _clientToServer.Writer.Complete();
        _serverToClient.Writer.Complete();
        try { await _serverTask.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        await _server.DisposeAsync().ConfigureAwait(false);
        await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        _serverCts.Dispose();
    }
}
