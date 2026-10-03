using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;

namespace DevTools.Mcp;

/// <summary>Registers built-in MCP tools and resources into the host catalog. Prompts are daemon-owned.</summary>
public sealed class BuiltInMcpRegistryProvider(
    IEnumerable<IBuiltInMcpTool> builtInTools,
    IEnumerable<IBuiltInMcpResource> builtInResources) : IRegistryProvider
{
    public string Name => "built-in";
    public ExecutionMode SourceKind => ExecutionMode.CSharp;

    public void ConfigurePaths(IReadOnlyList<string> paths)
    {
    }

    public RegistryCatalog LoadCatalog()
    {
        var tools = new List<RegisteredTool>();
        foreach (var builtIn in builtInTools)
        {
            var protocolTool = builtIn.ServerTool.ProtocolTool;
            var sourceAddress = $"BuiltIn.{builtIn.Name}";
            var binding = PrimitiveBinding.Create(
                ExecutionMode.CSharp,
                sourcePath: null,
                containerType: "BuiltIn",
                methodName: builtIn.Name,
                sourceAddress: sourceAddress,
                groupName: "Built-in");

            var id = PrimitiveBinding.CreatePrimitiveId(protocolTool.Name, sourceAddress);

            tools.Add(new RegisteredTool
            {
                Id = id,
                Descriptor = protocolTool,
                Binding = binding
            });
        }

        var resources = new List<RegisteredResource>();
        foreach (var builtIn in builtInResources)
        {
            var sourceAddress = $"BuiltIn.{builtIn.ProtocolResource.Name}";
            var binding = PrimitiveBinding.Create(
                ExecutionMode.CSharp,
                sourcePath: null,
                containerType: "BuiltIn",
                methodName: builtIn.ProtocolResource.Name,
                sourceAddress: sourceAddress,
                groupName: "Built-in");

            var id = PrimitiveBinding.CreatePrimitiveId(builtIn.ProtocolResource.Name, sourceAddress);

            resources.Add(new RegisteredResource
            {
                Id = id,
                Descriptor = builtIn.ProtocolResource,
                Binding = binding
            });
        }

        return new RegistryCatalog { Tools = tools, Resources = resources };
    }
}
