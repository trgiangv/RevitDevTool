# Execution Plan: MCP Flow Simplification

Date: 2026-10-03

## Status

Completed 2026-10-03. Decision: [0039](../../decisions/0039-mcp-flow-audit-sdk-reuse-and-vocabulary.md).
The temporary flow note was deleted with this completion.

The plan `2026-10-02-mcp-flow-consolidation` was deleted. Do not restore it and do not
merge `feature/mcp-consolidation`.

## Outcome

After this plan lands, one `invoke_dynamic` call no longer crosses a hand-written
JSON-RPC stack, a second result model, or a per-call Python module reload.
The host pipe is an SDK `McpServer`. The Daemon addresses an item with
`CapabilityId`. Search ranks by a field score. Tasks follow the SDK
`WithTasks` flow and are proven by a headless test, not by any one client.

## Fences

These override any older sentence in 0039 or in the flow note.

1. **S1/S2 is not a blocker.** Decision 3 no longer waits on the S5 packaging
   gate. A4 keeps the metadata parser. `ResultBridge` is the one JSON crossing
   out of the toolset ALC. Do not un-merge `ModelContextProtocol` from the
   add-in. The exit gate for the host `McpServer` step is pipe conformance on
   net48 and net10, plus an ILRepack check.
2. **B2 is not a step.** `DynamicToolResults` stays until an explicit
   `OutputSchema` is checked against a live client (0039 decision 8). The flow
   note section 8.5 describes the state after that check. Tasks do not depend
   on it.
3. **`ttlMs` is 60000** for `tools/list` and `resources/templates/list`.
   `resources/list` is `cacheScope: private` and `ttlMs: 0`. `resources/read`
   stays `ttlMs: 0`. `list_changed` is what invalidates a catalog.
   Do not stamp a non-zero `ttlMs` before `Catalog.ContentHash()` emits
   `list_changed` on a description, schema, or annotation change (E4).

## Context

- Findings and target: [0039](../../decisions/0039-mcp-flow-audit-sdk-reuse-and-vocabulary.md).
- File layout and vocabulary: flow note sections 7 and 8.
- Proof: `.agents/skills/build/SKILL.md`, `docs/agents/test-matrix.md`,
  `docs/agents/mcp-integration-test.md`.
- SDK pins: C# `ModelContextProtocol` 2.2.0, Python `mcp` 2.2.0.
  Tasks sample: `samples/TasksExtension` in the local csharp-sdk checkout.
- Already in the working tree, do not redo: `HostBackgroundController` (Revit
  and AutoCAD) no longer calls `ReloadAsync` after Python init.
  `StartAsync` still returns without awaiting env setup.

## Scope

In scope:

- `source/DevTools.Mcp.Catalog`, `DevTools.Mcp.Adapter`, `DevTools.Mcp.Client`,
  `DevTools.Mcp.Server`, `DevTools.Mcp.Core`, `DevTools.Mcp.Revit`,
  `DevTools.Mcp.Acad`
- `source/DevTools.Daemon` MCP surface
- `source/DevTools.Execution/External/Mcp` and
  `Resources/scripts/ToolInvoke.py`, `ToolParser.py`
- Matching tests, `RevitDevTool.slnx`, and the MCP docs this sequence changes
- `docs/product/mcp.md` and the revit/acad prompts, in the search step only

Out of scope:

- B2 / `DynamicToolResults` / Cursor `OutputSchema`
- Deleting `McpClientPassthrough` (needs a public SDK send that does not
  auto-retry MRTR)
- `RevitDispatcher` and the single Revit `ExternalEvent`
- S1/S2 packaging, the McpGateway repo, the pytest pipe
- A durable task store. `InMemoryMcpTaskStore` loses tasks on Daemon restart.
- Renaming wire tools `search_dynamic` and `invoke_dynamic`

## Approach

