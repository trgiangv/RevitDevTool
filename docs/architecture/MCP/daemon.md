# DevTools.Daemon

Standalone MewUI tray application that composes the external MCP server, hosts authentication, and manages multi-machine gateway connectivity. The tray icon uses `H.NotifyIcon` (core, not the WPF package). The right-click menu is a MewUI `ContextMenu` native popup (auto-size); an invisible 1×1 host owned by the tray `MessageWindow` is only the placement target.

## Capabilities

1. **Auto-starts** with Windows (HKCU Run value `DevToolsDaemon`, set by installer)
2. **Single-instance tray** enforced by mutex `DevToolsDaemon_v1`; duplicate tray launches exit silently
3. **Owns authentication** — OIDC/PKCE flow via system browser, tokens stored with DPAPI
4. **Hosts the MCP engine** — Stdio mode (separate process) for local AI clients, Gateway mode (tray process) for remote
5. **Multi-machine aware** — registers with Gateway including device metadata and host_apps
6. **Exposes a control pipe** (`DevToolsDaemon_Control`) for host add-in communication (tray only)
7. **Main window** — MewUI window (C# markup, Direct2D) showing auth state, hosts, gateway status, settings. Close hides; Quit from the tray exits.

## Startup Modes

| Args | Behavior |
|------|----------|
| `--stdio` | Direct MCP server on stdin/stdout. Self-contained process, no mutex, exits on disconnect. |
| _(none)_ | Desktop process. Acquires mutex; if already held, exits silently. Runs gateway + control pipe + UI. |

Stdio and desktop processes are fully independent — no IPC between them. Both discover host pipes via their own `DiscoveryHostedService`.

## Source Map

| Area | Path |
|------|------|
| Composition (tray vs `--stdio` hosts, discovery, file logging) | `source/DevTools.Daemon/Composition/` |
| Auth (Duende `OidcClient` + DPAPI store + loopback browser) | `source/DevTools.Daemon/Auth/` |
| Gateway WebSocket tunnel | `source/DevTools.Daemon/Gateway/` |
| Control pipe (`control/*`) | `source/DevTools.Daemon/Control/` |
| Desktop (mutex, Run key, settings, `AppState`) | `source/DevTools.Daemon/Desktop/` |
| MCP tool adapters (`list_machines`) | `source/DevTools.Daemon/Tools/` |
| Main window + views + tray | `source/DevTools.Daemon/Views/` |
| Icon / theme / UI dispatch | `source/DevTools.Daemon/Helpers/` |
| App entry point | `source/DevTools.Daemon/Program.cs` |
| External MCP surface | `source/DevTools.Mcp.Server/` |

Duende `OidcClient` owns PKCE, ID-token validation, and refresh. Local code is DPAPI `TokenStore`, `LoopbackBrowser`, and token revoke (`LogoutAsync` is a browser end-session, not revoke).

## Configuration

Production config is embedded in the single-file EXE (`appsettings.json` as EmbeddedResource).

| Layer | File | Purpose |
|-------|------|---------|
| Production | `appsettings.json` (embedded) | Auth, Gateway, `Logging:File` |
| Development | `appsettings.Development.json` (git-ignored) | Local overrides |
| User prefs | `%APPDATA%/RevitDevTool/settings.json` | `User` section — `IOptionsMonitor<UserSettings>` load, `UserSettingsStore` write |
| File logs | `%APPDATA%/RevitDevTool/logs/` | Hourly ZLogger rolling (`Logging:File`); tray and stdio share the folder, PID separates processes |
| CI/CD | GitHub Secrets | `AUTH_ISSUER`, `AUTH_CLIENT_ID`, `GATEWAY_URL` |

Auth/Gateway bind via `Configure<T>(GetSection)`. User prefs are a separate JSON file with a `User` section so they overlay configuration without colliding with Auth/Gateway. `IConfiguration` is read-only; `UserSettingsStore.Update` writes the file and reloads the configuration root.

## Control Pipe API

Host add-ins communicate via `DevToolsDaemon_Control` named pipe (tray mode only).  
Protocol: one JSON request line → one JSON response line per connection.

| Method | Response |
|--------|----------|
| `control/status` | `{isRunning, version}` |
| `control/auth_state` | `{isAuthenticated, userId, email, displayName, avatarUrl}` |
| `control/sign_in` | `{success, error}` |
| `control/sign_out` | `{success}` |
| `control/connected_hosts` | `[{hostApp, version, pid, pipeName}, ...]` |
| `control/open_dashboard` | `{success}` |

## Built-in Tools

See [tools.md](tools.md) for the complete tool, resource, and prompt catalog.

## Usage

```bash
# Normal startup (tray icon, auto-connect gateway if authenticated)
DevTools.Daemon.exe

# Stdio mode — spawned by AI clients (Cursor, Claude Desktop)
DevTools.Daemon.exe --stdio
```
