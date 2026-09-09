# Protocol

The Model Context Protocol (MCP) is a standard way for an AI client to discover and use capabilities exposed by a local server. RevitDevTool uses the protocol to connect an AI client to running Revit or AutoCAD-family hosts.

## Official SDKs

RevitDevTool pins the SDK versions used by its two toolset entry points:

| SDK | Pinned version | Official documentation |
| --- | --- | --- |
| .NET SDK | `2.2.0` | [C# SDK Getting Started](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/getting-started.md) · [C# SDK repository](https://github.com/modelcontextprotocol/csharp-sdk) |
| Python SDK | `2.1.1` | [Python SDK documentation](https://github.com/modelcontextprotocol/python-sdk/blob/main/docs/index.md) · [Python SDK repository](https://github.com/modelcontextprotocol/python-sdk) |

For the protocol itself, see the [official MCP specification](https://modelcontextprotocol.io/specification/2025-11-25/basic).

## Core concepts

| MCP concept | Meaning |
| --- | --- |
| Client | The AI application, such as Cursor, Claude Desktop, VS Code Copilot, or ChatGPT Desktop |
| Server | The local RevitDevTool MCP service that connects the client to hosts |
| Tool | An operation the client can call, such as querying a model or opening a document |
| Resource | Readable context identified by a URI, such as model state or an API cheatsheet |
| Prompt | A reusable instruction template exposed to the client |

The normal user-facing flow is: connect the client, choose a running host, search the available capabilities, invoke a tool or read a resource, then inspect the result.

## RevitDevTool default tools

These capabilities are available from the local MCP service or from a connected host:

| Tool | Purpose |
| --- | --- |
| `list_host_instances` | Show running Revit and AutoCAD-family instances |
| `launch_host` | Start a supported host instance |
| `read_file_info` | Read metadata and a safe summary for a local file |
| `list_machines` | List available local machine targets |
| `search_dynamic` | Find tools and resources available on a connected host |
| `invoke_dynamic` | Invoke a discovered capability |
| `execute_csharp_code` | Run C# code in the host context |
| `execute_python_code` | Run Python code in the host context |
| `open_document` | Open a document in the host |
| `navigate_history` | Move backward or forward through supported model changes |
| `view_screenshot` | Capture the current host view for visual inspection |

The exact host-specific entries depend on which host is running and which optional toolsets are registered.

## Default resources

RevitDevTool provides host reference and context resources. The available set depends on the connected host:

| Host | Resource URI | Purpose |
| --- | --- | --- |
| Revit | `revit://python-cheatsheet` | Revit Python usage guidance |
| Revit | `revit://csharp-cheatsheet` | Revit C# usage guidance |
| Revit | `revit://model/context` | Current model context and summary |
| Revit | `revit://model/warnings` | Current model warnings |
| Revit | `revit://version` | Host and API version information |
| AutoCAD-family | `acad://python-cheatsheet` | AutoCAD Python usage guidance |
| AutoCAD-family | `acad://csharp-cheatsheet` | AutoCAD C# usage guidance |

These resources are read-only context. Registered .NET or Python toolsets may add additional tools and resources for their own domain.

## Default prompts

The local service exposes:

- `revit_code` for Revit code-generation guidance;
- `acad_code` for AutoCAD-family code-generation guidance.

## .NET and Python toolsets

RevitDevTool accepts both compiled .NET assemblies and Python toolset folders containing a `*mcp.py` entry file. The entry file carries its own PEP 723 dependency metadata; its capabilities appear alongside the defaults when the relevant host is running.

Use [MCP .NET](/docs/mcp/MCP-CSharp) or [MCP Python](/docs/mcp/MCP-Python) for registration guidance.
