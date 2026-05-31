using DevTools.Execution.External.Mcp.Registry;
using DevTools.McpParser.Models;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>Registers built-in MCP tools into the tool catalog.</summary>
public sealed class BuiltInToolRegistryProvider(IEnumerable<IBuiltInMcpTool> builtInTools) : IMcpRegistryProvider
{
    public string Name => "built-in";
    public ExecutionMode SourceKind => ExecutionMode.CSharp;

    public void ConfigurePaths(IReadOnlyList<string> paths)
    {
        // Built-in tools don't use external paths.
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
                groupName: "DevTools Built-in");

            var id = McpPrimitiveBinding.CreatePrimitiveId(builtIn.ProtocolTool.Name, binding.SourceAddress);

            tools.Add(new McpRegisteredTool
            {
                Id = id,
                ProtocolTool = builtIn.ProtocolTool,
                Binding = binding
            });
        }

        return new McpRegistryCatalog { Tools = tools };
    }
}
