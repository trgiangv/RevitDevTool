using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Catalog.Discovery;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

public sealed class McpCatalogCreateOptionsTests
{
    [Fact]
    public void ForResource_UsesCatalogUriTemplate_NotSdkFallback()
    {
        var resource = new McpRegisteredResource
        {
            Id = "revit_element",
            TemplateDescriptor = new ResourceTemplate
            {
                Name = "revit_element",
                UriTemplate = "revit://element/{elementId}",
                MimeType = "application/json",
            },
            Binding = McpPrimitiveBinding.Create(
                ExecutionMode.Dotnet,
                "toolset.dll",
                "RevitMcpToolSet.Resources",
                "GetElement"),
        };

        var options = McpCatalogCreateOptions.ForResource(resource);

        Assert.Equal("revit://element/{elementId}", options.UriTemplate);
        Assert.Equal("revit_element", options.Name);
        Assert.Equal("application/json", options.MimeType);
    }

    [Fact]
    public void ForTool_PropagatesStructuredOutputFlag()
    {
        var tool = new McpRegisteredTool
        {
            Id = "revit_find_elements",
            Descriptor = new Tool
            {
                Name = "revit_find_elements",
                Title = "Find Elements",
                Description = "Find",
                OutputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }),
            },
            Binding = McpPrimitiveBinding.Create(
                ExecutionMode.Dotnet,
                "toolset.dll",
                "RevitMcpToolSet.Tools",
                "FindElements"),
        };

        var options = McpCatalogCreateOptions.ForTool(tool);

        Assert.Equal("revit_find_elements", options.Name);
        Assert.True(options.UseStructuredContent);
    }
}
