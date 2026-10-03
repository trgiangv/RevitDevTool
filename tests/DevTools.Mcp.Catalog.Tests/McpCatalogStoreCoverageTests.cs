using DevTools.Execution.Abstractions;
using DevTools.Mcp.Discovery;
using DevTools.Mcp.Hosting;
using DevTools.Mcp.Catalog.Tests.Harness;
using DevTools.Settings;
using DevTools.Settings.Configs;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using Moq;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class McpCatalogStoreCoverageTests
{
    [TestMethod]
    public void GetDescriptors_ReturnLoadedCatalogItems()
    {
        var direct = CreateResource("demo_status", uri: "sample://demo/status");
        var template = CreateTemplateResource("demo_view", "sample://demo/views/{viewId}");
        var store = CreateStore(
            [McpHostTestHarness.CreateRegisteredTool("execute_csharp_code")],
            [direct, template]);

        store.EnsureLoaded();

        Assert.HasCount(1, store.GetToolDescriptors());
        Assert.HasCount(1, store.GetResourceDescriptors());
        Assert.HasCount(1, store.GetResourceTemplateDescriptors());
    }

    [TestMethod]
    public void TryGetTool_ResolvesByIdOrName()
    {
        var tool = McpHostTestHarness.CreateRegisteredTool("execute_csharp_code");
        var store = CreateStore([tool], []);

        store.EnsureLoaded();

        Assert.IsTrue(store.TryGetTool(tool.Id, null, out var byId));
        Assert.AreSame(tool, byId);
        Assert.IsTrue(store.TryGetTool(null, "execute_csharp_code", out var byName));
        Assert.AreSame(tool, byName);
        Assert.IsFalse(store.TryGetTool("missing", null, out _));
    }

    [TestMethod]
    public void SdkResource_MatchesDirectAndTemplateUris()
    {
        var direct = SdkCollectionResource.Create(
            CreateResource("demo_status", uri: "sample://demo/status"),
            Mock.Of<IHostContextExecutor>(),
            (_, _) => Task.FromResult(new ReadResourceResult()));
        var template = SdkCollectionResource.Create(
            CreateTemplateResource("demo_view", "sample://demo/views/{viewId}"),
            Mock.Of<IHostContextExecutor>(),
            (_, _) => Task.FromResult(new ReadResourceResult()));

        Assert.IsTrue(direct.IsMatch("sample://demo/status"));
        Assert.IsTrue(template.IsMatch("sample://demo/views/wall-1"));
        Assert.IsFalse(direct.IsMatch("sample://missing"));
        Assert.IsFalse(direct.IsMatch(""));
    }

    [TestMethod]
    public async Task AddPathAsync_IgnoresUnsupportedPaths()
    {
        var config = new McpRegistryConfig();
        var store = CreateStore([], [], config);
        var raised = 0;
        store.CatalogChanged += (_, _) => raised++;

        await store.AddPathAsync(string.Empty);
        await store.AddPathAsync(@"C:\missing\readme.txt");

        Assert.AreEqual(0, raised);
        Assert.IsEmpty(config.DotnetPaths);
        Assert.IsEmpty(config.PythonPaths);
    }

    [TestMethod]
    public async Task AddPathAsync_PersistsValidDotnetPath_WhenCatalogContainsItems()
    {
        var dll = OptionalArtifact.ResolveMcpToolsetDemoDll(FindRepositoryRoot());
        if (dll is null)
            Assert.Inconclusive(OptionalArtifact.McpToolsetDemoHint);

        var catalog = new McpAssemblyParser(Microsoft.Extensions.Logging.Abstractions.NullLogger<McpAssemblyParser>.Instance)
            .ParseCatalogFromAssembly(dll);
        var config = new McpRegistryConfig();
        var store = CreateStore(catalog.Tools.ToArray(), catalog.Resources.ToArray(), config);

        await store.AddPathAsync(dll);

        Assert.IsTrue(config.DotnetPaths.Any(path => string.Equals(Path.GetFullPath(path), Path.GetFullPath(dll), StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task AddPathAsync_PersistsValidPythonPath_WhenCatalogContainsItems()
    {
        var pythonRoot = Path.Combine(FindRepositoryRoot(), "samples", "PythonDemo", "mcp_toolset");
        OptionalArtifact.RequireDirectory(pythonRoot, $"Expected Python sample toolset at '{pythonRoot}'.");
        if (!McpPathValidator.IsValidPythonPath(pythonRoot))
            Assert.Inconclusive("Python sample toolset does not contain *mcp.py files.");

        var tool = new RegisteredTool
        {
            Id = "python_tool",
            Descriptor = new Tool { Name = "python_tool", InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }) },
            Binding = PrimitiveBinding.Create(ExecutionMode.Python, pythonRoot, "module", "run", "", ""),
        };
        var config = new McpRegistryConfig();
        var store = CreateStore([tool], [], config);

        await store.AddPathAsync(pythonRoot);

        Assert.IsTrue(config.PythonPaths.Any(path => string.Equals(Path.GetFullPath(path), Path.GetFullPath(pythonRoot), StringComparison.OrdinalIgnoreCase)));
    }

    private static McpCatalogStore CreateStore(
        RegisteredTool[] tools,
        RegisteredResource[] resources,
        McpRegistryConfig? config = null)
    {
        var catalog = new RegistryCatalog { Tools = tools, Resources = resources };
        var loader = new Mock<ICatalogLoader>();
        loader
            .Setup(l => l.LoadCatalog(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(catalog);

        config ??= new McpRegistryConfig();
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.McpRegistryConfig).Returns(config);
        return new McpCatalogStore(loader.Object, settings.Object);
    }

    private static RegisteredResource CreateResource(string name, string uri) => new()
    {
        Id = name,
        Descriptor = new Resource { Name = name, Uri = uri, MimeType = "text/plain" },
        Binding = PrimitiveBinding.Create(ExecutionMode.Dotnet, "stub.dll", "Stub", name, "", ""),
    };

    private static RegisteredResource CreateTemplateResource(string name, string uriTemplate) => new()
    {
        Id = name,
        TemplateDescriptor = new ResourceTemplate { Name = name, UriTemplate = uriTemplate, MimeType = "application/json" },
        Binding = PrimitiveBinding.Create(ExecutionMode.Dotnet, "stub.dll", "Stub", name, "", ""),
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
