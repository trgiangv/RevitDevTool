# 0027 MCP SDK Adoption Boundary On Host Wire

Date: 2026-08-31

## Status

Accepted

Supersedes [0012](0012-host-mcp-spec-engine.md) rules 3 and 7. Does not
change [0010](0010-daemon-sole-mcp-host.md) (Daemon remains the sole
external MCP host).

Reviewed against **ModelContextProtocol 2.2.0** (`2026-07-28` protocol
family) source and the in-repo modules `DevTools.Mcp.Core`,
`DevTools.Mcp.Catalog`, `DevTools.Mcp.Client`, `DevTools.Mcp.Server`.

## Context

Repo pins `ModelContextProtocol` and `ModelContextProtocol.Extensions.Tasks`
**2.2.0**. Living MCP docs still say 2.0.0.

[0012](0012-host-mcp-spec-engine.md) required the host in-process runtime
to remove the MCP SDK entirely and forbade ILRepack of
`ModelContextProtocol*`. That is not the binary or the code:

- `DevTools.Mcp.Core`, `DevTools.Mcp.Adapter`, `DevTools.Mcp.Catalog`,
  and `DevTools.Execution` all `PackageReference` the SDK.
- `RevitDevTool.csproj` / `ACadDevTool.csproj` ILRepack do **not**
  exclude `ModelContextProtocol*`, so the SDK is merged into the host
  add-in.
- Host code already uses SDK types: `Tool`, `Icon`, `Annotations`,
  `RequestParams`, `CallToolRequestParams`, `InputRequiredResult`,
  `McpTaskExecutionMode`, `RequestContext<T>`.

The invariant that **is** held, and that this decision keeps: the host
named pipe does **not** run an SDK `McpServer` / `McpSession`. It runs
`IMcpHandler` (`server/discover`, per-request `_meta`, reject
`initialize`).

A 2026-08-31 audit proposed collapsing host-owned vocab
(`McpSpecKeys`, `McpToolDescriptor`, encoders, `McpInvocationRequest`)
onto SDK types. That direction is sound for **constants and request
params**. It overstated several items (see Corrections) and would
reintroduce `McpServer` on the host pipe — rejected below.

Real risks, in order:

1. **Doc drift** — agents decide from stale 0012 / 2.0.0 claims.
2. **Strict MCP clients** — Cursor drops the whole `tools/list` when
   daemon `outputSchema` is inferred from `JsonElement`.
3. **ALC type identity** — ILRepacked host SDK vs toolset SDK; JSON
   bridge is required in every alternative.
4. **Spec bugs on a dead path** — `progressToken` read top-level as
   `long` only; nothing reads `McpInvocationRequest.ProgressToken`.

## Decision

1. **Restate the host invariant.** Host pipe does not run MCP SDK
   session/server (`McpServer`, `McpSession`, `initialize` handshake).
   Host implements spec-first JSON-RPC (`server/discover` + per-request
   `_meta`) via `IMcpHandler` / `McpHandler`.

2. **SDK DTOs and constants are allowed and preferred on host.**
   DevTools-owned types exist only when there is a named reason:
   ALC type-identity cut (`McpInvocationResponse`, `McpContent`), or
   product domain with no SDK equivalent (`McpRegisteredTool`,
   `McpPrimitiveBinding`, `JsonSchemaModels`, `DynamicCapabilityId`).

3. **`ModelContextProtocol*` may be ILRepacked into the host add-in.**
   0012 rule 7 is withdrawn. Excluding the SDK now would break
   `DescriptorFactory`, `McpTaskExecutionMeta`, and `McpContent.Annotations`
   for bundle size only.

4. **`McpSpecKeys` holds DevTools-owned JSON keys only** (discover/list
   payload fields, JSON-RPC envelope names, JSON Schema, SdkAttributes,
   `ProtocolVersions.Current`). Call sites use SDK `RequestMethods`,
   `NotificationMethods`, `MetaKeys`, and `McpErrorCode` directly. Do
   not add aliases that re-export those SDK constants.

5. **ALC JSON bridges are boundary design, not debt.**
   `ToolsetResultSerializer`, `ToolsetMrtrBridge`, and
   `McpToolsetContext` stay for as long as toolsets compile against MCP
   with `ExcludeAssets=runtime` while the host ILRepacks a different
   copy.

6. **Keep these workarounds until the named unblocker exists:**

   | Workaround | Unblocker |
   |------------|-----------|
   | `McpClientPassthrough` (reflection on `McpClientImpl._sessionHandler`) | Public per-call “no auto-MRTR” send on `McpClient` |
   | `EnsureCurrentProtocolMeta` | Same; passthrough skips SDK `InjectRequestMetaIfNeeded` |
   | `DynamicToolCallResults` (manual `StructuredContent`, no `UseStructuredContent`) | Live proof Cursor + ≥1 other client accept explicit `OutputSchema` |
   | `SdkNoopTransport` | Public cheap `McpServer` / `RequestContext` factory without a live transport |
   | `InvokeDynamicMrtrState` in protocol `requestState` | None — correct use of opaque `requestState` for double-hop `capabilityId` |

