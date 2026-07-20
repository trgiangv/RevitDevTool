# MCP dispatch in the execution layer

`DevTools.Execution/External/Mcp/` hosts the SDK MCP server for every CAD/BIM
host process. `HostMcpServerHostedService` accepts concurrent named-pipe
sessions on `DevTools_{Host}_{Version}_{PID}` and delegates SDK primitives to
the shared registry and host adapters. It is the only host data-plane server.

`HostMcpServerOptionsFactory` publishes tools, prompts, resources, and
templates. Built-in names, including `pytest_run`, are reserved; duplicate
dynamic names or resource URIs are rejected and diagnosed. When execution
requires a host API context, adapters use `IHostContextExecutor`; all host API
dependencies remain outside shared dispatch.

The daemon reads host catalogs over its own MCP session and publishes immutable
generation-scoped snapshots. The pytest plugin opens a separate MCP session
and invokes `pytest_run`. Neither uses a second local protocol.
