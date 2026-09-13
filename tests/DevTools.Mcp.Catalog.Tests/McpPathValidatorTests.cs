using DevTools.Execution.Abstractions;
using DevTools.Mcp.Catalog.Tests.Harness;
using DevTools.Settings.Configs;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class McpPathValidatorTests
{
    [TestMethod]
    public void ClassifyInputPath_DetectsDotnetAssembly()
    {
        var dll = typeof(McpPathValidatorTests).Assembly.Location;

        Assert.AreEqual(ExecutionMode.Dotnet, McpPathValidator.ClassifyInputPath(dll));
    }

    [TestMethod]
    public void ClassifyInputPath_DetectsPythonToolset()
    {
        var directory = CreatePythonToolsetDirectory();

        try
        {
            Assert.AreEqual(ExecutionMode.Python, McpPathValidator.ClassifyInputPath(directory));
            Assert.IsTrue(McpPathValidator.IsValidPythonToolsetPath(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ClassifyInputPath_ReturnsUnsupported_ForMissingPath()
    {
        Assert.AreEqual(ExecutionMode.Unsupported, McpPathValidator.ClassifyInputPath(@"C:\missing\path.dll"));
        Assert.IsFalse(McpPathValidator.IsValidDotnetAssemblyPath(null));
        Assert.IsFalse(McpPathValidator.IsValidDotnetAssemblyPath("readme.txt"));
    }

    [TestMethod]
    public void PathProducesCatalogItems_MatchesExactAndNestedPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "DevTools.Mcp.Tests", Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "nested", "tool.py");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        File.WriteAllText(nested, "# stub");

        var catalog = new McpRegistryCatalog
        {
            Tools =
            [
                CreateBoundTool("exact", root),
                CreateBoundTool("nested", nested),
            ],
            Resources = [],
        };

        try
        {
            Assert.IsTrue(McpPathValidator.PathProducesCatalogItems(root, ExecutionMode.Python, catalog));
            Assert.IsTrue(McpPathValidator.PathProducesCatalogItems(nested, ExecutionMode.Python, catalog));
            Assert.IsFalse(McpPathValidator.PathProducesCatalogItems(Path.Combine(root, "other"), ExecutionMode.Python, catalog));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void AddDistinct_IsCaseInsensitive()
    {
        var paths = new List<string> { @"C:\Toolsets\Demo.dll" };

        McpPathValidator.AddDistinct(paths, @"c:\toolsets\demo.dll");
        McpPathValidator.AddDistinct(paths, @"C:\Toolsets\Other.dll");

        Assert.AreEqual(2, paths.Count);
    }

    [TestMethod]
    public void ResolvePaths_FiltersAndNormalizes()
    {
        var dll = typeof(McpPathValidatorTests).Assembly.Location;
        var resolved = McpPathValidator.ResolvePaths(
            [dll, @"C:\missing.dll", dll.ToUpperInvariant()],
            McpPathValidator.IsValidDotnetAssemblyPath);

        Assert.HasCount(1, resolved);
        Assert.AreEqual(Path.GetFullPath(dll), resolved[0]);
    }

    [TestMethod]
    public void PruneInvalidConfiguredPaths_RemovesPathsThatProduceNoCatalogItems()
    {
        var config = new McpRegistryConfig
        {
            DotnetPaths = [@"C:\missing\demo.dll"],
            PythonToolsetPaths = [@"C:\missing\toolset"],
        };
        var catalog = new McpRegistryCatalog
        {
            Tools = [CreateBoundTool("live", typeof(McpPathValidatorTests).Assembly.Location)],
            Resources = [],
        };

        McpPathValidator.PruneInvalidConfiguredPaths(config, catalog, NullLogger.Instance);

        Assert.IsEmpty(config.DotnetPaths);
        Assert.IsEmpty(config.PythonToolsetPaths);
    }

    private static string CreatePythonToolsetDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools.Mcp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "sample_mcp.py"), "# stub");
        return directory;
    }

    private static McpRegisteredTool CreateBoundTool(string name, string sourcePath) => new()
    {
        Id = name,
        Descriptor = new Tool { Name = name, InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }) },
        Binding = McpPrimitiveBinding.Create(ExecutionMode.Python, sourcePath, "Container", name),
    };
}