7. **Do not put `McpServer` on the host pipe.** Catalog is
   metadata-only (MetadataLoadContext + Python parser + ALC). Host
   dispatch already wraps `IHostContextExecutor` +
   `ExecutionGuardContext`. Type-identity JSON still required. SDK
   session machinery in a process we do not own adds cost without
   removing `McpHandler`-shaped code.

8. **Do not replace `McpInvocationResponse` with `CallToolResult` on
   the host wire.** That type is the identity cut. Moving to
   `CallToolResult` relocates the cut; it does not remove
   `ToolsetResultSerializer`.

9. **Do not call `McpServerTool.Create` from the catalog parser.**
   Discovery uses MetadataLoadContext and must not load toolset types
   into the host. `McpSchemaBuilder` / `JsonSchemaModels` stay for
   discovery and UI parse. Daemon and host **built-ins** already use
   `McpServerTool.Create`.

10. **Host wire MRTR is already spec-shaped.**
    `HostToolResultJson` serializes `InputRequiredResult` via
    `McpJsonUtilities.DefaultOptions` (`resultType: input_required`).
    `Meta["devtools.inputRequired"]` is an **in-process** channel
    because `McpInvocationResponse` has no discriminator field. Optional
    follow-up: add `InputRequired` on that record; wire bytes must stay
    identical.

## Corrections To The 2.2.0 Audit

These claims from the reuse audit are **wrong or overstated**. Do not
re-propose them:

| Claim | Fact |
|-------|------|
| MRTR is stuffed in meta instead of wire `input_required` | Wire is `InputRequiredResult`. Meta key is in-process only. |
| `JsonSchemaModels` is leftover registration schema | Required for MetadataLoadContext discovery, not only UI. |
| `iconSource` on annotations is a live spec break | `icons[]` is also emitted; `iconSource` is write-only and dropped by SDK `Tool` deserialize. Delete later, low priority. |
| `SdkNoopTransport` hacks internals | It is `TransportBase` + public `McpServer.Create`. Different class from `McpClientPassthrough`. |
| `McpProtocolJsonContext` should wrap SDK `JsonContext` | Production encoders build `JsonNode` by hand. Context is test-only → delete, do not wrap. |
| Host has no SDK (0012) | SDK is referenced and ILRepacked today. |

## Work Sequence

Phases 0–4 **landed 2026-08-31**. Evidence:
[`docs/plans/completed/2026-08-31-mcp-sdk-2-2-host-wire.md`](../plans/completed/2026-08-31-mcp-sdk-2-2-host-wire.md).
The sections below are the original gated sequence, not remaining work.

Architecture-doc edits belonged in Phase 0 (one layer per change set;
this ADR is the policy, those files are the map).

### Phase 0 — Doc truth (no product code)

- Pin version in living docs to **2.2.0**.
- Rename `docs/architecture/MCP/sdk-2-0-gap-matrix.md` →
  `sdk-gap-matrix.md` and fix links (`MCP/README.md`).
- Rewrite 0012-derived “no SDK on host” sentences in
  `platform-boundaries.md` to match this ADR.
- Replace `CallToolRequestServiceProvider` with
  `ToolsetInvocationServices`.
- Do not edit `docs/plans/completed/*` (historical).
- Progress contract: [0028](0028-host-alc-progress-notifications.md).

Gate: doc review only.

### Phase 1 — Constants and dead code

- Alias `McpSpecKeys` to SDK constants; add missing `_meta` keys;
  delete unused SpecKeys aliases (`Methods`, `Icon`, unused `JsonRpc`
  error ints). Keep `ContentPascal` for ALC PascalCase JSON.
- Delete `McpProtocolJsonContext`; point
  `McpProtocolModelsTests` at `McpJsonUtilities.DefaultOptions`.
- Add a unit test that `McpClientImpl._sessionHandler` and
  `SendRequestAsync(JsonRpcRequest, CancellationToken)` still exist
  (fail fast on SDK bump, not inside Revit).

Gate: compile touched projects +
`dotnet run --project tests/DevTools.Mcp.Tests -- --filter "Conformance|CatalogListEncoder|InvocationResponseEncoder|McpProtocolModels"`.

### Phase 2 — Invocation request

- `InvocationRequestReader` deserializes `CallToolRequestParams` with
  `McpJsonUtilities.DefaultOptions`.
- Delete `McpInvocationRequest` (including dead `ProgressToken`).
- Collapse `SdkInvocationRequest.ToCallToolParams` mapping.
- Keep `McpProtocol.EnsureCurrentProtocolMeta` for passthrough.

Gate: golden JSON for `tools/call` params with `_meta.progressToken`
as string **and** number; T-ALC-10..15; live MRTR checklist.

### Phase 3 — Catalog list DTOs (split 2026-08-31)

