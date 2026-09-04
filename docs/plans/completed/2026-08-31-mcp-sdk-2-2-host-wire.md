# Execution Plan: MCP SDK 2.2.0 Host-Wire Adoption

Date: 2026-08-31

## Status

Completed 2026-08-31

## Outcome

Living MCP docs match the **2.2.0** binary and [0027](../../decisions/0027-mcp-product-surface.md)
(host-wire / product-limit ADRs later collapsed into 0027). Host `McpSpecKeys`
aliases SDK constants. Host `tools/call` params deserialize as SDK
`CallToolRequestParams`. Catalog list DTOs serialize as SDK `Tool` /
`Resource` without changing Cursor `tools/list`. Host pipe still does
**not** run `McpServer`. MRTR session plan closed to
[`2026-08-02-mrtr-implementation.md`](2026-08-02-mrtr-implementation.md).

## Context

- Policy: [0027](../../decisions/0027-mcp-product-surface.md)
- Partially superseded: [0012](../../decisions/0012-host-mcp-spec-engine.md)
- Map (Phase 0 edits): [`docs/architecture/MCP/`](../../architecture/MCP/)
- Package pin: `Directory.Packages.props` → `ModelContextProtocol` **2.2.0**
- SDK source (read-only): `.opensrc/repos/github.com/modelcontextprotocol/csharp-sdk/main`
- Build proof: `.agents/skills/build/SKILL.md`
- Coordination: chief-of-staff agent; Composer 2.5 implements; Opus 5 only
  for high-level critical-path judgment (not routine code review)

## Scope

In scope:

- Phase 0 — living-doc truth (architecture MCP map; necessary link fixes)
- Phase 1 — `McpSpecKeys` alias, delete `McpProtocolJsonContext`, passthrough existence test
- Phase 2 — `CallToolRequestParams` at host `tools/call` boundary
- Phase 3 — catalog list DTOs → SDK `Tool`/`Resource` (Cursor live gate)
- Phase 4 — optional `InputRequired` field on `McpInvocationResponse` (byte-identical wire)

Out of scope:

- `McpServer` on host named pipe
- Replacing `McpInvocationResponse` with `CallToolResult`
- `McpServerTool.Create` in MetadataLoadContext catalog parser
- Enabling daemon `UseStructuredContent` without Cursor live proof
- Forwarding `invoke_dynamic` `Meta`/`progressToken` without a host progress emitter (G5 / 0027)
- Host MCP Tasks on the pipe, `resources/subscribe`, `completions`
- Editing `docs/plans/completed/*`
- Upstream SDK PR (file an issue later; do not block)

## Approach

Do not start phase *N+1* until phase *N* gate is green. Phase 0 and Phase 1
may run in parallel (docs vs `DevTools.Mcp.Core` + tests). Phase 2+ is serial.

### Phase 0 — Doc truth

Files (architecture layer + broken-link fixes only):

| File | Change |
|------|--------|
| `docs/architecture/MCP/sdk-2-0-gap-matrix.md` | `git mv` → `sdk-gap-matrix.md`; title/packages **2.2.0**; Progress row ❌/⏸ for host/ALC per 0027; keep ✅ only for daemon-fixed SDK tools; rename `CallToolRequestServiceProvider` → `ToolsetInvocationServices`; add G5 stub pointing at 0027 |
| `docs/architecture/MCP/README.md` | Link + “2.0.0” → 2.2.0 |
| `docs/architecture/MCP/platform-boundaries.md` | Date; ADR blurb cites 0027 (SDK types allowed; no SDK *session*); withdraw pass-through rule 2 `progressToken` claim; Progress table row; `CallToolRequestServiceProvider` name |
| `docs/product/mcp.md` | Fix gap-matrix link only |
| `docs/plans/active/2026-08-02-mrtr-implementation.md` | Fix gap-matrix path + provider name (then still active; **moved to completed** 2026-08-31 under [0027](../../decisions/0027-mcp-product-surface.md)) |
| `docs/decisions/0027-mcp-product-surface.md` | Update remaining `sdk-2-0-gap-matrix.md` link after rename |

Do **not** copy 0027 tables into architecture docs. Link the ADR.

### Phase 1 — Constants and dead code

`McpSpecKeys` stays. Alias spec keys to SDK **constant expressions**:

```csharp
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

public static class Methods
{
    public const string Initialize = RequestMethods.Initialize;
    public const string Initialized = NotificationMethods.InitializedNotification;
    public const string Ping = RequestMethods.Ping;
    public const string ToolsList = RequestMethods.ToolsList;
    public const string ToolsCall = RequestMethods.ToolsCall;
    public const string ResourcesList = RequestMethods.ResourcesList;
    public const string ResourcesTemplatesList = RequestMethods.ResourcesTemplatesList;
    public const string ResourcesRead = RequestMethods.ResourcesRead;
    public const string ServerDiscover = RequestMethods.ServerDiscover;
}

public static class Meta
{
    public const string Key = "_meta"; // no SDK constant
    public const string ProtocolVersion = MetaKeys.ProtocolVersion;
    public const string ClientInfo = MetaKeys.ClientInfo;
    public const string ServerInfo = MetaKeys.ServerInfo;
    public const string ClientCapabilities = MetaKeys.ClientCapabilities;
}

public static class JsonRpc
{
    public const string Version = "2.0"; // envelope literals stay
    // ... existing string keys ...
    public const int InvalidRequest = (int)McpErrorCode.InvalidRequest;
    public const int MethodNotFound = (int)McpErrorCode.MethodNotFound;
    public const int InvalidParams = (int)McpErrorCode.InvalidParams;
    public const int InternalError = (int)McpErrorCode.InternalError;
    public const int ParseError = (int)McpErrorCode.ParseError;
    public const int UnsupportedProtocolVersion = (int)McpErrorCode.UnsupportedProtocolVersion;
}
```

