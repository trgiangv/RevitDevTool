# MCP platform boundaries (host wire, pass-through, MRTR)

Authoritative map of **where** MCP SDK behavior is applied and **where** the host uses
spec wire DTOs. Product scope is [0027](../../decisions/0027-mcp-product-surface.md)
(Daemon envelope + search→invoke; not full protocol).

Last updated: 2026-10-03

> **ADR 0027 (amended 2026-10-03):** External clients still use only the Daemon envelope.
> The host pipe runs SDK **`McpServer`** per connection; see [0039](../../decisions/0039-mcp-flow-audit-sdk-reuse-and-vocabulary.md) **As implemented**.
> [0012](../../decisions/0012-host-mcp-spec-engine.md) spec-handler text is historical.

## Audience

- Reviewers validating that new code stays in the correct layer (`Daemon/Mcp` vs `Catalog` vs `Execution`).

Product behavior contracts: [`docs/product/mcp.md`](../../product/mcp.md).
MRTR session plan (closed): [`docs/plans/completed/2026-08-02-mrtr-implementation.md`](../../plans/completed/2026-08-02-mrtr-implementation.md).

---

## Topology

```text
External AI client
  │  tools/call: search_dynamic | invoke_dynamic | infrastructure
  ▼
DevTools.Daemon (MCP server, SDK; McpEngine in Daemon/Mcp)
  │  ProcessSessions (MCP client, SDK)
  │  Named pipe: DevToolsMcp_{Host}_{Version}_{PID}
  ▼
Host process (Revit / AutoCAD)
  McpPipeServer → SDK McpServer → IMcpSource backends
```

**Invariant:** External clients never call host tool names (`revit_find_elements`) on the
daemon. They use opaque `id` from `search_dynamic` → `invoke_dynamic`.

**Dynamic invoke path:**

```text
invoke_dynamic (daemon)
  → ProcessSession.CallToolPassthroughAsync(CallToolRequestParams)
  → host tools/call (SDK McpServer)
  → BuiltInSource | DotnetSource | PythonSource
  → built-in | .NET toolset ALC | Python toolset
```

---

## Layer responsibilities

| Layer | Owns | Does **not** own |
|-------|------|------------------|
| `DevTools.Daemon` (`Mcp/`) | External MCP surface; `ProcessSessions`; `search_dynamic` / `invoke_dynamic`; `McpClientPassthrough` | Host tool execution |
| `DevTools.Mcp.Catalog` | Host pipe server, `McpCatalogStore`, discovery, ALC lifecycle, `McpSpecKeys`, host `McpLogFilters` | Daemon auth/gateway |
| `DevTools.Execution` (`External/Mcp/`) | `IMcpSource` backends, built-in execute tools, `McpConnectTracker`, `McpCallFilter` | Daemon catalog search |
| `DevTools.Mcp.Revit` / `.Acad` | Host built-in tools/resources | Transport |
| `samples/*` | Business logic, structured output, product policies | Transport, toolset loading |

**Pass-through rules:**

1. Discovery from `ProcessCatalogs` / host list results only — no runtime inference on daemon.
2. `invoke_dynamic` forwards `arguments`, `inputResponses`, and `requestState` on `CallToolRequestParams` (not `progressToken` — host progress is a 0027 non-goal).
3. Success results pass `Content`, `StructuredContent`, `Meta`, `IsError` without re-wrapping business payloads.
4. Isolated .NET toolsets cross the ALC boundary as SDK-contract JSON; no second content-block model is maintained.

### Invocation request boundaries

SDK `CallToolRequestParams` is the shared DTO ([0027](../../decisions/0027-mcp-product-surface.md)). The SDK server deserializes `tools/call`. `RequestFactory` builds the `RequestContext` a .NET tool needs. An isolated toolset result becomes a host `CallToolResult` in `ResultBridge.ToHostCallToolResult`.

| Layer | Type | Role |
|-------|------|------|
| SDK server | `CallToolRequestParams` | `tools/call` params |
| `DevTools.Mcp.Catalog` | `RequestFactory.ToToolContext` | params → SDK `RequestContext` for .NET toolsets |
| `DevTools.Mcp.Catalog` | `ResultBridge.ToHostCallToolResult` | toolset object → host `CallToolResult` |
| Python MCP backend | private request/result operations | host request → embedded Python bridge JSON → result |

