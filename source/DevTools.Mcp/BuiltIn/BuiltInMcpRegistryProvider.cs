namespace DevTools.Mcp.BuiltIn;

/// <summary>Registers built-in MCP tools, resources, and prompts into the catalog.</summary>
public sealed class BuiltInMcpRegistryProvider(
    IEnumerable<IBuiltInMcpTool> builtInTools,
    IEnumerable<IBuiltInMcpResource> builtInResources,
    IEnumerable<IBuiltInMcpPrompt> builtInPrompts) : IMcpRegistryProvider
{
    public string Name => "built-in";
    public int Priority => 0;
    public ExecutionMode SourceKind => ExecutionMode.CSharp;

    public void ConfigurePaths(IReadOnlyList<string> paths)
    {
    }

    public McpRegistryCatalog LoadCatalog()
    {
        var tools = new List<McpRegisteredTool>();
        foreach (var builtIn in builtInTools)
        {
            var binding = McpPrimitiveBinding.Create(
                ExecutionMode.CSharp,
                sourcePath: null,
                containerType: "BuiltIn",
                methodName: builtIn.Name,
                groupName: "Built-in");

            var id = McpPrimitiveBinding.CreatePrimitiveId(builtIn.ProtocolTool.Name, binding.SourceAddress);

            tools.Add(new McpRegisteredTool
            {
                Id = id,
                ProtocolTool = builtIn.ProtocolTool,
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
                ProtocolResource = builtIn.ProtocolResource,
                Binding = binding
            });
        }

        var prompts = new List<McpRegisteredPrompt>();
        foreach (var builtIn in builtInPrompts)
        {
            var binding = McpPrimitiveBinding.Create(
                ExecutionMode.CSharp,
                sourcePath: null,
                containerType: "BuiltIn",
                methodName: builtIn.ProtocolPrompt.Name,
                groupName: "Built-in");

            var id = McpPrimitiveBinding.CreatePrimitiveId(builtIn.ProtocolPrompt.Name, binding.SourceAddress);

            prompts.Add(new McpRegisteredPrompt
            {
                Id = id,
                ProtocolPrompt = builtIn.ProtocolPrompt,
                Binding = binding
            });
        }

        return new McpRegistryCatalog { Tools = tools, Resources = resources, Prompts = prompts };
    }
}
