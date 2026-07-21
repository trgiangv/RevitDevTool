# MCP component compatibility

The unified MCP 4.0 release set is version-coupled. Components below the
documented minimum versions must fail fast during their handshake instead of
degrading silently.

## Minimum versions

| Component | Constant | Minimum | Current |
| --- | --- | --- | --- |
| In-host MCP runtime | `MinHostProtocolVersion` | `4.0.0` | `4.0.0` |
| DevTools.Daemon | `MinDaemonVersion` | `4.0.0` | GitVersion/tag-derived desktop bundle |
| revitdevtool_pytest | `MinPytestPluginVersion` | `0.4.0` | `pyproject.toml` / installed package |
| McpGateway | `MinGatewayVersion` | `2.0.0` | `package.json` |

Authoritative C# constants live in `source/DevTools.Ipc/ProtocolCompatibility.cs`.
Python mirrors them in `revitdevtool_pytest/compatibility.py`. Gateway mirrors
them in `src/compatibility.ts`.

## Handshake checks

### Host MCP initialize

The host advertises its protocol version in initialize capabilities:

```json
{
  "experimental": {
    "devtools": {
      "protocol": {
        "version": "4.0.0"
      }
    }
  }
}
```

`serverInfo.name` and `serverInfo.version` remain the host identity
(`Revit` / `2025`, etc.) and are validated separately against the pipe name.

Clients that connect directly to the host pipe validate the experimental protocol
version during initialize:

- `DevTools.Daemon` in `HostMcpSession.ConnectAsync`
- `revitdevtool_pytest` in `mcp_client.py`

Failure message shape:

```text
protocol_version_mismatch: host version <actual> is below required 4.0.0
```

### Gateway tunnel register

Daemon registration includes `daemon_version`. Gateway rejects versions below
`MinDaemonVersion` before accepting the tunnel.

The `registered` acknowledgement includes `gateway_version`. Daemon rejects
versions below `MinGatewayVersion` before treating the tunnel as connected.

Failure message shape:

```text
protocol_version_mismatch: daemon version <actual> is below required 4.0.0
protocol_version_mismatch: gateway version <actual> is below required 2.0.0
```

### Pytest plugin version

`MinPytestPluginVersion` documents the coordinated pytest release. The plugin
advertises its installed package version in initialize `clientInfo.version`.
Host-side rejection of older pytest clients is reserved for a later change;
current enforcement is host-protocol validation on the pytest client.
