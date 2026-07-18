using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.BuiltIn;

/// <summary>Registers built-in MCP tools, resources, and prompts into the catalog.</summary>
public sealed class BuiltInMcpRegistryProvider(
    IEnumerable<IBuiltInMcpTool> builtInTools,
    IEnumerable<IBuiltInMcpResource> builtInResources,
    IEnumerable<IBuiltInMcpPrompt> builtInPrompts,
    IMcpHostExecution hostExecution) : IMcpRegistryProvider, IMcpServerPrimitiveProvider
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
            var primitive = builtIn.Primitive;
            var binding = McpPrimitiveBinding.Create(
                ExecutionMode.CSharp,
                sourcePath: null,
                containerType: "BuiltIn",
                methodName: primitive.ProtocolTool.Name,
                groupName: "Built-in");

            var id = McpPrimitiveBinding.CreatePrimitiveId(primitive.ProtocolTool.Name, binding.SourceAddress);

            tools.Add(new McpRegisteredTool
            {
                Id = id,
                ProtocolTool = primitive.ProtocolTool,
                Binding = binding
            });
        }

        var resources = new List<McpRegisteredResource>();
        foreach (var builtIn in builtInResources)
        {
            var primitive = McpHostExecutionPrimitives.Wrap(builtIn.Primitive, hostExecution);
            var protocol = primitive.ProtocolResource!;
            var binding = McpPrimitiveBinding.Create(
                ExecutionMode.CSharp,
                sourcePath: null,
                containerType: "BuiltIn",
                methodName: protocol.Name,
                groupName: "Built-in");

            var id = McpPrimitiveBinding.CreatePrimitiveId(protocol.Name, binding.SourceAddress);

            resources.Add(new McpRegisteredResource
            {
                Id = id,
                ProtocolResource = protocol,
                Binding = binding
            });
        }

        var prompts = new List<McpRegisteredPrompt>();
        foreach (var builtIn in builtInPrompts)
        {
            var primitive = McpHostExecutionPrimitives.Wrap(builtIn.Primitive, hostExecution);
            var binding = McpPrimitiveBinding.Create(
                ExecutionMode.CSharp,
                sourcePath: null,
                containerType: "BuiltIn",
                methodName: primitive.ProtocolPrompt.Name,
                groupName: "Built-in");

            var id = McpPrimitiveBinding.CreatePrimitiveId(primitive.ProtocolPrompt.Name, binding.SourceAddress);

            prompts.Add(new McpRegisteredPrompt
            {
                Id = id,
                ProtocolPrompt = primitive.ProtocolPrompt,
                Binding = binding
            });
        }

        return new McpRegistryCatalog { Tools = tools, Resources = resources, Prompts = prompts };
    }

    public McpServerTool? CreateTool(McpRegisteredTool registration) => builtInTools
        .Select(tool => tool.Primitive)
        .Where(tool => string.Equals(tool.ProtocolTool.Name, registration.ProtocolTool.Name, StringComparison.OrdinalIgnoreCase))
        .Select(BuiltInToolExecution.Wrap)
        .FirstOrDefault();

    public McpServerPrompt? CreatePrompt(McpRegisteredPrompt registration) => builtInPrompts
        .Select(prompt => McpHostExecutionPrimitives.Wrap(prompt.Primitive, hostExecution))
        .FirstOrDefault(prompt => string.Equals(prompt.ProtocolPrompt.Name, registration.ProtocolPrompt.Name, StringComparison.OrdinalIgnoreCase));

    public McpServerResource? CreateResource(McpRegisteredResource registration)
    {
        var identity = registration.ProtocolResource?.Uri ?? registration.ProtocolTemplate?.UriTemplate;
        return builtInResources
            .Select(resource => McpHostExecutionPrimitives.Wrap(resource.Primitive, hostExecution))
            .FirstOrDefault(resource => string.Equals(
                resource.ProtocolResource?.Uri ?? resource.ProtocolResourceTemplate.UriTemplate,
                identity,
                StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Preserves the built-in tool's async guard lifecycle while delegating parameter binding to the SDK primitive.</summary>
internal static class BuiltInToolExecution
{
    public static McpServerTool Wrap(McpServerTool primitive) => new GuardedTool(primitive);

    private sealed class GuardedTool(McpServerTool primitive) : McpServerTool
    {
        public override Tool ProtocolTool => primitive.ProtocolTool;
        public override IReadOnlyList<object> Metadata => primitive.Metadata;

        public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
        {
            var previousMode = ExecutionGuardContext.Mode;
            ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
            try
            {
                return await primitive.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ExecutionGuardContext.Mode = previousMode;
            }
        }
    }
}
