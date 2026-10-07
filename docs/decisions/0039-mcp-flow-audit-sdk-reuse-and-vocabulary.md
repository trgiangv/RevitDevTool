# 0039 MCP Flow Audit - SDK Re-invention, Obsolete Workarounds, Search, Vocabulary

Date: 2026-10-02

## Status

**Accepted 2026-10-02. Implemented 2026-10-03** by
[`2026-10-03-mcp-flow-simplification`](../plans/completed/2026-10-03-mcp-flow-simplification.md).
The 2026-10-02 consolidation plan was deleted. Do not restore it.

Superseded in part by [0040](0040-bm25f-search-and-hit-annotations.md)
(accepted 2026-10-07): the scoring bullet in decision 9 and alternative 5.
Rank is BM25F inside `code_mode`. The rest of this decision stands.

Still open: B2 (`DynamicToolResults` until a live `OutputSchema` check), removing
`McpClientPassthrough` (needs a public SDK send that does not auto-retry MRTR),
folding `Execution/External/Mcp` into Catalog, and a durable task store.

Startup catalog load is `McpPipeServer.EnsureLoaded`. The Revit and AutoCAD
`HostBackgroundController` does not call `ReloadAsync` after Python init (B6).

Acceptance context: the owner stated that backward compatibility is not
required and that the architecture and projects may be redefined. That removes
the compatibility argument against decisions 3, 4, 7, 9 and 10.

Relationship to existing decisions:

- Does not change [0010](0010-daemon-sole-mcp-host.md) (Daemon is the sole MCP
  host for external clients) or the envelope loop in
  [0027](0027-mcp-product-surface.md) (`search_dynamic` / `invoke_dynamic`).
- **Amends** [0027](0027-mcp-product-surface.md) rule 3 and Alternative 1
  ("no `McpServer` session on the host pipe"). **Amends** rule 5 as follows:
  unused MRTR product types are removed (decision 5). `McpClientPassthrough`
  stays until the C# SDK has a public send that does not auto-retry MRTR
  (decision 1, B1). **Supersedes** the remainder of
  [0012](0012-host-mcp-spec-engine.md). Those two files become amended/stub
  entries when the documentation task in section 9 lands.
- Items marked **[GATE]** carry a condition: the host `McpServer` swap must
  pass the real-pipe conformance suite (plan task H1) on net48 and net10. If it
  cannot, decision 3 reverts to Proposed.
- Items marked **[FIX]** are defects or doc errors that stand regardless of the
  gated direction.
- **A4 is decided** (2026-10-02): keep the metadata parser and narrow the
  hand-built schema. Do not execute toolset code at discovery, and do not read
  the schema back from `McpServerTool.Create`. See A4.
- Still open: **B2** (replace `DynamicToolResults` only after an explicit
  `OutputSchema` is checked against a live client). See decision 8.

Pins audited: C# `ModelContextProtocol` **2.2.0** and Python `mcp` **2.2.0**
(protocol `2026-07-28`). The runtime pixi manifest pins `mcp >=2.1.1,<3`.

## As implemented (code truth, 2026-10-03+)

Authoritative structure: [`docs/architecture/MCP/README.md`](../architecture/MCP/README.md).
Product wire: [`docs/product/mcp.md`](../product/mcp.md). This section records
divergences from decision text **below** (which is kept as audit history).

| Topic | Implemented | Notes |
|-------|-------------|--------|
| Assemblies | `DevTools.Daemon`, `DevTools.Mcp.Catalog`, `DevTools.Mcp.Revit`/`Acad`, `Execution/External/Mcp` | `DevTools.Mcp.Core` / `.Client` / `.Server` / `.Adapter` projects removed; `DevTools.Mcp.Core.*` namespaces live under Catalog `Core/` |
| Daemon discovery | `ProcessSessions` + `ProcessCatalogs` | Replaces `HostBroker` / `ConnectedHostCatalog` |
| Locator type | `CatalogId`; wire **`id`** | Decision 10 said `CapabilityId` / `capabilityId` — product uses `id` |
| Search filter | **`processId`** | Not `hostInstanceId` |
| Kind enum | `CatalogType` + `CatalogTypeCodec` | Was `CatalogKind` / `CatalogKindFormat` in plans |
| Catalog models | `PrimitiveBinding`, `RegisteredTool`, `RegisteredResource`, `RegistryCatalog` | Was `McpPrimitiveBinding`, etc. |
| Host pipe | `McpPipeServer` → `PipeEndpoint` + SDK `McpServer` | `McpHandler` / `McpPipeSession` / `McpJsonRpc` removed |
| Dispatch | `IMcpSource` (`BuiltInSource`, `DotnetSource`, `PythonSource`) | `McpPrimitiveDispatcher` removed |
| Dynamic tools | `SearchTool`, `InvokeTool` in `DevTools.Daemon/Mcp/Tools/` | Contracts in `DynamicContracts.cs` |
| MRTR daemon state | `InvokeState` | `InvokeDynamicMrtrState` removed |
| Passthrough | `McpClientPassthrough` in Daemon | Still required (B1); decision 5 unchanged |
| Tasks | **Synchronous** or **Optional** only. **Required** unused until clients advertise `io.modelcontextprotocol/tasks` | Decision text below still says a single tool call is Optional |
| Connect UI / metrics | `McpConnectTracker`, `McpCallFilter` | `tests/DevTools.Execution.Mcp.Tests` |

Still open per Status above: B2 (`DynamicToolResults` / live `OutputSchema`), durable task store.

## Context

The MCP flow is spread over eight modules:

| Module | Role |
|--------|------|
| `DevTools.Daemon` | Desktop/stdio composition, Gateway WebSocket tunnel |
| `DevTools.Mcp.Server` | Daemon fixed tool/prompt surface, `McpServer` options |
| `DevTools.Mcp.Client` | `HostBroker`, `HostSession`, `ConnectedHostCatalog`, pipe scanner |
| `DevTools.Mcp.Core` | Contracts, content model, protocol keys, session interfaces |
| `DevTools.Mcp.Adapter` | In-host spec handler (`McpHandler`) and pipe server |
| `DevTools.Mcp.Catalog` | In-host catalog store, toolset discovery, ALC isolation bridge |
| `DevTools.Mcp.Revit` / `DevTools.Mcp.Acad` | Built-in host tools and resources |
| `DevTools.Execution` (MCP part) | Dispatcher, backends, `ToolInvoke.py`, `ToolParser.py` |

End-to-end flow:

```text
AI client -- stdio | Gateway WS --> Daemon McpServer (SDK; fixed tools + prompts)
                                      | search_dynamic / invoke_dynamic
                                      v
        HostBroker (poll 2s) -> HostSession (SDK McpClient) -> McpClientPassthrough
                                      | named pipe DevToolsMcp_{Host}_{Ver}_{PID}
                                      v
        McpPipeSession -> McpHandler (hand-written, 2026-07-28)
                                      v
        McpCatalogStore -> McpPrimitiveDispatcher -> IHostContextExecutor
                 +-- BuiltIn  (McpServerTool.InvokeAsync)
                 +-- Dotnet   (MetadataLoadContext parse; McpServerTool.Create in ALC)
                 +-- Python   (ToolInvoke.py; fake ServerRequestContext; anyio.run)
```

The Daemon half uses the SDK as intended. The host half and the Python scripts
re-implement a large part of the SDK server and client surface. Several
decisions that justified that (0012 rule 3 and 7, S5 "legacy branch" cleanup)
have since been withdrawn or completed, and the code still carries the shape of
the old rules. Search and vocabulary were never audited as a whole.

This ADR records the audit so future work inherits one list instead of
re-deriving it. Module inventory stays in
[`docs/architecture/MCP/`](../architecture/MCP/README.md); this file holds the
findings and the choice of direction only.

### Evidence legend

