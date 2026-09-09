# MCP Python

Use the Python MCP SDK to create a toolset for Revit or an AutoCAD-family host. RevitDevTool discovers a Python toolset from a folder containing an entry file that matches `*mcp.py`. The entry file owns the toolset's package dependencies through PEP 723 metadata. Register the folder in **Settings → MCP**.

RevitDevTool pins the Python SDK to **`2.1.1`**.

```powershell
uv add "mcp==2.1.1"
```

## What a Python toolset provides

A toolset can expose tools, resources, and prompts to an AI client. Keep the public names and descriptions focused on the user task, and return structured data for model queries.

## Create the toolset folder

The sample uses `revitdevtool_mcp.py` at the root of the registered folder. The filename is flexible as long as it ends in `mcp.py`; keep the package folders beside it so normal Python imports work:

```text
mcp_toolset/
├── revitdevtool_mcp.py       # discovery entry point + dependencies
├── tools/                    # @mcp.tool registrations
├── resources/                # @mcp.resource registrations
├── prompts/                  # @mcp.prompt registrations
├── dto/                      # request/response models
├── services/                 # host-facing application logic
└── shared/                   # shared helpers
```

Put the dependency declaration at the top of the `*mcp.py` entry file. RevitDevTool reads that file, resolves its PEP 723 metadata, then loads the module and its sibling packages. Do not put MCP toolset dependencies in `conftest.py`; that file belongs to pytest discovery, not MCP registration:

```python
# revitdevtool_mcp.py
# /// script
# dependencies = [
#     "polars",
#     "xlsxwriter",
# ]
# ///
from mcp.server.mcpserver import MCPServer

mcp = MCPServer("Revit Python Toolset")
```

The entry file creates the server and imports registration functions from `tools/`, `resources/`, and `prompts/`. Each registration function receives the same `mcp` instance and adds its capabilities:

```python
from tools.query_tools import register_query_tools
from resources.model_resources import register_model_resources

register_query_tools(mcp)
register_model_resources(mcp)
```

The user flow is:

1. Create a folder with one `*mcp.py` entry file.
2. Declare third-party packages in that entry file's PEP 723 header.
3. Import the modules that register tools, resources, or prompts.
4. Add the folder path in **Settings → MCP**.
5. Wait for the catalog to reload, then verify the capabilities from the host MCP registry or an AI client.

The sample at [`samples/PythonDemo/mcp_toolset`](https://github.com/trgiangv/RevitDevTool/tree/main/samples/PythonDemo/mcp_toolset) follows this pattern. `unittest` is not an alternative MCP toolset entry workflow; use the MCP entry file and its registered capabilities directly.

## Runtime guidance

- Import host APIs only when the tool executes in the Autodesk host.
- Keep tool parameters explicit and use snake_case names.
- Describe read-only and model-changing behavior accurately.
- Return structured results for element queries and summaries.
- Keep Revit and AutoCAD-specific logic separate when their APIs differ.

For the MCP concepts and the default capabilities already provided by RevitDevTool, see [MCP Protocol](/docs/mcp/MCP-Protocol).

Official references: [Python SDK documentation](https://github.com/modelcontextprotocol/python-sdk/blob/main/docs/index.md) · [Python SDK repository](https://github.com/modelcontextprotocol/python-sdk).

## Related

- [MCP .NET](/docs/mcp/MCP-CSharp)
- [MCP Protocol](/docs/mcp/MCP-Protocol)
