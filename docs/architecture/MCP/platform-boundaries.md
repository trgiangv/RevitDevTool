# MCP platform boundaries (host wire, pass-through, MRTR)

Authoritative map of **where** MCP SDK behavior is applied, **where** the host uses
spec wire DTOs, and **what remains** for Gateway E2E (G3) and host legacy
backcompat (G4).

Last updated: 2026-08-03

> **ADR 0012:** Host in-process runtime uses spec wire DTOs (`DevTools.Mcp.Core.Protocol`)
> via `McpHandler`. Daemon, client broker, and third-party toolsets keep the official MCP SDK.
> See [`docs/decisions/0012-host-mcp-spec-engine.md`](../../decisions/0012-host-mcp-spec-engine.md).

## Audience

- Reviewers validating that new code stays in the correct layer (`Core` vs `Catalog` vs `Server`).

Product behavior contracts: [`docs/product/mcp.md`](../../product/mcp.md).
MRTR session plan: [`docs/plans/active/2026-08-02-mrtr-implementation.md`](../../plans/active/2026-08-02-mrtr-implementation.md).

---

## Topology

```text
External AI client
  │  tools/call: search_dynamic | invoke_dynamic | infrastructure
  ▼
DevTools.Daemon (MCP server, SDK)          DevTools.Mcp.Server
  │  HostBroker (MCP client, SDK)
  │  Named pipe: DevToolsMcp_{Host}_{Version}_{PID}
  ▼
Host process (Revit / AutoCAD)
  HostMcpPipeServer → McpHandler (spec wire) → McpPrimitiveDispatcher
```

**Invariant:** External clients never call host tool names (`revit_find_elements`) on the
daemon. They use opaque `capabilityId` from `search_dynamic` → `invoke_dynamic`.

**Dynamic invoke path:**

```text
invoke_dynamic (daemon)
  → HostSession.CallToolPassthroughAsync(CallToolRequestParams)
  → host tools/call (McpHandler)
  → McpPrimitiveDispatcher
  → built-in | .NET toolset ALC | Python toolset
```

---

## Layer responsibilities

| Layer | Owns | Does **not** own |
|-------|------|------------------|
| `DevTools.Mcp.Core` | Wire DTOs, `IHostSession`, `HostToolCallOutcome`, broker contracts | Tool-specific MRTR schemas, ALC mapping |
| `DevTools.Mcp.Client` | `HostSession`, `McpClientPassthrough`, `ConnectedHostCatalog` | Host dispatch, catalog parsing |
| `DevTools.Mcp.Server` | Daemon fixed tools; `search_dynamic` / `invoke_dynamic`; MRTR re-throw | Host tool execution |
| `DevTools.Mcp.Catalog` | Parser, `McpToolsetContext`, `ToolsetInvoker`, `ToolsetResultSerializer` | Daemon external tool surface |
| `DevTools.Mcp.Adapter` | `McpHandler`, `HostMcpPipeServer` | Dynamic capability IDs |
| `DevTools.Execution` | `McpPrimitiveDispatcher`, built-in tools, Python bridge | Daemon catalog search |
| `samples/*` | Business logic, structured output, product policies | Transport, toolset loading |

**Pass-through rules:**

1. Discovery from `ConnectedHostCatalog` schema only — no runtime inference on daemon.
2. `invoke_dynamic` forwards `arguments`, `inputResponses`, `requestState`, `progressToken` on `CallToolRequestParams`.
3. Success results pass `Content`, `StructuredContent`, `Meta`, `IsError` without re-wrapping business payloads.
4. Isolated .NET toolsets: **Catalog** serializes toolset `CallToolResult` in the toolset domain and deserializes to host wire DTOs.

### Invocation request boundaries

`McpInvocationRequest` (`Core/Protocol/Invocation/`) is the shared DTO:

| Layer | Type | Role |
|-------|------|------|
| `DevTools.Mcp.Core` | `InvocationRequestReader.FromWire` | JSON-RPC `tools/call` params → DTO |
| `DevTools.Mcp.Catalog` | `SdkInvocationRequest.ToToolContext` | DTO → SDK `RequestContext` for .NET toolsets |
| `DevTools.Mcp.Adapter` | `PythonInvocationPayload.ToJson` | DTO → JSON for embedded Python bridge |

