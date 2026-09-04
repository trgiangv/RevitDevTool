using DevTools.Execution.Abstractions;
using DevTools.Mcp.Catalog.Tests.Harness;
using DevTools.Settings.Configs;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class McpPathValidatorTests
{
    [Fact]
    public void ClassifyInputPath_DetectsDotnetAssembly()
    {
        var dll = typeof(McpPathValidatorTests).Assembly.Location;

        Assert.Equal(ExecutionMode.Dotnet, McpPathValidator.ClassifyInputPath(dll));
    }

    [Fact]
    public void ClassifyInputPath_DetectsPythonToolset()
    {
        var directory = CreatePythonToolsetDirectory();

        try
        {
            Assert.Equal(ExecutionMode.Python, McpPathValidator.ClassifyInputPath(directory));
            Assert.True(McpPathValidator.IsValidPythonToolsetPath(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ClassifyInputPath_ReturnsUnsupported_ForMissingPath()
    {
        Assert.Equal(ExecutionMode.Unsupported, McpPathValidator.ClassifyInputPath(@"C:\missing\path.dll"));
        Assert.False(McpPathValidator.IsValidDotnetAssemblyPath(null));
        Assert.False(McpPathValidator.IsValidDotnetAssemblyPath("readme.txt"));
    }

    [Fact]
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
            Assert.True(McpPathValidator.PathProducesCatalogItems(root, ExecutionMode.Python, catalog));
            Assert.True(McpPathValidator.PathProducesCatalogItems(nested, ExecutionMode.Python, catalog));
            Assert.False(McpPathValidator.PathProducesCatalogItems(Path.Combine(root, "other"), ExecutionMode.Python, catalog));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AddDistinct_IsCaseInsensitive()
    {
        var paths = new List<string> { @"C:\Toolsets\Demo.dll" };

        McpPathValidator.AddDistinct(paths, @"c:\toolsets\demo.dll");
        McpPathValidator.AddDistinct(paths, @"C:\Toolsets\Other.dll");

        Assert.Equal(2, paths.Count);
    }

    [Fact]
    public void ResolvePaths_FiltersAndNormalizes()
    {
        var dll = typeof(McpPathValidatorTests).Assembly.Location;
        var resolved = McpPathValidator.ResolvePaths(
            [dll, @"C:\missing.dll", dll.ToUpperInvariant()],
            McpPathValidator.IsValidDotnetAssemblyPath);

        Assert.Single(resolved);
        Assert.Equal(Path.GetFullPath(dll), resolved[0]);
    }

    [Fact]
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

        Assert.Empty(config.DotnetPaths);
        Assert.Empty(config.PythonToolsetPaths);
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
