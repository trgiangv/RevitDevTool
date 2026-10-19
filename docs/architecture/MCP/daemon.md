# DevTools.Daemon

Standalone HandyControl WPF tray application that composes the external MCP server, hosts authentication, and manages multi-machine gateway connectivity. The tray icon is HandyControl `NotifyIcon`. The right-click menu is a WPF `ContextMenu`. Close hides the main window; Quit exits.

## Capabilities

1. **Auto-starts** with Windows (HKCU Run value `DevToolsDaemon`, set by installer)
2. **Single-instance tray** enforced by mutex `DevToolsDaemon_v1`; duplicate tray launches exit silently
3. **Owns authentication** — OIDC/PKCE flow via system browser, tokens stored with DPAPI
4. **Hosts the MCP engine** — Stdio mode (separate process) for local AI clients, Gateway mode (tray process) for remote
5. **Multi-machine aware** — registers with Gateway including device metadata and host_apps
6. **Exposes a control pipe** (`DevToolsDaemon_Control`) for host add-in communication (tray only)
7. **Main window** — standard WPF `Window` with Win32 title-bar theme and system buttons, showing auth state, hosts, gateway status, settings. Close hides; Quit from the tray exits.

## Startup Modes

| Args | Behavior |
|------|----------|
| `--stdio` | Direct MCP server on stdin/stdout. Self-contained process, no mutex, exits on disconnect. |
| _(none)_ | Desktop process. Acquires mutex; if already held, exits silently. Runs gateway + control pipe + UI. |

Stdio and desktop processes are fully independent — no IPC between them. Both discover host pipes via their own `DiscoveryHostedService`.

## Source Map

| Area | Path |
|------|------|
| Process entry, app constants, and host construction | `source/DevTools.Daemon/Program.cs`, `App.xaml.cs`, `AppConstants.cs`, `AppHostBuilder.cs` |
| Logging shared by tray and stdio modes | `source/DevTools.Daemon/Logging/` |
| Control pipe, single-instance, and Windows auto-start | `source/DevTools.Daemon/Control/` |
| Desktop views and view models | `source/DevTools.Daemon/Desktop/Views/`, `Desktop/ViewModels/` |
| Desktop settings model, persistence, and UI dispatch | `source/DevTools.Daemon/Desktop/Models/`, `Desktop/Helpers/` |
| Desktop theme and icons | `source/DevTools.Daemon/Desktop/` |
| Authentication | `source/DevTools.Daemon/Auth/` |
| WebSocket tunnel | `source/DevTools.Daemon/Tunnel/` |
| MCP hosting and process discovery | `source/DevTools.Daemon/Mcp/Hosting/`, `Mcp/Processes/` |
| External MCP surface (tools and prompts) | `source/DevTools.Daemon/Mcp/` |

Duende `OidcClient` owns PKCE, ID-token validation, and refresh. Local code is DPAPI `TokenStore`, `AuthBrowser`, and token revoke (`LogoutAsync` is a browser end-session, not revoke).

## Configuration

Config is the committed `appsettings.json`, embedded in the single-file EXE. `Auth` and `Gateway` are public client values (issuer, client id, tunnel URL), not secrets.

| Layer | File | Purpose |
|-------|------|---------|
| App | `appsettings.json` (embedded) | Auth, Gateway, `Logging:File` |
| User prefs | `%APPDATA%/RevitDevTool/settings.json` | `User` section — `IOptionsMonitor<UserSettings>` load, `UserSettingsStore` write |
| File logs | `%APPDATA%/RevitDevTool/logs/` | Hourly ZLogger rolling (`Logging:File`); tray and stdio share the folder, PID separates processes |

Auth/Gateway bind via `Configure<T>(GetSection)`. User prefs are a separate JSON file with a `User` section so they overlay configuration without colliding with Auth/Gateway. `IConfiguration` is read-only; `UserSettingsStore.Update` writes the file and reloads the configuration root.

## Daemon control pipe

Host add-ins send commands on `DevToolsDaemon_Control` (tray mode only).
The message format is one JSON command line. The pipe has no reply.
Choice: [0043](../../decisions/0043-ipc-shared-infrastructure-separate-protocols.md).

| Method | Effect |
|--------|--------|
| `control/sign_in` | Starts Daemon sign-in. The result stays on the Daemon window. |

## Built-in Tools

See [tools.md](tools.md) for the complete tool, resource, and prompt catalog.

## Usage

```bash
# Normal startup (tray icon, auto-connect gateway if authenticated)
DevTools.Daemon.exe

# Stdio mode — spawned by AI clients (Cursor, Claude Desktop)
DevTools.Daemon.exe --stdio
```

Policy: [0032](../../decisions/0032-daemon-mewui-and-aot.md) — HandyControl WPF; production publish is framework-dependent JIT; Native AOT is dropped.
