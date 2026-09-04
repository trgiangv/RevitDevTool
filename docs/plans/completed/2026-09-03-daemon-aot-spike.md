> **Rolled back (2026-09-03):** Native AOT was not adopted. Production publish is framework-dependent single-file again (`PublishSingleFile=true`, `SelfContained=false`).

# Execution Plan: DevTools.Daemon Native AOT Spike

Date: 2026-09-03

## Status

Completed 2026-09-03 — **rolled back**. Native AOT is not production publish.
Policy: [0032](../../decisions/0032-daemon-mewui-and-aot.md). JSON work that
unblocks a future AOT cutover: [0031](../../decisions/0031-daemon-json-source-gen.md).

## Outcome

`DevTools.Daemon` publishes as Native AOT (`PublishAot`, `TrimMode=full`) and `DeployDevToolsDaemon` copies the native exe to bundle Contents. Spike profile `win-x64-aot.pubxml` removed. App JSON uses `ControlJsonContext` / `UserSettingsJsonContext` (no `DaemonJson*` names). Catalog/ALC/MCP SDK reflection remain trim warnings, not publish blockers.

## Cutover (2026-09-03)

- `dotnet publish source/DevTools.Daemon -c Release` is AOT (no publish profile).
- Dev `dotnet build` stays JIT / framework-dependent.
- Wire names (`DevToolsDaemon*`) unchanged.

## Context

- Daemon: `net10.0-windows`, MewUI Direct2D, `H.NotifyIcon`, MCP gateway + catalog + stdio server.
- Default publish: `SelfContained=false`, `PublishSingleFile=true`, `DeployDevToolsDaemon` → bundle.
- [ADR 0018](../../decisions/0018-host-identity-and-out-of-process-infrastructure.md) rejected NativeAOT as a **Runner** design driver; this spike is Daemon-only and does not reverse that ADR.
- MewUI supports AOT with Direct2D (`PublishAot` + `MewUIBackend=Direct2D`).

## Scope

In scope:

- Opt-in publish profile `source/DevTools.Daemon/Properties/PublishProfiles/win-x64-aot.pubxml`
- Cheap csproj hygiene (`EnableConfigurationBindingGenerator`, `EnableAotAnalyzer` as warnings)
- Skip `DeployDevToolsDaemon` when `PublishAot=true`
- Publish attempt + blocker inventory

Out of scope:

- Rewriting `McpClientPassthrough`, `AssemblyIsolation`, or MCP SDK integration
- Dropping FileMetadata/Hosting references to greenwash AOT
- Runtime smoke test of AOT binary (follow-up)
- Runner or host add-in AOT

## Approach

1. Add MewUI-pattern publish profile (no `PublishAot` on default csproj).
2. Enable binding generator + AOT analyzer warnings (not errors) on JIT builds.
3. Run publish; classify trim/AOT warnings into app vs package vs architecture.
4. Record evidence; defer production decision.

## Risks And Recovery

- AOT publish overwriting bundle: mitigated by `DeployDevToolsDaemon` `Condition="'$(PublishAot)' != 'true'"`.
- `IsAotCompatible=true` breaks Debug build (IL→error): omitted; use `WarningsNotAsErrors` for IL codes instead.
- Publish succeeds but runtime fails on reflection/ALC paths: treat as **blocked for production** until verified.

## Progress

- [x] Add `win-x64-aot.pubxml` publish profile
- [x] Csproj comments + `EnableConfigurationBindingGenerator` + `EnableAotAnalyzer` (warnings only)
- [x] Guard `DeployDevToolsDaemon` from AOT publish
- [x] `dotnet build -c Debug` — pass (14 IL3050 warnings in Daemon)
- [x] AOT publish attempt — pass (exit 0, ~40s)
- [x] Blocker inventory (below)
- [x] Runtime smoke: AOT desktop PID 28392 alive; `control/status` → `isRunning:true`; `control/open_dashboard` → window **DevTools Daemon**; log has no JSON reflection errors

## Decisions

- 2026-09-03: `IsAotCompatible` omitted — promotes IL analyzer findings to errors on Debug build.
- 2026-09-03: No `TrimMode=full` in spike profile — compiler defaults only.
- 2026-09-03: AOT desktop is alive but JSON reflection is disabled — `TokenStore.TryLoad` logged `JsonSerializerIsReflectionDisabled`. Source-gen `DaemonJsonContext` / `UserSettingsJsonContext` for token, control pipe, gateway, settings.

## Blocker Inventory

### Architecture (cannot AOT without redesign)

| Area | Location | Evidence |
|------|----------|----------|
| Collectible ALC + metadata-only load | `DevTools.AssemblyIsolation`, `DevTools.Mcp.Catalog` | Dynamic assembly loading by design; incompatible with closed AOT graph |
| MCP SDK private reflection | `DevTools.Mcp.Client/McpClientPassthrough.cs` | IL2026 + IL2075 on `Assembly.GetType` / `GetField` / `GetMethod` into `McpClientImpl._sessionHandler` |
| Catalog tool discovery | `DevTools.Mcp.Catalog` + `CliWrap` | Reflection-based MCP tool registration; not trim-analyzed in this publish |

