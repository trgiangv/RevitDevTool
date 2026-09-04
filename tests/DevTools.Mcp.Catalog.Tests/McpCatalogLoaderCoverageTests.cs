using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Catalog.Tests.Harness;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Moq;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class McpCatalogLoaderCoverageTests
{
    [Fact]
    public void LoadCatalog_Continues_WhenProviderThrows()
    {
        var healthy = new StubProvider("built-in", ExecutionMode.CSharp, McpHostTestHarness.CreateRegisteredTool("healthy_tool"));
        var failing = new ThrowingProvider("dotnet-mcp", ExecutionMode.Dotnet);
        var logger = new CapturingLogger<McpCatalogLoader>();
        var loader = new McpCatalogLoader([healthy, failing], logger);

        var catalog = loader.LoadCatalog([], []);

        Assert.Single(catalog.Tools);
        Assert.Contains(logger.Messages, message => message.Contains("dotnet-mcp", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("boom", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadCatalog_SkipsItemsWithEmptyNames()
    {
        var provider = new StubProvider("built-in", ExecutionMode.CSharp, ToolWithEmptyName());
        var logger = new CapturingLogger<McpCatalogLoader>();
        var loader = new McpCatalogLoader([provider], logger);

        var catalog = loader.LoadCatalog([], []);

        Assert.Empty(catalog.Tools);
        Assert.Contains(logger.Messages, message => message.Contains("empty name", StringComparison.Ordinal));
    }

    private static McpRegisteredTool ToolWithEmptyName() => new()
    {
        Id = "empty",
        Descriptor = new Tool
        {
            Name = "",
            InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }),
        },
        Binding = McpPrimitiveBinding.Create(ExecutionMode.CSharp, "", "BuiltIn", ""),
    };

    private sealed class ThrowingProvider(string name, ExecutionMode sourceKind) : IMcpRegistryProvider
    {
        public string Name { get; } = name;
        public ExecutionMode SourceKind { get; } = sourceKind;
        public void ConfigurePaths(IReadOnlyList<string> paths) { }
        public McpRegistryCatalog LoadCatalog() => throw new InvalidOperationException("boom");
    }

    private sealed class StubProvider(string name, ExecutionMode sourceKind, params McpRegisteredTool[] tools) : IMcpRegistryProvider
    {
        public string Name { get; } = name;
        public ExecutionMode SourceKind { get; } = sourceKind;
        public void ConfigurePaths(IReadOnlyList<string> paths) { }
        public McpRegistryCatalog LoadCatalog() => new() { Tools = tools, Resources = [] };
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
