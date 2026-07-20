# 4.0.0 Unified MCP Runtime Release Gate

Status: **not approved for publication**. This is the coordinated breaking
release record for the tag-derived desktop version `4.0.0`,
`revitdevtool_pytest` 0.4.0, and McpGateway 2.0.0.

## Breaking-change and deployment contract

- The canonical host data pipe is `DevTools_{HostApp}_{HostVersion}_{PID}` and
  carries standard MCP only. `DevTools.Mcp.v2.{PID}` is removed.
- The former Python framed bridge is unsupported. There is no fallback decoder,
  dual pipe, alias, protocol sniffing, or `dynamic_*` compatibility path.
- Gateway accepts tunnel v2 only. An old daemon receives
  `unsupported_tunnel_protocol` until upgraded.
- Prepare desktop-installer and Python-plugin artifacts, enter a maintenance
  window, deploy Gateway v2, then release and announce the compatible desktop
  components. The installer upgrades desktop components together.

## Acceptance audit

| # | Criterion | Evidence | Status |
| --- | --- | --- | --- |
| 1 | One canonical host pipe; old MCP pipe removed | `HostPipeName` source audit and `McpNamedPipeIntegrationTests` (12 passing) | Pass |
| 2 | Concurrent daemon and pytest sessions | `McpNamedPipeIntegrationTests` includes concurrent independent sessions | Pass |
| 3 | Local collection/direct `pytest_run`/typed reporting | Pytest unit suite (53 passing) and collection-only (65 collected) | Pass |
| 4 | Domain versus infrastructure failure ownership | `PytestMcpToolTests` (14 passing) and client contract tests | Pass |
| 5 | Request-scoped progress and case events | Pytest MCP and named-pipe tests (24 passing) | Pass |
| 6 | Cancellation is session-scoped | Local Task 7 concurrent-session/cancellation test | Pass |
| 7 | Streamable HTTP session contract | Gateway Vitest suite (52 passing) | Pass |
| 8 | Independent daemon MCP sessions | Gateway session/correlation tests | Pass |
| 9 | Notification/cancellation forwarding | Gateway SSE/cancellation tests | Pass |
| 10 | Replacement-safe generations | Gateway generation/reconnect tests | Pass |
| 11 | Fixed six-tool Broker surface | Broker tests (39 passing) and source audit | Pass |
| 12 | Readable non-error selection candidates | Broker invoke tests | Pass |
| 13 | Bounded deadline/no unsafe retry | Broker invoke deadline tests | Pass |
| 14 | Concurrent generation-safe catalog and O(1) PID | Broker catalog/session tests | Pass |
| 15 | Prefix-filtered discovery | daemon/Python pipe discovery tests and source audit | Pass |
| 16 | Immutable low-host-count snapshot publication | Broker documentation and catalog tests | Pass |
| 17 | Legacy bridge/relay removal | production source audit; current deployed `Contents` audit clean | Pass |
| 18 | Synchronous STA entry point/headless stdio | `DaemonStartupContractTests`, build, reflection, and stdio checks | Pass; tray smoke pending |
| 19 | Matrix plus live scenarios | automated matrix below; live scenarios unavailable | Blocked |

## Automated matrix

- Root execution tests: 37/37 passed. Full server run: 239/243 passed; four
  existing scalar-binding assertions expect exceptions that the SDK returns as
  error results.
- Direct Debug.Autodesk.2024 (net48) and Debug.Autodesk.2027 (net10) solution
  builds passed. The prescribed helper scripts resolve a parent path outside
  this external worktree, so direct equivalent commands were used.
- Pytest: `uv run pytest tests/unit -q` passed 53 tests; `uv run pytest
  --collect-only -q` collected 65 tests without import errors.
- Gateway: `npm test` passed 52 tests and `npm run typecheck` passed.
- The package pipeline completed through `dotnet run -c Release`. The helper
  wrapper has the same external-worktree path-resolution limitation.

## Artifact remediation evidence

The deployed bundle previously contained stale side-by-side dependencies from
an older overlay. They were moved recoverably to
`RevitDevTool.bundle/_quarantine/20260720T150548` before redeployment. A
post-redeployment binary scan of `RevitDevTool.bundle/Contents` found no
`McpPipeName`, `BridgeMessage`, `BridgePipeConnection`, `DevToolsPipeServer`,
or `PytestRequestHandler` symbol. Current Release.Autodesk.2022, 2024, and
2027 host outputs are clean by the same scan. The quarantined files are not
part of deployed `Contents` and remain available for recovery.

## Required live evidence before publication

- No Revit or AutoCAD-family process was available. Verify one canonical pipe
  per host, concurrent daemon/pytest sessions, daemon continuity after pytest
  disconnect, launch readiness, and timeout/disconnect classifications.
- No authenticated Gateway tunnel, OIDC credentials, or Cloudflare deployment
  configuration was available. Verify overlapping JSON-RPC IDs, SSE progress,
  cancellation, replacement-driven old-session 404, and new-generation
  initialization.

These are environment blockers, not passing substitutes. Do not publish this
release until they are resolved or an explicitly authorized release decision
accepts the outstanding live-risk evidence.