List and read results are SDK `ListToolsResult` / `ListResourcesResult` /
`ListResourceTemplatesResult` / `ReadResourceResult`. A tool result leaves the
host as `CallToolResult`. Daemon passthrough reads that JSON back into
`CallToolResult` or `InputRequiredResult`.

### Catalog ports

| Port | Implementation | Role |
|------|----------------|------|
| Daemon catalog index | `ProcessCatalogs` (`ProcessSessions`) | Search hits from connected host sessions |

Host in-process catalog is `McpCatalogStore` (Catalog). UI injects the store directly (`CatalogChanged`, `ReloadAsync`, SDK descriptors).

---

## Dispatch paths by backend

`IMcpSource` routing uses `RegisteredTool.Binding.SourceKind` (`PrimitiveBinding`):

| Backend | Invoke mechanism | MRTR | ALC notes |
|---------|------------------|------|-----------|
| **Built-in C#** | Direct `IBuiltInMcpTool` invoke | Host wire forwards `input_required` | Same assembly as host |
| **.NET toolset** | Cached SDK `McpServerTool` + SDK-contract JSON result bridge | Low-level `InputRequiredException` + retry params | Isolated ALC; private identity still needs ALC-local primitive |
| **Python toolset** | `PythonExecutor.Execute(initializer, …)` + `ToolInvoke.py` | `InputRequiredResult` → exception on wire | Interpreted |
| **Ad-hoc C#** | Rare catalog path | Same as built-in if registered | — |

Resources (.NET toolset): dispatcher resource path with template URI from catalog metadata.

---

## Toolset load boundary

Isolated .NET toolsets load via `McpToolsetContext`, whose feature-owned
`McpToolsetIsolationPlan` uses the shared `DevTools.AssemblyIsolation` session.
The plan `Pin`s MCP contract assemblies from the host default load context
(simple-name keyed). It resolves private toolset dependencies from the toolset
resolver and sibling directory on modern .NET (sibling directory only in the
scoped net48 session), and emits structured resolution diagnostics to the MCP
logger. Host ILRepack embeds copy-local MCP into the add-in DLL, removing
standalone `ModelContextProtocol*` assembly identities; automatic bind from the
host load context for repacked MCP is **not implemented** in the isolation kernel
today. Identity and lifecycle work live under `Catalog/Isolation/`; protocol
shape mapping is delegated to the SDK serializer rather than a reflected reader.
The .NET backend now creates and caches `McpServerTool` for the resolved method,
so SDK binding and result normalization are used on the normal identity path.
The remaining private-SDK packaging path must move this same primitive creation
inside the toolset ALC; it must not reintroduce protocol-shape switches.
Metadata-only discovery accepts explicit dependency roots from the runtime
composition layer. These paths are added to `PathAssemblyResolver` as files or
directories; missing explicit paths fail that provider load instead of being
silently forgiven. Host-specific assemblies therefore belong in runtime
composition, not persisted MCP registry configuration or parser hardcoded paths.

| Toolset returns | Host wire |
|-----------------|-----------|
| `CallToolResult` (text / structured / image / resource_link) | ✅ Full pass-through after JSON bridge |
| Plain `object` / `string` | ✅ Mapper builds structured + text |
| Low-level `InputRequiredException` | ✅ Forwarded on wire; retry via `InputResponses` / `RequestState` |
| High-level `ElicitAsync` / `MrtrContext` suspend | ❌ Sync invoker — documented unsupported |

Toolsets reference MCP with `ExcludeAssets=runtime`. Resolving MCP contracts on a
repacked host without a private toolset copy requires a packaging strategy not yet
chosen ([S5 strategy gate](../../plans/completed/2026-09-03-mcp-layer-identity-s5.md)
— S1 / S2 open). Private toolset dependencies remain toolset-local.

### Tests

- `tests/DevTools.Mcp.Catalog.Tests/AlcCallToolResultBridgeReproTests.cs`
- `tests/DevTools.Mcp.Catalog.Tests/ToolsetInvokerTests.cs`
- Live checklist: `docs/agents/mcp-integration-test.md`

---

## MRTR (Multi-Round Tool Results) — wire vs product

### Protocol stack (SDK 2.2.0, `2026-07-28+`)

