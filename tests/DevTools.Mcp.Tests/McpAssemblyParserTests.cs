using DevTools.Mcp.Catalog.Discovery;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Mcp.Tests;

public sealed class McpAssemblyParserTests
{
    [Fact]
    public void Metadata_session_preserves_the_sample_tool_and_resource_descriptors()
    {
        var catalog = new McpAssemblyParser(NullLogger<McpAssemblyParser>.Instance)
            .ParseCatalogFromAssembly(GetSampleAssemblyPath());

        var tool = catalog.Tools.Single(item => item.Descriptor.Name == "get_demo_status").Descriptor;
        var resource = catalog.Resources.Single(item => item.Descriptor?.Name == "demo_status").Descriptor!;
        var template = catalog.Resources.Single(item => item.TemplateDescriptor?.Name == "demo_view").TemplateDescriptor!;

        Assert.Equal("Get Demo Status", tool.Title);
        Assert.True(tool.Annotations?.ReadOnly);
        Assert.Equal("sample://demo/status", resource.Uri);
        Assert.Equal("sample://demo/views/{viewId}", template.UriTemplate);
    }

    private static string GetSampleAssemblyPath()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "RevitDevTool.slnx")))
            root = root.Parent;

        Assert.NotNull(root);
        var candidates = new[]
        {
            Path.Combine(root!.FullName, "samples", "McpToolsetDemo", "bin", "Debug.Autodesk.2025", "McpToolsetDemo.dll"),
            Path.Combine(root.FullName, "samples", "McpToolsetDemo", "bin", "Debug", "net8.0", "McpToolsetDemo.dll"),
        };
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Build McpToolsetDemo before running metadata parser tests.");
    }
}
