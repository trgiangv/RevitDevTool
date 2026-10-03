using DevTools.Execution.Abstractions;
using DevTools.Mcp;
using DevTools.Mcp.Hosting;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;
using DevTools.Settings;
using DevTools.Settings.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Mcp.Adapter.Tests.Harness;

internal static class McpHostTestHarness
{
    public static (McpServerOptions Options, IMcpSource Source, McpCatalogStore CatalogStore, ServiceProvider Services)
        CreateServerOptionsWithTool(
            string toolName,
            string responseText = "pong",
            string? description = null)
    {
        var catalog = new RegistryCatalog
        {
            Tools = [CreateRegisteredTool(toolName, description)],
            Resources = [],
        };

        var loader = new Mock<ICatalogLoader>();
        loader
            .Setup(l => l.LoadCatalog(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(catalog);

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.McpRegistryConfig).Returns(new McpRegistryConfig());

        var catalogStore = new McpCatalogStore(loader.Object, settings.Object);
        var source = new FixedTextToolSource(responseText);

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddMcp();
        services.AddSingleton<IHostContextExecutor>(new InlineHostContextExecutor());
        var provider = services.BuildServiceProvider();

        var options = McpServerCollections.CreateOptions(
            catalogStore,
            [source],
            provider.GetRequiredService<IHostContextExecutor>(),
            provider);

        return (options, source, catalogStore, provider);
    }

    public static RegisteredTool CreateRegisteredTool(string name, string? description = null) => new()
    {
        Id = name,
        Descriptor = new Tool
        {
            Name = name,
            Description = description ?? $"{name} description",
            InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }),
        },
        Binding = PrimitiveBinding.Create(ExecutionMode.Dotnet, "stub.dll", "Stub", name, "", ""),
    };

    private sealed class FixedTextToolSource(string responseText) : IMcpSource
    {
        public ExecutionMode SourceKind => ExecutionMode.Dotnet;

        public McpServerTool CreateTool(RegisteredTool tool, IHostContextExecutor hostContext) =>
            SdkCollectionTool.Create(
                tool.Descriptor,
                (_, _) => Task.FromResult(new CallToolResult
                {
                    Content = [new TextContentBlock { Text = responseText }],
                }));

        public McpServerResource CreateResource(RegisteredResource resource, IHostContextExecutor hostContext) =>
            SdkCollectionResource.Create(resource, hostContext, (_, _) => Task.FromResult(new ReadResourceResult()));

        public void ClearCaches()
        {
        }
    }

    private sealed class InlineHostContextExecutor : IHostContextExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default) =>
            Task.FromResult(handler());

        public Task ExecuteAsync(Action action, CancellationToken token = default)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
