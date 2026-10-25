# Protocol

The Model Context Protocol (MCP) is a standard way for an AI client to discover and use capabilities exposed by a server. RevitDevTool uses the protocol to connect an AI client to running Revit or AutoCAD-family hosts.

<LogoRow items={['mcp', 'cursor', 'claude', 'copilot', 'codex']} />

:::tip Any MCP 2.0 client
Any client that supports MCP 2.0 can connect. Read the [latest protocol specification](https://modelcontextprotocol.io/specification/2026-07-28).
:::

## Official SDKs

RevitDevTool pins the SDK versions used by its two toolset entry points:

| SDK | Pinned version | Official documentation |
| --- | --- | --- |
| .NET SDK | `2.3.0` | [C# SDK Getting Started](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/getting-started.md) · [C# SDK repository](https://github.com/modelcontextprotocol/csharp-sdk) |
| Python SDK | `2.3.0` | [Python SDK documentation](https://github.com/modelcontextprotocol/python-sdk/blob/main/docs/index.md) · [Python SDK repository](https://github.com/modelcontextprotocol/python-sdk) |

For the protocol itself, see the [official MCP specification](https://modelcontextprotocol.io/specification/2026-07-28).

## Core concepts

| MCP concept | Meaning |
| --- | --- |
| Client | The AI application, such as Cursor, Claude, GitHub Copilot or Codex |
| Server | The local RevitDevTool MCP service that connects the client to hosts |
| Tool | An operation the client can call, such as querying a model or opening a document |
| Resource | Readable context identified by a URI, such as model state or an API cheatsheet |
| Prompt | A reusable instruction template exposed to the client |

The normal user-facing flow is: connect the client, list running hosts, then write one `code_mode` program that searches the host's capabilities, calls the best match, and returns only the result you need.

<SequenceFlow
  participants={[
    { id: 'client', title: 'AI client', subtitle: 'Cursor, Claude, Copilot, Codex' },
    { id: 'server', title: 'MCP server', subtitle: 'DevTools.Daemon' },
    { id: 'host', title: 'Revit / AutoCAD', subtitle: 'RevitDevTool' },
  ]}
  steps={[
    { from: 'client', to: 'server', text: 'list_processes', detail: 'The assistant asks which hosts are running' },
    { from: 'server', to: 'client', text: 'Running hosts', back: true, detail: 'The server lists the Revit and AutoCAD sessions it found' },
    { from: 'client', to: 'server', text: 'code_mode', detail: 'The assistant sends one short program: search, call, return' },
    { from: 'server', to: 'host', text: 'Find and run the capability', detail: 'The server finds the matching capability and runs it in the host' },
    { from: 'host', to: 'server', text: 'Result', back: true, detail: 'The host returns the result of the call' },
    { from: 'server', to: 'client', text: 'Only what your program returns', back: true, detail: 'Only the values the program returns go back to the assistant' },
  ]}
/>

## The MCP view

The **MCP** toggle in the panel shows a list of every tool the host can offer: the built-in ones and those from the .NET and Python toolsets you load. It shows how many tools each source provides, how often each was called, and lets you search by name.

![MCP view listing built-in, .NET and Python tools](/images/features/Feature_MCP.png)

Use **Load .NET Toolset** and **Load Python Toolset** at the bottom to add a toolset.

:::info Settings only lists and removes sources
Toolsets are added from the **MCP** view. **Settings → MCP** lists the sources you have loaded under **Custom Loaded Sources** and lets you remove them.

![MCP settings listing custom loaded sources](/images/settings/Settings_MCP.png)
:::

## Tools your client sees

The MCP service exposes a small, fixed tool list:

| Tool | Purpose |
| --- | --- |
| `list_processes` | Show running Revit and AutoCAD-family instances |
| `launch_host` | Start a supported host instance |
| `read_file_info` | Read metadata and a safe summary for a local file |
| `code_mode` | Run a short C# program that searches, describes, invokes, and reads host capabilities |

The list does not change when you register toolsets. Host and toolset capabilities are reached from inside `code_mode`, not added to the client's tool list.

## Host capabilities

`code_mode` searches the catalog of the selected host. Typical built-in capabilities are:

| Capability | Purpose |
| --- | --- |
| `execute_csharp_code` | Run C# code in the host context |
| `execute_python_code` | Run Python code in the host context |
| `open_document` | Open a document in the host |
| `navigate_history` | Move backward or forward through supported model changes |
| `view_screenshot` | Capture the current host view for visual inspection |

Specialized capabilities and registered toolsets are preferred over the code-execution fallbacks. Pass `readOnly: true` to `code_mode` to block any capability that is not marked read-only. The exact entries depend on which host is running and which toolsets are registered.

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