Steps are ordered. Stop after any step; the tree must still build and the
tests for that step must pass. B6 and E5 touch different projects and may
land together before step 1. Do not run later steps in parallel with each
other.

Do not deploy. Host compiles use
`-p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false`.
Do not kill a running Revit or AutoCAD.

### Before step 1

**B6.** `McpPipeServer.StartAsync` calls `EnsureLoaded` on the thread pool
and does not wait for Python. `PythonMcpRegistryProvider` returns an empty
catalog when Python is not initialized, and `PruneInvalidConfiguredPaths`
then removes `PythonToolsetPaths` from the in-memory list. A later
`SaveSettings` would persist that list.

The eager load does not prune Python paths while
`PythonInitializer.IsInitialized` is false. A load after Python is ready still
prunes a path that produced no items. Do not put `ReloadAsync` back on
`HostBackgroundController`.

**E5.** `HostBroker` removes a pipe name from the remembered set when the
session drops and the process is still alive, then connects again. Fix it on
the current `HostBroker`. Step 5 deletes that type.

E6 (unstable machine-id fallback) and E7 (dispose the pipe client when
`McpClient.CreateAsync` throws) land with the Daemon session code in step 5,
not here.

### Step 1 — Delete code with no production caller

Delete `ToolsetInvoker`, `ToolsetArgumentBinder`, `ToolsetInvocationServices`
(including `ToolsetProgressReporter`'s blocking `GetResult`), the CliWrap
branch of `PythonToolsetParser`, and `McpTaskExecutionMeta` plus
`[McpMeta(tasks.executionMode)]` on `execute_*`. Delete tests that only call
that code. Keep tests that cover production behavior.

Keep `ToolsetMrtrBridge`. `McpPrimitiveDispatcher` and `HostToolResultJson`
use it for live input-required results. Move
`McpTaskExecutionMeta.Invocation.InputRequired` onto that bridge before
deleting the meta type. Remove `ExecutionModeSelector` from `AddMcp`. Keep
`WithTasks` and `ModelContextProtocol.Extensions.Tasks`.

Do not delete `McpClientPassthrough`.

### Step 2 — Python client cache

`ToolInvoke.py` runs in-process via `scope.Exec` on the host main thread. It is
not a child process. Replace its body with `mcp.client.Client(server)`.
Cache one client per path plus mtime on the C# side so `ClearCaches()` clears
it. Build the client before `IHostContextExecutor.ExecuteAsync`. The tool body
stays inside `ExecuteAsync` when it calls the host API.

Pass arguments and MRTR fields as named `call_tool` parameters. Keep the JSON
boundary in `PythonSource`.

### Step 3 — Host `McpServer`

Replace `McpHandler`, `McpPipeSession`, and `McpJsonRpc` with `PipeServer` and
`PipeEndpoint` in `DevTools.Mcp.Catalog/Transport`, on
`StreamServerTransport`. Do not move the pipe server into
`DevTools.Execution`.

Stamp `ttlMs` and `cacheScope` per the fence above, and ship
`Catalog.ContentHash()` in the same step. E1, E2, and E3 close here: concurrent
requests, `notifications/cancelled`, and cache fields.

Exit gate: existing pipe tests green on net48 and net10, plus the ILRepack
check in `docs/agents/mcp-integration-test.md`. Do not start step 4 until that
gate passes. If the SDK server cannot serve the pipe on net48, stop and record
the failure. Decision 3 then reverts to Proposed. Do not invent a second
JSON-RPC handler to paper over it.

### Step 4 — Narrow the .NET parser

Keep `MetadataLoadContext` and `FullName` attribute matching. Split
`McpAssemblyParser` into `SchemaReader` and `MethodLookup`. Narrow
`McpSchemaBuilder` to what attributes can express. Drop the depth cap, the
`object` to `[]` mapping, and dropped annotations. Do not read a schema from
`McpServerTool.Create`.

One JSON crossing, `ResultBridge`, returns the host `CallToolResult`.
Replace both `GetAwaiter().GetResult()` calls in `DotnetSource` with
an await on the host executor.

### Step 5 — Daemon projects and search

Move `DevTools.Mcp.Server` and `DevTools.Mcp.Client` into
`DevTools.Daemon/Mcp/`. Split `DevTools.Mcp.Core`: host contracts into
Catalog, Daemon session types into Daemon. `DevTools.Mcp.Revit` and
`DevTools.Mcp.Acad` stay separate projects.

Same change, because both edit agent-visible strings:

- `CapabilityId` format `dci2.{processId}.{kind}.{contentHash}.{target}`.
  No `machineId`, no base64, no catalog-version GUID. The C# type is
  `CapabilityId`. New types do not use the prefix `Dynamic`.
- `SearchIndex` score: each token adds target 4, name 2, description 1,
  divided by token count. Drop matches below half the tokens. Result type
  `Match`. No BM25.
- Wire tools stay `search_dynamic` and `invoke_dynamic`.
- `ExecutionMode` stays `CSharp`, `Dotnet`, `Python`.
- Update `docs/product/mcp.md` and the revit/acad prompts in this step.
  Fix the D4 doc drift that this step touches. One doc layer per fact.

Session types: `ProcessSessions` (the directory) and `ProcessSession` (one
connection). Key is `processId`. `list_machines` still carries `machineId`.

`ProcessSession` keeps sending through `McpClientPassthrough`.

### Step 6 — Tasks

`AddMcpServer().WithTasks(InMemoryMcpTaskStore, ...)` with
`TaskSelection`: `Optional` for a single `invoke_dynamic` tool call,
`Synchronous` for resource reads, `reads[]`, `search_dynamic`, and the
infrastructure tools. One store shared by stdio and every Gateway tunnel.
`DefaultTimeToLive` is 30 minutes. `DefaultPollIntervalMs` is 1000.

The task `CancellationToken` reaches the host call. A Revit transaction
already running is not interrupted.

Proof is a headless test mirroring `samples/TasksExtension`:
`CallToolAsTaskAsync`, then `GetTaskAsync`. Required cases: no opt-in returns
`CallToolResult` and creates no task; opt-in returns `CreateTaskResult` and
the polled result equals the tool result; `tasks/cancel` ends `Cancelled` and
signals the host token; `IsError` ends `Completed`; `McpProtocolException`
ends `Failed`. No live client opt-in is required.

## Risks And Recovery

- Step 3 can fail on net48 or ILRepack. Stop there. The earlier steps stay.
- Step 5 changes ids agents already hold. Old `dci1` ids fail once. Prompts
  move in the same step.
- Each step is one commit-sized slice when the owner asks for commits. Until
  then, leave the work in the working tree. Do not push unless asked.
- Recovery is `git checkout` of the files touched by the current step. Do not
  reset unrelated working-tree edits (the controller change and the auth
  browser test opener are outside this plan).

## Progress

- [x] B6 eager load does not prune Python paths (2026-10-03: EnsureLoaded skips Python prune; ReloadAsync still prunes. Catalog.Tests 154 passed, 9 skipped, 0 failed)
- [x] E5 broker reconnects a live pipe (2026-10-03: forget a pipe only after that session is removed. Client.Tests 22 passed)
- [x] Step 1 dead code deleted, passthrough kept (2026-10-03: invoker, CliWrap parser branch, and McpTaskExecutionMeta removed. ToolsetMrtrBridge and WithTasks kept. Core 57 passed, Catalog 130 passed / 11 skipped, Server 59 passed)
- [x] Step 2 Python client cache (2026-10-03: path+mtime cache, module load before ExecuteAsync, Client entered on the host thread because the loop is thread-affine. Stale mtime entries drop on the owner thread. Invoke tests 9 passed after that fix; full Python suite was 131 passed / 2 skipped)
- [x] Step 3 host `McpServer` and `ContentHash`, net48 and net10 gate (2026-10-03: PipeEndpoint on StreamServerTransport. ContentHash includes description, schema, and annotations. ttlMs 60000 / 0 as fenced. Adapter.Tests net10 20 passed / 1 skipped; net48 NamedPipe 2 passed; ILRepack Debug.Autodesk.2025 deploy off succeeded)
- [x] Step 4 parser narrowed, `ResultBridge`, no `GetResult` (2026-10-03: SchemaReader + MethodLookup; McpAssemblyParser is a test forwarder and is not in DI. ResultBridge returns host CallToolResult. Dotnet invoke awaits on the host executor. Catalog.Tests 135 passed / 11 skipped; Execution.Mcp 33; Pytest 84; Testing.Host 69)
- [x] Step 5 Daemon merge, `CapabilityId`, search score, prompts (2026-10-03: Daemon/Mcp, dci2, ProcessSessions. Search drops a query when fewer than half its tokens match, then divides field points by token count. Server 59, Client 23, Daemon 78, Core 56 passed. Host compile-only OK)
- [x] Step 6 Tasks headless proof (2026-10-03: TaskSelection.Select, one store, 30 min TTL, poll 1000 ms. Daemon task tests 10 passed; full Daemon.Tests 88 passed)
- [x] D4 docs touched by the landed steps updated; flow note deleted

## Decisions

- 2026-10-03: S1/S2 does not block step 3. JSON bridge stays.
- 2026-10-03: B2 is out of this plan.
- 2026-10-03: `ttlMs` 60000 on tools and resource templates; 0 on
  `resources/list` and `resources/read`.
- 2026-10-03: task store TTL 30 minutes, poll interval 1000 ms.

## Validation

- Focused proof: the test project that owns the step
  (`dotnet run --project tests/<Project>/<Project>.csproj`), after
  `dotnet build` of that project. Daemon.Tests may run; the auth browser
  tests do not launch a URL.
- Step 3: pipe conformance on net48 and net10, compile-only props, no deploy.
- Step 6: the headless task cases listed above.
- Repository-required checks: build skill for every touched csproj. Do not
  treat a diff as done.

## Result

Landed 2026-10-03 in the working tree. No commit was requested.

The host pipe is an SDK `McpServer` (`PipeEndpoint` on `StreamServerTransport`) in Catalog. `McpCatalogContentHash` emits `list_changed` when description, schema, or annotations change. Cache hints are `ttlMs` 60000 on `tools/list` and `resources/templates/list`, and 0 on `resources/list` (private) and `resources/read`. Python toolsets keep one in-process `Client` per path and mtime. Dotnet tools cross the ALC once through `ResultBridge` and await on the host executor. The Daemon owns sessions (`ProcessSessions` / `ProcessSession`), ids `dci2.{processId}.{kind}.{contentHash}.{target}`, and field-score search. Tasks use one `InMemoryMcpTaskStore` (30 minutes, poll 1000 ms). Only a single `invoke_dynamic` tool call is `Optional`.

Verified: Catalog, Client, Server, Core, Execution.Mcp, Pytest, Testing.Host, and Daemon tests for the steps above; net48 named-pipe connect; ILRepack `Debug.Autodesk.2025` with deploy off. Daemon task cases re-run: 10 passed.

Limitations:
- `DynamicToolResults` stays until B2 checks an explicit `OutputSchema` against a live client.
- `McpClientPassthrough` stays until the SDK exposes a send that does not auto-retry MRTR.
- `Execution/External/Mcp` was not folded into Catalog.
- The `McpProtocolException` → `Failed` case uses a test-only tool forced to `Optional`. Production tasks are the single `invoke_dynamic` tool call.
- If the machine-id file cannot be written, E6 still mints a new Guid for that process.
- Search rows are still named `HostCatalogHit`.

Follow-up: B2, the public SDK MRTR send, and a durable task store. Each needs its own decision.
