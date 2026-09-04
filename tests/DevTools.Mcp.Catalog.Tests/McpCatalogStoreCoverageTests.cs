using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Catalog.Tests.Harness;
using DevTools.Settings;
using DevTools.Settings.Configs;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using Moq;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class McpCatalogStoreCoverageTests
{
    [Fact]
    public void GetDescriptors_ReturnLoadedCatalogItems()
    {
        var direct = CreateResource("demo_status", uri: "sample://demo/status");
        var template = CreateTemplateResource("demo_view", "sample://demo/views/{viewId}");
        var store = CreateStore(
            [McpHostTestHarness.CreateRegisteredTool("execute_csharp_code")],
            [direct, template]);

        store.EnsureLoaded();

        Assert.Single(store.GetToolDescriptors());
        Assert.Single(store.GetResourceDescriptors());
        Assert.Single(store.GetResourceTemplateDescriptors());
    }

    [Fact]
    public void TryGetTool_ResolvesByIdOrName()
    {
        var tool = McpHostTestHarness.CreateRegisteredTool("execute_csharp_code");
        var store = CreateStore([tool], []);

        store.EnsureLoaded();

        Assert.True(store.TryGetTool(tool.Id, null, out var byId));
        Assert.Same(tool, byId);
        Assert.True(store.TryGetTool(null, "execute_csharp_code", out var byName));
        Assert.Same(tool, byName);
        Assert.False(store.TryGetTool("missing", null, out _));
    }

    [Fact]
    public void TryResolveResourceByUri_MatchesDirectAndTemplateUris()
    {
        var direct = CreateResource("demo_status", uri: "sample://demo/status");
        var template = CreateTemplateResource("demo_view", "sample://demo/views/{viewId}");
        var store = CreateStore([], [direct, template]);
        store.EnsureLoaded();

        Assert.True(store.TryResolveResourceByUri("sample://demo/status", out var resolvedDirect));
        Assert.Same(direct, resolvedDirect);
        Assert.True(store.TryResolveResourceByUri("sample://demo/views/wall-1", out var resolvedTemplate));
        Assert.Same(template, resolvedTemplate);
        Assert.False(store.TryResolveResourceByUri("sample://missing", out _));
        Assert.False(store.TryResolveResourceByUri("", out _));
    }

    [Fact]
    public async Task AddPathAsync_IgnoresUnsupportedPaths()
    {
        var config = new McpRegistryConfig();
        var store = CreateStore([], [], config);
        var raised = 0;
        store.CatalogChanged += (_, _) => raised++;

        await store.AddPathAsync(string.Empty);
        await store.AddPathAsync(@"C:\missing\readme.txt");

        Assert.Equal(0, raised);
        Assert.Empty(config.DotnetPaths);
        Assert.Empty(config.PythonToolsetPaths);
    }

    [Fact]
    public async Task AddPathAsync_PersistsValidDotnetPath_WhenCatalogContainsItems()
    {
        var dll = OptionalArtifact.ResolveMcpToolsetDemoDll(FindRepositoryRoot());
        if (dll is null)
            Assert.Skip(OptionalArtifact.McpToolsetDemoHint);

        var catalog = new McpAssemblyParser(Microsoft.Extensions.Logging.Abstractions.NullLogger<McpAssemblyParser>.Instance)
            .ParseCatalogFromAssembly(dll);
        var config = new McpRegistryConfig();
        var store = CreateStore(catalog.Tools.ToArray(), catalog.Resources.ToArray(), config);

        await store.AddPathAsync(dll);

        Assert.Contains(config.DotnetPaths, path => string.Equals(Path.GetFullPath(path), Path.GetFullPath(dll), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AddPathAsync_PersistsValidPythonPath_WhenCatalogContainsItems()
    {
        var pythonRoot = Path.Combine(FindRepositoryRoot(), "samples", "PythonDemo", "mcp_toolset");
        OptionalArtifact.RequireDirectory(pythonRoot, $"Expected Python sample toolset at '{pythonRoot}'.");
        if (!McpPathValidator.IsValidPythonToolsetPath(pythonRoot))
            Assert.Skip("Python sample toolset does not contain *mcp.py files.");

        var tool = new McpRegisteredTool
        {
            Id = "python_tool",
            Descriptor = new Tool { Name = "python_tool", InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }) },
            Binding = McpPrimitiveBinding.Create(ExecutionMode.Python, pythonRoot, "module", "run"),
        };
        var config = new McpRegistryConfig();
        var store = CreateStore([tool], [], config);

        await store.AddPathAsync(pythonRoot);

        Assert.Contains(config.PythonToolsetPaths, path => string.Equals(Path.GetFullPath(path), Path.GetFullPath(pythonRoot), StringComparison.OrdinalIgnoreCase));
    }

    private static McpCatalogStore CreateStore(
        McpRegisteredTool[] tools,
        McpRegisteredResource[] resources,
        McpRegistryConfig? config = null)
    {
        var catalog = new McpRegistryCatalog { Tools = tools, Resources = resources };
        var loader = new Mock<IMcpCatalogLoader>();
        loader
            .Setup(l => l.LoadCatalog(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(catalog);

        config ??= new McpRegistryConfig();
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.McpRegistryConfig).Returns(config);
        return new McpCatalogStore(loader.Object, settings.Object);
    }

    private static McpRegisteredResource CreateResource(string name, string uri) => new()
    {
        Id = name,
        Descriptor = new Resource { Name = name, Uri = uri, MimeType = "text/plain" },
        Binding = McpPrimitiveBinding.Create(ExecutionMode.Dotnet, "stub.dll", "Stub", name),
    };

    private static McpRegisteredResource CreateTemplateResource(string name, string uriTemplate) => new()
    {
        Id = name,
        TemplateDescriptor = new ResourceTemplate { Name = name, UriTemplate = uriTemplate, MimeType = "application/json" },
        Binding = McpPrimitiveBinding.Create(ExecutionMode.Dotnet, "stub.dll", "Stub", name),
    };

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RevitDevTool.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the RevitDevTool repository root.");
    }
}
