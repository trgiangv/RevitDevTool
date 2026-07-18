using System.Text.Json;
using DevTools.Mcp;
using DevTools.Mcp.Models;
using DevTools.Mcp.Registry;
using DevTools.Settings;
using DevTools.Settings.Configs;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RevitDevTool.Server.Tests;

public sealed class McpCatalogIdentityTests
{
    [Theory]
    [InlineData("tool", "execute_csharp_code")]
    [InlineData("prompt", "revit_code")]
    [InlineData("resource", "revit://model/context")]
    public void DuplicateProtocolIdentity_IsRejected(string kind, string key)
    {
        var loader = new McpCatalogLoader(CreateDuplicateProviders(kind, key), NullLogger<McpCatalogLoader>.Instance);

        var result = loader.LoadCatalog([], []);
        var catalog = result.Catalog;

        Assert.Single(Items(catalog, kind, key));
        Assert.Contains(result.Snapshot.Diagnostics, diagnostic =>
            diagnostic.Code == "duplicate_primitive" && diagnostic.Kind == kind && diagnostic.Key == key);
    }

    [Fact]
    public void BuiltInToolName_ReservesTheProtocolIdentity()
    {
        var loader = new McpCatalogLoader(
        [
            Provider("a-dynamic", Tool("dynamic-tool", "execute_csharp_code", ExecutionMode.Dotnet)),
            Provider("z-built-in", Tool("built-in-tool", "execute_csharp_code", ExecutionMode.CSharp))
        ],
        NullLogger<McpCatalogLoader>.Instance);

        var catalog = loader.LoadCatalog([], []).Catalog;

        var tool = Assert.Single(catalog.Tools);
        Assert.Equal(ExecutionMode.CSharp, tool.Binding.SourceKind);
    }

    [Fact]
    public void EmptySuccessfulCatalog_IsLoadedOnce()
    {
        var loader = new McpCatalogLoader([], NullLogger<McpCatalogLoader>.Instance);
        McpServerPrimitiveCollection<McpServerTool> tools = [];
        McpServerPrimitiveCollection<McpServerPrompt> prompts = [];
        McpServerResourceCollection resources = [];
        var store = new McpCatalogStore(loader, new EmptySettingsService(), tools, prompts, resources);

        store.EnsureLoaded();
        var generation = store.Generation;
        store.EnsureLoaded();

        Assert.True(store.IsLoaded);
        Assert.Equal(1, generation);
        Assert.Equal(generation, store.Generation);
        Assert.Empty(tools);
        Assert.Empty(prompts);
        Assert.Empty(resources);
    }

    private static IReadOnlyList<IMcpRegistryProvider> CreateDuplicateProviders(string kind, string key) =>
    [
        kind switch
        {
            "tool" => Provider("first", Tool("first-tool", key, ExecutionMode.Dotnet)),
            "prompt" => Provider("first", Prompt("first-prompt", key, ExecutionMode.Dotnet)),
            "resource" => Provider("first", Resource("first-resource", key, ExecutionMode.Dotnet)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        },
        kind switch
        {
            "tool" => Provider("second", Tool("second-tool", key, ExecutionMode.Python)),
            "prompt" => Provider("second", Prompt("second-prompt", key, ExecutionMode.Python)),
            "resource" => Provider("second", Resource("second-resource", key, ExecutionMode.Python)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        }
    ];

    private static IEnumerable<object> Items(McpRegistryCatalog catalog, string kind, string key) => kind switch
    {
        "tool" => catalog.Tools.Where(item => item.ProtocolTool.Name == key),
        "prompt" => catalog.Prompts.Where(item => item.ProtocolPrompt.Name == key),
        "resource" => catalog.Resources.Where(item => item.ProtocolResource?.Uri == key),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static TestProvider Provider(string name, params object[] items) => new(name, new McpRegistryCatalog
    {
        Tools = items.OfType<McpRegisteredTool>().ToList(),
        Prompts = items.OfType<McpRegisteredPrompt>().ToList(),
        Resources = items.OfType<McpRegisteredResource>().ToList()
    });

    private static McpRegisteredTool Tool(string id, string name, ExecutionMode sourceKind) => new()
    {
        Id = id,
        ProtocolTool = new Tool { Name = name, InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }) },
        Binding = Binding(sourceKind, id)
    };

    private static McpRegisteredPrompt Prompt(string id, string name, ExecutionMode sourceKind) => new()
    {
        Id = id,
        ProtocolPrompt = new Prompt { Name = name },
        Binding = Binding(sourceKind, id)
    };

    private static McpRegisteredResource Resource(string id, string uri, ExecutionMode sourceKind) => new()
    {
        Id = id,
        ProtocolResource = new Resource { Uri = uri, Name = id },
        Binding = Binding(sourceKind, id)
    };

    private static McpPrimitiveBinding Binding(ExecutionMode sourceKind, string name) =>
        McpPrimitiveBinding.Create(sourceKind, null, "Tests", name, "Tests");

    private sealed class TestProvider(string name, McpRegistryCatalog catalog) : IMcpRegistryProvider
    {
        public string Name => name;
        public int Priority => name == "z-built-in" ? 0 : 100;
        public ExecutionMode SourceKind => ExecutionMode.Unsupported;
        public void ConfigurePaths(IReadOnlyList<string> paths) { }
        public McpRegistryCatalog LoadCatalog() => catalog;
    }

    private sealed class EmptySettingsService : ISettingsService
    {
        public GeneralConfig GeneralConfig => null!;
        public ExecutionConfig ExecutionConfig => null!;
        public McpRegistryConfig McpRegistryConfig { get; } = new();
        public LogConfig LogConfig => null!;
        public void SaveSettings() { }
        public void LoadSettings() { }
        public void ResetSettings() { }
    }
}
