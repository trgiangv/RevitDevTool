# MCP And PyTest Bridge Digest

Deep sources: `docs/MCP/README.md` and `docs/PyTest/README.md`.

## MCP

- Parser library: `source/DevTools.McpParser/`.
- Standalone MCP server: `source/DevTools.McpServer/`.
- In-host runtime: `source/DevTools.Execution/External/Mcp/`.
- Registry store: `ToolRegistryStore`.
- Providers: `DotnetToolRegistryProvider` and `PythonToolRegistryProvider`.
- Dispatchers: tool, prompt, and resource dispatch.

External MCP clients call the standalone server, which talks to the in-host pipe server. The current standalone helper tools are Revit-oriented, while the in-host registry/dispatch runtime is shared.

## PyTest Bridge

- Server side: `source/DevTools.Execution/External/Testing/`.
- Embedded runner: `source/DevTools.Execution/Resources/scripts/PytestRunner.py`.
- Protocol routes include `tests/discover` and `tests/run`.
- The client pytest process talks to the host through a framed named pipe.

## Change Checklist

- For MCP parser changes, verify parser library tests and at least one sample catalog path.
- For runtime registry/dispatch changes, verify both standalone server assumptions and in-host pipe flow.
- For pytest bridge changes, verify discovery and run paths separately when possible.
- If a live host is required and unavailable, report the named pipe/host blocker precisely.
