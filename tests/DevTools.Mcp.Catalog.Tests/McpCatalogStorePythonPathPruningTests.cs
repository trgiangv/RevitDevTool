using DevTools.Mcp.Catalog.Tests.Harness;
using DevTools.Settings;
using DevTools.Settings.Configs;
using Moq;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class McpCatalogStorePythonPathPruningTests
{
    [TestMethod]
    public void EnsureLoaded_RemovesPathsThatProduceNoCatalogItems()
    {
        var pythonPath = CreatePythonDirectory();
        var missingDotnet = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.dll");
        var config = new McpRegistryConfig
        {
            DotnetPaths = [missingDotnet],
            PythonPaths = [pythonPath],
        };

        try
        {
            var store = CreateStore(
                () => Catalog(McpHostTestHarness.CreateRegisteredTool("execute_csharp_code")),
                config);

            store.EnsureLoaded();

            Assert.IsEmpty(config.PythonPaths);
            Assert.IsEmpty(config.DotnetPaths);
        }
        finally
        {
            Directory.Delete(pythonPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReloadAsync_RemovesPythonPath_WhenCatalogHasNoPythonItems()
    {
        var pythonPath = CreatePythonDirectory();
        var config = new McpRegistryConfig
        {
            PythonPaths = [pythonPath],
        };

        try
        {
            var store = CreateStore(
                () => Catalog(McpHostTestHarness.CreateRegisteredTool("execute_csharp_code")),
                config);

            await store.ReloadAsync();

            Assert.IsEmpty(config.PythonPaths);
        }
        finally
        {
            Directory.Delete(pythonPath, recursive: true);
        }
    }

    private static McpCatalogStore CreateStore(Func<RegistryCatalog> catalogFactory, McpRegistryConfig config)
    {
        var loader = new Mock<ICatalogLoader>();
        loader
            .Setup(l => l.LoadCatalog(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(catalogFactory);

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.McpRegistryConfig).Returns(config);
        return new McpCatalogStore(loader.Object, settings.Object);
    }

    private static RegistryCatalog Catalog(params RegisteredTool[] tools) => new()
    {
        Tools = tools,
        Resources = [],
    };

    private static string CreatePythonDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools.Mcp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "sample_mcp.py"), "# stub");
        return directory;
    }
}
