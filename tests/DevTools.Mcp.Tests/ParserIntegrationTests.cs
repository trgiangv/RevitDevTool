using System.Text.Json;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
namespace DevTools.Mcp.Tests;

public sealed class ParserIntegrationTests
{
    private static readonly PythonToolsetParser PythonParser = new(NullLogger<PythonToolsetParser>.Instance);
    private static readonly McpAssemblyParser Parser = new(NullLogger<McpAssemblyParser>.Instance);
    [Fact]
    public void DotnetParser_ExtractsSampleToolAnnotations()
    {
        var tools = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath()).Tools;
        var toolRegistration = tools.Single(item => item.Descriptor.Name == "get_demo_status");
        var advancedRegistration = tools.Single(item => item.Descriptor.Name == "get_advanced_demo_status");
        var tool = toolRegistration.Descriptor;
        var advanced = advancedRegistration.Descriptor;
        var protocolTool = toolRegistration.Descriptor;

        Assert.Equal("Get Demo Status", tool.Annotations!.Title);
        Assert.Equal("Get Demo Status", tool.Title);
        Assert.True(tool.Annotations.ReadOnlyHint);
        Assert.True(tool.Annotations.IdempotentHint);
        Assert.False(tool.Annotations.OpenWorldHint);
        Assert.Null(tool.Annotations.DestructiveHint);
        Assert.Equal("get_demo_status", protocolTool.Name);
        Assert.Equal("Get Demo Status", protocolTool.Title);
        Assert.True(protocolTool.Annotations!.ReadOnlyHint);
        Assert.True(protocolTool.Annotations.IdempotentHint);
        Assert.False(protocolTool.Annotations.OpenWorldHint);
        Assert.Equal(JsonValueKind.Object, protocolTool.InputSchema.ValueKind);

