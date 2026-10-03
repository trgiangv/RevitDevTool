using System.Text.Json;
using DevTools.Mcp.Catalog.Tests.Harness;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Mcp.Catalog.Tests;

[DependsOn(typeof(ParserIntegrationTests), nameof(ParserIntegrationTests.PythonRuntime_BindsPixiVersionedDll))]
[TestClass]
public sealed class PythonInProcessParserTests : IDisposable
{
    private static readonly McpPythonParser Parser = new(NullLogger<McpPythonParser>.Instance);

    public void Dispose()
    {
    }

    [TestMethod]
    public void InProcess_ParsesAnnotationSample_Tools()
    {
        var toolsetDirectory = GetToolsetDirectory();
        var result = McpPythonParserTestSupport.RunInProcessParser(toolsetDirectory);

        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result));

        var catalog = Parser.ParseCatalogFromDirectory(toolsetDirectory, _ => result);
        var tool = catalog.Tools.SingleOrDefault(t => t.Descriptor.Name == "get_parser_sample_status");

        Assert.IsNotNull(tool);
        Assert.AreEqual("Get Parser Sample Status", tool.Descriptor.Annotations!.Title);
        Assert.IsTrue(tool.Descriptor.Annotations.ReadOnlyHint);
        Assert.IsTrue(tool.Descriptor.Annotations.IdempotentHint);
        Assert.IsFalse(tool.Descriptor.Annotations.OpenWorldHint);
    }

    [TestMethod]
    public void InProcess_ParsesAnnotationSample_Resources()
    {
        var toolsetDirectory = GetToolsetDirectory();
        var result = McpPythonParserTestSupport.RunInProcessParser(toolsetDirectory);

        Assert.IsNotNull(result);

        var catalog = Parser.ParseCatalogFromDirectory(toolsetDirectory, _ => result);
        var direct = catalog.Resources.SingleOrDefault(r => r.Descriptor?.Name == "parser_status_resource");
        var template = catalog.Resources.SingleOrDefault(r => r.TemplateDescriptor?.Name == "parser_view_resource");

        Assert.IsNotNull(direct);
        Assert.IsNotNull(template);
        Assert.AreEqual("sample://parser/status", direct.Descriptor!.Uri);
        Assert.IsNotNull(template.TemplateDescriptor);
    }

    [TestMethod]
    public void InProcess_ParsesLowLevelSample()
    {
        var toolsetDirectory = GetToolsetDirectory();
        var result = McpPythonParserTestSupport.RunInProcessParser(toolsetDirectory);

        Assert.IsNotNull(result);

        var catalog = Parser.ParseCatalogFromDirectory(toolsetDirectory, _ => result);
        var tool = catalog.Tools.SingleOrDefault(t => t.Descriptor.Name == "parser_lowlevel_tool");
        var resource = catalog.Resources.SingleOrDefault(r => r.Descriptor?.Name == "parser_lowlevel_resource");

        Assert.IsNotNull(tool);
        Assert.IsNotNull(resource);
        Assert.AreEqual("Parser Low-Level Tool", tool.Descriptor.Title);
    }

    [TestMethod]
    public void InProcess_OutputIsValidJson()
    {
        var toolsetDirectory = GetToolsetDirectory();
        var result = McpPythonParserTestSupport.RunInProcessParser(toolsetDirectory);

        Assert.IsNotNull(result);

        var doc = JsonDocument.Parse(result);
        Assert.IsTrue(doc.RootElement.TryGetProperty("tools", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("resources", out _));
        Assert.IsFalse(doc.RootElement.TryGetProperty("prompts", out _));
    }

    private static string GetToolsetDirectory() =>
        Path.Combine(FindRepositoryRoot(), "samples", "PythonDemo", "mcp_toolset");

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RevitDevTool.slnx"))
                || File.Exists(Path.Combine(current.FullName, "RevitDevTool.sln")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the RevitDevTool repository root.");
    }
}
