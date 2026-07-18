using System.Text.Json;
using DevTools.Mcp;
using DevTools.Mcp.Models;
using DevTools.Mcp.Registry;
using DevTools.Presentation.ViewModels;
using DevTools.Execution.External.Mcp.Registry;
using DevTools.Execution.External.Connections;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResourceUriCollisions_AreRejectedAcrossDirectAndTemplateResources(bool firstIsTemplate)
    {
        const string uri = "revit://model/{id}";
        var loader = new McpCatalogLoader(
        [
            Provider("first", firstIsTemplate ? TemplateResource("first-resource", uri, ExecutionMode.Dotnet) : Resource("first-resource", uri, ExecutionMode.Dotnet)),
            Provider("second", firstIsTemplate ? Resource("second-resource", uri, ExecutionMode.Python) : TemplateResource("second-resource", uri, ExecutionMode.Python))
        ],
        NullLogger<McpCatalogLoader>.Instance);

        var result = loader.LoadCatalog([], []);

        Assert.Single(result.Catalog.Resources);
        Assert.Contains(result.Snapshot.Diagnostics, diagnostic =>
            diagnostic.Code == "duplicate_primitive" && diagnostic.Kind == "resource" && diagnostic.Key == uri);
    }

    [Fact]
    public void TemplateResourceUriCollisions_AreRejected()
    {
        const string uri = "revit://model/{id}";
        var loader = new McpCatalogLoader(
        [
            Provider("first", TemplateResource("first-resource", uri, ExecutionMode.Dotnet)),
            Provider("second", TemplateResource("second-resource", uri, ExecutionMode.Python))
        ],
        NullLogger<McpCatalogLoader>.Instance);

        var result = loader.LoadCatalog([], []);

        Assert.Single(result.Catalog.Resources);
        Assert.Contains(result.Snapshot.Diagnostics, diagnostic =>
            diagnostic.Code == "duplicate_primitive" && diagnostic.Kind == "resource" && diagnostic.Key == uri);
    }

    [Fact]
    public void CatalogDiagnostics_AreOperatorVisibleInTheRegistryViewModel()
    {
        var loader = new McpCatalogLoader(
        [
            Provider("first", Tool("first", "duplicate", ExecutionMode.Dotnet)),
            Provider("second", Tool("second", "duplicate", ExecutionMode.Python)),
            new ThrowingProvider("broken")
        ],
        NullLogger<McpCatalogLoader>.Instance);
        McpServerPrimitiveCollection<McpServerTool> tools = [];
        McpServerPrimitiveCollection<McpServerPrompt> prompts = [];
        McpServerResourceCollection resources = [];
        var store = new McpCatalogStore(loader, new EmptySettingsService(), tools, prompts, resources);
        using var viewModel = new McpRegistryViewModel(store, new(NullLogger<ConnectionState>.Instance));

        store.EnsureLoaded();
        typeof(McpRegistryViewModel)
            .GetMethod("OnRegistryChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(viewModel, [store, EventArgs.Empty]);

        Assert.Contains(viewModel.Diagnostics, diagnostic => diagnostic.Code == "duplicate_primitive");
        Assert.Contains(viewModel.Diagnostics, diagnostic => diagnostic.Code == "provider_load_failed");
        Assert.True(viewModel.HasDiagnostics);
    }

    [Fact]
    public void NonEmptySnapshot_ReplacesAllSdkPrimitiveCollections()
    {
        var adapter = new TestPrimitiveAdapter();
        var loader = new McpCatalogLoader(
        [Provider("test", Tool("tool", "test_tool", ExecutionMode.CSharp), Prompt("prompt", "test_prompt", ExecutionMode.CSharp), Resource("resource", "revit://test/resource", ExecutionMode.CSharp))],
        NullLogger<McpCatalogLoader>.Instance,
        [adapter]);
        McpServerPrimitiveCollection<McpServerTool> tools = [];
        McpServerPrimitiveCollection<McpServerPrompt> prompts = [];
        McpServerResourceCollection resources = [];
        var store = new McpCatalogStore(loader, new EmptySettingsService(), tools, prompts, resources);

        store.EnsureLoaded();

        Assert.Equal("test_tool", Assert.Single(tools).ProtocolTool.Name);
        Assert.Equal("test_prompt", Assert.Single(prompts).ProtocolPrompt.Name);
        Assert.Equal("revit://test/resource", Assert.Single(resources).ProtocolResource!.Uri);
        Assert.Equal(1, store.Generation);
    }

    [Fact]
    public async Task StandardSdkPrimitives_UseHostContextWithSuppressedExecutionGuard()
    {
        var hostContext = new RecordingHostContextExecutor();
        var execution = new HostContextMcpExecution(hostContext);
        var observedModes = new List<ExecutionGuardMode>();
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StreamServerTransport(input, output);
        await using var server = McpServer.Create(transport, new McpServerOptions());
        var tool = McpHostExecutionPrimitives.Wrap(new ProbeTool(observedModes), execution);
        var prompt = McpHostExecutionPrimitives.Wrap(new ProbePrompt(observedModes), execution);
        var resource = McpHostExecutionPrimitives.Wrap(new ProbeResource(observedModes), execution);

        await tool.InvokeAsync(CreateRequest(server, new CallToolRequestParams { Name = "probe_tool" }, RequestMethods.ToolsCall), TestContext.Current.CancellationToken);
        await prompt.GetAsync(CreateRequest(server, new GetPromptRequestParams { Name = "probe_prompt" }, RequestMethods.PromptsGet), TestContext.Current.CancellationToken);
        await resource.ReadAsync(CreateRequest(server, new ReadResourceRequestParams { Uri = "revit://probe" }, RequestMethods.ResourcesRead), TestContext.Current.CancellationToken);

        Assert.Equal(3, hostContext.CallCount);
        Assert.All(observedModes, mode => Assert.Equal(ExecutionGuardMode.Suppress, mode));
        Assert.Equal(ExecutionGuardMode.Passthrough, ExecutionGuardContext.Mode);
    }

    private static RequestContext<T> CreateRequest<T>(McpServer server, T parameters, string method) =>
        new(server, new JsonRpcRequest { Id = new RequestId(Guid.NewGuid().ToString("N")), Method = method }, parameters);

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

    private static McpRegisteredResource TemplateResource(string id, string uri, ExecutionMode sourceKind) => new()
    {
        Id = id,
        ProtocolTemplate = new ResourceTemplate { UriTemplate = uri, Name = id },
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

    private sealed class ThrowingProvider(string name) : IMcpRegistryProvider
    {
        public string Name => name;
        public int Priority => 300;
        public ExecutionMode SourceKind => ExecutionMode.Unsupported;
        public void ConfigurePaths(IReadOnlyList<string> paths) { }
        public McpRegistryCatalog LoadCatalog() => throw new InvalidOperationException("Test provider failure.");
    }

    private sealed class TestPrimitiveAdapter : IMcpServerPrimitiveAdapter
    {
        public ExecutionMode SourceKind => ExecutionMode.CSharp;
        public McpServerTool? CreateTool(McpRegisteredTool registration) => new ProbeTool([]) { ProtocolToolValue = registration.ProtocolTool };
        public McpServerPrompt? CreatePrompt(McpRegisteredPrompt registration) => new ProbePrompt([]) { ProtocolPromptValue = registration.ProtocolPrompt };
        public McpServerResource? CreateResource(McpRegisteredResource registration) => new ProbeResource([]) { ProtocolResourceValue = registration.ProtocolResource! };
    }

    private sealed class ProbeTool(List<ExecutionGuardMode> observedModes) : McpServerTool
    {
        public Tool ProtocolToolValue { get; init; } = new() { Name = "probe_tool", InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }) };
        public override Tool ProtocolTool => ProtocolToolValue;
        public override IReadOnlyList<object> Metadata => [];
        public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
        {
            observedModes.Add(ExecutionGuardContext.Mode);
            return ValueTask.FromResult(new CallToolResult());
        }
    }

    private sealed class ProbePrompt(List<ExecutionGuardMode> observedModes) : McpServerPrompt
    {
        public Prompt ProtocolPromptValue { get; init; } = new() { Name = "probe_prompt" };
        public override Prompt ProtocolPrompt => ProtocolPromptValue;
        public override IReadOnlyList<object> Metadata => [];
        public override ValueTask<GetPromptResult> GetAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken = default)
        {
            observedModes.Add(ExecutionGuardContext.Mode);
            return ValueTask.FromResult(new GetPromptResult());
        }
    }

    private sealed class ProbeResource(List<ExecutionGuardMode> observedModes) : McpServerResource
    {
        public Resource ProtocolResourceValue { get; init; } = new() { Uri = "revit://probe", Name = "probe_resource" };
        public override Resource? ProtocolResource => ProtocolResourceValue;
        public override ResourceTemplate ProtocolResourceTemplate => new() { UriTemplate = ProtocolResourceValue.Uri, Name = ProtocolResourceValue.Name };
        public override IReadOnlyList<object> Metadata => [];
        public override bool IsMatch(string uri) => string.Equals(uri, ProtocolResourceValue.Uri, StringComparison.OrdinalIgnoreCase);
        public override ValueTask<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken = default)
        {
            observedModes.Add(ExecutionGuardContext.Mode);
            return ValueTask.FromResult(new ReadResourceResult());
        }
    }

    private sealed class RecordingHostContextExecutor : IHostContextExecutor
    {
        public int CallCount { get; private set; }
        public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(handler());
        }

        public Task ExecuteAsync(Action action, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            CallCount++;
            action();
            return Task.CompletedTask;
        }
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
