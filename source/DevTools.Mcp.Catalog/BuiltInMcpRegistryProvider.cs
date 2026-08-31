using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;

namespace DevTools.Mcp.Catalog;

/// <summary>Registers built-in MCP tools and resources into the host catalog. Prompts are daemon-owned.</summary>
public sealed class BuiltInMcpRegistryProvider(
    IEnumerable<IBuiltInMcpTool> builtInTools,
    IEnumerable<IBuiltInMcpResource> builtInResources) : IMcpRegistryProvider
{
    public string Name => "built-in";
    public ExecutionMode SourceKind => ExecutionMode.CSharp;

    public void ConfigurePaths(IReadOnlyList<string> paths)
    {
    }

    public McpRegistryCatalog LoadCatalog()
    {
        var tools = new List<McpRegisteredTool>();
        foreach (var builtIn in builtInTools)
        {
            var protocolTool = builtIn.ServerTool.ProtocolTool;
            var binding = McpPrimitiveBinding.Create(
                ExecutionMode.CSharp,
                sourcePath: null,
                containerType: "BuiltIn",
                methodName: builtIn.Name,
                groupName: "Built-in");

            var id = McpPrimitiveBinding.CreatePrimitiveId(protocolTool.Name, binding.SourceAddress);

            tools.Add(new McpRegisteredTool
            {
                Id = id,
                Descriptor = protocolTool,
                Binding = binding
            });
        }

        var resources = new List<McpRegisteredResource>();
        foreach (var builtIn in builtInResources)
        {
            var binding = McpPrimitiveBinding.Create(
                ExecutionMode.CSharp,
                sourcePath: null,
                containerType: "BuiltIn",
                methodName: builtIn.ProtocolResource.Name,
                groupName: "Built-in");

            var id = McpPrimitiveBinding.CreatePrimitiveId(builtIn.ProtocolResource.Name, binding.SourceAddress);

            resources.Add(new McpRegisteredResource
            {
                Id = id,
                Descriptor = builtIn.ProtocolResource,
                Binding = binding
            });
        }

        return new McpRegistryCatalog { Tools = tools, Resources = resources };
    }
}