Wire encoders: `CatalogListEncoder`, `InvocationResponseEncoder`, `ReadResourceEncoder`.

### Catalog ports

| Port | Implementation | Role |
|------|----------------|------|
| `IConnectedHostCatalog` | `ConnectedHostCatalog` (Client) | Daemon: capabilities from connected host sessions |
| `IHostPrimitiveRegistry` | `McpCatalogStore` (Catalog) | Host: primitives loaded in-process |

---

## Dispatch paths by backend

`McpPrimitiveDispatcher.DispatchToolAsync` routes on `McpRegisteredTool.Binding.SourceKind`:

| Backend | Invoke mechanism | MRTR | ALC notes |
|---------|------------------|------|-----------|
| **Built-in C#** | Direct `IBuiltInMcpTool` invoke | Host wire forwards `input_required` | Same assembly as host |
| **.NET toolset** | `ToolsetInvoker` + `ToolsetResultSerializer` | Low-level `InputRequiredException` + retry params | Isolated ALC; JSON bridge |
| **Python toolset** | `PythonExecutor` + `ToolInvoke.py` | `InputRequiredResult` → exception on wire | Interpreted |
| **Ad-hoc C#** | Rare catalog path | Same as built-in if registered | — |

Resources (.NET toolset): dispatcher resource path with template URI from catalog metadata.

---

## Toolset load boundary

Isolated .NET toolsets load via `McpToolsetContext` (collectible ALC + assembly resolve).
Invoke uses `ToolsetInvoker` → `AIFunction`; results cross the boundary through
`ToolsetResultSerializer` (serialize in toolset domain, deserialize to `McpInvocationResponse`).

| Toolset returns | Host wire |
|-----------------|-----------|
| `CallToolResult` (text / structured / image / resource_link) | ✅ Full pass-through after JSON bridge |
| Plain `object` / `string` | ✅ Mapper builds structured + text |
| Low-level `InputRequiredException` | ✅ Forwarded on wire; retry via `InputResponses` / `RequestState` |
| High-level `ElicitAsync` / `MrtrContext` suspend | ❌ Sync invoker — documented unsupported |

Toolsets reference MCP with `ExcludeAssets=runtime`. Runtime binds to host/toolset load context
per ADR 0012 packaging rules.

### Tests

- `tests/DevTools.Mcp.Tests/ToolsetResultSerializerTests.cs`
- `tests/DevTools.Mcp.Tests/ToolsetInvokerTests.cs`
- Live checklist: `docs/agents/mcp-integration-test.md`

---

## MRTR (Multi-Round Tool Results) — wire vs product

### Protocol stack (SDK 2.0.0, `2026-07-28+`)

| Mechanism | SDK surface |
|-----------|-------------|
| Incomplete tool result | `InputRequiredResult` on wire (`resultType: input_required`) or `InputRequiredException` from handler |
| Client retry | `CallToolRequestParams.InputResponses` + `RequestState` |
| Server detection | `McpServer.IsMrtrSupported`, `ClientSupportsMrtr()`, stateful transport back-compat resolution |

### What DevTools implements today

| Hop | Behavior | Source |
|-----|----------|--------|
| Daemon → host (dynamic tool) | **Single round-trip** per `invoke_dynamic` call; no `McpClient.CallToolAsync` auto-retry | `HostSession.CallToolPassthroughAsync` |
| Host returns `input_required` | Deserialized to `HostToolCallOutcome.FromInputRequired` | `HostSession` |
| Daemon → external client | `InvokeDynamicTool` throws `InputRequiredException` with `ForwardInputRequired` — embeds `InvokeDynamicMrtrState` in daemon `requestState` | `InvokeDynamicTool.cs` |
| Client retry `invoke_dynamic` | Parses `InvokeDynamicMrtrState`; forwards `inputResponses` + host `requestState` on `CallToolRequestParams` | `InvokeDynamicTool.InvokeSingleAsync` |
| Mock proof | `InvokeDynamic_ForwardsHostInputRequired`, `InvokeDynamic_MrtrRetry_ForwardsInputResponses…` | `InvokeDynamicSdkHarnessTests.cs` |

