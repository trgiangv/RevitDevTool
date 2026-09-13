using DevTools.Mcp.Catalog.Discovery;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class McpAssemblyParserTests
{
    [TestMethod]
    public void Metadata_session_preserves_the_sample_tool_and_resource_descriptors()
    {
        var catalog = new McpAssemblyParser(NullLogger<McpAssemblyParser>.Instance)
            .ParseCatalogFromAssembly(GetSampleAssemblyPath());

        var tool = catalog.Tools.Single(item => item.Descriptor.Name == "get_demo_status").Descriptor;
        var resource = catalog.Resources.Single(item => item.Descriptor?.Name == "demo_status").Descriptor!;
        var template = catalog.Resources.Single(item => item.TemplateDescriptor?.Name == "demo_view").TemplateDescriptor!;

        Assert.AreEqual("Get Demo Status", tool.Title);
        Assert.IsTrue(tool.Annotations?.ReadOnlyHint);
        Assert.AreEqual("sample://demo/status", resource.Uri);
        Assert.AreEqual("sample://demo/views/{viewId}", template.UriTemplate);
    }

    private static string GetSampleAssemblyPath()
    {
        var root = FindRepositoryRoot();
        var sampleAssembly = OptionalArtifact.ResolveMcpToolsetDemoDll(root);
        if (sampleAssembly is null)
            Assert.Inconclusive(OptionalArtifact.McpToolsetDemoHint);
        return sampleAssembly;
    }

    private static string FindRepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "RevitDevTool.slnx")))
            root = root.Parent;

        if (root is null)
            throw new DirectoryNotFoundException("Could not locate the RevitDevTool repository root.");

        return root.FullName;
    }
}