Keep unchanged (no SDK equivalent): `Discover.*`, `ProtocolVersions.Current`,
`Initialize.*` field names, `Capabilities.*`, `Implementation.*`, `Tools.*`
property names, `ToolResult.Content` (`"content"`), `Content.*`,
`ContentBlockTypes.*`, `Icon.*`, `ResultType.*`, `Resources.*`, `JsonSchema.*`,
`SdkAttributes.*`.

**Amendment vs 0027:** do **not** delete `ToolResult.ContentPascal`.
`ToolsetResultSerializer.IsCallToolResult` uses it to detect PascalCase
`Content` from foreign/ILRepack JSON. Treat as DevTools-only ALC key.
Escalate to Opus 5 only if an implementer wants to remove that detection.

Delete `McpProtocolJsonContext` from `McpProtocolModels.cs`. Point
`tests/DevTools.Mcp.Tests/McpProtocolModelsTests.cs` at
`McpJsonUtilities.DefaultOptions` (or serialize via existing encoder
helpers). Do not wrap SDK `JsonContext`.

Add `tests/DevTools.Mcp.Tests/McpClientPassthroughSurfaceTests.cs` (name
flexible): assert `McpClientImpl` private `_sessionHandler` and
`SendRequestAsync(JsonRpcRequest, CancellationToken)` still exist, same
as `McpClientPassthrough` uses. Fail with a message that names SDK 2.2.0
and 0027 — not `TypeInitializationException` inside Revit.

Do not churn ~50 `McpSpecKeys` call sites.

### Phase 2 — Invocation request

- `InvocationRequestReader.FromWire` deserializes `CallToolRequestParams`
  with `McpJsonUtilities.DefaultOptions` (or `SerializeToElement` then
  deserialize). `progressToken` must come from `_meta`, string **or** number.
- Delete `McpInvocationRequest`. Update Catalog `SdkInvocationRequest`,
  Adapter Python payload, dispatcher, tests.
- Collapse `SdkInvocationRequest.ToCallToolParams` if it becomes identity.
- Keep `McpProtocol.EnsureCurrentProtocolMeta`.
- Golden tests: `tools/call` params with `_meta.progressToken` string and
  number. Re-run T-ALC-10..15 filters.

Escalate to Opus 5 if deleting `McpInvocationRequest` forces a public
API break that Catalog/Adapter cannot absorb in one change set.

### Phase 3 — Catalog list DTOs

- Store/encode SDK `Tool`, `Resource`, `ResourceTemplate`,
  `ReadResourceResult`.
- Delete `CatalogListEncoder` and `ReadResourceEncoder`.
- Keep `PrepareForWire` / `PreviewStructured`.
- Delete write-only `annotations.iconSource`.
- Remove unused `IHostSession.CallToolAsync`; production remains
  `CallToolPassthroughAsync`.

**Stop before merge without Cursor live `tools/list`.** Escalate to
Opus 5 if golden JSON diverges in a way that might drop tools.

### Phase 4 — Optional

`McpInvocationResponse.InputRequired`; `HostToolResultJson` switches on
the field; wire bytes identical to Phase 3.

## Risks And Recovery

- Cursor drops daemon `tools/list` if Phase 3/`UseStructuredContent`
  leaks invalid `outputSchema`. Mitigation: Phase 3 live gate; never
  enable `UseStructuredContent` on envelope tools in this plan.
- `McpSpecKeys` alias fails compile if SDK makes a constant non-const.
  Mitigation: fail compile, do not copy literals back.
- Passthrough reflection breaks on SDK bump. Mitigation: Phase 1
  surface test.
- Phase 2 public DTO deletion. Recovery: revert the Phase 2 commit;
  Phases 0–1 stay.
- Rollback: revert the phase commit; docs Phase 0 is independently
  revertable.

## Progress

