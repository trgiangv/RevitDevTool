using DevTools.Mcp.Catalog.Tests.Harness;
using Microsoft.Extensions.Logging;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class McpCatalogLoaderTests
{
    [TestMethod]
    public void LoadCatalog_LogsOnlyWhenProviderAddsNewTools()
    {
        var builtIn = new StubProvider("built-in", ExecutionMode.CSharp, Tool("execute_csharp_code"));
        var emptyDotnet = new StubProvider("dotnet-mcp", ExecutionMode.Dotnet);
        var logger = new CapturingLogger<McpCatalogLoader>();
        var loader = new McpCatalogLoader([builtIn, emptyDotnet], logger);

        loader.LoadCatalog([], []);
        loader.LoadCatalog([], []);

        Assert.IsTrue(logger.Messages.Any(message => message.Contains("Provider 'built-in' added 1 tool(s)", StringComparison.Ordinal)));
        Assert.IsTrue(logger.Messages.Any(message => message.Contains("Tool store added 1 tool(s)", StringComparison.Ordinal)));
        Assert.IsFalse(logger.Messages.Any(message => message.Contains("dotnet-mcp", StringComparison.Ordinal)));
        Assert.AreEqual(2, logger.Messages.Count);

        builtIn.Catalog = CreateCatalog(Tool("execute_csharp_code"), Tool("execute_python_code"));
        logger.Messages.Clear();
        loader.LoadCatalog([], []);

        Assert.IsTrue(logger.Messages.Any(message => message.Contains("Provider 'built-in' added 1 tool(s)", StringComparison.Ordinal)));
        Assert.IsTrue(logger.Messages.Any(message => message.Contains("Tool store added 1 tool(s)", StringComparison.Ordinal)));
        Assert.IsTrue(logger.Messages.Any(message => message.Contains("total 2 tools", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void LoadCatalog_DoesNotLogEmptyProviders()
    {
        var logger = new CapturingLogger<McpCatalogLoader>();
        var loader = new McpCatalogLoader(
            [new StubProvider("dotnet-mcp", ExecutionMode.Dotnet), new StubProvider("python-mcp", ExecutionMode.Python)],
            logger);

        var catalog = loader.LoadCatalog([], []);

        Assert.IsEmpty(catalog.Tools);
        Assert.IsEmpty(logger.Messages);
    }

    [TestMethod]
    public void LoadCatalog_DropsDuplicateProtocolNamesInsteadOfChoosingFirstAtInvoke()
    {
        var first = new StubProvider("first", ExecutionMode.Dotnet, Tool("same_name"));
        var second = new StubProvider("second", ExecutionMode.Python, Tool("same_name"));
        var loader = new McpCatalogLoader([first, second], new CapturingLogger<McpCatalogLoader>());

        var catalog = loader.LoadCatalog([], []);

        Assert.HasCount(1, catalog.Tools);
        Assert.AreEqual(ExecutionMode.Dotnet, catalog.Tools[0].Binding.SourceKind);
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
