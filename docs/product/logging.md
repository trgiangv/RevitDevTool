# Logging Product Contract

Unified multi-sink logging for host and shared platform code, including console
and Python `print` redirection when configured.

## Behavior

- Sinks may include monitor, file, and HTTP destinations.
- Host context enrichment is allowed; geometry interception can route to
  visualization on Revit.
- If RevitDevTool or AcadDevTool throws during add-in startup (or an unhandled
  exception fires while startup trace is active), a dump is written to
  `%APPDATA%\RevitDevTool\{Year}\Logs\crash_{app}_{ver}_{pid}.log` with coarse
  milestones and the exception. Successful startup creates no crash file. Rolling
  session logs stay `log_*` via FileLogProcessor.
- Logging must not become a substitute for executable product proof.

## Revit monitor click-to-select

Clickable tokens in the Revit monitor:

| Kind | Example | Click |
|------|---------|-------|
| Typed `ElementId` | `12345` | Host document only (`ShowElements`). **ZLog interpolations only** — a bare number on `Trace` / `print` is not a link. |
| UniqueId | 45-character id | Host document only. |
| IFC GUID | 22-character id | Host document only. |
| Scoped (`linkInstanceId@inner`) | `8821@445566`, `8821@{UniqueId}`, `8821@{IfcGuid}` | Same three kinds, searched only in that instance’s link document. Revit 2023+ selects the linked element; **2022 selects the instance**. All years zoom the active host view to the transformed box. |

Write `{instanceId}@{inner}` in the log line from a pick `Reference` (`ElementId` + `LinkedElementId`), e.g. `{instance.Id}@{linked.Id}`. UniqueId / IFC GUID: `{instance.Id}@{inner}`. Do not interpolate a `LinkElementId` object (the CLR type name is not clickable). Unscoped tokens never search links. Unloaded / missing instance → silent miss.

Misses are silent: no throw, no message box.

Policy and examples: [0036](../decisions/0036-revit-monitor-link-element-tokens.md).

## Related

- Architecture: [`docs/architecture/Logging/README.md`](../architecture/Logging/README.md)
