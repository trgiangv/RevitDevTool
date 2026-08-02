using System.Text.Json;
using DevTools.Mcp.Server.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Mcp.Tests;

public class HostCallLoggingFilterTests
{
    [Fact]
    public async Task CallToolFilter_LogsToolsCallWithArgsAndResult()
    {
        var logger = new CapturingCategoryLogger("DevTools.Mcp.ToolCall");
        var factory = new SingleCategoryLoggerFactory(logger);
        var options = new McpServerOptions();
        McpLogFilters.Attach(options, factory);

        Assert.NotEmpty(options.Filters.Request.CallToolFilters);

        McpRequestHandler<CallToolRequestParams, CallToolResult> terminal = (_, _) =>
            new ValueTask<CallToolResult>(new CallToolResult
            {
                Content = [new TextContentBlock { Text = "pong" }]
            });

        var handler = options.Filters.Request.CallToolFilters[0](terminal);
        await handler(
            CreateCallToolRequest("echo", new Dictionary<string, JsonElement>
            {
                ["message"] = JsonSerializer.SerializeToElement("hi")
            }),
            TestContext.Current.CancellationToken);

        var log = Assert.Single(logger.Messages);
        Assert.Contains("tools/call", log, StringComparison.Ordinal);
        Assert.Contains(" ok ", log, StringComparison.Ordinal);
        Assert.Contains("target=echo", log, StringComparison.Ordinal);
        Assert.Contains("durationMs=", log, StringComparison.Ordinal);
        Assert.Contains("args=", log, StringComparison.Ordinal);
        Assert.Contains("\"message\":\"hi\"", log, StringComparison.Ordinal);
        Assert.Contains("result=", log, StringComparison.Ordinal);
        Assert.Contains("pong", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadResourceFilter_LogsResourcesReadWithResult()
    {
        var logger = new CapturingCategoryLogger("DevTools.Mcp.ResourceRead");
        var factory = new SingleCategoryLoggerFactory(logger);
        var options = new McpServerOptions();
        McpLogFilters.Attach(options, factory);

        Assert.NotEmpty(options.Filters.Request.ReadResourceFilters);

        McpRequestHandler<ReadResourceRequestParams, ReadResourceResult> terminal = (_, _) =>
            new ValueTask<ReadResourceResult>(new ReadResourceResult
            {
                Contents = [new TextResourceContents { Uri = "revit://model/context", Text = "ok", MimeType = "text/plain" }]
            });

        var handler = options.Filters.Request.ReadResourceFilters[0](terminal);
        await handler(
            CreateReadResourceRequest("revit://model/context"),
            TestContext.Current.CancellationToken);

        var log = Assert.Single(logger.Messages);
        Assert.Contains("resources/read", log, StringComparison.Ordinal);
        Assert.Contains(" ok ", log, StringComparison.Ordinal);
        Assert.Contains("target=revit://model/context", log, StringComparison.Ordinal);
        Assert.Contains("durationMs=", log, StringComparison.Ordinal);
        Assert.Contains("result=", log, StringComparison.Ordinal);
    }

    private static RequestContext<CallToolRequestParams> CreateCallToolRequest(
        string name,
        Dictionary<string, JsonElement> args) =>
        new(
            new Mock<McpServer>().Object,
            new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
            new CallToolRequestParams
            {
                Name = name,
                Arguments = args
            });

    private static RequestContext<ReadResourceRequestParams> CreateReadResourceRequest(string uri) =>
        new(
            new Mock<McpServer>().Object,
            new JsonRpcRequest { Method = "resources/read", Id = new RequestId("1") },
            new ReadResourceRequestParams { Uri = uri });

    private sealed class SingleCategoryLoggerFactory(CapturingCategoryLogger logger) : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName) =>
            string.Equals(categoryName, logger.Category, StringComparison.Ordinal) ? logger : new NullLogger();
        public void Dispose() { }
    }

    private sealed class CapturingCategoryLogger(string category) : ILogger
    {
        public string Category { get; } = category;
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class NullLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
