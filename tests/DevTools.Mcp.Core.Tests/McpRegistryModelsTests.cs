using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Models;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

[TestClass]
public sealed class McpRegistryModelsTests
{
    [TestMethod]
    public void McpRegistryCatalog_Merge_CombinesToolsAndResources()
    {
        var left = new McpRegistryCatalog
        {
            Tools = [CreateTool("left")],
            Resources = [CreateResource("left://resource")],
        };
        var right = new McpRegistryCatalog
        {
            Tools = [CreateTool("right")],
            Resources = [CreateResource("right://resource")],
        };

        var merged = left.Merge(right);

        Assert.AreSequenceEqual(["left", "right"], merged.Tools.Select(tool => tool.Id));
        Assert.AreSequenceEqual(["left://resource", "right://resource"], merged.Resources.Select(resource => resource.Id));
    }

    [TestMethod]
    public void McpRegistryCatalog_Empty_IsSingleton()
    {
        Assert.IsEmpty(McpRegistryCatalog.Empty.Tools);
        Assert.IsEmpty(McpRegistryCatalog.Empty.Resources);
    }

    [TestMethod]
    public void McpPrimitiveBinding_Create_BuildsFallbackAddressAndGroup()
    {
        var binding = McpPrimitiveBinding.Create(
            ExecutionMode.Python,
            sourcePath: @"C:\toolsets\demo\tools.py",
            containerType: "DemoTools",
            methodName: "ping");

        Assert.AreEqual(ExecutionMode.Python, binding.SourceKind);
        Assert.AreEqual("tools:DemoTools.ping", binding.SourceAddress);
        Assert.AreEqual("demo", binding.GroupName);
    }

    [TestMethod]
    public void McpPrimitiveBinding_CreatePrimitiveId_NormalizesSegments()
    {
        var id = McpPrimitiveBinding.CreatePrimitiveId("My Tool", @"pkg\tool.py:Main.run");

        Assert.AreEqual("My-Tool_[pkg/tool.py:Main.run]", id);
    }

    [TestMethod]
    public void McpRegisteredResource_DisplayName_PrefersDescriptorName()
    {
        var fromDescriptor = new McpRegisteredResource
        {
            Id = "r1",
            Descriptor = new Resource { Name = "demo_status", Uri = "demo://status" },
            Binding = McpPrimitiveBinding.Create(ExecutionMode.Dotnet, "x.dll", "X", "Read"),
        };
        var fromTemplate = new McpRegisteredResource
        {
            Id = "r2",
            TemplateDescriptor = new ResourceTemplate { Name = "template_name", UriTemplate = "demo://{id}" },
            Binding = McpPrimitiveBinding.Create(ExecutionMode.Dotnet, "x.dll", "X", "Read"),
        };

        Assert.AreEqual("demo_status", fromDescriptor.DisplayName);
        Assert.AreEqual("template_name", fromTemplate.DisplayName);
    }

    private static McpRegisteredTool CreateTool(string id) => new()
    {
        Id = id,
        Descriptor = new Tool { Name = id, InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }) },
        Binding = McpPrimitiveBinding.Create(ExecutionMode.Dotnet, "stub.dll", "Stub", id),
    };

    private static McpRegisteredResource CreateResource(string uri) => new()
    {
        Id = uri,
        Descriptor = new Resource { Name = uri, Uri = uri },
        Binding = McpPrimitiveBinding.Create(ExecutionMode.Dotnet, "stub.dll", "Stub", "Read"),
    };
}
