# Host Boundaries

The platform is host-agnostic. Shared libraries depend on abstractions and ModelContextProtocol SDK types; Revit/AutoCAD API calls stay in host projects.

## Shared layer

- `source/DevTools.Execution/` — execution engine, host MCP server, and direct pytest server-side lane.
- `source/DevTools.Execution.Abstractions/` — `IHostContextExecutor`, command/document abstractions, and execution contracts.
- `source/DevTools.Ipc/` — pipe utilities plus the intentionally retained direct pytest `BridgeMessage`/framing lane; no MCP DTO ownership.
- `source/DevTools.Mcp/` — host-neutral MCP catalog/routing primitives and SDK-oriented daemon routing.
- `source/DevTools.Daemon/` — external MCP server, gateway lifecycle, host sessions, and product-neutral `IHostDriver` routing.

## Host layer

- Revit host: `source/RevitDevTool/`; Revit-only core: `source/RevitDevTool.Core/`.
- AutoCAD host: `source/AcadDevTool/`.
- Revit DirectContext3D rendering: `source/RevitDevTool/Visualization/` only.
- Host projects implement host context execution, transactions, document access, script/debug adapters, and host-specific primitive registrations.

The shared host MCP server uses `IHostContextExecutor` only when a primitive requires host API context. It must not gain Autodesk API references. New daemon product behavior belongs in an `IHostDriver`; new host API support belongs in a host project/adapter, not a product branch in broker or shared routing code.

## Runtime boundaries

- MCP Runtime V2: SDK sessions over `DevTools.Mcp.v2.{pid}` between daemon and host.
- Direct pytest: independent `DevTools_{Host}_{Version}_{PID}` pipe, four-byte framed `BridgeMessage`, `tests/run`, and `notifications/tests/progress`.
- Gateway `machine_id` selects a daemon before MCP initialization; broker `hostId` selects a host PID only within that daemon.

## Review checklist

- Keep host API threading, transactions, document context, and rendering in host projects.
- Check Revit and AutoCAD registration when adding shared primitive behavior.
- Preserve shared target compatibility for Autodesk 2022–2024 (`net48`) as well as current `net8.0-windows` and `net10.0-windows` paths.
- Use a focused host build and state any live-host/threading/packaging evidence gap.
