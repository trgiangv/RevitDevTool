using DevTools.Mcp.Tests.Harness;
using Microsoft.Extensions.Logging;

namespace DevTools.Mcp.Tests;

public sealed class McpCatalogLoaderTests
{
    [Fact]
    public void LoadCatalog_LogsOnlyWhenProviderAddsNewTools()
    {
        var builtIn = new StubProvider("built-in", ExecutionMode.CSharp, Tool("execute_csharp_code"));
        var emptyDotnet = new StubProvider("dotnet-mcp", ExecutionMode.Dotnet);
        var logger = new CapturingLogger<McpCatalogLoader>();
        var loader = new McpCatalogLoader([builtIn, emptyDotnet], logger);

        loader.LoadCatalog([], []);
        loader.LoadCatalog([], []);

        Assert.Contains(logger.Messages, message => message.Contains("Provider 'built-in' added 1 tool(s)", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("Tool store added 1 tool(s)", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("dotnet-mcp", StringComparison.Ordinal));
        Assert.Equal(2, logger.Messages.Count);

        builtIn.Catalog = CreateCatalog(Tool("execute_csharp_code"), Tool("execute_python_code"));
        logger.Messages.Clear();
        loader.LoadCatalog([], []);

        Assert.Contains(logger.Messages, message => message.Contains("Provider 'built-in' added 1 tool(s)", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("Tool store added 1 tool(s)", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("total 2 tools", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadCatalog_DoesNotLogEmptyProviders()
    {
        var logger = new CapturingLogger<McpCatalogLoader>();
        var loader = new McpCatalogLoader(
            [new StubProvider("dotnet-mcp", ExecutionMode.Dotnet), new StubProvider("python-mcp", ExecutionMode.Python)],
            logger);

        var catalog = loader.LoadCatalog([], []);

        Assert.Empty(catalog.Tools);
        Assert.Empty(logger.Messages);
    }

    private static McpRegistryCatalog CreateCatalog(params McpRegisteredTool[] tools) => new()
    {
        Tools = tools,
        Resources = [],
    };

    private static McpRegisteredTool Tool(string name) => McpHostTestHarness.CreateRegisteredTool(name);

    private sealed class StubProvider(string name, ExecutionMode sourceKind, params McpRegisteredTool[] tools) : IMcpRegistryProvider
    {
        public string Name { get; } = name;
        public ExecutionMode SourceKind { get; } = sourceKind;
        public McpRegistryCatalog Catalog { get; set; } = CreateCatalog(tools);
        public void ConfigurePaths(IReadOnlyList<string> paths) { }
        public McpRegistryCatalog LoadCatalog() => Catalog;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
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
}