### App code (fixable with source-gen / API changes)

| Area | Location | Evidence |
|------|----------|----------|
| Reflection JSON (no `JsonSerializerContext`) | Daemon: `TokenStore`, `UserSettingsStore`, `ControlPipeHandler`, `GatewayTunnelClient` | IL2026 + IL3050 |
| `JsonStringEnumConverter` (non-generic) | `UserSettingsStore` static ctor | IL3050 |
| Reflection JSON | `DevTools.Mcp.Core/Utils/ToolHelpers.cs`, `DevTools.Mcp.Server` contracts/tools | IL2026 + IL3050 |
| `XmlSerializer` | `DevTools.FileMetadata.Revit/TransmissionDataReader.cs` | IL2026 + IL3050 (dynamic XML codegen) |
| Config options binding | `AuthOptions`, `GatewayOptions`, `UserSettings`, `FileLogOptions` | `EnableConfigurationBindingGenerator` added; full AOT binding not verified at runtime |

### Package (third-party trim/AOT risk)

| Package | Evidence |
|---------|----------|
| `ACadSharp` 3.7.1 | IL2104 — assembly produced trim warnings |
| `OpenMcdf` | In closure via FileMetadata; no direct IL line in this publish log |
| `ModelContextProtocol` 2.2 | In closure; SDK reflection not surfaced as app IL lines in this run |
| `Duende.IdentityModel.OidcClient` | In closure; no IL lines in this publish (verify at runtime) |
| `H.NotifyIcon` | No IL warnings in publish log |
| `ZLogger` | No IL warnings in publish log |

### Unverified at runtime

Publish **compiled** a native exe despite warnings. Expected failure modes if exercised:

- MCP passthrough session wiring (`McpClientPassthrough`)
- Dynamic catalog load / live `invoke_dynamic` RPC (tool *registration* no longer crashes)
- Revit transmission-data XML read
- ACadSharp metadata paths
- OIDC browser auth flow

## Go / No-Go

| Criterion | Result |
|-----------|--------|
| Default JIT build/publish unchanged | **Go** — Debug build passes; default csproj flags unchanged |
| AOT publish produces binary | **Go** — native exe produced |
| Trim/AOT warning budget acceptable | **No-go** — 88 IL warnings; reflection-heavy closure |
| Catalog + ALC + MCP reflection addressed | **No-go** — architectural blockers remain |
| Runtime verified | **No-go** — not tested |

**Production decision: NO.** Daemon stays on framework-dependent single-file publish until catalog/ALC/MCP reflection and JSON source-gen work are scoped and proven at runtime.

## Validation

### Focused proof

```powershell
dotnet build source/DevTools.Daemon/DevTools.Daemon.csproj -c Debug
# Exit 0, 14 IL3050 warnings (Daemon project only)
```

### AOT publish (spike command)

```powershell
dotnet publish source/DevTools.Daemon/DevTools.Daemon.csproj -c Release -f net10.0-windows -p:PublishProfile=win-x64-aot
```

| Metric | Value |
|--------|-------|
| Exit code | **0** (success) |
| Elapsed | ~40s |
| `DevTools.Daemon.exe` | **27.83 MB** (native) |
| Publish dir total | **140.23 MB** (exe + PDBs + satellite `.pdb` for project refs) |
| Output path | `source/DevTools.Daemon/bin/publish/DevTools.Daemon/win-x64-aot/` |
| Unique warning codes | IL2026 (42), IL3050 (43), IL2075 (2), IL2104 (1) |
| Errors | 0 |
| Full log | `source/DevTools.Daemon/aot-publish.log` (local, not committed) |

### Repository-required checks

- [x] Debug build passes
- [ ] AOT runtime smoke (deferred)

## Result

Spike infrastructure landed. Native AOT **publish succeeds** with a large warning surface; production cutover is blocked on architectural MCP/catalog/ALC work plus JSON/XML source-gen across Daemon and MCP layers. Next steps: runtime smoke of AOT binary on gateway + stdio paths; scope `JsonSerializerContext` for Daemon-owned types; evaluate MCP SDK AOT story or replace `McpClientPassthrough`; separate decision on whether catalog can move out of Daemon closure for a slim AOT gateway build.

## Next Steps

1. Runtime smoke: launch AOT `DevTools.Daemon.exe` (desktop + `--stdio`), exercise auth, gateway, control pipe, one MCP tool call.
2. Add `JsonSerializerContext` for Daemon IPC/auth/settings types; replace `JsonStringEnumConverter` with generic variant.
3. Track MCP SDK 2.x AOT/trim support; plan `McpClientPassthrough` replacement if SDK stays reflection-based.
4. Decide product split: AOT-friendly **gateway-only** daemon vs full **catalog-hosting** daemon (ADR follow-up, not this spike).
