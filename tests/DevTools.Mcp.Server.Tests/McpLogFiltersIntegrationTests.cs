using System.IO.Pipelines;
using DevTools.Mcp.Server.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Tests;

public sealed class McpLogFiltersIntegrationTests
{
    [Fact]
    public async Task CallToolFilters_LogSuccessErrorAndExceptions()
    {
        var logger = new ListLogger();
        var options = CreateOptions(logger);
        await using var harness = await InMemoryHarness.StartAsync(options, TestContext.Current.CancellationToken);

        var ok = await harness.Client.CallToolAsync("ok", cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEqual(true, ok.IsError);

        var err = await harness.Client.CallToolAsync("fail", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(err.IsError == true);

        var boom = await harness.Client.CallToolAsync("boom", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(boom.IsError == true);

        Assert.Contains(logger.Entries, entry => entry.Contains("tools/call ok target=ok", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Contains("tools/call error target=fail", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Contains("tools/call error target=boom", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadResourceFilters_LogSuccess()
    {
        var logger = new ListLogger();
        var options = CreateOptions(logger);
        await using var harness = await InMemoryHarness.StartAsync(options, TestContext.Current.CancellationToken);

        var result = await harness.Client.ReadResourceAsync("demo://item", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(result.Contents.OfType<TextResourceContents>(), c => c.Text == "payload");

        Assert.Contains(logger.Entries, entry => entry.Contains("resources/read ok target=demo://item", StringComparison.Ordinal));
    }

    private static McpServerOptions CreateOptions(ListLogger logger)
    {
        var tools = new McpServerPrimitiveCollection<McpServerTool>
        {
            McpServerTool.Create(
                () => "ok",
                new McpServerToolCreateOptions { Name = "ok" }),
            McpServerTool.Create(
                () => new CallToolResult
                {
                    IsError = true,
                    Content = [new TextContentBlock { Text = "nope" }],
                },
                new McpServerToolCreateOptions { Name = "fail" }),
            McpServerTool.Create(
                () =>
                {
                    throw new InvalidOperationException("boom");
#pragma warning disable CS0162
                    return "";
#pragma warning restore CS0162
                },
                new McpServerToolCreateOptions { Name = "boom" }),
        };

        var resources = new McpServerResourceCollection
        {
            McpServerResource.Create(
                () => new TextResourceContents { Uri = "demo://item", Text = "payload", MimeType = "text/plain" },
                new McpServerResourceCreateOptions { UriTemplate = "demo://item" }),
        };

        var options = new McpServerOptions
        {
            ServerInfo = new Implementation { Name = "log-filter-host", Version = "1.0.0" },
            ToolCollection = tools,
            ResourceCollection = resources,
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability(),
                Resources = new ResourcesCapability(),
            },
        };

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(builder => builder.AddProvider(new ListLoggerProvider(logger))));
        using var provider = services.BuildServiceProvider();
        McpServerConfigurator.Apply(options, provider);
        return options;
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(formatter(state, exception));
    }

    private sealed class ListLoggerProvider(ListLogger logger) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => logger;
        public void Dispose() { }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }

    private sealed class InMemoryHarness : IAsyncDisposable
    {
        private readonly Pipe _clientToServer;
        private readonly Pipe _serverToClient;
        private readonly CancellationTokenSource _cts;
        private readonly Task _serverTask;
        private readonly McpServer _server;

        private InMemoryHarness(McpClient client, McpServer server, Task serverTask, Pipe clientToServer, Pipe serverToClient, CancellationTokenSource cts)
        {
            Client = client;
            _server = server;
            _serverTask = serverTask;
            _clientToServer = clientToServer;
            _serverToClient = serverToClient;
            _cts = cts;
        }

        public McpClient Client { get; }

        public static async Task<InMemoryHarness> StartAsync(McpServerOptions options, CancellationToken cancellationToken)
        {
            var clientToServer = new Pipe();
            var serverToClient = new Pipe();
            var cts = new CancellationTokenSource();

            var transport = new StreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream(),
                "log-filter-server",
                NullLoggerFactory.Instance);
            var server = McpServer.Create(transport, options, NullLoggerFactory.Instance, TestMcpAppServices.Create());
            var serverTask = server.RunAsync(cts.Token);

            var client = await McpClient.CreateAsync(
                new StreamClientTransport(
                    clientToServer.Writer.AsStream(),
                    serverToClient.Reader.AsStream(),
                    NullLoggerFactory.Instance),
                loggerFactory: NullLoggerFactory.Instance,
                cancellationToken: cancellationToken);

            return new InMemoryHarness(client, server, serverTask, clientToServer, serverToClient, cts);
        }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await _cts.CancelAsync();
            _clientToServer.Writer.Complete();
            _serverToClient.Writer.Complete();
            try { await _serverTask; } catch { /* ignored */ }
            await _server.DisposeAsync();
            _cts.Dispose();
        }
    }
}
