using System.Text.Json;
using System.Threading.Tasks.Sources;
using DevTools.Mcp;
using DevTools.Mcp.BuiltIn;
using DevTools.Mcp.Models;
using DevTools.Mcp.Registry;
using DevTools.Presentation.ViewModels;
using DevTools.Execution.External.Mcp.Registry;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.External.Connections;
using DevTools.Execution.Interfaces;
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
    public void SdkEqualNormalizedDirectResourceUris_AreRejected()
    {
        const string firstUri = "revit://model/%7Econtext";
        const string secondUri = "revit://model/~context";
        var loader = new McpCatalogLoader(
        [
            Provider("first", Resource("first-resource", firstUri, ExecutionMode.Dotnet)),
            Provider("second", Resource("second-resource", secondUri, ExecutionMode.Python))
        ],
        NullLogger<McpCatalogLoader>.Instance);

        var result = loader.LoadCatalog([], []);

        Assert.Single(result.Catalog.Resources);
        Assert.Contains(result.Snapshot.Diagnostics, diagnostic =>
            diagnostic.Code == "duplicate_primitive" && diagnostic.Kind == "resource" && diagnostic.Key == secondUri);
    }

    [Fact]
    public void SdkDistinctDirectResourcePathCase_IsAccepted()
    {
        const string upperPathUri = "revit://model/Context";
        const string lowerPathUri = "revit://model/context";
        var loader = new McpCatalogLoader(
        [
            Provider("first", Resource("upper-resource", upperPathUri, ExecutionMode.CSharp)),
            Provider("second", Resource("lower-resource", lowerPathUri, ExecutionMode.CSharp))
        ],
        NullLogger<McpCatalogLoader>.Instance,
        [new TestPrimitiveAdapter()]);
        McpServerPrimitiveCollection<McpServerTool> tools = [];
        McpServerPrimitiveCollection<McpServerPrompt> prompts = [];
        McpServerResourceCollection resources = [];
        var store = new McpCatalogStore(loader, new EmptySettingsService(), tools, prompts, resources);

        store.EnsureLoaded();

        Assert.Equal(2, store.ResourceCatalog.Count);
        Assert.Equal(2, resources.Count);
        Assert.Contains(resources, resource => resource.ProtocolResource!.Uri == upperPathUri);
        Assert.Contains(resources, resource => resource.ProtocolResource!.Uri == lowerPathUri);
    }

    [Fact]
    public void TemplateResourceLiteralCase_IsAccepted()
    {
        const string upperTemplate = "revit://model/{Id}";
        const string lowerTemplate = "revit://model/{id}";
        var loader = new McpCatalogLoader(
        [
            Provider("first", TemplateResource("upper-template", upperTemplate, ExecutionMode.Dotnet)),
            Provider("second", TemplateResource("lower-template", lowerTemplate, ExecutionMode.Python))
        ],
        NullLogger<McpCatalogLoader>.Instance);

        var result = loader.LoadCatalog([], []);

        Assert.Equal(2, result.Catalog.Resources.Count);
        Assert.DoesNotContain(result.Snapshot.Diagnostics, diagnostic => diagnostic.Code == "duplicate_primitive");
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

    [Fact]
    public async Task DelayedTool_IsRejectedBeforeItCanRegisterAContinuationOutsideHostScope()
    {
        var hostContext = new RecordingHostContextExecutor();
        var delayed = new DelayedTool();
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StreamServerTransport(input, output);
        await using var server = McpServer.Create(transport, new McpServerOptions());
        var tool = McpHostExecutionPrimitives.Wrap(delayed, new HostContextMcpExecution(hostContext));

        await AssertIncompleteInvocationIsRejected(
            tool.InvokeAsync(CreateRequest(server, new CallToolRequestParams { Name = "delayed_tool" }, RequestMethods.ToolsCall), TestContext.Current.CancellationToken).AsTask(),
            delayed.Completion,
            hostContext);
    }

    [Fact]
    public async Task DelayedPrompt_IsRejectedBeforeItCanRegisterAContinuationOutsideHostScope()
    {
        var hostContext = new RecordingHostContextExecutor();
        var delayed = new DelayedPrompt();
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StreamServerTransport(input, output);
        await using var server = McpServer.Create(transport, new McpServerOptions());
        var prompt = McpHostExecutionPrimitives.Wrap(delayed, new HostContextMcpExecution(hostContext));

        await AssertIncompleteInvocationIsRejected(
            prompt.GetAsync(CreateRequest(server, new GetPromptRequestParams { Name = "delayed_prompt" }, RequestMethods.PromptsGet), TestContext.Current.CancellationToken).AsTask(),
            delayed.Completion,
            hostContext);
    }

    [Fact]
    public async Task DelayedResource_IsRejectedBeforeItCanRegisterAContinuationOutsideHostScope()
    {
        var hostContext = new RecordingHostContextExecutor();
        var delayed = new DelayedResource();
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StreamServerTransport(input, output);
        await using var server = McpServer.Create(transport, new McpServerOptions());
        var resource = McpHostExecutionPrimitives.Wrap(delayed, new HostContextMcpExecution(hostContext));

        await AssertIncompleteInvocationIsRejected(
            resource.ReadAsync(CreateRequest(server, new ReadResourceRequestParams { Uri = "revit://delayed" }, RequestMethods.ResourcesRead), TestContext.Current.CancellationToken).AsTask(),
            delayed.Completion,
            hostContext);
    }

    [Fact]
    public async Task CompletedCancellationAndException_ArePropagatedFromTheHostCallback()
    {
        var hostContext = new RecordingHostContextExecutor();
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StreamServerTransport(input, output);
        await using var server = McpServer.Create(transport, new McpServerOptions());
        var cancelled = McpHostExecutionPrimitives.Wrap(new CancelledTool(), new HostContextMcpExecution(hostContext));
        var faulted = McpHostExecutionPrimitives.Wrap(new FaultedTool(), new HostContextMcpExecution(hostContext));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cancelled.InvokeAsync(CreateRequest(server, new CallToolRequestParams { Name = "cancelled_tool" }, RequestMethods.ToolsCall), TestContext.Current.CancellationToken).AsTask());
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            faulted.InvokeAsync(CreateRequest(server, new CallToolRequestParams { Name = "faulted_tool" }, RequestMethods.ToolsCall), TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("expected failure", exception.Message);
        Assert.Equal(2, hostContext.CallCount);
    }

    [Fact]
    public async Task StandardSdkBuiltInTool_AllowsAsyncPreparationAndPropagatesSuppressedGuardToItsHostCallback()
    {
        var hostContext = new RecordingHostContextExecutor();
        var builtIn = new AsyncBuiltInTool(hostContext);
        var tool = LoadBuiltInTool(builtIn, hostContext);
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StreamServerTransport(input, output);
        await using var server = McpServer.Create(transport, new McpServerOptions());

        var result = await tool.InvokeAsync(
            CreateRequest(server, new CallToolRequestParams { Name = builtIn.Primitive.ProtocolTool.Name }, RequestMethods.ToolsCall),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError ?? false);
        Assert.Equal([false, false, true], builtIn.HostScopes);
        Assert.All(builtIn.ObservedModes, mode => Assert.Equal(ExecutionGuardMode.Suppress, mode));
        Assert.Equal(1, hostContext.CallCount);
        Assert.Equal(1, hostContext.ScopeExitCount);
        Assert.Equal(ExecutionGuardMode.Passthrough, ExecutionGuardContext.Mode);
    }

    [Fact]
    public async Task StandardSdkBuiltInTool_PropagatesCancellationAfterAsyncPreparation()
    {
        var hostContext = new RecordingHostContextExecutor();
        var tool = LoadBuiltInTool(new CancellableBuiltInTool(), hostContext);
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StreamServerTransport(input, output);
        await using var server = McpServer.Create(transport, new McpServerOptions());
        using var cancellation = new CancellationTokenSource();

        var invocation = tool.InvokeAsync(
            CreateRequest(server, new CallToolRequestParams { Name = "cancellable_built_in" }, RequestMethods.ToolsCall),
            cancellation.Token).AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation);
    }

    [Fact]
    public async Task StandardSdkBuiltInTool_PropagatesFaultAfterAsyncPreparation()
    {
        var hostContext = new RecordingHostContextExecutor();
        var tool = LoadBuiltInTool(new FaultingBuiltInTool(), hostContext);
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StreamServerTransport(input, output);
        await using var server = McpServer.Create(transport, new McpServerOptions());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tool.InvokeAsync(CreateRequest(server, new CallToolRequestParams { Name = "faulting_built_in" }, RequestMethods.ToolsCall), TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("built-in failure", exception.Message);
    }

    [Fact]
    public async Task StandardSdkCollection_InvokesOpenDocumentBuiltInThroughItsAsyncDocumentBridge()
    {
        var hostContext = new RecordingHostContextExecutor();
        var documentBridge = new AsyncDocumentBridge();
        var tool = LoadBuiltInTool(new OpenDocumentTool(documentBridge), hostContext);
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StreamServerTransport(input, output);
        await using var server = McpServer.Create(transport, new McpServerOptions());
        var filePath = Path.GetTempFileName();
        try
        {
            var result = await tool.InvokeAsync(
                CreateRequest(server, new CallToolRequestParams
                {
                    Name = "open_document",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["filePath"] = JsonSerializer.SerializeToElement(filePath)
                    }
                }, RequestMethods.ToolsCall),
                TestContext.Current.CancellationToken);

            Assert.False(result.IsError ?? false);
            Assert.Equal(filePath, documentBridge.OpenedPath);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task StandardSdkBuiltInResource_RunsHostDependentReadInsideSuppressedHostScope()
    {
        var hostContext = new RecordingHostContextExecutor();
        var builtIn = new HostDependentBuiltInResource(hostContext);
        var resource = LoadBuiltInResource(builtIn, hostContext);
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StreamServerTransport(input, output);
        await using var server = McpServer.Create(transport, new McpServerOptions());

        await resource.ReadAsync(
            CreateRequest(server, new ReadResourceRequestParams { Uri = builtIn.Primitive.ProtocolResourceTemplate.UriTemplate }, RequestMethods.ResourcesRead),
            TestContext.Current.CancellationToken);

        Assert.Equal([true], builtIn.HostScopes);
        Assert.Equal([ExecutionGuardMode.Suppress], builtIn.ObservedModes);
        Assert.Equal(1, hostContext.CallCount);
        Assert.Equal(1, hostContext.ScopeExitCount);
        Assert.Equal(ExecutionGuardMode.Passthrough, ExecutionGuardContext.Mode);
    }

    [Fact]
    public async Task StandardSdkBuiltInPrompt_RunsInsideSuppressedHostScope()
    {
        var hostContext = new RecordingHostContextExecutor();
        var builtIn = new HostDependentBuiltInPrompt(hostContext);
        var prompt = LoadBuiltInPrompt(builtIn, hostContext);
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StreamServerTransport(input, output);
        await using var server = McpServer.Create(transport, new McpServerOptions());

        await prompt.GetAsync(
            CreateRequest(server, new GetPromptRequestParams { Name = builtIn.Primitive.ProtocolPrompt.Name }, RequestMethods.PromptsGet),
            TestContext.Current.CancellationToken);

        Assert.Equal([true], builtIn.HostScopes);
        Assert.Equal([ExecutionGuardMode.Suppress], builtIn.ObservedModes);
        Assert.Equal(1, hostContext.CallCount);
        Assert.Equal(1, hostContext.ScopeExitCount);
        Assert.Equal(ExecutionGuardMode.Passthrough, ExecutionGuardContext.Mode);
    }

    private static McpServerTool LoadBuiltInTool(IBuiltInMcpTool builtIn, RecordingHostContextExecutor hostContext)
    {
        var loader = new McpCatalogLoader(
            [new BuiltInMcpRegistryProvider([builtIn], [], [], new HostContextMcpExecution(hostContext))],
            NullLogger<McpCatalogLoader>.Instance);
        McpServerPrimitiveCollection<McpServerTool> tools = [];
        var store = new McpCatalogStore(loader, new EmptySettingsService(), tools, [], []);

        store.EnsureLoaded();

        return Assert.Single(tools);
    }

    private static McpServerPrompt LoadBuiltInPrompt(IBuiltInMcpPrompt builtIn, RecordingHostContextExecutor hostContext)
    {
        var loader = new McpCatalogLoader(
            [new BuiltInMcpRegistryProvider([], [], [builtIn], new HostContextMcpExecution(hostContext))],
            NullLogger<McpCatalogLoader>.Instance);
        McpServerPrimitiveCollection<McpServerPrompt> prompts = [];
        var store = new McpCatalogStore(loader, new EmptySettingsService(), [], prompts, []);

        store.EnsureLoaded();

        return Assert.Single(prompts);
    }

    private static McpServerResource LoadBuiltInResource(IBuiltInMcpResource builtIn, RecordingHostContextExecutor hostContext)
    {
        var loader = new McpCatalogLoader(
            [new BuiltInMcpRegistryProvider([], [builtIn], [], new HostContextMcpExecution(hostContext))],
            NullLogger<McpCatalogLoader>.Instance);
        McpServerResourceCollection resources = [];
        var store = new McpCatalogStore(loader, new EmptySettingsService(), [], [], resources);

        store.EnsureLoaded();

        return Assert.Single(resources);
    }

    private static async Task AssertIncompleteInvocationIsRejected<T>(
        Task<T> invocation,
        DeferredValueTaskSource<T> completion,
        RecordingHostContextExecutor hostContext)
    {
        var completed = await Task.WhenAny(invocation, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.Same(invocation, completed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => invocation);
        Assert.Equal(0, completion.ContinuationRegistrationCount);
        Assert.Equal(1, hostContext.ScopeExitCount);
        Assert.False(hostContext.IsInScope);
        completion.SetResult(default!);
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

    private sealed class AsyncBuiltInTool(RecordingHostContextExecutor hostContext) : IBuiltInMcpTool
    {
        public McpServerTool Primitive => McpServerTool.Create(typeof(AsyncBuiltInTool).GetMethod(nameof(ExecuteAsync))!, this);
        public List<ExecutionGuardMode> ObservedModes { get; } = [];
        public List<bool> HostScopes { get; } = [];

        [McpServerTool(Name = "async_built_in")]
        public async Task<CallToolResult> ExecuteAsync(CancellationToken ct)
        {
            ObservedModes.Add(ExecutionGuardContext.Mode);
            HostScopes.Add(hostContext.IsInScope);
            await Task.Yield();
            ObservedModes.Add(ExecutionGuardContext.Mode);
            HostScopes.Add(hostContext.IsInScope);
            await hostContext.ExecuteAsync(() =>
            {
                ObservedModes.Add(ExecutionGuardContext.Mode);
                HostScopes.Add(hostContext.IsInScope);
                return 0;
            }, ct);
            return new CallToolResult();
        }
    }

    private sealed class CancellableBuiltInTool : IBuiltInMcpTool
    {
        public McpServerTool Primitive => McpServerTool.Create(typeof(CancellableBuiltInTool).GetMethod(nameof(ExecuteAsync))!, this);

        [McpServerTool(Name = "cancellable_built_in")]
        public async Task<CallToolResult> ExecuteAsync(CancellationToken ct)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("Cancellation was not observed.");
        }
    }

    private sealed class FaultingBuiltInTool : IBuiltInMcpTool
    {
        public McpServerTool Primitive => McpServerTool.Create(typeof(FaultingBuiltInTool).GetMethod(nameof(ExecuteAsync))!, this);

        [McpServerTool(Name = "faulting_built_in")]
        public async Task<CallToolResult> ExecuteAsync(CancellationToken ct)
        {
            await Task.Yield();
            throw new InvalidOperationException("built-in failure");
        }
    }

    private sealed class AsyncDocumentBridge : IDocumentBridge
    {
        public string? OpenedPath { get; private set; }

        public async Task<DocumentOperationResult> OpenDocumentAsync(string filePath, CancellationToken ct = default)
        {
            await Task.Yield();
            OpenedPath = filePath;
            return new DocumentOperationResult(true, "opened", Path.GetFileName(filePath));
        }

        public Task<DocumentOperationResult> CloseDocumentAsync(bool save, CancellationToken ct = default) =>
            Task.FromResult(new DocumentOperationResult(true, "closed"));

        public Task<DocumentOperationResult> SaveDocumentAsync(string? savePath, CancellationToken ct = default) =>
            Task.FromResult(new DocumentOperationResult(true, "saved"));
    }

    private sealed class HostDependentBuiltInPrompt(RecordingHostContextExecutor hostContext) : IBuiltInMcpPrompt
    {
        public McpServerPrompt Primitive => McpServerPrompt.Create(typeof(HostDependentBuiltInPrompt).GetMethod(nameof(GetAsync))!, this);
        public List<ExecutionGuardMode> ObservedModes { get; } = [];
        public List<bool> HostScopes { get; } = [];

        [McpServerPrompt(Name = "host_dependent_prompt")]
        public GetPromptResult GetAsync()
        {
            ObservedModes.Add(ExecutionGuardContext.Mode);
            HostScopes.Add(hostContext.IsInScope);
            return new GetPromptResult();
        }
    }

    private sealed class HostDependentBuiltInResource(RecordingHostContextExecutor hostContext) : IBuiltInMcpResource
    {
        public McpServerResource Primitive => McpServerResource.Create(typeof(HostDependentBuiltInResource).GetMethod(nameof(ReadAsync))!, this);
        public List<ExecutionGuardMode> ObservedModes { get; } = [];
        public List<bool> HostScopes { get; } = [];

        [McpServerResource(UriTemplate = "revit://model/host-dependent", Name = "host_dependent_resource")]
        public ReadResourceResult ReadAsync()
        {
            ObservedModes.Add(ExecutionGuardContext.Mode);
            HostScopes.Add(hostContext.IsInScope);
            return new ReadResourceResult();
        }
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

    private sealed class DelayedTool : McpServerTool
    {
        public DeferredValueTaskSource<CallToolResult> Completion { get; } = new();
        public override Tool ProtocolTool { get; } = new() { Name = "delayed_tool", InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }) };
        public override IReadOnlyList<object> Metadata => [];
        public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default) => Completion.CreateValueTask();
    }

    private sealed class DelayedPrompt : McpServerPrompt
    {
        public DeferredValueTaskSource<GetPromptResult> Completion { get; } = new();
        public override Prompt ProtocolPrompt { get; } = new() { Name = "delayed_prompt" };
        public override IReadOnlyList<object> Metadata => [];
        public override ValueTask<GetPromptResult> GetAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken = default) => Completion.CreateValueTask();
    }

    private sealed class DelayedResource : McpServerResource
    {
        public DeferredValueTaskSource<ReadResourceResult> Completion { get; } = new();
        public override Resource? ProtocolResource { get; } = new() { Uri = "revit://delayed", Name = "delayed_resource" };
        public override ResourceTemplate ProtocolResourceTemplate { get; } = new() { UriTemplate = "revit://delayed", Name = "delayed_resource" };
        public override IReadOnlyList<object> Metadata => [];
        public override bool IsMatch(string uri) => uri == ProtocolResource!.Uri;
        public override ValueTask<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken = default) => Completion.CreateValueTask();
    }

    private sealed class CancelledTool : McpServerTool
    {
        public override Tool ProtocolTool { get; } = new() { Name = "cancelled_tool", InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }) };
        public override IReadOnlyList<object> Metadata => [];
        public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default) => ValueTask.FromCanceled<CallToolResult>(new CancellationToken(true));
    }

    private sealed class FaultedTool : McpServerTool
    {
        public override Tool ProtocolTool { get; } = new() { Name = "faulted_tool", InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }) };
        public override IReadOnlyList<object> Metadata => [];
        public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default) => ValueTask.FromException<CallToolResult>(new InvalidOperationException("expected failure"));
    }

    private sealed class DeferredValueTaskSource<T> : IValueTaskSource<T>
    {
        private ManualResetValueTaskSourceCore<T> _source = new() { RunContinuationsAsynchronously = true };
        public int ContinuationRegistrationCount { get; private set; }

        public ValueTask<T> CreateValueTask() => new(this, _source.Version);
        public void SetResult(T result) => _source.SetResult(result);
        public T GetResult(short token) => _source.GetResult(token);
        public ValueTaskSourceStatus GetStatus(short token) => _source.GetStatus(token);
        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            ContinuationRegistrationCount++;
            _source.OnCompleted(continuation, state, token, flags);
        }
    }

    private sealed class RecordingHostContextExecutor : IHostContextExecutor
    {
        public int CallCount { get; private set; }
        public int ScopeExitCount { get; private set; }
        public bool IsInScope { get; private set; }
        public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            CallCount++;
            IsInScope = true;
            try
            {
                return Task.FromResult(handler());
            }
            finally
            {
                IsInScope = false;
                ScopeExitCount++;
            }
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
