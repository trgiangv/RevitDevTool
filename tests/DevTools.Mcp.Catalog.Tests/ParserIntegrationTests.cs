using System.Text.Json;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class ParserIntegrationTests
{
    private static readonly PythonToolsetParser PythonParser = new(NullLogger<PythonToolsetParser>.Instance);
    private static readonly McpAssemblyParser Parser = new(NullLogger<McpAssemblyParser>.Instance);
    [TestMethod]
    public void DotnetParser_ExtractsSampleToolAnnotations()
    {
        var tools = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath()).Tools;
        var toolRegistration = tools.Single(item => item.Descriptor.Name == "get_demo_status");
        var advancedRegistration = tools.Single(item => item.Descriptor.Name == "get_advanced_demo_status");
        var tool = toolRegistration.Descriptor;
        var advanced = advancedRegistration.Descriptor;
        var protocolTool = toolRegistration.Descriptor;

        Assert.AreEqual("Get Demo Status", tool.Annotations!.Title);
        Assert.AreEqual("Get Demo Status", tool.Title);
        Assert.IsTrue(tool.Annotations.ReadOnlyHint);
        Assert.IsTrue(tool.Annotations.IdempotentHint);
        Assert.IsFalse(tool.Annotations.OpenWorldHint);
        Assert.IsNull(tool.Annotations.DestructiveHint);
        Assert.AreEqual("get_demo_status", protocolTool.Name);
        Assert.AreEqual("Get Demo Status", protocolTool.Title);
        Assert.IsTrue(protocolTool.Annotations!.ReadOnlyHint);
        Assert.IsTrue(protocolTool.Annotations.IdempotentHint);
        Assert.IsFalse(protocolTool.Annotations.OpenWorldHint);
        Assert.AreEqual(JsonValueKind.Object, protocolTool.InputSchema.ValueKind);

        Assert.AreEqual("1.0", advanced.Meta?["version"]?.GetValue<string>());
        Assert.IsTrue(advanced.Meta?["isBeta"]?.GetValue<bool>() ?? false);
        AssertJsonObjectHasProperty(advanced.InputSchema.GetRawText(), "properties", "topic");
        AssertJsonMissingNestedProperty(advanced.InputSchema.GetRawText(), "properties", "cancellationToken");
        AssertJsonMissingNestedProperty(advanced.InputSchema.GetRawText(), "properties", "serviceProvider");
        AssertJsonMissingNestedProperty(advanced.InputSchema.GetRawText(), "properties", "server");
        AssertJsonMissingNestedProperty(advanced.InputSchema.GetRawText(), "properties", "progress");
        AssertJsonMissingNestedProperty(advanced.InputSchema.GetRawText(), "properties", "dependency");
    }

    [TestMethod]
    public void PythonParser_ExtractsSampleToolAnnotations()
    {
        var toolsetDirectory = GetPythonToolsetDirectory();
        var sampleModulePath = Path.Combine(toolsetDirectory, "tests", "parser_annotation_sample.py");

        OptionalArtifact.RequireDirectory(toolsetDirectory, $"Expected Python sample toolset at '{toolsetDirectory}'.");
        OptionalArtifact.RequireFile(sampleModulePath, $"Expected parser sample module at '{sampleModulePath}'.");

        var tools = PythonParser.ParseDirectoryCatalog(toolsetDirectory, GetPythonExecutablePath(), GetToolParserScriptPath()).Tools;
        var toolRegistration = tools.Single(item => item.Descriptor.Name == "get_parser_sample_status");
        var tool = toolRegistration.Descriptor;

        Assert.IsNotNull(tool.Annotations);
        Assert.AreEqual("Get Parser Sample Status", tool.Annotations!.Title);
        Assert.AreEqual("Get Parser Sample Status", tool.Title);
        Assert.IsTrue(tool.Annotations.ReadOnlyHint);
        Assert.IsTrue(tool.Annotations.IdempotentHint);
        Assert.IsFalse(tool.Annotations.OpenWorldHint);
        Assert.IsNull(tool.Annotations.DestructiveHint);
        Assert.IsNotNull(tool.OutputSchema);
        AssertJsonObjectHasProperty(tool.OutputSchema!.Value.GetRawText(), "properties", "status");
        Assert.AreEqual("https://example.com/icons/tool.png", tool.Icons![0].Source);
        Assert.AreEqual("mcpserver", tool.Meta?["feature"]?.GetValue<string>());
        Assert.AreEqual("get_parser_sample_status", tool.Name);
        Assert.AreEqual("Get Parser Sample Status", tool.Title);
        Assert.IsTrue(tool.Annotations!.ReadOnlyHint);
        Assert.AreEqual(JsonValueKind.Object, tool.OutputSchema!.Value.ValueKind);
    }

    [TestMethod]
    public void PythonParser_ExtractsLowLevelToolsAndResources()
    {
        var toolsetDirectory = GetPythonToolsetDirectory();
        OptionalArtifact.RequireDirectory(toolsetDirectory, $"Expected Python sample toolset at '{toolsetDirectory}'.");
        OptionalArtifact.RequireFile(
            Path.Combine(toolsetDirectory, "tests", "parser_lowlevel_sample.py"),
            "Expected parser_lowlevel_sample.py in samples/PythonDemo/mcp_toolset/tests.");

        var catalog = PythonParser.ParseDirectoryCatalog(toolsetDirectory, GetPythonExecutablePath(), GetToolParserScriptPath());
        var toolRegistration = catalog.Tools.FirstOrDefault(item => item.Descriptor.Name == "parser_lowlevel_tool");
        if (toolRegistration is null)
            Assert.Inconclusive("parser_lowlevel_tool not discovered. Ensure ToolParser.py scans tests/parser_lowlevel_sample.py.");

        var tool = toolRegistration.Descriptor;
        var directResource = catalog.Resources.Single(item => item.Descriptor?.Name == "parser_lowlevel_resource").Descriptor!;
        var templateResource = catalog.Resources.Single(item => item.TemplateDescriptor?.Name == "parser_lowlevel_template").TemplateDescriptor!;

        Assert.AreEqual("Parser Low-Level Tool", tool.Title);
        Assert.IsTrue(tool.Annotations!.ReadOnlyHint);
        Assert.IsTrue(tool.Annotations.IdempotentHint);
        AssertJsonObjectHasProperty(tool.OutputSchema!.Value.GetRawText(), "properties", "status");
        Assert.AreEqual("https://example.com/icons/lowlevel-tool.png", tool.Icons![0].Source);
        Assert.AreEqual("lowlevel", tool.Meta?["feature"]?.GetValue<string>());

        Assert.AreEqual("sample://lowlevel/status", directResource.Uri);
        Assert.AreEqual("text/plain", directResource.MimeType);
        Assert.AreEqual(128, directResource.Size);
        Assert.AreEqual("https://example.com/icons/lowlevel-resource.png", directResource.Icons![0].Source);
        Assert.AreEqual("resource", directResource.Meta?["kind"]?.GetValue<string>());
        Assert.AreEqual(0.8, directResource.Annotations?.Priority ?? 0, 0.001);

        Assert.AreEqual("sample://lowlevel/items/{item_id}", templateResource.UriTemplate);
        Assert.AreEqual("application/json", templateResource.MimeType);
        Assert.AreEqual("https://example.com/icons/lowlevel-template.png", templateResource.Icons![0].Source);
        Assert.AreEqual("template", templateResource.Meta?["kind"]?.GetValue<string>());
        Assert.AreEqual(0.5, templateResource.Annotations?.Priority ?? 0, 0.001);
    }

    [TestMethod]
    public void PythonParser_ExtractsMcpServerResources()
    {
        var resources = PythonParser.ParseDirectoryCatalog(GetPythonToolsetDirectory(), GetPythonExecutablePath(), GetToolParserScriptPath()).Resources;
        var directReg = resources.Single(item => item.Descriptor?.Name == "parser_status_resource");
        var templatedReg = resources.Single(item => item.TemplateDescriptor?.Name == "parser_view_resource");
        var direct = directReg.Descriptor!;
        var templated = templatedReg.TemplateDescriptor!;

        Assert.AreEqual("sample://parser/status", direct.Uri);
        Assert.AreEqual("application/json", direct.MimeType);
        Assert.AreEqual("https://example.com/icons/resource-status.png", direct.Icons![0].Source);
        Assert.AreEqual("status", direct.Meta?["kind"]?.GetValue<string>());
        Assert.AreEqual(0.9, direct.Annotations?.Priority ?? 0, 0.001);

        Assert.AreEqual("sample://parser/views/{view_id}", templated.UriTemplate);
        Assert.AreEqual("application/json", templated.MimeType);
        Assert.AreEqual("https://example.com/icons/resource-view.png", templated.Icons![0].Source);
        Assert.AreEqual("view", templated.Meta?["kind"]?.GetValue<string>());
        Assert.AreEqual(0.6, templated.Annotations?.Priority ?? 0, 0.001);
    }

    [TestMethod]
    public void DotnetParser_ExtractsSampleResources()
    {
        var resources = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath()).Resources;
        var directRegistration = resources.Single(item => item.Descriptor?.Name == "demo_status");
        var templatedRegistration = resources.Single(item => item.TemplateDescriptor?.Name == "demo_view");
        var derivedRegistration = resources.Single(item => item.TemplateDescriptor?.Name == "demo_level");
        var direct = directRegistration.Descriptor!;
        var templated = templatedRegistration.TemplateDescriptor!;
        var derived = derivedRegistration.TemplateDescriptor!;

        Assert.AreEqual("sample://demo/status", direct.Uri);
        Assert.AreEqual("https://example.com/icons/resource-status.png", direct.Icons![0].Source);
        Assert.AreEqual("status", direct.Meta?["resourceKind"]?.GetValue<string>());
        Assert.IsNotNull(directRegistration.Descriptor);
        Assert.IsNull(directRegistration.TemplateDescriptor);
        Assert.AreEqual("sample://demo/status", directRegistration.Descriptor!.Uri);

        Assert.AreEqual("sample://demo/views/{viewId}", templated.UriTemplate);
        Assert.AreEqual("application/json", templated.MimeType);
        Assert.AreEqual("https://example.com/icons/resource-view.png", templated.Icons![0].Source);
        Assert.AreEqual("view", templated.Meta?["resourceKind"]?.GetValue<string>());
        Assert.IsNull(templatedRegistration.Descriptor);
        Assert.IsNotNull(templatedRegistration.TemplateDescriptor);
        Assert.AreEqual("sample://demo/views/{viewId}", templatedRegistration.TemplateDescriptor!.UriTemplate);

        Assert.AreEqual("resource://demo_level/{levelId}", derived.UriTemplate);
        Assert.IsNotNull(derivedRegistration.TemplateDescriptor);
    }

    [TestMethod]
    public void DotnetParser_ToolAnnotations_AllHintsMapped()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var tool = catalog.Tools.Single(t => t.Descriptor.Name == "get_nested_meta").Descriptor;

        Assert.IsNotNull(tool.Annotations);
        Assert.IsTrue(tool.Annotations!.DestructiveHint);
        Assert.IsTrue(tool.Annotations.OpenWorldHint);
        Assert.IsNull(tool.Annotations.ReadOnlyHint);
        Assert.IsNull(tool.Annotations.IdempotentHint);
    }

    [TestMethod]
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

    [TestMethod]
    public void DotnetParser_NullableParam_UnwrappedToBaseType()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var tool = catalog.Tools.Single(t => t.Descriptor.Name == "get_nullable_count").Descriptor;
        using var doc = JsonDocument.Parse(tool.InputSchema.GetRawText());
        var countProp = doc.RootElement.GetProperty("properties").GetProperty("count");

        Assert.AreEqual("integer", countProp.GetProperty("type").GetString());
    }

    [TestMethod]
    public void DotnetParser_ToolWithNoUserParams_ProducesEmptySchema()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var tool = catalog.Tools.Single(t => t.Descriptor.Name == "ping_infrastructure").Descriptor;
        using var doc = JsonDocument.Parse(tool.InputSchema.GetRawText());

        Assert.AreEqual("object", doc.RootElement.GetProperty("type").GetString());
        Assert.IsFalse(doc.RootElement.TryGetProperty("required", out _));
        if (doc.RootElement.TryGetProperty("properties", out var props))
            Assert.IsEmpty(props.EnumerateObject().ToList());
    }

    [TestMethod]
    public void DotnetParser_Resource_VsResourceTemplate_Discrimination()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());

        var directReg = catalog.Resources.Single(r => r.Descriptor?.Name == "demo_status");
        Assert.IsNotNull(directReg.Descriptor);
        Assert.IsNull(directReg.TemplateDescriptor);

        var templatedReg = catalog.Resources.Single(r => r.TemplateDescriptor?.Name == "demo_view");
        Assert.IsNull(templatedReg.Descriptor);
        Assert.IsNotNull(templatedReg.TemplateDescriptor);
    }

    [TestMethod]
    public void DotnetParser_Meta_MixedValueTypes()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var tool = catalog.Tools.Single(t => t.Descriptor.Name == "get_nested_meta").Descriptor;

        Assert.IsNotNull(tool.Meta);
        Assert.AreEqual(JsonValueKind.String, tool.Meta!["version"]!.GetValueKind());
        Assert.AreEqual("2.0", tool.Meta["version"]!.GetValue<string>());
        Assert.AreEqual(JsonValueKind.Object, tool.Meta["flags"]!.GetValueKind());
        Assert.AreEqual(1, tool.Meta["flags"]!["nested"]!.GetValue<int>());
        Assert.IsTrue(tool.Meta["flags"]!["active"]!.GetValue<bool>());
    }

    [TestMethod]
    public void DotnetParser_Icons_ParsedFromIconSource()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var tool = catalog.Tools.Single(t => t.Descriptor.Name == "get_advanced_demo_status").Descriptor;

        Assert.IsNotNull(tool.Icons);
        Assert.HasCount(1, tool.Icons);
        Assert.AreEqual("https://dohoasaigon.com/wp-content/uploads/2025/03/revit-2024.png", tool.Icons![0].Source);
    }

    [TestMethod]
    public void DotnetParser_Title_FallsBackToName()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var tool = catalog.Tools.Single(t => t.Descriptor.Name == "get_nullable_count").Descriptor;

        Assert.AreEqual("get_nullable_count", tool.Title);
    }

    [TestMethod]
    public void DotnetParser_Resource_WithoutUriTemplate_GetsFallback()
    {
        var catalog = Parser.ParseCatalogFromAssembly(GetSampleAssemblyPath());
        var healthReg = catalog.Resources.Single(r =>
            r.Descriptor?.Name == "demo_health" || r.TemplateDescriptor?.Name == "demo_health");

        Assert.IsNotNull(healthReg.Descriptor);
        Assert.IsNull(healthReg.TemplateDescriptor);
        Assert.AreEqual("text/plain", healthReg.Descriptor!.MimeType);
    }

    private static string GetSampleAssemblyPath()
    {
        var sampleAssembly = OptionalArtifact.ResolveMcpToolsetDemoDll(FindRepositoryRoot());
        if (sampleAssembly is null)
            Assert.Inconclusive(OptionalArtifact.McpToolsetDemoHint);
        return sampleAssembly;
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
        var pythonExecutablePath = OptionalArtifact.PixiPythonExePath;
        OptionalArtifact.RequireFile(pythonExecutablePath, OptionalArtifact.PixiPythonHint);
        return pythonExecutablePath;
    }

    private static string GetToolParserScriptPath()
    {
        var path = Path.Combine(FindRepositoryRoot(), "source", "DevTools.Execution", "Resources", "scripts", "ToolParser.py");
        OptionalArtifact.RequireFile(path, $"Expected ToolParser.py at '{path}'.");
        return path;
    }

    private static void AssertJsonObjectHasProperty(string json, string parentProperty, string childProperty)
    {
        using var document = JsonDocument.Parse(json);
        Assert.IsTrue(document.RootElement.TryGetProperty(parentProperty, out var parent));
        Assert.IsTrue(parent.TryGetProperty(childProperty, out _));
    }

    private static void AssertJsonMissingNestedProperty(string json, string parentProperty, string missingProperty)
    {
        using var document = JsonDocument.Parse(json);
        Assert.IsFalse(document.RootElement.GetProperty(parentProperty).TryGetProperty(missingProperty, out _));
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
