using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Models;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

[TestClass]
public sealed class McpRegistryModelsTests
{
    [TestMethod]
    public void RegistryCatalog_Merge_CombinesToolsAndResources()
    {
        var left = new RegistryCatalog
        {
            Tools = [CreateTool("left")],
            Resources = [CreateResource("left://resource")],
        };
        var right = new RegistryCatalog
        {
            Tools = [CreateTool("right")],
            Resources = [CreateResource("right://resource")],
        };

        var merged = left.Merge(right);

        Assert.AreSequenceEqual(["left", "right"], merged.Tools.Select(tool => tool.Id));
        Assert.AreSequenceEqual(["left://resource", "right://resource"], merged.Resources.Select(resource => resource.Id));
    }

    [TestMethod]
    public void RegistryCatalog_Empty_IsSingleton()
    {
        Assert.IsEmpty(RegistryCatalog.Empty.Tools);
        Assert.IsEmpty(RegistryCatalog.Empty.Resources);
    }

    [TestMethod]
    public void PrimitiveBinding_CreatePrimitiveId_KeepsNameAndAddress()
    {
        var id = PrimitiveBinding.CreatePrimitiveId("My Tool", @"pkg\tool.py:Main.run");

        Assert.AreEqual(@"My Tool_[pkg\tool.py:Main.run]", id);
    }

    [TestMethod]
    public void RegisteredResource_DisplayName_PrefersDescriptorName()
    {
        var fromDescriptor = new RegisteredResource
        {
            Id = "r1",
            Descriptor = new Resource { Name = "demo_status", Uri = "demo://status" },
            Binding = PrimitiveBinding.Create(ExecutionMode.Dotnet, "x.dll", "X", "Read", "", ""),
        };
        var fromTemplate = new RegisteredResource
        {
            Id = "r2",
            TemplateDescriptor = new ResourceTemplate { Name = "template_name", UriTemplate = "demo://{id}" },
            Binding = PrimitiveBinding.Create(ExecutionMode.Dotnet, "x.dll", "X", "Read", "", ""),
        };

        Assert.AreEqual("demo_status", fromDescriptor.DisplayName);
        Assert.AreEqual("template_name", fromTemplate.DisplayName);
    }

    private static RegisteredTool CreateTool(string id) => new()
    {
        Id = id,
        Descriptor = new Tool { Name = id, InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }) },
        Binding = PrimitiveBinding.Create(ExecutionMode.Dotnet, "stub.dll", "Stub", id, "", ""),
    };

    private static RegisteredResource CreateResource(string uri) => new()
    {
        Id = uri,
        Descriptor = new Resource { Name = uri, Uri = uri },
        Binding = PrimitiveBinding.Create(ExecutionMode.Dotnet, "stub.dll", "Stub", "Read", "", ""),
    };
}