### Gaps (MRTR session scope)

Refined 2026-08-02 — full matrix in
[`2026-08-02-mrtr-implementation.md`](../../plans/active/2026-08-02-mrtr-implementation.md).

| Gap | Impact | Likely fix layer |
|-----|--------|------------------|
| **G1-a** `DotnetToolsetAiFunctionFactory` `IsAugmentedWith` | ✅ Done — create-time bind mirrors SDK | — |
| **G1-b** ALC automated throw→retry with `InputResponses` | ✅ Done — T-ALC-10..15 harness | — |
| **G1-c** High-level `ElicitAsync` / `MrtrContext` suspend | Incompatible with sync ALC `InvokeSync` | **Documented unsupported** — low-level `InputRequiredException` only (see below) |
| **G1-Py** Full python-sdk `Resolve(Elicit[T])` inside embedded toolsets | Manual `InputRequiredResult` return + MRTR payload retry works; Resolve DI not embedded | Out of scope — bridge done (`PythonInvocationPayload` / parser) |
| **G2** Product delete confirm | **B recorded** — warning + `dryRun`; MRTR elicitation deferred pending G3/G4 | — |
| **G3** Gateway / cloud elicitation | Unproven through `McpGateway` | Checklist T-GW-* |
| **G4** Stateful host backcompat when daemon lacks MRTR | Possible hang/legacy elicitation to daemon | Spike T-HOST-03/04 before G2=A |

### Double-hop MRTR flow (wire implemented; Gateway E2E open)

```text
1. Client: invoke_dynamic(capabilityId, arguments={...})
2. Daemon → Host tools/call (passthrough)
3. Host tool throws InputRequiredException OR host server serializes input_required
4. Daemon invoke_dynamic → InputRequiredException (daemon requestState wraps capabilityId + arguments + host requestState)
5. External client fulfills elicitation (MRTR-capable connector)
6. Client: invoke_dynamic(same capabilityId, inputResponses=..., requestState=daemon state)
7. Daemon forwards inputResponses + host requestState to host tools/call
8. Host completes → CallToolResult pass-through to client
```

**Do not reintroduce:** `__mcp*` argument augmentation, `DotnetToolProtocolBridge`, Core `McpMrtrMeta` — rejected as over-engineering; MRTR state stays in protocol fields + `InvokeDynamicMrtrState`.

### ALC MRTR policy (G1-c — locked)

Isolated .NET toolsets (ALC) support **low-level** MRTR only — the same pattern as the
csharp-sdk `InputRequiredException` docs:

1. Round 1: throw `InputRequiredException` with `inputRequests` (e.g. elicitation confirm)
   and optional opaque `requestState`.
2. Round 2: client retries with `CallToolRequestParams.InputResponses` and echoed
   `RequestState`; tool reads `context.Params.InputResponses` / `RequestState` (csharp-sdk
   does **not** merge responses into `Arguments`).
3. Gate on `server.IsMrtrSupported` when product cares about legacy clients — return a soft
   message instead of throwing.

**Not supported on ALC:** csharp-sdk **high-level** implicit MRTR (`server.ElicitAsync`,
`MrtrContext` handler suspension across round-trips inside `McpServerImpl`). Those APIs keep
the handler task alive until elicitation completes. `ToolsetInvoker` uses sync
completion on the host thread; if the task is incomplete it throws `NotSupportedException`. ALC tools must not call `ElicitAsync`.

| MRTR style | Built-in / full SDK path | ALC .NET toolset |
|------------|--------------------------|------------------|
| Low-level `InputRequiredException` + read `Params` on retry | ✅ | ✅ (after G1-a bind) |
| High-level `ElicitAsync` / `MrtrContext` suspend | ✅ | ❌ sync invoker |
| python-sdk `Resolve(Elicit[T])` | N/A | ❌ (G1-Py deferred) |

**Live spike stub:** `samples/McpToolsetDemo` tool `test_mrtr_confirm` (T-HOST-02). Register
the demo DLL in `McpRegistryConfig.json` — do not use for product delete confirm (G2=B).

---

## MCP SDK 2.0 adaptation matrix

See **[SDK 2.0 gap matrix](sdk-2-0-gap-matrix.md)** for the living ✅/⚠️/⏸ table vs
`ModelContextProtocol` 2.0.0. Summary below.