- **V** - verified by reading the code or the SDK source.
- **R** - needs runtime confirmation (live host, Cursor, or a unit test) before
  work starts.
- Severity: **High** (wrong behavior or spec violation), **Med** (cost, drift,
  or silent data loss risk), **Low** (cleanup).

### SDK facts relied on

C# SDK (`.opensrc/repos/github.com/modelcontextprotocol/csharp-sdk/main/src/ModelContextProtocol.Core`):

| Fact | Location |
|------|----------|
| `StreamServerTransport` is public; newline-delimited JSON-RPC over any `Stream` | `Server/StreamServerTransport.cs` |
| `McpServerImpl` subscribes to `McpServerPrimitiveCollection.Changed` and sends `list_changed` on stateful transports | `Server/McpServerImpl.cs` (ctor) |
| Server fills `ttlMs` / `cacheScope` on cacheable results, and adds `serverInfo` to result `_meta` | `Server/McpServerImpl.cs` |
| Session handler tracks in-flight requests and honors `notifications/cancelled`; requests are handled concurrently | `McpSessionHandler.cs` |
| `DiscoverResult`, `MetaKeys`, `RequestMethods`, `NotificationMethods` are public; `McpProtocolVersions` is internal | `Protocol/*` |
| `McpServerToolCreateOptions` exposes `OutputSchema` and `UseStructuredContent` | `Server/McpServerToolCreateOptions.cs` |
| `McpServerTool` is abstract (`ProtocolTool`, `InvokeAsync`); subclassing is supported | `Server/McpServerTool.cs` |
| Client logs a conformance warning when a 2026-07-28 server omits `ttlMs` / `cacheScope` | `Client/McpClientImpl.cs` (SEP-2549 check) |
| MRTR auto-retry (max 10) is inside the public send path; no public "do not retry" option | `Client/McpClientImpl.cs` |
| `UriTemplate` is internal | `Server/*` |

Python SDK (`.opensrc/.../python-sdk/main/src/mcp`):

| Fact | Location |
|------|----------|
| `Client(server)` accepts `MCPServer` or low-level `Server` and runs in-process via a direct dispatcher | `client/`, `shared/direct_dispatcher.py` |
| `call_tool` / `read_resource` accept `input_responses` and `request_state`; `session.call_tool(..., allow_input_required=True)` exists | `client/` |
| `MCPServer.list_tools` / `call_tool` / `read_resource` / `list_resources` / `list_resource_templates` are public | `server/mcpserver/server.py` |

## Findings

### A. Layers that re-invent the SDK

#### A1. Host wire stack replaces `McpServer` + `StreamServerTransport` **[GATE]** - High - V

- **Where:** `Adapter/Host/McpHandler.cs` (~250 lines), `McpPipeSession.cs`
  (~160), `McpJsonRpc.cs`, `Core/Protocol/McpSpecKeys.cs` (146 lines of string
  constants), `Core/Protocol/IMcpHandler.cs`, `Adapter/External/McpPipeServer.cs`
  (pipe accept loop and ACL stay).
- **What:** A manual `switch` on method name implements `server/discover`,
  `ping`, `tools/list`, `tools/call`, `resources/list`,
  `resources/templates/list`, `resources/read`. Unknown notifications return
  `null`. `McpPipeSession` documents "same framing as SDK stream transport".
- **Already contradicts the ADR text:** `RequestFactory` +
  `ToolExecutionTransport` call `McpServer.Create(...)` inside the host just to
  build `RequestContext<T>`. The Daemon already runs the same
  `StreamServerTransport` over stdio and over a WebSocket adapter.
- **Cost:** every behavior the SDK gives for free is either re-implemented or
  absent. See E1-E3 for the concrete defects. The 0027 non-goals "host
  progress" and "cancel" are partly a consequence of this handler, not of the
  product.
- **SDK equivalent:** `McpServer.Create(new StreamServerTransport(pipe, pipe),
  options)` with `ToolCollection` / `ResourceCollection`,
  `Filters.Request.CallToolFilters` for tracker/logging, and a custom
  `McpServerTool` or `CallToolHandler` that marshals through
  `IHostContextExecutor`.
- **Constraints to resolve before acting:** net48 target of `Mcp.Core` /
  `Adapter`, ILRepack identity ([0019](0019-ilrepack-and-polyfill-isolated-alc.md)),
  main-thread marshalling (0027 Alt 1 rationale), and the S1/S2 gate.
  `McpProtocolVersions` is internal, so a local `"2026-07-28"` constant is
  acceptable.

#### A2. Second result model and four conversions - Med - V

- **Where:** `Core/Invocation/McpInvocationResponse.cs` (`McpContent`,
  `McpTextContent`, `McpImageContent`, `McpAudioContent`, embedded/linked
  resource content), `Core/Protocol/InvocationResponseEncoder.cs`,
  `Core/Protocol/Invocation/InvocationRequestReader.cs`,
  `Catalog/Discovery/ToolsetResultSerializer.cs`,
  `Adapter/Bridging/SdkInvocationMapper.cs`, `Adapter/Host/HostToolResultJson.cs`.
- **What:** `McpInvocationResponse` mirrors `CallToolResult`. Built-in tools
  already return the host's own `CallToolResult`, yet the result still travels
  `CallToolResult -> ToolsetResultSerializer -> McpInvocationResponse ->
  SdkInvocationMapper -> CallToolResult -> JsonNode`.
