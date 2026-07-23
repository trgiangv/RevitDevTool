# MCP Product Contract

External AI clients reach host capabilities through `DevTools.Daemon`. In-host
MCP runtime is shared across registered hosts.

## Behavior

- Daemon owns stdio MCP, gateway, auth, host discovery, and catalog tools.
- Host pipes follow `DevTools_{Host}_{Version}_{PID}`.
- Built-in host tools include code execution and document open; dynamic catalog
  tools call into host-registered toolsets.
- New MCP features default to sharable unless a host API forces otherwise.

## Related

- Architecture: [`docs/architecture/MCP/README.md`](../architecture/MCP/README.md)
- Workflows: [`docs/architecture/MCP/workflows.md`](../architecture/MCP/workflows.md)
- Agent digest: [`docs/agents/mcp-pytest-bridge.md`](../agents/mcp-pytest-bridge.md)
