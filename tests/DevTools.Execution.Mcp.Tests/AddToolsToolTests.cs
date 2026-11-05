using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Mcp;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;
using DevTools.Settings;
using DevTools.Settings.Configs;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;
using System.Text.Json;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class AddToolsToolTests
{
    private readonly McpRegistryConfig _config = new();
    private readonly Mock<ISettingsService> _settings = new();
    private readonly FakeLoader _loader = new();
    private McpCatalogStore _store = null!;
    private AddToolsTool _tool = null!;
    private string _directory = null!;
    private int _catalogChanges;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), "add-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _settings.SetupGet(s => s.McpRegistryConfig).Returns(_config);
        _store = new McpCatalogStore(_loader, _settings.Object);
        _store.CatalogChanged += (_, _) => _catalogChanges++;

        var services = new ServiceCollection().AddSingleton(_store).BuildServiceProvider();
        _tool = new AddToolsTool(services);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public async Task Add_Dll_RegistersToolsAndSavesPath()
    {
        var dll = CreateDll("Tools.dll", "tool_a", "tool_b");

        var result = await InvokeToolAsync(new { path = dll });

        Assert.IsNull(result.IsError);
        using var document = JsonDocument.Parse(Text(result));
        Assert.AreEqual("Added", document.RootElement.GetProperty("status").GetString());
        var toolset = document.RootElement.GetProperty("toolset");
        Assert.AreEqual("dotnet", toolset.GetProperty("kind").GetString());
        CollectionAssert.AreEqual(new[] { "tool_a", "tool_b" }, Names(toolset.GetProperty("tools")));
        CollectionAssert.AreEqual(new[] { dll }, _config.DotnetPaths);
        _settings.Verify(s => s.SaveSettings(), Times.Once);
        Assert.AreEqual(1, _catalogChanges);
    }

    [TestMethod]
    public async Task Add_RegisteredPath_ReloadsItEvenWhenMetadataIsUnchanged()
    {
        var dll = CreateDll("Tools.dll", "tool_a");
        await InvokeToolAsync(new { path = dll });

        var result = await InvokeToolAsync(new { path = dll });

        using var document = JsonDocument.Parse(Text(result));
        Assert.AreEqual("Reloaded", document.RootElement.GetProperty("status").GetString());
        Assert.AreEqual(2, _catalogChanges);
        Assert.AreEqual(2, _loader.Loads);
        _settings.Verify(s => s.SaveSettings(), Times.Once);
    }

    [TestMethod]
    public async Task Add_PythonFolder_ReportsOnlyItsOwnTools()
    {
        var dll = CreateDll("Tools.dll", "tool_a");
        var folder = CreatePythonToolset("py", "py_tool");
        await InvokeToolAsync(new { path = dll });

        var result = await InvokeToolAsync(new { action = "add", path = folder });

        using var document = JsonDocument.Parse(Text(result));
        var toolset = document.RootElement.GetProperty("toolset");
        Assert.AreEqual("python", toolset.GetProperty("kind").GetString());
        CollectionAssert.AreEqual(new[] { "py_tool" }, Names(toolset.GetProperty("tools")));
        CollectionAssert.AreEqual(new[] { folder }, _config.PythonPaths);
    }

    [TestMethod]
    public async Task Add_PathWithoutTools_ReturnsErrorAndSavesNothing()
    {
        var dll = CreateDll("Empty.dll");

        var result = await InvokeToolAsync(new { path = dll });

        Assert.IsTrue(result.IsError);
        Assert.Contains("Nothing to register", Text(result));
        Assert.IsEmpty(_config.DotnetPaths);
        _settings.Verify(s => s.SaveSettings(), Times.Never);
    }

    [TestMethod]
    public async Task Add_DllThatFailsToLoad_ReturnsTheLoadFailureAndTheSdkFix()
    {
        var dll = CreateDll("NoSdk.dll");
        _loader.Fail(dll, "Could not find assembly 'ModelContextProtocol.Core, Version=2.3.0.0'.");

        var result = await InvokeToolAsync(new { path = dll });

        Assert.IsTrue(result.IsError);
        Assert.Contains("Could not find assembly 'ModelContextProtocol.Core", Text(result));
        Assert.Contains("without ExcludeAssets=runtime", Text(result));
        Assert.IsEmpty(_config.DotnetPaths);
    }

    [TestMethod]
    public async Task Add_InvalidPaths_ReturnErrorsWithoutLoading()
    {
        File.WriteAllText(Path.Combine(_directory, "notes.txt"), string.Empty);
        var empty = Directory.CreateDirectory(Path.Combine(_directory, "empty")).FullName;

        var missing = await InvokeToolAsync(new { path = Path.Combine(_directory, "missing.dll") });
        var text = await InvokeToolAsync(new { path = Path.Combine(_directory, "notes.txt") });
        var folder = await InvokeToolAsync(new { path = empty });
        var blank = await InvokeToolAsync(new { path = "" });

        Assert.Contains("not found", Text(missing));
        Assert.Contains("Unsupported file type", Text(text));
        Assert.Contains("*mcp.py", Text(folder));
        Assert.Contains("must not be empty", Text(blank));
        Assert.AreEqual(0, _loader.Loads);
        Assert.AreEqual(0, _catalogChanges);
    }

    [TestMethod]
    public async Task Remove_RegisteredPath_DropsToolsAndSavesPath()
    {
        var dll = CreateDll("Tools.dll", "tool_a");
        await InvokeToolAsync(new { path = dll });

        var result = await InvokeToolAsync(new { action = "remove", path = dll });

        using var document = JsonDocument.Parse(Text(result));
        Assert.AreEqual("Removed", document.RootElement.GetProperty("status").GetString());
        CollectionAssert.AreEqual(new[] { "tool_a" }, Names(document.RootElement.GetProperty("toolset").GetProperty("tools")));
        Assert.IsEmpty(_config.DotnetPaths);
        Assert.IsFalse(_store.RegisteredTools.Any(tool => tool.Descriptor.Name == "tool_a"));
        _settings.Verify(s => s.SaveSettings(), Times.Exactly(2));
        Assert.AreEqual(2, _catalogChanges);
    }

    [TestMethod]
    public async Task Remove_UnregisteredPath_ReturnsError()
    {
        var result = await InvokeToolAsync(new { action = "remove", path = Path.Combine(_directory, "Tools.dll") });

        Assert.IsTrue(result.IsError);
        Assert.Contains("is not a registered toolset", Text(result));
        Assert.AreEqual(0, _catalogChanges);
    }

    [TestMethod]
    public async Task ReloadAndList_ReturnEveryToolset()
    {
        var dll = CreateDll("Tools.dll", "tool_a");
        var folder = CreatePythonToolset("py", "py_tool");
        await InvokeToolAsync(new { path = dll });
        await InvokeToolAsync(new { path = folder });

        var reload = await InvokeToolAsync(new { action = "reload" });
        var list = await InvokeToolAsync(new { action = "list" });

        Assert.AreEqual(3, _catalogChanges);
        Assert.AreEqual(Text(reload), Text(list));
        using var document = JsonDocument.Parse(Text(list));
        var toolsets = document.RootElement.GetProperty("toolsets");
        Assert.AreEqual(2, toolsets.GetArrayLength());
        Assert.AreEqual(dll, toolsets[0].GetProperty("path").GetString());
        Assert.AreEqual(folder, toolsets[1].GetProperty("path").GetString());
    }

    [TestMethod]
    public async Task UnknownAction_ReturnsError()
    {
        var result = await InvokeToolAsync(new { action = "clear" });

        Assert.IsTrue(result.IsError);
        Assert.Contains("Unknown action", Text(result));
    }

    private string CreateDll(string name, params string[] tools)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, []);
        _loader.Provide(path, ExecutionMode.Dotnet, path, tools);
        return path;
    }

    private string CreatePythonToolset(string folderName, params string[] tools)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_directory, folderName)).FullName;
        var module = Path.Combine(folder, "demo_mcp.py");
        File.WriteAllText(module, string.Empty);
        _loader.Provide(folder, ExecutionMode.Python, module, tools);
        return folder;
    }

    private async Task<CallToolResult> InvokeToolAsync(object args)
    {
        var argumentMap = JsonSerializer.SerializeToElement(args).EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value);

        return await _tool.ServerTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                Mock.Of<McpServer>(),
                new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
                new CallToolRequestParams { Name = _tool.Name, Arguments = argumentMap }),
            TestContext.CancellationToken);
    }

    private static string Text(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().Single().Text;

    private static string[] Names(JsonElement array) =>
        array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private sealed class FakeLoader : ICatalogLoader
    {
        private readonly Dictionary<string, RegisteredTool[]> _toolsByPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _failures = new(StringComparer.OrdinalIgnoreCase);

        public int Loads { get; private set; }

        public void Fail(string path, string message) => _failures[path] = message;

        public void Provide(string path, ExecutionMode mode, string sourcePath, string[] names) =>
            _toolsByPath[path] = names.Select(name => new RegisteredTool
            {
                Id = PrimitiveBinding.CreatePrimitiveId(name, $"{sourcePath}.{name}"),
                Descriptor = new Tool { Name = name, InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }) },
                Binding = PrimitiveBinding.Create(mode, sourcePath, "Tools", name, $"{sourcePath}.{name}", "Tools"),
            }).ToArray();

        public RegistryCatalog LoadCatalog(IReadOnlyCollection<string> dotnetPaths, IReadOnlyCollection<string> pythonPaths)
        {
            Loads++;
            var paths = dotnetPaths.Concat(pythonPaths).ToList();
            var tools = paths
                .SelectMany(path => _toolsByPath.TryGetValue(path, out var found) ? found : [])
                .ToList();
            var failures = paths
                .Where(_failures.ContainsKey)
                .ToDictionary(path => path, path => _failures[path], StringComparer.OrdinalIgnoreCase);
            return new RegistryCatalog { Tools = tools, Resources = [], Failures = failures };
        }
    }
}
