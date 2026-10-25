# MCP .NET

Use the official .NET MCP SDK to create a toolset for Revit or an AutoCAD-family host. A toolset is a .NET assembly that you load from the **MCP** toggle in the panel.

<LogoRow items={['dotnet', 'csharp', 'mcp']} />

- **Tools** run actions or queries.
- **Resources** expose reference information or live model context.
- **Prompts** start a repeatable workflow for an AI client.

The host discovers the registered assembly and makes its capabilities available to the connected MCP client. The client searches for a capability, calls it, and reads the result.

## Create the class library

Create a normal **Class Library** project, as you would for a Revit or AutoCAD add-in, and build it for the Autodesk year you target:

```xml title="McpToolsetDemo.csproj"
<ItemGroup>
  <PackageReference Include="ModelContextProtocol" Version="2.3.0">
    <ExcludeAssets>runtime</ExcludeAssets>
  </PackageReference>
</ItemGroup>
```

## Write a tool

Mark a static class with `[McpServerToolType]` and its methods with `[McpServerTool]`:

```csharp title="McpSampleTools.cs"
using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class McpSampleTools
{
    [McpServerTool(
        Name = "get_demo_status",
        Title = "Get Demo Status",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Return demo status.")]
    public static object GetDemoStatus()
    {
        return new { status = "success", summary = "Demo MCP tool is reachable." };
    }
}
```

- `Name` is the name an AI client sees. Use `snake_case`.
- `ReadOnly`, `Idempotent`, `Destructive` and `OpenWorld` describe what the tool does, so clients can treat it safely.
- `[Description]` on the class, method and parameters feeds the search an AI client uses to find your tool.
- `CancellationToken`, `IServiceProvider` and `McpServer` parameters are filled in for you and do not appear in the tool's schema.

## Write a resource

Resources return read-only context from a URI:

```csharp title="McpSampleResources.cs"
[McpServerResourceType]
public static class McpSampleResources
{
    [McpServerResource(UriTemplate = "sample://demo/status", Name = "demo_status", MimeType = "text/plain")]
    [Description("Returns a static status resource.")]
    public static string DemoStatus() => "ok";
}
```

## Load and try it

1. Build the assembly for the host year that will load it.
2. Turn on the **MCP** toggle in the panel and choose **Load .NET Toolset**, then select the built `.dll`.
3. Confirm the tool appears in the MCP list.
4. Ask your AI client to call `get_demo_status`.

For complete samples see [`McpToolsetDemo`](https://github.com/trgiangv/RevitDevTool/tree/main/samples/McpToolsetDemo) (minimal tools, resources and prompts) and [`RevitMcpToolSet`](https://github.com/trgiangv/RevitDevTool/tree/main/samples/RevitMcpToolSet) (a production-style Revit toolset). A walkthrough is in [MCP Toolsets example](/docs/examples/Examples-MCPToolsets).

## Runtime guidance

- Load the compiled assembly, not a source file.
- Keep `ExcludeAssets="runtime"` on the MCP packages; the host provides the MCP SDK.
- Keep tool inputs small and return structured results where practical.
- Use host transactions and document rules for operations that modify a model.

For the MCP concepts and the default capabilities already provided by RevitDevTool, see [MCP Protocol](/docs/mcp/MCP-Protocol).

Official references: [C# SDK Getting Started](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/getting-started.md) · [C# SDK repository](https://github.com/modelcontextprotocol/csharp-sdk).

## Related

- [MCP Python](/docs/mcp/MCP-Python)
- [MCP Protocol](/docs/mcp/MCP-Protocol)
