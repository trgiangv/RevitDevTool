# DevTools.Daemon

Standalone WPF tray application that hosts the MCP engine, authentication, and multi-machine gateway connectivity.

## Capabilities

1. **Auto-starts** with Windows (Registry Run key, set by installer)
2. **Single-instance tray** enforced by global Mutex (`DevToolsDaemon_v1`); duplicate tray launches exit silently
3. **Owns authentication** — OIDC/PKCE flow via system browser, tokens stored with DPAPI
4. **Hosts the MCP engine** — Stdio mode (separate process) for local AI clients, Gateway mode (tray process) for remote
5. **Multi-machine aware** — registers with Gateway including device metadata and host_apps
6. **Exposes a control pipe** (`DevToolsDaemon_Control`) for host add-in communication (tray only)
7. **Dashboard UI** — MahApps-themed window showing auth state, hosts, gateway status, settings

## Startup Modes

| Args | Behavior |
|------|----------|
| `--stdio` | Direct MCP server on stdin/stdout. Self-contained process, no mutex, exits on disconnect. |
| _(none)_ | Tray host. Acquires mutex; if already held, exits silently. Runs gateway + control pipe + UI. |

Stdio and tray processes are fully independent — no IPC between them. Both discover host pipes via their own `DiscoveryHostedService`.

## Host Session and Catalog Ownership

Each daemon host composes one `HostSessionManager`, which owns typed MCP session
slots, reconnect backoff, and the temporary legacy bridge-client surface. A
single `HostCatalogCoordinator` subscribes only to `SessionsChanged` and
serializes catalog rebuilds for that daemon host. `StdioHostedService` owns only
the stdio transport; it does not create a catalog or subscribe to discovery.

`CatalogService` retains the last successful snapshot for each still-connected
host when that host's list operation fails. It removes that host snapshot only
after the session is no longer connected.

Daemon file and launch tools resolve product behavior through `HostDriverRegistry`.
Each `IHostDriver` owns its file extensions, host-product membership, executable
resolution, launch arguments, and offline metadata reader. The registry keeps MCP
tool routing product-neutral, so a future host adds a driver rather than a broker or
tool-routing branch.

## Source Map

| Area | Path |
|------|------|
| Hosting (builders, services, single-instance) | `source/DevTools.Daemon/Hosting/` |
| MCP engine | `source/DevTools.Daemon/Mcp/` |
| Daemon host drivers | `source/DevTools.Daemon/Hosts/` |
| Auth (OIDC/PKCE) | `source/DevTools.Daemon/Auth/` |
| Dashboard (window + views) | `source/DevTools.Daemon/Dashboard/` |
| App entry point | `source/DevTools.Daemon/App.xaml.cs` |

## Configuration

Production config is embedded in the single-file EXE (`appsettings.json` as EmbeddedResource).

| Layer | File | Purpose |
|-------|------|---------|
| Production | `appsettings.json` (embedded) | Placeholder values, injected by CI/CD |
| Development | `appsettings.Development.json` (git-ignored) | Local overrides for dev builds |
| CI/CD | GitHub Secrets | `AUTH_ISSUER`, `AUTH_CLIENT_ID`, `GATEWAY_URL` |

## Control Pipe API

Host add-ins communicate via `DevToolsDaemon_Control` named pipe (tray mode only).  
Protocol: one JSON request line → one JSON response line per connection.

| Method | Response |
|--------|----------|
| `daemon/status` | `{isRunning, version}` |
| `daemon/auth_state` | `{isAuthenticated, userId, email, displayName, avatarUrl}` |
| `daemon/trigger_signin` | `{success, error}` |
| `daemon/trigger_signout` | `{success}` |
| `daemon/connected_hosts` | `[{hostApp, version, pid, pipeName}, ...]` |
| `daemon/open_dashboard` | `{success}` |

## Built-in Tools

See [tools.md](tools.md) for the complete tool, resource, and prompt catalog.

## Usage

```bash
# Normal startup (tray icon, auto-connect gateway if authenticated)
DevTools.Daemon.exe

# Stdio mode — spawned by AI clients (Cursor, Claude Desktop)
DevTools.Daemon.exe --stdio
```