| Mechanism | SDK surface |
|-----------|-------------|
| Incomplete tool result | `InputRequiredResult` on wire (`resultType: input_required`) or `InputRequiredException` from handler |
| Client retry | `CallToolRequestParams.InputResponses` + `RequestState` |
| Server detection | `McpServer.IsMrtrSupported`, `ClientSupportsMrtr()`, stateful transport back-compat resolution |

### What DevTools implements today

| Hop | Behavior | Source |
|-----|----------|--------|
| Daemon → host (dynamic tool) | **Single round-trip** per `invoke_dynamic` call; `McpClientPassthrough` avoids auto-MRTR | `ProcessSession.CallToolPassthroughAsync` |
| Host returns `input_required` | Deserialized from host JSON in passthrough | `ProcessSession` |
| Daemon → external client | `InvokeTool` throws `InputRequiredException` with `ForwardInputRequired` — embeds `InvokeState` in daemon `requestState` | `InvokeTool.cs` |
| Client retry `invoke_dynamic` | Parses `InvokeState`; forwards `inputResponses` + host `requestState` on `CallToolRequestParams` | `InvokeTool` |
| Mock proof | `InvokeDynamic_ForwardsHostInputRequired`, `InvokeDynamic_MrtrRetry_ForwardsInputResponses…` | `InvokeDynamicSdkHarnessTests.cs` |

### Closed gaps and non-goals

Historical G1–G5 labels live in
[`2026-08-02-mrtr-implementation.md`](../../plans/completed/2026-08-02-mrtr-implementation.md).
They are **not** a delivery backlog ([0027](../../decisions/0027-mcp-product-surface.md)).

| Item | Status |
|------|--------|
| ALC create-time bind + low-level throw/retry | ✅ Done |
| High-level `ElicitAsync` / `MrtrContext` suspend | ❌ Sync ALC invoker — low-level `InputRequiredException` only |
| Python `Resolve(Elicit[T])` | ❌ Out of scope — payload + `InputRequiredResult` bridge exists |
| Product delete confirm | Warning + `dryRun`; elicitation is not the product path |
| Gateway elicitation / host `IsMrtrSupported` backcompat / host progress | ⏸ Not product |

### Double-hop MRTR flow (plumbing only — not a product loop)

```text
1. Client: invoke_dynamic(id, arguments={...})
2. Daemon → Host tools/call (passthrough)
3. Host tool throws InputRequiredException OR host server serializes input_required
4. Daemon invoke_dynamic → InputRequiredException (daemon requestState wraps id + arguments + host requestState)
5. External client fulfills elicitation (MRTR-capable connector)
6. Client: invoke_dynamic(same id, inputResponses=..., requestState=daemon state)
7. Daemon forwards inputResponses + host requestState to host tools/call
8. Host completes → CallToolResult pass-through to client
```

**Do not reintroduce:** `__mcp*` argument augmentation or parallel MRTR meta keys — MRTR state stays in protocol fields + daemon `InvokeState`.

### ALC MRTR policy

Isolated .NET toolsets (ALC) support **low-level** MRTR only — the same pattern as the
csharp-sdk `InputRequiredException` docs:

1. Round 1: throw `InputRequiredException` with `inputRequests` (e.g. elicitation confirm)
   and optional opaque `requestState`.
2. Round 2: client retries with `CallToolRequestParams.InputResponses` and echoed
   `RequestState`; tool reads `context.Params.InputResponses` / `RequestState` (csharp-sdk
   does **not** merge responses into `Arguments`).
3. Do not gate product tools on `server.IsMrtrSupported` — that backcompat path is
   not a delivery track ([0027](../../decisions/0027-mcp-product-surface.md)).

**Not supported on ALC:** csharp-sdk **high-level** implicit MRTR (`server.ElicitAsync`,
`MrtrContext` handler suspension across round-trips inside `McpServerImpl`). Those APIs keep
the handler task alive until elicitation completes. `ToolsetInvoker` uses sync
completion on the host thread; if the task is incomplete it throws `NotSupportedException`. ALC tools must not call `ElicitAsync`. Host built-ins on the named pipe are the same: no high-level suspend.