**Packages:** `ModelContextProtocol` + `ModelContextProtocol.Extensions.Tasks` **2.0.0**
(`Directory.Packages.props`).

| SDK / spec feature | Status | Notes |
|--------------------|--------|-------|
| `StructuredContent` + `OutputSchema` | ✅ | Toolsets, daemon search/list/read |
| Resource templates | ✅ | C# + Python samples; batch `reads[]` |
| `CallToolRequestParams` full shape | ✅ | Passthrough on dynamic invoke |
| MRTR wire (`InputRequiredResult`) | ✅ | Forward + mocks + ALC retry (G1); product G2=B warning-first |
| MCP Tasks Optional | ✅ | Export + execute tools |
| `ResourceLinkBlock` pass-through | ✅ | No auto-fetch |
| Image / audio `ContentBlock` | ✅ | `view_screenshot`, harness |
| Progress notifications | ✅ | `CallToolRequestServiceProvider` |
| `resources/subscribe` | ⏸ | Intentional |
| `completions` | ⏸ | Intentional |
| `ToolUse` / `ToolResult` blocks | ⏸ | Intentional |

**Custom patterns (intentional):**

- Opaque `capabilityId` locators (daemon-local, catalog-versioned).
- `CallToolPassthroughAsync` instead of client auto-MRTR on daemon→host leg.
- ALC `AIFunction` invoker + JSON return mapper (not in SDK).

---

## JSON and logging

| Role | Serializer |
|------|------------|
| Wire, logs, discovery, mapper | `McpJsonUtilities.DefaultOptions` |
| Tool text summaries | Compact JSON in `TextContentBlock` when structured path used |
| Monitor logs | `McpLogPayload`; binary blocks log mime/length only |

Structured output policy:

| Surface | `StructuredContent` | Text `Content` |
|---------|---------------------|----------------|
| Daemon fixed tools | Full payload | Compact mirror |
| Host toolsets | Full payload when `UseStructuredContent=true` | One-line summary |
| `invoke_dynamic` errors | None | Compact JSON envelope |

---

## Operator configuration

Dynamic .NET toolsets require paths in
`%AppData%\RevitDevTool\{Year}\Settings\McpRegistryConfig.json` (`dotnetToolsetPaths`).
Paths are **per host year** — registering only for 2025 does not load toolset on Revit 2027.

Register **one** overlapping toolset at a time (Python vs C# `revit_*` name collision).

---

## Verification commands

```powershell
# ALC mapper + invoke harness (no live host)
dotnet test tests/DevTools.Mcp.Tests --filter "FullyQualifiedName~ToolsetResultSerializer|ToolsetInvoker|InvokeDynamicSdkHarness"

# Parser / structured output
dotnet test tests/DevTools.Mcp.Tests --filter "FullyQualifiedName~RevitMcpToolSetParser|StructuredOutput"

# Live integration checklist
# docs/agents/mcp-integration-test.md — Scenarios 9 (templates), 10 (delete warning)
```

---

## Source index (boundaries)

| Topic | Primary files |
|-------|----------------|
| Daemon invoke + MRTR forward | `source/DevTools.Mcp.Server/Tools/InvokeDynamicTool.cs` |
| MRTR state envelope | `source/DevTools.Mcp.Server/Contracts/InvokeDynamicMrtrState.cs` |
| Host passthrough | `source/DevTools.Mcp.Client/HostSession.cs` |
| Outcome union | `source/DevTools.Mcp.Core/HostToolCallOutcome.cs` |
| Dispatcher | `source/DevTools.Execution/External/Mcp/Dispatchers/McpPrimitiveDispatcher.cs` |
| ALC invoke + map | `source/DevTools.Mcp.Catalog/Discovery/ToolsetInvoker.cs`, `ToolsetResultSerializer.cs` |
| Toolset load | `source/DevTools.Mcp.Catalog/Discovery/McpToolsetContext.cs` |
| Host adapter | `source/DevTools.Mcp.Adapter/Host/McpHandler.cs` |
| Mock MRTR harness | `tests/DevTools.Mcp.Tests/Harness/McpSdkTestHarness.cs` |
