# DevTools.Daemon

Standalone WPF tray application that hosts the MCP engine, authentication, and multi-machine gateway connectivity.

## Capabilities

1. **Auto-starts** with Windows (Registry Run key, set by installer)
2. **Single-instance** enforced by global Mutex; secondary launches become stdio proxies
3. **Owns authentication** — OIDC/PKCE flow via system browser, tokens stored with DPAPI
4. **Hosts the MCP engine** — Stdio mode for local AI clients, Gateway mode for remote
5. **Multi-machine aware** — registers with Gateway including device metadata and host_apps
6. **Exposes a control pipe** (`DevToolsDaemon_Control`) for host add-in communication
7. **Dashboard UI** — MahApps-themed window showing auth state, hosts, gateway status, settings

## Source Map

| Area | Path |
|------|------|
| MCP engine | `source/DevTools.Daemon/Mcp/` |
| Auth (OIDC/PKCE) | `source/DevTools.Daemon/Auth/` |
| Control pipe server | `source/DevTools.Daemon/Control/` |
| Tray icon + menu | `source/DevTools.Daemon/Tray/` |
| Dashboard (window + views) | `source/DevTools.Daemon/Dashboard/` |
| Lifecycle (mutex, autostart, stdio proxy) | `source/DevTools.Daemon/Lifecycle/` |

## Configuration

Production config is embedded in the single-file EXE (`appsettings.json` as EmbeddedResource).

| Layer | File | Purpose |
|-------|------|---------|
| Production | `appsettings.json` (embedded) | Placeholder values, injected by CI/CD |
| Development | `appsettings.Development.json` (git-ignored) | Local overrides for dev builds |
| CI/CD | GitHub Secrets | `AUTH_ISSUER`, `AUTH_CLIENT_ID`, `GATEWAY_URL` |

## Control Pipe API

Host add-ins communicate via `DevToolsDaemon_Control` named pipe.  
Protocol: one JSON request line → one JSON response line per connection.

| Method | Response |
|--------|----------|
| `daemon/status` | `{isRunning, version}` |
| `daemon/auth_state` | `{isAuthenticated, userId, email, displayName, avatarUrl}` |
| `daemon/trigger_signin` | `{success, error}` |
| `daemon/trigger_signout` | `{success}` |
| `daemon/connected_hosts` | `[{hostApp, version, pid, pipeName}, ...]` |
| `daemon/open_dashboard` | `{success}` |

## Usage

```bash
# Normal startup (tray icon, auto-connect gateway if authenticated)
DevTools.Daemon.exe

# Second launch becomes stdio proxy to running instance
# AI clients like Cursor just point to DevTools.Daemon.exe path
DevTools.Daemon.exe
```