| MRTR style | Daemon SDK tools | Host built-in / ALC / Python |
|----------|------------------|------------------------------|
| Low-level `InputRequiredException` + read `Params` on retry | ✅ plumbing | ✅ |
| High-level `ElicitAsync` / `MrtrContext` suspend | Not a product workflow | ❌ |
| python-sdk `Resolve(Elicit[T])` | N/A | ❌ |

**Live spike stub:** `samples/McpToolsetDemo` tool `test_mrtr_confirm`. Register
the demo DLL in `McpRegistryConfig.json` — do not use for product delete confirm.

---

## MCP SDK 2.2 adaptation matrix

See **[SDK gap matrix](sdk-gap-matrix.md)** for the living ✅/⚠️/⏸ table vs
`ModelContextProtocol` 2.2.0. Summary below.

**Packages:** `ModelContextProtocol` + `ModelContextProtocol.Extensions.Tasks` **2.2.0**
(`Directory.Packages.props`).

| SDK / spec feature | Status | Notes |
|--------------------|--------|-------|
| `StructuredContent` + `OutputSchema` | ✅ | Toolsets, daemon search/list/read |
| Resource templates | ✅ | C# + Python samples; batch `reads[]` |
| `CallToolRequestParams` full shape | ✅ | Passthrough on dynamic invoke |
| MRTR wire (`InputRequiredResult`) | ✅ | Forward + mocks + ALC retry (G1); product G2=B warning-first |
| MCP Tasks | ✅ | **Synchronous** or **Optional** only. **Required** unused until clients advertise `io.modelcontextprotocol/tasks` |
| `ResourceLinkBlock` pass-through | ✅ | No auto-fetch |
| Image / audio `ContentBlock` | ✅ | `view_screenshot`, harness |
| Progress notifications | ⚠️ Daemon fixed tools ✅; host pipe / `invoke_dynamic` / ALC / Python / built-ins ❌ | [0027](../../decisions/0027-mcp-product-surface.md) non-goal |
| `resources/subscribe` | ⏸ | Intentional |
| `completions` | ⏸ | Intentional |
| `ToolUse` / `ToolResult` blocks | ⏸ | Intentional |

**Custom patterns (intentional):**

- Opaque `id` locators (`CatalogId` / `dci2.*`; daemon-local, content-hash freshness).
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
| Host toolsets | Full payload when `UseStructuredContent=true` | SDK-generated content; custom `CallToolResult` only for content blocks |
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
# ALC mapper (Catalog) + invoke harness (Server) — no live host
dotnet run --project tests/DevTools.Mcp.Catalog.Tests/DevTools.Mcp.Catalog.Tests.csproj -- --filter "ToolsetResultSerializer|ToolsetInvoker"
dotnet run --project tests/DevTools.Mcp.Server.Tests/DevTools.Mcp.Server.Tests.csproj -- --filter "InvokeDynamicSdkHarness"

# Parser / structured output
dotnet run --project tests/DevTools.Mcp.Catalog.Tests/DevTools.Mcp.Catalog.Tests.csproj -- --filter "RevitMcpToolSetParser"
dotnet run --project tests/DevTools.Mcp.Server.Tests/DevTools.Mcp.Server.Tests.csproj -- --filter "StructuredOutput"

# Live integration checklist
# docs/agents/mcp-integration-test.md — Scenarios 9 (templates), 10 (delete warning)
```

---

## Source index (boundaries)

| Topic | Primary files |
|-------|----------------|
| Daemon invoke + MRTR forward | `source/DevTools.Daemon/Mcp/Tools/InvokeTool.cs` |
| MRTR state envelope | `source/DevTools.Daemon/Mcp/Contracts/DynamicContracts.cs` (`InvokeState`) |
| Host passthrough | `source/DevTools.Daemon/Mcp/Processes/ProcessSession.cs`, `McpClientPassthrough.cs` |
| IMcpSource backends | `source/DevTools.Execution/External/Mcp/Backends/` |
| ALC result bridge | `source/DevTools.Mcp.Catalog/Discovery/ResultBridge.cs` |
| Toolset load | `source/DevTools.Mcp.Catalog/Isolation/McpToolsetContext.cs`, `McpToolsetIsolationPlan.cs` |
| Host pipe server | `source/DevTools.Mcp.Catalog/Transport/McpPipeServer.cs` |
| Mock MRTR harness | `tests/DevTools.Mcp.Server.Tests/Harness/McpSdkTestHarness.cs` |