- [x] Phase 0 — living docs 2.2.0 + 0027/0028 map (2026-08-31; `sdk-gap-matrix.md`)
- [x] Phase 1 — `McpSpecKeys` alias + delete `McpProtocolJsonContext` + passthrough test (20/20 with `-p:SelfContained=false`)
- [x] Phase 2 — `CallToolRequestParams` host params (53/53 focused tests; `McpInvocationRequest` deleted)
- [x] Phase 3a — drop `annotations.iconSource` + unused `IHostSession.CallToolAsync` (icons[] kept; PythonParser_ExtractsSampleToolAnnotations fail is pre-existing env)
- [x] Phase 3b — catalog SDK `Tool`/`Resource` DTOs (67/68 focused tests; `PythonParser_ExtractsSampleToolAnnotations` pre-existing env; live `search_dynamic` on existing Revit 2025 catalog; host-pipe `tools/list` not re-verified until add-in redeploy)
- [x] Phase 4 — optional in-process `InputRequired` on `McpInvocationResponse`; `HostToolResultJson` switches on the field; **wire byte-identical** (2026-08-31)
- [x] Live 2025 (net8) — Cursor MCP Python toolset **pass** after SDK-aligned `resources/read` (PID 30512; `revit://toolset/capabilities` + `revit://model/worksets`).
- [x] Live 2024 (net48) — same resource reads **pass** (PID 28684)

## Decisions

- 2026-08-31: Policy in 0027/0028; this plan is sequencing only.
- 2026-08-31: Keep `ToolResult.ContentPascal` (ALC PascalCase detect).
  0027 “delete unused” was wrong — one production caller.
- 2026-08-31: Folder-matched namespaces (`DevTools.Mcp.Core.Sessions` etc.)
  landed in the same worktree. Global usings added on tests, Daemon, Catalog.
  Test `dotnet run` needs `-p:SelfContained=false` (NETSDK1150 vs Daemon).
- 2026-08-31: Opus 5 Phase 3 verdict **B**. Cursor `tools/list` is the
  daemon six-tool surface, not host `CatalogListEncoder`. Split 3a/3b;
  3b gate is parser + `HostCatalogEntry` round-trip + live
  `search_dynamic`, not Cursor. Amended 0027.
- 2026-08-31: Phase 3b landed. Host list/read uses SDK `ListToolsResult` /
  `ReadResourceResult` + `McpJsonUtilities`. `CoerceInputSchema` protects
  `Tool.InputSchema` setter. Add-in redeployed 2026-08-31 for live Python
  `resources/read` (2024+2025).
- 2026-08-31: FastMCP `ReadResourceContents` is an in-process helper (`content`, not
  SDK `text`). Fix: `ToolInvoke.py` uses low-level `resources/read` (python-sdk
  `_handle_read_resource`); C# deserializes SDK `ReadResourceResult`. Live
  confirmed 2024 PID 28684 + 2025 PID 30512 (`capabilities` + `worksets`).
- 2026-08-31: Phase 4 landed. `McpInvocationResponse.InputRequired` is the in-process MRTR
  discriminator; host named-pipe `tools/call` still serializes SDK `InputRequiredResult`
  via `McpJsonUtilities.DefaultOptions` (byte-identical to Phase 3).

Promote lasting product or architecture decisions into `docs/decisions/`.

## Validation

- Focused proof:
  - Phase 0: grep living docs for `ModelContextProtocol` **2.0.0** (exclude
    `docs/plans/completed`, Changelog, historical version headers).
  - Phase 1: `dotnet build source/DevTools.Mcp.Core/DevTools.Mcp.Core.csproj -c Debug`
    and `source/DevTools.Mcp.Catalog/DevTools.Mcp.Catalog.csproj -c Debug`
    (multi-TFM). Then
    `dotnet run --project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj -- --filter "Conformance|CatalogListEncoder|InvocationResponseEncoder|McpProtocolModels|PassthroughSurface|ToolsetResultSerializer"`.
  - Phase 2: same test project filter plus invocation/ALC
    (`InvocationRequest|T-ALC|InvokeDynamic`).
- Integration or end-to-end proof:
  - Phase 3a: compile + CatalogListEncoder/conformance goldens; no
    `iconSource` in annotations JSON; `icons[]` still parsed from
    `[McpServerTool(IconSource=…)]`.
  - Phase 3b: parser tests on real toolsets + Python path +
    `HostCatalogEntry` round-trip + live `search_dynamic`. Not Cursor
    `tools/list`.
  - Phase 2 live MRTR only if a host is available; otherwise note blocker.
- Repository-required checks: compile skill after every `.cs` edit;
  do not claim done from diff.

## Result

Phases 0–4 complete (2026-08-31).

- Host wire uses SDK constants, `CallToolRequestParams`, catalog `Tool`/`Resource`,
  and `ReadResourceResult`. Host pipe still has no `McpServer` session.
- Live Cursor MCP on Revit **2024** (net48) and **2025** (net8) with Python
  `mcp_toolset`: tools + `revit://toolset/capabilities` / `revit://model/worksets`
  after aligning `ToolInvoke.py` with python-sdk `_handle_read_resource`.
- Phase 4: `McpInvocationResponse.InputRequired` is in-process only. Named-pipe
  JSON remains SDK `InputRequiredResult`. CoS re-ran
  `AlcInputRequired|HostToolResultJson` — **7/7 passed** (byte-identical golden).
- Not in this plan: daemon `UseStructuredContent`, host progress (0028/G5),
  live host-pipe MRTR round-trip, Revit 2024 dockable pane click (MCP path green).

Moved to `docs/plans/completed/` after this result.