        Assert.Equal("1.0", advanced.Meta?["version"]?.GetValue<string>());
        Assert.True(advanced.Meta?["isBeta"]?.GetValue<bool>() ?? false);
        AssertJsonObjectHasProperty(advanced.InputSchema.GetRawText(), "properties", "topic");
        AssertJsonMissingNestedProperty(advanced.InputSchema.GetRawText(), "properties", "cancellationToken");
        AssertJsonMissingNestedProperty(advanced.InputSchema.GetRawText(), "properties", "serviceProvider");
        AssertJsonMissingNestedProperty(advanced.InputSchema.GetRawText(), "properties", "server");
        AssertJsonMissingNestedProperty(advanced.InputSchema.GetRawText(), "properties", "progress");
        AssertJsonMissingNestedProperty(advanced.InputSchema.GetRawText(), "properties", "dependency");
    }

    [Fact]
    public void PythonParser_ExtractsSampleToolAnnotations()
    {
        var toolsetDirectory = GetPythonToolsetDirectory();
        var sampleModulePath = Path.Combine(toolsetDirectory, "tests", "parser_annotation_sample.py");

        Assert.True(Directory.Exists(toolsetDirectory), $"Expected Python sample toolset at '{toolsetDirectory}'.");
        Assert.True(File.Exists(sampleModulePath), $"Expected parser sample module at '{sampleModulePath}'.");

        var tools = PythonParser.ParseDirectoryCatalog(toolsetDirectory, GetPythonExecutablePath(), GetToolParserScriptPath()).Tools;
        var toolRegistration = tools.Single(item => item.Descriptor.Name == "get_parser_sample_status");
        var tool = toolRegistration.Descriptor;

        Assert.NotNull(tool.Annotations);
        Assert.Equal("Get Parser Sample Status", tool.Annotations!.Title);
        Assert.Equal("Get Parser Sample Status", tool.Title);
        Assert.True(tool.Annotations.ReadOnlyHint);
        Assert.True(tool.Annotations.IdempotentHint);
        Assert.False(tool.Annotations.OpenWorldHint);
        Assert.Null(tool.Annotations.DestructiveHint);
        Assert.NotNull(tool.OutputSchema);
        AssertJsonObjectHasProperty(tool.OutputSchema!.Value.GetRawText(), "properties", "status");
        Assert.Equal("https://example.com/icons/tool.png", tool.Icons![0].Source);
        Assert.Equal("mcpserver", tool.Meta?["feature"]?.GetValue<string>());
        Assert.Equal("get_parser_sample_status", tool.Name);
        Assert.Equal("Get Parser Sample Status", tool.Title);
        Assert.True(tool.Annotations!.ReadOnlyHint);
        Assert.Equal(JsonValueKind.Object, tool.OutputSchema!.Value.ValueKind);
    }

    [Fact]
    public void PythonParser_ExtractsLowLevelToolsAndResources()
    {
        var catalog = PythonParser.ParseDirectoryCatalog(GetPythonToolsetDirectory(), GetPythonExecutablePath(), GetToolParserScriptPath());

        var tool = catalog.Tools.Single(item => item.Descriptor.Name == "parser_lowlevel_tool").Descriptor;
        var directResource = catalog.Resources.Single(item => item.Descriptor?.Name == "parser_lowlevel_resource").Descriptor!;
        var templateResource = catalog.Resources.Single(item => item.TemplateDescriptor?.Name == "parser_lowlevel_template").TemplateDescriptor!;

        Assert.Equal("Parser Low-Level Tool", tool.Title);
        Assert.True(tool.Annotations!.ReadOnlyHint);
        Assert.True(tool.Annotations.IdempotentHint);
        AssertJsonObjectHasProperty(tool.OutputSchema!.Value.GetRawText(), "properties", "status");
        Assert.Equal("https://example.com/icons/lowlevel-tool.png", tool.Icons![0].Source);
        Assert.Equal("lowlevel", tool.Meta?["feature"]?.GetValue<string>());

        Assert.Equal("sample://lowlevel/status", directResource.Uri);
        Assert.Equal("text/plain", directResource.MimeType);
        Assert.Equal(128, directResource.Size);
        Assert.Equal("https://example.com/icons/lowlevel-resource.png", directResource.Icons![0].Source);
        Assert.Equal("resource", directResource.Meta?["kind"]?.GetValue<string>());
        Assert.Equal(0.8, directResource.Annotations?.Priority ?? 0, 3);

        Assert.Equal("sample://lowlevel/items/{item_id}", templateResource.UriTemplate);
        Assert.Equal("application/json", templateResource.MimeType);
        Assert.Equal("https://example.com/icons/lowlevel-template.png", templateResource.Icons![0].Source);
        Assert.Equal("template", templateResource.Meta?["kind"]?.GetValue<string>());
        Assert.Equal(0.5, templateResource.Annotations?.Priority ?? 0, 3);
    }

    [Fact]
    public void PythonParser_ExtractsMcpServerResources()
    {
        var resources = PythonParser.ParseDirectoryCatalog(GetPythonToolsetDirectory(), GetPythonExecutablePath(), GetToolParserScriptPath()).Resources;
        var directReg = resources.Single(item => item.Descriptor?.Name == "parser_status_resource");
        var templatedReg = resources.Single(item => item.TemplateDescriptor?.Name == "parser_view_resource");
        var direct = directReg.Descriptor!;
        var templated = templatedReg.TemplateDescriptor!;

        Assert.Equal("sample://parser/status", direct.Uri);
        Assert.Equal("application/json", direct.MimeType);
        Assert.Equal("https://example.com/icons/resource-status.png", direct.Icons![0].Source);
        Assert.Equal("status", direct.Meta?["kind"]?.GetValue<string>());
        Assert.Equal(0.9, direct.Annotations?.Priority ?? 0, 3);

        Assert.Equal("sample://parser/views/{view_id}", templated.UriTemplate);
        Assert.Equal("application/json", templated.MimeType);
        Assert.Equal("https://example.com/icons/resource-view.png", templated.Icons![0].Source);
        Assert.Equal("view", templated.Meta?["kind"]?.GetValue<string>());
        Assert.Equal(0.6, templated.Annotations?.Priority ?? 0, 3);
    }

    [Fact]
    public void DotnetParser_ExtractsSampleResources()
    {
        var resources = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath()).Resources;
        var directRegistration = resources.Single(item => item.Descriptor?.Name == "demo_status");
        var templatedRegistration = resources.Single(item => item.TemplateDescriptor?.Name == "demo_view");
        var derivedRegistration = resources.Single(item => item.TemplateDescriptor?.Name == "demo_level");
        var direct = directRegistration.Descriptor!;
        var templated = templatedRegistration.TemplateDescriptor!;
        var derived = derivedRegistration.TemplateDescriptor!;

        Assert.Equal("sample://demo/status", direct.Uri);
        Assert.Equal("https://example.com/icons/resource-status.png", direct.Icons![0].Source);
        Assert.Equal("status", direct.Meta?["resourceKind"]?.GetValue<string>());
        Assert.NotNull(directRegistration.Descriptor);
        Assert.Null(directRegistration.TemplateDescriptor);
        Assert.Equal("sample://demo/status", directRegistration.Descriptor!.Uri);

        Assert.Equal("sample://demo/views/{viewId}", templated.UriTemplate);
        Assert.Equal("application/json", templated.MimeType);
        Assert.Equal("https://example.com/icons/resource-view.png", templated.Icons![0].Source);
        Assert.Equal("view", templated.Meta?["resourceKind"]?.GetValue<string>());
        Assert.Null(templatedRegistration.Descriptor);
        Assert.NotNull(templatedRegistration.TemplateDescriptor);
        Assert.Equal("sample://demo/views/{viewId}", templatedRegistration.TemplateDescriptor!.UriTemplate);

        Assert.Equal("resource://demo_level/{levelId}", derived.UriTemplate);
        Assert.NotNull(derivedRegistration.TemplateDescriptor);
    }

    [Fact]
    public void DotnetParser_ToolAnnotations_AllHintsMapped()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var tool = catalog.Tools.Single(t => t.Descriptor.Name == "get_nested_meta").Descriptor;

        Assert.NotNull(tool.Annotations);
        Assert.True(tool.Annotations!.DestructiveHint);
        Assert.True(tool.Annotations.OpenWorldHint);
        Assert.Null(tool.Annotations.ReadOnlyHint);
        Assert.Null(tool.Annotations.IdempotentHint);
    }

    [Fact]
    public void DotnetParser_InfrastructureParams_ExcludedFromSchema()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var tool = catalog.Tools.Single(t => t.Descriptor.Name == "get_advanced_demo_status").Descriptor;
        var schemaJson = tool.InputSchema.GetRawText();

        AssertJsonObjectHasProperty(schemaJson, "properties", "topic");
        AssertJsonMissingNestedProperty(schemaJson, "properties", "cancellationToken");
        AssertJsonMissingNestedProperty(schemaJson, "properties", "serviceProvider");
        AssertJsonMissingNestedProperty(schemaJson, "properties", "server");
        AssertJsonMissingNestedProperty(schemaJson, "properties", "progress");
        AssertJsonMissingNestedProperty(schemaJson, "properties", "dependency");
    }

    [Fact]
    public void DotnetParser_NullableParam_UnwrappedToBaseType()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var tool = catalog.Tools.Single(t => t.Descriptor.Name == "get_nullable_count").Descriptor;
        using var doc = JsonDocument.Parse(tool.InputSchema.GetRawText());
        var countProp = doc.RootElement.GetProperty("properties").GetProperty("count");

        Assert.Equal("integer", countProp.GetProperty("type").GetString());
    }

    [Fact]
    public void DotnetParser_ToolWithNoUserParams_ProducesEmptySchema()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var tool = catalog.Tools.Single(t => t.Descriptor.Name == "ping_infrastructure").Descriptor;
        using var doc = JsonDocument.Parse(tool.InputSchema.GetRawText());

        Assert.Equal("object", doc.RootElement.GetProperty("type").GetString());
        Assert.False(doc.RootElement.TryGetProperty("required", out _));
        if (doc.RootElement.TryGetProperty("properties", out var props))
            Assert.Empty(props.EnumerateObject().ToList());
    }

    [Fact]
    public void DotnetParser_Resource_VsResourceTemplate_Discrimination()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());

        var directReg = catalog.Resources.Single(r => r.Descriptor?.Name == "demo_status");
        Assert.NotNull(directReg.Descriptor);
        Assert.Null(directReg.TemplateDescriptor);

        var templatedReg = catalog.Resources.Single(r => r.TemplateDescriptor?.Name == "demo_view");
        Assert.Null(templatedReg.Descriptor);
        Assert.NotNull(templatedReg.TemplateDescriptor);
    }

    [Fact]
    public void DotnetParser_Meta_MixedValueTypes()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var tool = catalog.Tools.Single(t => t.Descriptor.Name == "get_nested_meta").Descriptor;

        Assert.NotNull(tool.Meta);
        Assert.Equal(JsonValueKind.String, tool.Meta!["version"]!.GetValueKind());
        Assert.Equal("2.0", tool.Meta["version"]!.GetValue<string>());
        Assert.Equal(JsonValueKind.Object, tool.Meta["flags"]!.GetValueKind());
        Assert.Equal(1, tool.Meta["flags"]!["nested"]!.GetValue<int>());
        Assert.True(tool.Meta["flags"]!["active"]!.GetValue<bool>());
    }

    [Fact]
    public void DotnetParser_Icons_ParsedFromIconSource()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var tool = catalog.Tools.Single(t => t.Descriptor.Name == "get_advanced_demo_status").Descriptor;

        Assert.NotNull(tool.Icons);
        Assert.Single(tool.Icons);
        Assert.Equal("https://dohoasaigon.com/wp-content/uploads/2025/03/revit-2024.png", tool.Icons![0].Source);
    }

    [Fact]
    public void DotnetParser_Title_FallsBackToName()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var tool = catalog.Tools.Single(t => t.Descriptor.Name == "get_nullable_count").Descriptor;

        Assert.Equal("get_nullable_count", tool.Title);
    }

    [Fact]
    public void DotnetParser_Resource_WithoutUriTemplate_GetsFallback()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var healthReg = catalog.Resources.Single(r =>
            r.Descriptor?.Name == "demo_health" || r.TemplateDescriptor?.Name == "demo_health");

        Assert.NotNull(healthReg.Descriptor);
        Assert.Null(healthReg.TemplateDescriptor);
        Assert.Equal("text/plain", healthReg.Descriptor!.MimeType);
    }

    private static string GetSampleAssemblyPath()
    {
        var candidates = new[]
        {
            Path.Combine(FindRepositoryRoot(), "samples", "McpToolsetDemo", "bin", "Debug.Autodesk.2025", "McpToolsetDemo.dll"),
            Path.Combine(FindRepositoryRoot(), "samples", "McpToolsetDemo", "bin", "Debug", "net8.0", "McpToolsetDemo.dll"),
        };

        var sampleAssembly = candidates.FirstOrDefault(File.Exists);
        Assert.True(sampleAssembly is not null, $"Expected sample tool assembly at one of: {string.Join(", ", candidates)}. Build McpToolsetDemo before running this test.");
        return sampleAssembly!;
    }

    private static string GetPythonToolsetDirectory()
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "samples",
            "PythonDemo",
            "mcp_toolset");
    }

    private static string GetPythonExecutablePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var pythonExecutablePath = Path.Combine(appData, "RevitDevTool", "pixi-env", ".pixi", "envs", "default", "python.exe");
        Assert.True(File.Exists(pythonExecutablePath), $"Expected Python environment at '{pythonExecutablePath}'.");
        return pythonExecutablePath;
    }

    private static string GetToolParserScriptPath()
    {
        var path = Path.Combine(FindRepositoryRoot(), "source", "DevTools.Execution", "Resources", "scripts", "ToolParser.py");
        Assert.True(File.Exists(path), $"Expected ToolParser.py at '{path}'.");
        return path;
    }

    private static void AssertJsonObjectHasProperty(string json, string parentProperty, string childProperty)
    {
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty(parentProperty, out var parent));
        Assert.True(parent.TryGetProperty(childProperty, out _));
    }

    private static void AssertJsonMissingNestedProperty(string json, string parentProperty, string missingProperty)
    {
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.GetProperty(parentProperty).TryGetProperty(missingProperty, out _));
    }

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