- **Legitimate part:** a `CallToolResult` produced by a toolset in its own ALC
  has a different type identity, so it must be read from JSON. That is a
  boundary function, not a model (the `Catalog/Isolation/README.md` rule: "must
  not grow a second content-block object model").
- **Direction:** host-internal result type is `CallToolResult`. One function
  turns foreign-identity output into `CallToolResult`.

#### A3. Dead production code kept alive by tests - Med - V

- `Catalog/Discovery/ToolsetInvoker.cs`, `ToolsetArgumentBinder.cs`,
  `ToolsetInvocationServices.cs` (with `ToolsetProgressReporter`,
  `ToolsetNopProgress`): referenced only by tests. Production
  `DotnetSource` uses `McpServerTool.Create(...).InvokeAsync(context)`.
  The progress reporter blocks with `NotifyProgressAsync(...).GetAwaiter().GetResult()`
  on a discarded transport.
- `Catalog/Discovery/PythonToolsetParser.ParseDirectoryCatalog(dir, pythonExe,
  parserScriptPath)` and `RunParserProcess` (CliWrap): no production caller.
  Only the `Func<string,string?>` overload is used by
  `PythonMcpRegistryProvider`. The `CliWrap` package exists for this path.
- `McpTaskExecutionMeta.Invocation.InputRequired` (`devtools.inputRequired`
  key): read in `ToolsetMrtrBridge.TryGetInputRequiredResult`, written only by a
  test. The S5 plan (S5-B) listed this branch for deletion; it is still present.
  `McpInvocationResponse.InputRequired` already carries the same data.
- **Docs still describe code that is gone or never existed:** `ReturnMapper`
  and an "ALC `AIFunction` invoker" (`platform-boundaries.md`,
  `sdk-gap-matrix.md`). The `ToolsetArgumentBinder` comment says "without SDK
  AIFunction" while production uses SDK `McpServerTool`.

#### A4. Hand-built descriptors and schemas - Med - V

- **Where:** `Catalog/Discovery/McpAssemblyParser.cs` (~325 lines,
  MetadataLoadContext, attributes matched by `FullName` string),
  `McpSchemaBuilder.cs` (~165 lines), `DotnetMethodResolver.cs`,
  `MethodResolutionHelper.cs`, `McpCatalogCreateOptions.cs`.
- **What:** The advertised `Tool` (name, title, description, input/output
  schema, annotations, meta, icons) is built by hand. At invoke time the SDK
  rebuilds its own schema inside `McpServerTool.Create`. There are **two schema
  truths** for one tool.
- **Known limits of the hand-built schema:** depth greater than 4 yields `{}`;
  `object` yields `[]`; no `[Description]` on DTO properties; no defaults,
  `format`, or min/max; enums as a string array.
- `McpCatalogCreateOptions` forwards name/title/description/meta and sets
  `UseStructuredContent = OutputSchema is not null`, but not annotations or the
  `OutputSchema` itself.
- **Root cause:** metadata-only discovery exists because of the host/toolset MCP
  identity split (0019). `MetadataLoadContext` assemblies are never the same
  identity as the host's, so the parser matches `[McpServerTool]` and
  `[Description]` by `FullName` string and builds the schema itself. At runtime
  the same split forces `ToolsetResultSerializer` to cross the ALC as JSON,
  because ILRepack folds `ModelContextProtocol` into the add-in DLL and no
  standalone assembly identity remains for the collectible ALC to share.
- **Decided 2026-10-02:** keep the metadata parser and narrow it. Un-merging MCP
  so the ALC could share the host's types would put `Microsoft.Extensions.*` —
  the packages 0019 repacks with `/union` precisely because they collide —
  back into the add-in output as loose DLLs, multiplying the conflicts 0019
  exists to prevent. The parser therefore stays the only reader of a toolset's
  attributes. What gets deleted is the hand-built schema surface that goes past
  what attribute data can support (depth limit, `object` to `[]`, dropped
  annotations), not the parser itself. `McpServerTool.Create` remains the invoke
  path inside the ALC; its schema is not reachable from the host and is not a
  second source of truth worth reconciling.

#### A5. URI template matching and double indexes in `McpCatalogStore` - Low - V

- `UriMatches` builds a `Regex` from the template (`[^/]+?`) on each call. The
  SDK solves this inside `McpServerResourceCollection`; `UriTemplate` itself is
  internal, so the replacement is "use the SDK collection", not "call
  `UriTemplate`".
- Indexes by id and by name; `HasLoadedCatalog` / `IndexesMatchCatalog`
  revalidate consistency on every `EnsureLoaded`.
- The primitive id `CreatePrimitiveId(name, sourceAddress)` = `name_[address]`
  never reaches the wire. The host resolves by tool name; the id is used for
  call-recording and for the catalog diff in E4.

#### A6. Built-in tool/resource boilerplate - Low - V

- `IBuiltInMcpResource` is a hand-written parallel of `McpServerResource.Create`
  (template + `Resource` + read). `IBuiltInMcpTool.Name` duplicates
  `ServerTool.ProtocolTool.Name`.
- `Revit/Resources/RevitCSharpCheatsheet`, `RevitPythonCheatsheet`,
  `Acad/Resources/AcadCSharpCheatsheet`, `AcadPythonCheatsheet`: four
  near-identical embedded-markdown loaders (`GetManifestResourceNames().EndsWith`).
- `Revit` and `Acad` `ViewScreenshotTool` / `NavigateHistoryTool` are
  near-duplicates.

#### A7. Daemon option plumbing emulates `AddMcpServer()` - Low - V

- `Mcp.Server/Hosting/McpEngine`, `McpServerFactory.CreateOptions`,
  `McpServerConfigurator.Apply` iterate `IConfigureOptions<McpServerOptions>` by
  hand to pick up `WithTasks`, then attach `McpLogFilters`. The host-side
  `Catalog/McpCatalogExtensions.AddMcp()` registers the Tasks store and
  selector, and is consumed by the Daemon.

#### A8. Python scripts use private SDK internals - Med - V

Verified against the Python SDK 2.2.0 checkout on 2026-10-02
(`src/mcp/client/client.py`). `ToolInvoke.py` (~267 lines) is a hand-written
client where the SDK ships a public one.

- `__resolve_lowlevel_server` reads `MCPServer._lowlevel_server` (private) and
  `get_request_handler("tools/call")`, then calls the raw handler with a
  `ServerRequestContext(session=None, lifespan_context={}, ...)` built in
  `__make_lowlevel_context`. Any tool that touches `ctx.session` — progress,
  elicitation, logging — fails, because there is no session.
- `__fallback_read_resource_helper` re-implements `MCPServer._handle_read_resource`
  for the case where that handler is not registered: a copy of SDK code inside
  the repo, which drifts on the next SDK change.
- Two parallel paths do one job. `MCPServer` goes through `server.call_tool`;
  `LowLevelServer` goes through the raw handler; the MRTR branch drops back to
  the raw handler for both.
- `__build_call_tool_params` probes both casings (`inputResponses` /
  `input_responses`, `requestState` / `request_state`) and, when it finds
  either, treats the payload as an envelope rather than arguments.
- **SDK equivalent, confirmed in the docstring:** `mcp.client.Client` takes a
  `Server` or `MCPServer` directly and connects in-process, dispatching without
  JSON-RPC framing. `Client.call_tool(name, arguments, input_responses=...,
  request_state=..., meta=...)` and `read_resource` are public and accept both
  server kinds, so the private access, the fallback copy, the dual path, and
  the key probing all have a replacement.
- **What stays either way:** the C# side still serializes `CallToolRequestParams`
  to JSON and reads `CallToolResult` back (`PythonSource.WriteRequest` /
  `ReadToolResult`). pythonnet does not share types with the C# SDK, so that
  JSON boundary is real. Everything inside `ToolInvoke.py` above it is not.

#### A9. Server-only types in shared layers - Low - V

- `Core/McpTaskExecutionMeta.cs` (uses `ModelContextProtocol.Extensions.Tasks`,
  `RequestContext<CallToolRequestParams>`) lives in the contracts project that
  the host add-in references. The add-in carries the Tasks extension for a
  `const string`.
- `Core/Sessions/*` (`IHostBroker`, `IConnectedHostCatalog`, `HostKey`, ...) is
  daemon-only but sits next to host contracts.

#### A10. WebSocket stream adapters - Low - V

- `Daemon/Gateway/GatewayNdjsonStreams.cs` converts WebSocket messages to
  newline streams so `StreamServerTransport` can be reused. The SDK has no
  WebSocket transport, so this is acceptable. A message-oriented `ITransport`
  would remove the manual `\n` / `\r` padding. Keep unless touched.

### B. Redundant or obsolete workarounds

#### B1. `McpClientPassthrough` reflects into SDK private members - Med - V

- `Client/McpClientPassthrough.cs` reads `McpClientImpl._sessionHandler`
  (non-public field) and invokes `SendRequestAsync` by reflection to send
  `tools/call` without MRTR auto-retry. It also needs
  `McpProtocol.EnsureCurrentProtocolMeta` because it bypasses the SDK's meta
  injection (protocol version, client info, capabilities).
- Pinned to 2.2.0 private names. Python SDK has a public equivalent; C# SDK
  does not, so the gap is real.
- It drags in `InvokeDynamicMrtrState`, `HostToolCallOutcome`,
  `ToolsetMrtrBridge`, and the `InputRequired` meta key.
- 0027 rule 4 calls MRTR "plumbing, not a product workflow", yet it shapes
  four types and a reflection hack.

#### B2. `DynamicToolResults` manual structured content - Med - V

- `Server/Contracts/DynamicToolResults.cs` serializes the payload twice (text
  JSON and `StructuredContent`) instead of `UseStructuredContent = true`.
  Reason recorded in 0027: inferred schema from `JsonElement` made Cursor drop
  `tools/list`.
- `McpServerToolCreateOptions.OutputSchema` allows an explicit schema, which is
  the unblocker 0027 names. **R:** confirm with Cursor.

#### B3. Tasks are advertised but unreachable on host tools - Med - V

- `CSharpCodeTool` and `PythonCodeTool` carry
  `[McpMeta(McpTaskExecutionMeta.MetaKey, Mode.Optional)]`.
- `McpTaskExecutionMeta.SelectForRequest` reads `ToolCollection` of the
  **Daemon** server, which holds only fixed synchronous tools. Host tools run
  behind `invoke_dynamic` (a synchronous Daemon tool), so the host meta is never
  read.
- [`docs/product/mcp.md`](../product/mcp.md) and
  [`sdk-gap-matrix.md`](../architecture/MCP/sdk-gap-matrix.md) state
  `execute_*` tools are "Optional task-capable on the host". 0027 rule 4 lists
  "MCP Tasks on `search_dynamic` / `invoke_dynamic`" as out of scope. Docs and
  ADR contradict each other, and code behavior follows the ADR.
- The `McpTaskExecutionMeta` class doc invites toolset authors to copy it, which
  teaches an inert attribute.
- **Amended 2026-10-02 (owner review):** the diagnosis stands (the wiring is
  inert) and the remedy is **rewire, not remove**. The acceptance bar is the
  SDK's own task flow, not any one client. See decision 7.

#### B4. Python module reload per call and duplicated parse - Med - V

- `__load_module` in `ToolInvoke.py` uses a fresh module name
  `rdt_invoke_{uuid}` and `exec_module` on every call, so each `invoke_dynamic`
  that reaches a Python tool re-parses the file and rebuilds every tool's
  pydantic model. Cost grows with the whole toolset, not with the tool called.
  `ToolParser.py` loads the same file a second time at discovery.
- `PythonSource.ClearCaches()` is an empty method because nothing is
  cached, so there is nowhere for a cache invalidation to land.
- The payload shape is polymorphic: a plain argument dict without MRTR,
  `{arguments, inputResponses, requestState}` with MRTR. A tool parameter named
  `inputResponses` or `requestState` is misread as the envelope.
- A `Client` built once per file (key: path plus mtime) removes the reload; the
  named `call_tool` parameters remove the polymorphic payload. See decision 6.

#### B5. Compensation for the second content model - Low - V

- `InvocationResponseEncoder.PrepareForWire` fills empty text with a 240-char
  preview of structured content.
- `ToolsetResultSerializer.SerializeRuntime` serializes then deserializes to
  cross ALC identity; `McpSpecKeys.ToolResult.ContentPascal = "Content"` tolerates
  PascalCase from foreign serializers.
- All three exist because of A2; they disappear with it.

#### B6. Destructive config pruning - Med - V

- `McpPathValidator.PruneInvalidConfiguredPaths` removes configured paths
  that produced no primitives in the last load. A transient failure (locked
  DLL, Python not initialized, ALC error) drops the user's paths from the
  in-memory list. A later `SaveSettings` persists that list.
- **Sharpened 2026-10-03.** `McpPipeServer.StartAsync` calls `EnsureLoaded`
  on the thread pool and does not wait for Python.
  `PythonMcpRegistryProvider` returns an empty catalog when
  `PythonInitializer.IsInitialized` is false, and the prune then removes
  `PythonToolsetPaths`. `HostBackgroundController` used to call `ReloadAsync`
  after both Python inits. That call is removed: built-in tools and .NET
  toolsets do not need it, and `StartAsync` must return without awaiting env
  setup (`docs/architecture/Execution/python-runtime.md`). With the reload
  gone, a cold start that has Python toolset paths configured loses those
  paths whenever `EnsureLoaded` wins the race, which is the usual order.
  Built-in tools and .NET DLLs still appear, so the failure is easy to miss.
- **Fix:** the eager `EnsureLoaded` does not prune Python paths while Python
  is not initialized. A load that runs after Python is ready still prunes a
  path that produced no items. Do not put `ReloadAsync` back on the controller.

#### B7. Three independent pipe scans - Low - V

- `HostBroker.RunAsync` scans every 2 s: `Directory.GetFiles(@"\\.\pipe\")`,
  regex, and `Process.GetProcessById` per match.
  `list_host_instances` and the Gateway register/heartbeat call
  `IMcpPipeScanner.Discover()` again on their own schedule.
- Named pipes have no watcher, so polling stays; the scan result should be
  shared and cached, not recomputed by each caller.

#### B8. Eager list refresh and wrapper allocations - Low - V

- `HostBroker.RefreshCatalogAsync` issues three sequential list calls under a
  gate on every `list_changed`, regardless of which list changed.
- `HostSession` uses `ListToolsAsync` (creates `McpClientTool` wrappers) and
  then unwraps `.ProtocolTool`; the typed request returns the protocol result
  with TTL directly.

#### B9. Blocking and O(n) checks on the hot path - Low - V

- `McpCatalogStore.EnsureLoaded` uses `_gate.Wait()` (synchronous) and
  re-verifies index consistency in `tools/list`, `tools/call`, and
  `resources/read`.

#### B10. Implicit duplicate-name policy - Low - V (policy), R (exact outcome)

- `McpCatalogLoader` drops a tool whose name was already seen, in provider
  order, and tracks `_knownToolIds` for log counts. The rule "first provider
  wins, silently" is not documented. Provider order decides which toolset a
  user sees.

### C. Search (`ConnectedHostCatalog.Search`, `search_dynamic`)

Performance is **not** a problem at current catalog sizes. The problems are
recall, ranking, and result weight.

#### C1. Per-field AND matching causes false zero-hit results - High - V

`RankMatch` requires **all** query tokens in the **same** single field
(target, then name, then description). A query `delete wall elements` does not
match a tool whose target has `delete`/`elements` and whose description has
`wall` but not all three tokens. There is no cross-field matching, no weighting,
no partial-match threshold, no stemming, no synonyms.

#### C2. Rank 6 is unreachable - Low - V

Rank 6 ("whole phrase contained in a field") can only hold when every token is
contained in that field, so ranks 3, 4, and 5 have already matched.

#### C3. Searchable surface is too small - Med - V

Not indexed: tool `Title`, annotation titles, `_meta`, input-schema parameter
names, camelCase boundaries, URI separators (`://`, `/`, `{}`). `Normalize`
only replaces `_` and `-` and splits on spaces.

#### C4. `machineId` is accepted by the catalog API but ignored by the tool - Low - V

`SearchDynamicTool.Search` does not pass the machine filter.

**Decided 2026-10-03:** do not add the filter. One Daemon tracks the processes
on its own machine. A client selects the machine by which Daemon it connects
to (`list_machines`). `capabilityId` does not carry `machineId`. See decision 9.

#### C5. Heavy and duplicated result payload - Med - V (structure), R (token count)

- `capabilityId` = `dci1.` + base64url JSON (machine GUID, PID, kind, target,
  catalog-version GUID, SHA256 fingerprint) is roughly 250-300 characters per
  hit. Base64 tokenizes poorly; twelve hits cost an estimated 1-1.5k tokens.
- `machineId` is repeated per hit in addition to being inside the id.
- The same payload is returned as a text block and as `StructuredContent`.

#### C6. Capability ids are invalidated on every host refresh - High - V

`CatalogVersionFor(entry)` attaches a new GUID to each `HostCatalogEntry`
object through `ConditionalWeakTable`. Each host `list_changed` replaces the
entry, so every outstanding `capabilityId` fails with `host_catalog_changed`
even when the tool's fingerprint is unchanged. The agent pays an extra
search-then-invoke round trip after any unrelated catalog change.

#### C7. Small avoidable work - Low - V

- `Catalog.List()` allocates and sorts on every call; `ToItem` calls
  `List().First(...)` once per hit.
- `Normalize` (Replace, Split, ToLowerInvariant, Join) runs on target, name and
  description for every hit on every query.
- SHA256 of the schema text is recomputed per hit and again per invoke.
- `Resolve` is a linear scan over all entries and kinds.

#### C8. Empty results give no guidance - Low - V

A zero-hit response does not list the names available on the host, so an agent
cannot recover without guessing a better query.

### D. Vocabulary

#### D1. One concept, many names

| Concept | Names in use |
|---------|--------------|
| A running CAD process | host, instance, host instance, session, bridge (`bridgeConnected`), PID; wire `hostInstanceId` actually holds `ProcessId`; `processId` elsewhere; `InstanceInfo`, `ConnectedInstanceEntry`, `HostKey` |
| A machine | `machineId`, `DeviceMetadata`, "device" in docs, `machine_id` on the Gateway |
| A catalog entry | tool/resource/template (spec), primitive (host), capability (daemon), descriptor, `Target` (tool name **or** resource URI) |
| Entry kind | wire `tool` / `resource` / `resource_template`; enum `HostCatalogKind.Tool/Resource/ResourceTemplate`; `ExecutionMode`; `SourceKind` |
| The set of entries | Registry, Catalog, Store, Loader, Provider: `McpRegistryCatalog`, `McpCatalogStore`, `IMcpCatalogLoader`, `IMcpRegistryProvider`, `ConnectedHostCatalog`, `McpRegistryConfig.json`, `__mcp_registry__.py` |
| Source of a tool | `ExecutionMode.CSharp` (really **built-in**), `ExecutionMode.Dotnet` (ALC toolset), "ad-hoc C#" in `mcp-dispatch.md` |
| "Dynamic" | `search_dynamic` / `invoke_dynamic` cover **all** host capabilities including built-ins; docs call user toolsets "Dynamic Tools (User-Registered)". **Decided 2026-10-03:** new types do not use the prefix. The id type is `CapabilityId`. `InvokeDynamicTool` becomes `InvokeTool`. `DynamicContracts` and `DynamicToolResults` disappear with their files. The wire tool names stay |

#### D2. Names that mean something else in the SDK or another layer

- `Core.Results.McpErrorCode` (strings `validation.failed`, `execution.failed`,
  `execution.cancelled`) collides with `ModelContextProtocol.McpErrorCode`
  (JSON-RPC integers); call sites need aliases.
- `ValidationProblem` is defined twice: Core `(Property, Message)` and Server
  `(Name, Message)`.
- `McpTaskExecutionMeta` also hosts `Invocation.InputRequired`, an MRTR key.
- Gateway `host_apps` holds **pipe names** (for example
  `DevToolsMcp_Revit_2025_1234`), not host app names.
- `LaunchHostResult.Version` vs `ConnectedInstanceEntry.VersionNumber` vs tool
  descriptions "version".
- `launch_host` text lists Navisworks as supported; other docs
  (`RevitDevTool.PyTest` AGENTS.md) call it a registry stub.

#### D3. Inconsistent error and naming conventions

- Three error styles: `validation_error`, `stale_capability`,
  `invocation_canceled` (snake_case, one `l`); `validation.failed`,
  `execution.cancelled` (dotted, two `l`); host tags `[COMPILATION ERROR]`.
- JSON casing: camelCase (`capabilityId`, `hostApp`) vs snake_case
  (`back_remaining`, `forward_available` from `navigate_history`; Gateway
  `machine_id`, `host_apps`).
- Tool names: `view_screenshot`, `navigate_history`, `execute_csharp_code`
  unprefixed; `revit_find_elements` prefixed. No stated rule.

#### D4. Documentation drift that misleads agents - High for agent-facing pages - V

| Document | Says | Code |
|----------|------|------|
| `architecture/MCP/workflows.md` | `invoke_dynamic` with `kind=` / `target=`; `hostInstanceId` inside `arguments`; "always pass `hostInstanceId`" | `invoke_dynamic` takes `capabilityId` (id encodes the host) |
| `architecture/MCP/tools.md` | `DotnetMcpAssemblyParser`, `[McpTool]`, `McpTaskExecutionSelector.Select`, "`DescriptorFactory` + wire list" for built-ins | `McpAssemblyParser`, `[McpServerTool]`, `McpTaskExecutionMeta.SelectForRequest`, descriptor from `ServerTool.ProtocolTool` |
| `architecture/MCP/README.md` | Shared features "call-log filters" on the host wire | `McpLogFilters` is used only by the Daemon (`Mcp.Server`) |
| `architecture/Execution/mcp-dispatch.md` | `ExecutionMode.CSharp` = "ad-hoc C#, rare catalog path" | `ExecutionMode.CSharp` is the built-in path |
| `platform-boundaries.md`, `sdk-gap-matrix.md` | `ReturnMapper`, ALC `AIFunction` invoker | neither exists |
| `product/mcp.md`, `sdk-gap-matrix.md` | Tasks Optional on host `execute_*` | unreachable (B3); 0027 says out of scope |
| `Resources/scripts/ToolParser.py` docstring | `mcp == 2.0.0` | pixi pin `>=2.1.1,<3`; audited 2.2.0 |

**Agent-facing drift (D4):** rows above are the **2026-10-02 audit**. Architecture
`MCP/*`, `Execution/mcp-dispatch.md`, `product/mcp.md`, and `sdk-gap-matrix.md`
were aligned to code on **2026-10-03**; use [**As implemented**](#as-implemented-code-truth-2026-10-03) and
[`architecture/MCP/README.md`](../architecture/MCP/README.md) for current behavior.

### E. Defects found while reading the flow

#### E1. Host list results omit `ttlMs` / `cacheScope` - High - V

`McpHandler` builds `tools/list`, `resources/list`, and
`resources/templates/list` results without `TimeToLive` / `CacheScope`. These
are protocol fields from revision `2026-07-28` (SEP-2549), not SDK inventions.
On the wire they are `ttlMs` (milliseconds, like HTTP `Cache-Control: max-age`;
`0` means immediately stale) and `cacheScope` (`public` or `private`, like
`Cache-Control: public/private`). A missing `ttlMs` means immediately stale; a
missing `cacheScope` means `public`. They are hints: a `list_changed`
notification invalidates a cached response regardless of remaining TTL.

`McpServerImpl` stamps both automatically, but only on a connection negotiated
to `2026-07-28` or later (older revisions reject the keys as unrecognized,
SDK issue #1721). When a handler leaves them unset it fills the conservative
defaults `ttlMs = 0` and `cacheScope = "private"`. `McpHandler` serializes its
own `ListToolsResult` / `ListResourcesResult` and never passes through
`McpServer`, so nothing stamps them. The one exception is `server/discover`,
where `McpHandler` already writes `cacheScope: private` by hand.

The receiver is `HostSession`, a conforming SDK client, so it treats every host
list as immediately stale and re-fetches it. `search_dynamic` therefore asks
the host again instead of using a cached catalog.

**Correction (decided 2026-10-02).** Two parts:

1. Stamp the fields, so the host matches what the SDK server emits. Set
   `TimeToLive` and `CacheScope` on the three list results. Use `Private` for
   `resources/list`, whose contents vary per open model.
2. Give `tools/list` and `resources/templates/list` a non-zero `ttlMs`. The
   host catalog changes only when a toolset is loaded or unloaded, and that
   path already emits `list_changed`, which invalidates the cache. A positive
   TTL lets the Daemon keep the list between changes instead of re-reading it
   on every search. The value is a freshness hint, so a too-large number only
   costs staleness until the next notification, not correctness.

`resources/read` carries the same two fields and is read live from the model,
so it stays at `ttlMs = 0`.

#### E2. `server/discover` places `serverInfo` in the body - Med - R

The hand-built `JsonObject` carries `serverInfo` at the top level. SDK 2.2.0
servers put it under `_meta` (`io.modelcontextprotocol/serverInfo`). A 2.2.0
client may ignore the body field.

#### E3. No cancellation and serial request handling per session - High - V

- `McpHandler` returns `null` for every unrecognized notification, including
  `notifications/cancelled`.
- `McpPipeSession.ReadLoopAsync` awaits each request before reading the next
  line, so a long `execute_csharp_code` blocks `ping` and cancel on that
  session. The SDK session handler runs requests concurrently and cancels them
  by request id.

#### E4. Catalog change detection compares ids only - High - V

`McpCatalogStore.TryApplyCatalog` returns early when `CatalogIdsMatch` is true.
Id is `name_[address]`. Editing a tool's description, schema, or annotations
without renaming it or moving its file produces no catalog change, no
`list_changed`, and no Daemon refresh until restart.

**Fix, with the host `McpServer` step (flow doc section 9, step 3):** compare
`Catalog.ContentHash()` over name, description, schema, and annotations of all
three collections. Emit `list_changed` when the hash changes. Do not ship a
non-zero `ttlMs` while this comparison is still `name_[address]`: the Daemon
would keep the old schema until the TTL expires.

#### E5. Broker may never reconnect to a live host - High - R

`HostBroker.SyncPipesAsync` keeps `knownPipes` as a local set. On session
`Disconnected`, `DisconnectAsync(pipeName)` removes the session and catalog
entry but does not remove the name from `knownPipes`. If the transport breaks
while the host process (and pipe) remain, the pipe is still "known" and is not
reconnected until the pipe name disappears.

**Fix in place, before the Daemon project merge** (flow doc section 9). Step 5
deletes `HostBroker`. Remove the pipe name from the remembered set when the
session drops and the process is still alive, then connect again.

#### E6. Unstable machine id fallback - Low - V

`DeviceMetadata` falls back to a random `Guid` when reading the registry
`MachineGuid` fails. The id then changes across Daemon restarts and invalidates
every `capabilityId`.

#### E7. Resource leak on failed connect - Low - V

`HostSession.ConnectAsync` does not dispose the pipe client if
`McpClient.CreateAsync` throws.

#### E8. `invoke_dynamic` drops `Meta` / `progressToken` - Low - V

`InvokeDynamicTool` builds `CallToolRequestParams` from name, arguments,
`InputResponses`, and `RequestState` only. Consistent with the 0027 non-goal but
not stated in the tool description.

## Decision

The decisions are directions and rules. The execution plan that implemented them is
[`2026-10-03-mcp-flow-simplification`](../plans/completed/2026-10-03-mcp-flow-simplification.md).

### 1. SDK-first rule (adopt now)

New MCP wire, transport, descriptor, schema, or client/server code **uses the SDK
public surface**. A custom layer or a reflection hack is allowed only when its
source file names (a) the SDK member it replaces and (b) the unblocker that
removes it. `McpClientPassthrough` (B1) and the foreign-identity JSON reader
(A2) are the two current exemptions and keep their documented unblockers.

### 2. Fix defects now, independent of layer direction **[FIX]**

E1-E8 are defects. E1, E2, and E3 fold into the host `McpServer` replacement
(decision 3, flow doc step 3). Do not patch `McpHandler` to stamp `ttlMs` and
then delete that handler in the same cycle. E4 lands in that same step, as
`Catalog.ContentHash()`, because a non-zero TTL without a real change
notification serves a stale schema. E5 is fixed on the current `HostBroker`
before step 5 deletes it. B6 is fixed on the current `EnsureLoaded`: the eager
load does not prune Python paths while Python is not initialized. E6 and E7
are fixed in place with the Daemon session code they sit on.

### 3. Host wire becomes an SDK `McpServer` **[GATE]**

Direction: replace `McpHandler` / `McpPipeSession` / `McpJsonRpc` /
`McpSpecKeys` with `McpServer.Create` + `StreamServerTransport` per pipe
connection. The pipe types are `PipeServer` and `PipeEndpoint` in
`DevTools.Mcp.Catalog/Transport`. They do not move into `DevTools.Execution`.
`Execution/External/Mcp` backends become Catalog sources (`BuiltIn/`, `Dotnet/`,
`Python/`) and that Execution folder is deleted. `DevTools.Mcp.Revit` and
`DevTools.Mcp.Acad` stay their own projects because they reference host APIs.
If accepted, this amends 0027 rule 3 and Alternative 1. The
rejection rationale there ("client never talks to that pipe; catalog and
main-thread marshalling stay") is accurate but does not address E1-E3, the
existing in-host `McpServer`, or the removal of ~1k lines. Main-thread marshalling
stays in the tool handler.

Exit gate: pipe conformance on net48 and net10
(`docs/agents/mcp-integration-test.md`) and an ILRepack identity check.
The S1/S2 packaging choice is not a blocker. A4 and `ResultBridge` keep the
metadata parser and one JSON crossing. Do not un-merge `ModelContextProtocol`
from the add-in.

### 4. One host result type

Adopt `CallToolResult` as the host-internal result. Keep exactly one
foreign-identity-to-`CallToolResult` function. The target name is `ResultBridge`
in `DevTools.Mcp.Catalog` (flow doc section 8.3). It reads JSON from the
toolset ALC and returns the host `CallToolResult`. It is not a second content
model, and it does not read a schema from `McpServerTool.Create`. Remove
`McpInvocationResponse`, `McpContent*`, `InvocationResponseEncoder`,
`SdkInvocationMapper`, `HostToolResultJson` once decision 3 allows it;
until then do not add new consumers of the second model.

### 5. Delete dead code now; keep the MRTR send exemption

Remove A3 items, the `Invocation.InputRequired` meta key and its branch, the
CliWrap parser path, and the stale API names in docs (D4). Keep tests only when
they cover production behavior. This is flow doc step 1.

Do **not** remove `McpClientPassthrough` in that step. `McpClient.CallToolAsync`
is public and still auto-retries MRTR. SDK 2.2.0 has no public send that turns
the retry off (B1). Decision 1 keeps the passthrough as an exemption until that
send exists. The dead MRTR types around it do go: `InvokeDynamicMrtrState`,
`InvokeSingleOutcome`, `HostToolCallOutcome`, `ToolsetMrtrBridge`,
`McpInvocationResponse.InputRequired`, the Python MRTR key probing, and the MRTR
demo toolset. No built-in tool or product flow uses elicitation (verified by
search of `source/` and `samples/`). A host tool that throws reaches the agent
as `invocation_failed` with the message. If a future product flow needs
elicitation, it gets its own ADR and uses the SDK handler API.

### 6. Python: public SDK surface and caching

Replace the body of `ToolInvoke.py` with the SDK's in-process client
(verified against Python SDK 2.2.0, `src/mcp/client/client.py`):

```python
async with Client(server) as client:
    result = await client.call_tool(name, arguments, input_responses=..., request_state=...)
```

`Client` accepts both `MCPServer` and low-level `Server`, so the `isinstance`
branching, the `_lowlevel_server` access, the `session=None` context, and the
`_handle_read_resource` fallback all go. `read_resource` replaces
`__invoke_read_resource`.

Rules:

1. Build the `Client` once per toolset file and reuse it. The cache key is the
   file path plus its mtime, held on the C# side so `ClearCaches()` actually
   clears something. A changed file loads again; an unchanged file never
   re-executes.
2. Pass arguments and the MRTR fields as separate named parameters, never as one
   polymorphic JSON object. This removes the `inputResponses` / `requestState`
   key probing and the bug where a tool argument of either name is read as the
   envelope.
3. Keep the JSON boundary in `PythonSource`: `WriteRequest` out,
   `ReadToolResult` back. That boundary is the ALC-equivalent for Python and is
   not part of what this decision removes.
4. One `anyio` loop per cached client instead of `anyio.run` per call.

Resolves A8 and B4 in the same change.

`ToolInvoke.py` is not a child process. `PythonSource.InvokeToolAsync`
passes `scope.Exec(ToolInvokeScript)` to `IHostContextExecutor.ExecuteAsync`,
so the script runs inside the host process on the Revit main thread. The only
child-process path is `PythonToolsetParser.RunParserProcess` (CliWrap), and
that path has no production caller (A3, decision 5).

Caching the `Client` removes the per-call `exec_module`. It does not take the
tool body off the main thread. Building the cached client does not call the
Revit API, so it runs before `ExecuteAsync`. The tool body stays inside
`ExecuteAsync` when it calls the host API.

`DotnetSource` blocks that same thread with
`task.GetAwaiter().GetResult()` at both invoke sites. The replacement awaits
the toolset task on the host executor. That change ships with the single
result function in decision 4 (`ResultBridge`), not as a new project.

Proof is a Python toolset test that
calls the same tool twice and asserts the module loads once, plus a tool that
reads `ctx.session` and returns instead of throwing.

### 7. Tasks: follow the SDK task flow exactly (rewritten 2026-10-02)

The acceptance bar is the C# SDK's own task flow, not the behavior of any one
consumer. Whether Cursor (or any other client) opts into tasks is outside this
decision: a client that does not opt in gets the ordinary synchronous result,
which is what the spec requires, and the feature is still correct.

**Source of truth** (SDK 2.2.0, local checkout under `.opensrc`):

- Spec: `docs/concepts/tasks/tasks.md` in
  `modelcontextprotocol/csharp-sdk`, which follows SEP-2663.
- Concrete sample: `samples/TasksExtension/Program.cs`. A server calls
  `AddMcpServer().WithTools(...).WithTasks(store)`; a client calls
  `CallToolAsTaskAsync`, then polls `GetTaskAsync` until the status is
  `Completed`, `Failed`, or `Cancelled`.

**How the SDK flow actually works** (from
`Server/McpTasksBuilderExtensions.cs`, not from docs alone):

1. `WithTasks(store, configure)` registers one `IConfigureOptions<McpServerOptions>`.
   It advertises `io.modelcontextprotocol/tasks` in `ServerCapabilities.Extensions`
   and adds the `tasks/get`, `tasks/update`, and `tasks/cancel` request handlers.
2. It inserts one alternate-result filter
   (`Filters.Request.CallToolWithAlternateFilters`). That filter runs **before**
   the tool body. Filters registered before it run before task creation; filters
   registered after it, and all ordinary call-tool filters, run in the background
   before the tool.
3. Per call, `McpTasksOptions.ExecutionModeSelector` returns a
   `McpTaskExecutionMode`. The default selector returns `Optional` for every
   tool. `Synchronous` skips the task path. `Required` rejects a call that lacks
   the opt-in with `MissingRequiredClientCapabilityException`.
4. A task is created only when both are true: the negotiated protocol is
   `2026-07-28` or later, and the request opts in. The opt-in is either
   `ClientCapabilities.Extensions["io.modelcontextprotocol/tasks"]` or the same
   key under `_meta["io.modelcontextprotocol/clientCapabilities"].extensions`.
   The server must never return `CreateTaskResult` without that opt-in, and the
   SDK enforces it.
5. On opt-in the filter calls `store.CreateTaskAsync` **eagerly**, returns
   `CreateTaskResult` immediately (`resultType = "task"`), and runs the tool
   pipeline on `Task.Run` with its own DI scope. There is no lazy or
   mid-execution promotion to a task.
6. The background pipeline records the outcome: a `CallToolResult` (including
   `IsError = true`) becomes `Completed`; an `McpProtocolException` becomes
   `Failed` with the JSON-RPC error; a plain exception is wrapped as
   `CallToolResult { IsError = true }` and becomes `Completed`; cancellation
   becomes `Cancelled`. `Failed` is reserved for protocol-level errors.
7. `tasks/cancel` is eventually consistent and cooperative. The handler calls
   `SetCancelledAsync` and signals the task's `CancellationTokenSource`. The
   notifications-cancelled mechanism is not used for task cancellation.

**What this repo must do to match that flow:**

1. Keep `ModelContextProtocol.Extensions.Tasks` only in the Daemon
   (`DevTools.Mcp.Server` / `DevTools.Daemon`). The host add-in and `Mcp.Core`
   do not reference it (A9). Remove the inert parts: `[McpMeta(tasks.executionMode)]`
   on host tools and the Core `McpTaskExecutionMeta` helper. Host tool metadata
   can never drive task selection, because the Daemon `ToolCollection` holds only
   the fixed tools.
2. Enable tasks with `AddMcpServer().WithTasks(store, options => ...)`, the same
   call the sample uses. Do not hand-roll `tasks/*` handlers or a second task
   result type.
3. The selector returns `Optional` for a single `invoke_dynamic` tool call and
   `Synchronous` for resource reads and `reads[]` batches. It decides from the
   tool name and the decoded request arguments. `search_dynamic` and the
   infrastructure tools stay `Synchronous`.
4. One `IMcpTaskStore` singleton shared by the stdio server and every Gateway
   tunnel server. `InMemoryMcpTaskStore` is the SDK's store for development and
   tests; set a finite `DefaultTimeToLive` (the SDK default is `null`, which
   keeps tasks forever) and a `DefaultPollIntervalMs`. A durable store is a
   separate decision.
5. The task's `CancellationToken` must reach the host call, so `tasks/cancel`
   stops work that has not entered a host transaction. A Revit transaction
   already running is not interrupted; that limit is documented in
   `product/mcp.md`.
6. `invoke_dynamic` result size limits (1 MiB default) still apply to the
   `CallToolResult` stored on the task.
7. 0027 rule 4 ("MCP Tasks on `search_dynamic` / `invoke_dynamic`" out of scope)
   is **amended**: tasks on `invoke_dynamic` are in scope; tasks on
   `search_dynamic` and the infrastructure tools stay out.

**Proof, before any code lands:**

A headless test that mirrors `samples/TasksExtension`: an in-process client
using `CallToolAsTaskAsync` against the Daemon server, then `GetTaskAsync` until
a terminal status. It must show (a) no opt-in returns the ordinary
`CallToolResult` and creates no task, (b) opt-in returns `CreateTaskResult`
within the budget and the polled result equals the host result, (c)
`tasks/cancel` ends `Cancelled` and the host token is signaled, (d) a tool
`IsError` ends `Completed`, an `McpProtocolException` ends `Failed`.

**Known SDK limits to respect** (from the same doc, "Known limitations"): no
server-push task status (clients poll only); `CreateTaskAsync` runs eagerly even
for fast tools; a tool cannot start synchronously and then become a task; the
v2 tasks extension needs protocol `2026-07-28` and has no bridge to the v1
experimental tasks.

### 8. Structured output via explicit `OutputSchema`

After a live Cursor check (R), replace `DynamicToolResults` with
`UseStructuredContent = true` and a hand-written `OutputSchema` for the six
Daemon tools. On failure, record the observed Cursor behavior in 0027 and keep
the manual path.

### 9. Search contract v2

Adopt, under a revision of the `search_dynamic` contract. This is the same
contract as flow doc sections 8.3.2 and 8.3.3.

- Tokenized in-memory index (`SearchIndex`) rebuilt when one process catalog
  changes. Tokens split on `_`, `-`, and camelCase boundaries. Indexed fields:
  target, name, title, description, parameter names.
- Score is a weighted token sum, not BM25 and not a rank ladder. Each token
  adds target 4, name 2, description 1. Divide by the token count. Drop a match
  below half the tokens. Remove rank 6.
- One Daemon, one machine. Do not add a `machineId` filter to search, and do
  not put `machineId` in `capabilityId`. `list_machines` is how a client
  selects a machine. Today's `HostKey` is `(MachineId, ProcessId)`; the target
  key is `processId` only.
- Capability id format:

  ```text
  dci2.{processId}.{kind}.{contentHash}.{target}
  ```

  `kind` is one character (`t` tool, `r` resource, `p` template).
  `contentHash` is the first 8 hex characters of SHA-256 over name,
  description, and schema. `target` stays readable. No base64, no JSON, no
  catalog-version GUID. Stale means the hash differs or the item is gone.
  The same content keeps the same id across a catalog reload.
- Result type is `Match(item, processId, score)`. One payload copy (structured
  or text), not both.
- Zero-hit responses list available names.

Wire fields are an agent-facing contract. Changing them needs updating
`docs/product/mcp.md` and the revit/acad prompts in the same change. That
change is flow doc step 5, together with the Daemon project merge, so the
agent-visible strings move once.

### 10. Canonical vocabulary

Adopt this glossary for new code and for renames that are internal only. Wire
names change only together with decision 9.

| Term | Use for | Replaces |
|------|---------|----------|
| **process** | one running Revit/AutoCAD process on this Daemon. Wire field `processId`. Types `ProcessSessions` (the directory) and `ProcessSession` (one connection) | host instance, host process, `hostInstanceId`, and the `Host` prefix on Daemon types (`HostBroker`, `HostSession`, `HostKey`) |
| **machine** | a computer running a Daemon; `machineId` on `list_machines` and the Gateway only | device. Not a field of `capabilityId` or of `ProcessSession` |
| **catalog** | every item of one process. Daemon type `ProcessCatalogs` (one catalog per `processId`). Host type `Catalog` | registry, store, loader, `ConnectedHostCatalog`, `McpCatalogStore` as target names |
| **item** | one tool, resource, or resource template. The word "entry" is not used | entry, primitive, descriptor |
| **source** | the producer of items: built-in, dotnet toolset, or python toolset. Tools and resources from the same file share one source | provider, registry provider |
| **kind** | `tool` / `resource` / `resourceTemplate` on an item | mixing `kind` with `SourceKind` or `ExecutionMode` |
| **built-in / dotnet toolset / python** | source words only. The enum stays `ExecutionMode.CSharp` / `.Dotnet` / `.Python` | do not rename the enum to `BuiltIn` or `Toolset`. The enum names a language runtime, not an MCP category |
| **capability** | an item as addressed from the Daemon. Type `CapabilityId`. `capabilityId` is the only wire word for the id. Identity of the item is `(processId, kind, name)`; the hash only checks freshness | primitive, descriptor, the `Dynamic` prefix (`DynamicCapabilityId`), and treating `capabilityId` as the tool's own identity |
| **pipe name** | `DevToolsMcp_*` string | `host_apps` field (Gateway) |

Rules:

- Rename Core `McpErrorCode` to a name that does not collide with the SDK (for
  example `DevToolsErrorCode`); merge the two `ValidationProblem` types.
- Spell `canceled` the same everywhere on the wire (decide once; the SDK uses
  American spelling in `OperationCanceledException`).
- Agent-facing JSON is camelCase. `navigate_history` moves to camelCase with the
  next tool revision. The Gateway wire is a separate protocol owned by
  `McpGateway` and keeps snake_case; that exception is written in
  `transport.md`.
- Built-in tool names follow one rule (prefixed `revit_` / `acad_` only when the
  host differs); record it in `docs/product/mcp.md`.
- Version fields: `versionNumber` on every wire type.
- Fix D4 documentation drift. Update one layer per fact
  ([AGENTS.md](../../AGENTS.md) doc update rule).

## Alternatives Considered

1. **Keep 0027 as written; only fix defects.** Rejected for the layers: E1-E3 and
   B1/B2 are symptoms of the same cause, and the ADR text already conflicts with
   `RequestFactory`. Accepted for E4-E8, which stand alone.
2. **Rewrite the whole host half at once.** Rejected. The order is section 9
   of the flow doc: dead code, Python cache, host `McpServer` (with E4),
   narrow the parser, Daemon merge plus search, Tasks last. E5 and B6 are
   in-place fixes before the step that deletes their files.
3. **Drop the foreign-identity bridge by forbidding private SDK copies.** Rejected
   here - it is the S1/S2 gate decision, owned by the S5 plan.
4. **Keep the opaque `capabilityId` as is and only add a content hash.** Rejected -
   it fixes C6 but keeps C5's weight. Decision 9 keeps opacity as a contract
   (clients still do not call host tool names) while shortening the id.
5. **Add BM25 or a vector index library.** Rejected - the catalog is small;
   an in-box weighted token score meets recall needs without a dependency.

## Consequences

Positive:

- One list of defects and layers that future work inherits; fewer ad hoc
  re-audits.
- Fewer lines to maintain: A1-A3, A8, B1-B5 together account for an estimated
  1.5-2k lines across C# and Python.
- Search recall and token cost improve without changing the envelope loop
  (search, `capabilityId`, invoke).
- Host protocol compliance (cache fields, cancel, concurrent requests) follows
  the SDK instead of a local copy.
- Vocabulary and docs stop contradicting the code.

Tradeoffs:

- Decision 3 reopens an ADR choice and touches net48, ILRepack, and
  main-thread rules; it carries the highest risk and the longest lead time.
- Decision 9 changes the agent-facing wire; prompts, cheat sheets, and docs must
  move with it, and older cached ids fail once.
- Renames (decision 10) are broad and mechanical; they touch tests and
  source-generated JSON contexts (0031).
- Tasks (decision 7) add a daemon-side store and background execution, matching
  the SDK's `WithTasks` filter. `InMemoryMcpTaskStore` loses running and finished
  tasks when the Daemon restarts; clients then see `tasks/get` fail and must
  re-invoke. A durable store is a separate decision, not part of this one. The
  feature is proven against the SDK sample flow, so its correctness does not
  depend on any particular client opting in.
- Estimates of lines and tokens are approximate.

## Follow-Up

- Implemented 2026-10-03. Record:
  [`2026-10-03-mcp-flow-simplification`](../plans/completed/2026-10-03-mcp-flow-simplification.md).
  The 2026-10-02 consolidation plan was deleted. Do not restore it. Landed order: (1) delete dead code, leaving `McpClientPassthrough`
  in place; (2) Python `Client` cache; (3) host `McpServer` together with
  `Catalog.ContentHash()` so E4 does not survive the TTL; (4) narrow the
  metadata parser and replace `DotnetSource`'s `GetResult` with an
  await on the host executor; (5) merge the Daemon-side projects and ship
  search contract v2 together; (6) Tasks via `WithTasks`, last. Before step 1,
  B6 on `EnsureLoaded` and E5 on the broker were fixed.
- B2 stays open until an explicit `OutputSchema` is checked against a live
  client. That check is evidence for decision 8. It is not an acceptance bar
  for Tasks (decision 7).
- Open an upstream issue on `modelcontextprotocol/csharp-sdk` for a public
  "do not auto-retry MRTR" send path, then remove B1. Until that issue is
  resolved, `McpClientPassthrough` stays.
- On acceptance of the doc updates that section 9 reaches: update the
  [index](README.md), the status of 0027 rule 3 and rule 5, and the MCP
  architecture docs listed in D4.

## References

- [0010](0010-daemon-sole-mcp-host.md), [0012](0012-host-mcp-spec-engine.md),
  [0019](0019-ilrepack-and-polyfill-isolated-alc.md),
  [0023](0023-shared-assembly-isolation-kernel.md),
  [0027](0027-mcp-product-surface.md), [0031](0031-daemon-json-source-gen.md)
- [S5 plan](../plans/completed/2026-09-03-mcp-layer-identity-s5.md)
- [`docs/architecture/MCP/`](../architecture/MCP/README.md),
  [`docs/product/mcp.md`](../product/mcp.md)
- Code: `source/DevTools.Mcp.{Core,Adapter,Catalog,Client,Server,Revit,Acad}`,
  `source/DevTools.Daemon`, `source/DevTools.Execution/External/Mcp`,
  `source/DevTools.Execution/Resources/scripts/{ToolInvoke,ToolParser}.py`