Cursor talks to the **daemon** six-tool list (`McpEngine.LocalTools`,
`ListChanged = false`). Host `CatalogListEncoder` is only used by
`McpHandler` on the named pipe. Phase 3 **cannot** drop Cursor
`tools/list`. The Cursor-zero risk remains `UseStructuredContent` /
inferred `outputSchema` on daemon envelope tools — still out of
sequence.

**3a (now):** Delete write-only `annotations.iconSource` (`icons[]` is
the real channel). Remove unused `IHostSession.CallToolAsync`
(production is `CallToolPassthroughAsync`). Gate: compile + focused
tests; golden list JSON unchanged for tools without `iconSource`.

**3b (later change set):** Store/encode SDK `Tool` / `Resource` /
`ResourceTemplate` / `ReadResourceResult`; delete `CatalogListEncoder`
and `ReadResourceEncoder`; keep `PrepareForWire` / `PreviewStructured`.
Gate: parser tests on real toolset assemblies + Python parser
(`Tool.InputSchema` setter throws on invalid schema), host→daemon
`HostCatalogEntry` round-trip, one live `search_dynamic`. **Not**
Cursor `tools/list`.

### Phase 4 — Optional MRTR discriminator (landed 2026-08-31)

- Add `InputRequiredResult? InputRequired` on `McpInvocationResponse`.
- `HostToolResultJson` switches on the field, not the meta key.
- Wire must be byte-identical to Phase 3.

### Explicitly out of sequence

- `McpServer` on host pipe.
- `CallToolResult` as the host wire result DTO.
- `McpServerTool.Create` in the catalog parser.
- Host MCP Tasks extension on the pipe.
- `resources/subscribe`, `completions`.
- Enabling daemon `UseStructuredContent` without the Cursor live gate.
- Blocking any phase on an upstream SDK passthrough API.

## Alternatives Considered

1. **Run `McpServer` on the host pipe and delete `IMcpHandler`.**
   Rejected. Dynamic metadata-only catalog still needs custom
   `tools/list` / `tools/call`. Main-thread marshalling stays. ALC
   JSON bridge stays. Adds session/DI surface inside Revit/AutoCAD.

2. **Honor 0012 literally: strip SDK from the host.**
   Rejected. Breaks types already on the host path. Only win is
   add-in size.

3. **Delete `McpSpecKeys` and replace every remaining DevTools-owned key.**
   Rejected for discover/list JsonNode builders and MetadataLoadContext
   reflection. SDK method/`_meta`/error-code aliases **were** removed;
   those call sites use `RequestMethods` / `MetaKeys` / `McpErrorCode`.

4. **Replace `McpInvocationResponse` with `CallToolResult` end-to-end.**
   Rejected. Identity cut remains; loses a place to hang an
   in-process MRTR discriminator.

5. **Forward `Meta` / `progressToken` from `invoke_dynamic` now.**
   Rejected as a standalone fix. Host does not emit
   `notifications/progress`; `SdkNoopTransport` swallows ALC
   `IProgress`. See [0028](0028-host-alc-progress-notifications.md).

6. **Wait for upstream no-auto-MRTR before Phase 1.**
   Rejected. File an SDK issue (`McpRequestOptions.ResolveInputRequests
   = false` per call, preferred over a process-wide flag). Keep
   reflection as the standing solution plus the Phase 1 existence test.

## Consequences

Positive:

- Docs match the binary (SDK on host, no SDK *session* on host).
- Constant drift becomes a compile error when the SDK removes a key.
- Adoption order is gated so Cursor `tools/list` cannot break in
  Phase 1–2.
- Agents stop re-proposing `McpServer` on the pipe or
  `McpServerTool.Create` under MetadataLoadContext.

Tradeoffs:

- `DevTools.Mcp.Core` keeps a multi-TFM SDK dependency (including
  net48).
- `McpSpecKeys` remains only for keys the SDK does not export.
- `McpClientPassthrough` stays brittle until upstream ships an API.

## Follow-Up

- Upstream feature request: per-call disable of client MRTR auto-resolve.
- Optional rename of app-layer `McpErrorCode` (dotted strings) if the
  collision with SDK `McpErrorCode` (JSON-RPC ints) becomes painful.

## References

- [0012 Host MCP spec engine](0012-host-mcp-spec-engine.md)
- [0019 ILRepack and Polyfill](0019-ilrepack-and-polyfill-isolated-alc.md)
- [0028 Host/ALC progress notifications](0028-host-alc-progress-notifications.md)
- [`docs/architecture/MCP/platform-boundaries.md`](../architecture/MCP/platform-boundaries.md)
- [`docs/architecture/MCP/sdk-gap-matrix.md`](../architecture/MCP/sdk-gap-matrix.md)
- SDK 2.2.0: `ModelContextProtocol.Protocol` (`RequestMethods`,
  `MetaKeys`, `Tool`, `CallToolRequestParams`, `CallToolResult`,
  `InputRequiredResult`), `McpJsonUtilities`,
  `McpServerTool.Create`
