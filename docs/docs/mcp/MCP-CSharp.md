# MCP .NET

Use the official .NET MCP SDK to create a toolset for Revit or an AutoCAD-family host. A toolset is a .NET assembly that you register in **Settings → MCP**.

## What a .NET toolset provides

A toolset can expose tools for actions or queries, resources for reference information or live model context, and prompts that help an AI client start a repeatable workflow.

The host discovers the registered assembly and makes its capabilities available to the connected MCP client. The client searches for a capability, invokes it, and reads the result.

## Create the class library

Create a normal **Class Library** project, just as you would for a Revit or AutoCAD add-in. Follow the structure shown by the official .NET SDK, add the MCP SDK reference, implement the tools/resources/prompts you need, and build for the Autodesk host version you target.

```xml
<PackageReference Include="ModelContextProtocol" Version="2.2.0" />
```

Register the resulting `.dll` in **Settings → MCP**. Keep the toolset's host API references aligned with the Autodesk year.

## Runtime guidance

- Register the compiled assembly path, not a source file.
- Build against the host version that will load the toolset.
- Keep tool inputs small and return structured results where practical.
- Use host transactions and document rules for operations that modify a model.
- Confirm the tool appears in **Settings → MCP** before using it from an AI client.

For the MCP concepts and the default capabilities already provided by RevitDevTool, see [MCP Protocol](/docs/mcp/MCP-Protocol).

Official references: [C# SDK Getting Started](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/getting-started.md) · [C# SDK repository](https://github.com/modelcontextprotocol/csharp-sdk).

## Related

- [MCP Python](/docs/mcp/MCP-Python)
- [MCP Protocol](/docs/mcp/MCP-Protocol)
