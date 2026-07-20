# ADR 002: MCP Runtime V2

Status: Accepted
Date: 2026-07-18
Supersedes: custom BridgeMessage transport decisions from `2025-06-27-ipc-mcp-pytest-layer-restructure`

## Decision

Host processes serve standard MCP over named pipes with ModelContextProtocol SDK transports.
The daemon is an MCP client of every host and an MCP server to external clients.
The default external surface is a fixed broker surface because client support for runtime list refresh is unreliable.
Pytest is exposed as an MCP tool with standard progress notifications.
Primitive names are unique inside one host catalog and built-in names are reserved.

## Consequences

The existing BridgeMessage MCP routes and dynamic list/call/read/get tools are removed.
External pytest clients must initialize an MCP session and invoke the pytest tool.
Native flattened primitives are opt-in and require list-changed support.
