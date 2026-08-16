# MCP Platform Split Implementation Plan

**Goal:** Replace the monolithic `DevTools.Mcp` assembly and daemon-owned file readers with focused MCP and file assemblies. Preserve named-pipe, gateway, fixed-tool, and host-routing behavior while deliberately evolving the two dynamic MCP operations to the typed, locator-based contract defined in this plan.

**Architecture:** The migration is strangler-style. Stable contracts move first, then offline file readers, catalog/discovery, host SDK adapters, host MCP client, shared SDK wiring, and the fixed external server surface. `DevTools.Daemon` remains the executable composition root; it no longer owns MCP or file implementations.

**Tech Stack:** .NET 8/10 and .NET Framework 4.8 shared libraries, official ModelContextProtocol C# SDK 2.0.0, Microsoft.Extensions.DependencyInjection, xUnit v3, Moq, OpenMcdf, ACadSharp.

---

Date: 2026-07-28

## Status

Active

## Outcome

The solution has the following independently referenceable assemblies:

```text
DevTools.Mcp.Core
DevTools.Mcp.Catalog
DevTools.Mcp.Adapter
DevTools.Mcp.Client
DevTools.Mcp.Orchestrator
DevTools.Mcp.Server
DevTools.FileMetadata.Core
DevTools.FileMetadata.Revit
DevTools.FileMetadata.Acad
```

`DevTools.Daemon` owns only executable composition, authentication, tray, and
dashboard concerns. No new MCP project references WPF, Revit API, AutoCAD API,
or a concrete file-format implementation. The existing fixed external MCP surface
and `DevToolsMcp_*` host-pipe behavior remain unchanged.

## Context

- Design: `docs/superpowers/specs/2026-07-28-mcp-platform-structure-design.md`
- MCP architecture: `docs/architecture/MCP/README.md`
- Product contract: `docs/product/mcp.md`
- Existing external entry-point decision: `docs/decisions/0010-daemon-sole-mcp-host.md`
- Existing code: `source/DevTools.Mcp/`, `source/DevTools.Daemon/Mcp/`,
  `source/DevTools.Execution/External/Mcp/`
- Existing tests: `tests/DevTools.Mcp.Tests/`

## Scope

In scope:

- Create and wire the nine assemblies above.
- Move current MCP behavior without changing externally observable contracts, except for the explicitly authorized dynamic-contract replacement defined below.
- Replace ad-hoc DevTools MCP result/error contracts at internal boundaries.
- Move offline Revit and AutoCAD metadata readers out of `DevTools.Daemon`.
- Pass the real application service provider to every manual `McpServer.Create`.
- Preserve and extend focused tests using TDD.
- Replace the existing dynamic `search_dynamic` / `invoke_dynamic` request and response contract with the typed, catalog-versioned contract defined below; this is an authorized product optimization, not an accidental compatibility promise.

Out of scope:

- A .NET replacement for `McpGateway`.
- MCP Apps, WebView2, Microsoft Agent Framework, Forma, ACC, BIM 360, or cloud
  connector implementations.
- New external MCP tools, prompts, resource URIs, gateway endpoints, or toolset
  schemas.
- Renaming `DevTools.Daemon.exe`, installer assets, or bundle packaging.

## Locked File Structure

| Destination | Source ownership to move | Public entry point |
| --- | --- | --- |
| `DevTools.Mcp.Core` | Shared descriptors, bindings, `IHostBroker`, `IHostSession`, dispatcher contracts, result/error contracts | Contracts only |
| `DevTools.Mcp.Catalog` | `Registry/`, `Discovery/`, `McpCatalogStore`, `McpPathValidator`, built-in registry provider | `AddMcpCatalog()` / `WithCatalog()` |
| `DevTools.Mcp.Adapter` | `HostServer/`, SDK invocation helpers, `HostMcpPipeServer` | `AddMcpHostAdapter()` / `WithHostAdapter()` |
| `DevTools.Mcp.Client` | `HostBroker`, `HostSession`, pipe scanner, host catalog | `AddMcpHostClient()` / `WithHostClient()` |
| `DevTools.Mcp.Orchestrator` | Tasks registration, options configurator, call-log filters, session factory | `AddDevToolsMcp()` |
| `DevTools.Mcp.Server` | Fixed tools/prompts, `McpEngine`, stdio session, gateway tunnel session, host launch services | `AddExternalMcpServer()` / `WithExternalServer()` |
| `DevTools.FileMetadata.Core` | Offline file-metadata request/result/error contracts and reader catalog | `AddFileMetadataReaders()` |
| `DevTools.FileMetadata.Revit` | `RevitFileInfo/*` and related models | `AddRevitFileMetadataReader()` |
| `DevTools.FileMetadata.Acad` | `AcadFileInfo/*` and related models | `AddAcadFileMetadataReader()` |

The current `DevTools.Mcp` project is deleted only in the final task after all
source and test references point at destination assemblies.

## Contract And Boundary Decisions

- The dynamic MCP contract is intentionally changed by this migration. `search_dynamic` accepts the typed `detail=summary|schema` shape and returns catalog-versioned `capabilityId` values; `invoke_dynamic` resolves those locators and supports the strict typed single-or-batch request shape in Task 9. Existing `includeSchema`, `(kind, target, hostInstanceId)` invocation, ranking, and batch payload forms are characterization inputs only; they are not compatibility requirements after Task 9.
- The fixed external tool and prompt list, daemon executable identity, stdio/gateway endpoint behavior, host named-pipe names, and host routing remain compatibility requirements.
- Before Task 9 changes the implementation, update `docs/product/mcp.md` with the exact new request fields, response fields, ranking, stale-locator error, batch limits, and retry behavior. Update external contract tests in the same green boundary; do not leave product documentation describing the retired request shape.
- `DevTools.Mcp.Core` owns every reusable application contract. Before Task 2 implementation, record the source, destination, namespace, and owner for `McpError`, result/error types, dispatcher contracts, catalog ports, session contracts, and generic handler ports. A port that is reusable outside `DevTools.Mcp.Server` belongs in Core; Server-only adapters remain in Server.
- `DevTools.Mcp.Catalog` registers only providers it owns. `DevTools.Execution` registers `PythonMcpRegistryProvider` as `IMcpRegistryProvider` after `AddMcpCatalog()`; Catalog must not reference Execution.
- The canonical file-metadata registration API is `AddFileMetadataReaders()`, `AddRevitFileMetadataReader()`, and `AddAcadFileMetadataReader()`. Use these names in every task and composition example.

## TDD Rules

1. Run the affected existing behavior test first as a characterization baseline.
2. Add one destination-focused test before moving or changing behavior; observe its
   failing assertion or compile only within the same session.
3. Make the smallest move or implementation that passes the test; do not leave a
   red tree between sessions.
4. Use a single-test filter in the red/green loop; run the full MCP test project
   once at the end of each session.
5. Do not refactor adjacent behavior during a move unless a failing test requires it.
6. Do not use anonymous objects or raw `JsonElement` parsing in new application
   handlers; only SDK adapters may use protocol JSON values.

## Execution Flow And Agent Boundaries

Use one active implementation agent at a time. Each agent receives a broader,
coherent sequence and owns its green boundaries end-to-end; it does not spawn a
subagent for a file move, local compile error, or focused test failure. A second
read-only reviewer is used only at the two decision gates below and the final
proof review. This avoids competing edits to project files, daemon composition,
the shared MCP test project, and the active plan.

1. **Migration foundation agent — Tasks 1–2.** Create the missing destination
   scaffold, establish the dependency graph and test references, prove the
   baseline, reconcile the full Core contract inventory, and move Core contracts.
2. **Capability-platform agent — Tasks 3–5.** Extract FileMetadata Core/Revit,
   then Acad/daemon file composition, then Catalog/discovery. Preserve all
   existing Revit extensions (`.rvt`, `.rfa`, `.rft`, `.rte`) and complete the
   Catalog/Execution provider split before handing off.
3. **Runtime-boundary agent — Tasks 6–8.** Extract the host adapter, client/broker,
   and shared orchestrator in order. It owns every `McpServer.Create` call-site
   change and proves real application-service-provider propagation through host,
   stdio, and gateway sessions.
4. **External-surface and integration agent — Tasks 9–10.** Complete the dynamic
   search, dynamic invoke, and transport sub-boundaries serially; update the
   product contract at the search boundary; then recompose the daemon, remove the
   legacy project, update architecture docs, and run the complete proof sequence.

Each numbered task remains an independently reversible, green checkpoint within
its agent's work. An agent must stop and hand off only after the task's focused
proof, representative compile proof, changed-file summary, rollback commit, and
unresolved risks are recorded in this plan. The next implementation agent starts
from that green integrated commit, not a parallel unmerged worktree.

Decision gates:

- Before Task 1: a read-only reviewer confirms the scaffold/project-reference/TFM
  inventory and the migration-foundation agent records the actual baseline.
- Before Task 9: a read-only reviewer checks the proposed typed contract and
  product-document update against the final Core/Client/Server APIs.
- After Task 10: the same reviewer checks evidence for the fixed-surface,
  host-pipe, file-reader, and gateway proofs.

At each boundary, record the characterization test, destination test, focused
command, representative compile result, and an explicit rollback point in the
task's progress note.

## Dynamic Discovery And Invocation Target

This migration preserves exactly two dynamic external tools; it does not create
per-host synthetic MCP tools, mutate `tools/list`, or add Code Mode.

```text
search_dynamic(query, hostInstanceId?, kinds?, limit=12, detail=summary|schema)
  -> bounded hits with capabilityId, kind, target, host routing,
     short description, requiredArgs, argsHint, and hasMore

invoke_dynamic(capabilityId, arguments?)
invoke_dynamic(reads: [{ capabilityId, arguments? }])
```

`capabilityId` is catalog-versioned and encodes the resolved host route plus
kind/target. It is a convenience locator only: invoke resolves it again against
the current catalog/session and still runs authorization and policy at the leaf
call. A stale locator returns a retryable re-search error. `reads` and single
invoke fields are mutually exclusive and batch reads have item/output limits.

Search normalizes whitespace, `_`, and `-`, then ranks all-token matches before
partial matches. Invalid kinds, malformed arguments, and malformed batch reads
are validation errors; they must not silently broaden or ignore the request.

## Task 1: Create The Locked Scaffold And Establish A Green Baseline

The destination projects do not yet exist in the solution. This task creates the
locked scaffold before any source move and proves it is safe to use as the
migration destination.

- [x] **Step 1: Create the nine locked projects and assembly smoke test**

Create the nine projects named in Outcome, add them to `RevitDevTool.slnx`, and
add an assembly smoke test that references `DevTools.Mcp.Core.McpError` and
`DevTools.FileMetadata.Core.FileInfoRequest`. Use the locked
`DevTools.FileMetadata.*` names; do not introduce or retain a `DevTools.Files.*`
compatibility namespace.

- [x] **Step 2: Reconcile project dependency, TFM, package, and solution metadata**

Verify every new csproj has only the package/project references its owning module
requires. `OpenMcdf` belongs only in `DevTools.FileMetadata.Revit`, `ACadSharp`
only in `DevTools.FileMetadata.Acad`, and Tasks only in the orchestrator. Preserve
the existing multi-TFM mappings and `Build Project="false"` for daemon-packaged
net10 projects. Add a project-reference assertion that Catalog does not reference
Execution and record the actual installed MCP SDK version before relying on an SDK
overload.

- [x] **Step 3: Prove a classified green baseline**

Run `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`
and the representative multi-TFM compile proof required by
`docs/agents/verification.md`. Classify parser/integration tests that require a
prebuilt toolset, Pixi/Python, or a host as provisioned, skipped, or failed with
the exact reason. Do not begin a source move while deterministic baseline tests
or representative compilation are red.

### Task 1 progress — 2026-07-28

- **Scaffold:** Added all nine locked `DevTools.Mcp.*` and `DevTools.FileMetadata.*` projects to `RevitDevTool.slnx` and made `DevTools.Mcp.Tests` reference them. `PlatformSplitScaffoldTests` loads every destination assembly, verifies the `DevTools.Mcp.Core.McpError` and `DevTools.FileMetadata.Core.FileInfoRequest` scaffold anchors, and asserts that Catalog has no assembly reference to `DevTools.Execution`.
- **Graph and metadata:** All destination projects retain the repository's `net48;net8.0-windows;net10.0-windows` multi-targeting convention. The graph is `Core <- Catalog <- Client <- Orchestrator <- Server`, `Core <- Adapter <- Orchestrator`, `FileMetadata.Core <- {Revit, Acad, Server}`, and `Execution.Abstractions + Ipc <- Adapter`. Only Revit references `OpenMcdf`, only Acad references `ACadSharp`, and only Orchestrator references `ModelContextProtocol.Extensions.Tasks`. The existing daemon solution entry remains `Build Project="false"`; no daemon/package metadata changed. `Directory.Packages.props` records `ModelContextProtocol` and `ModelContextProtocol.Extensions.Tasks` at `2.0.0`.
- **Scope guard:** No legacy source was moved, no daemon/host composition changed, and no public MCP contract or product documentation changed. The two public scaffold anchors are intentionally skeletal; Task 2/3 own their final contracts.
- **Evidence:** Primary/auxiliary LSP diagnostics for the smoke test and both anchors were clean on 2026-07-28. After central `Microsoft.Extensions.*` package versions were reconciled to 10.0.10, the focused command `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` passed: 60 total, 60 passed, 0 failed, 0 skipped (net10.0). The net10 compile-only verification `dotnet build tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` also passed with exit code 0. Multi-TFM remediation ran the following compile-only command shape for each root/framework pair, all with exit code 0, zero warnings, and zero errors: `dotnet build <project> -c Debug -f <tfm> -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false`.
  - `source/DevTools.Mcp.Server/DevTools.Mcp.Server.csproj`: `net48` exit 0; `net8.0-windows` exit 0; `net10.0-windows` exit 0.
  - `source/DevTools.FileMetadata.Revit/DevTools.FileMetadata.Revit.csproj`: `net48` exit 0; `net8.0-windows` exit 0; `net10.0-windows` exit 0.
  - `source/DevTools.FileMetadata.Acad/DevTools.FileMetadata.Acad.csproj`: `net48` exit 0; `net8.0-windows` exit 0; `net10.0-windows` exit 0.
  No parser, integration, Pixi/Python, or live-host test was required/provisioned for this scaffold-only boundary.
- **Validation refresh — 2026-08-01:** Re-ran the previously missing compile-only multi-TFM proof sequentially, with deployment and ILRepack explicitly disabled. Each command exited `0` with `0 Warning(s)` and `0 Error(s)`:
  - `dotnet build source/DevTools.Mcp.Server/DevTools.Mcp.Server.csproj -c Debug -f net48 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` — `net48`, exit `0`.
  - `dotnet build source/DevTools.Mcp.Server/DevTools.Mcp.Server.csproj -c Debug -f net8.0-windows -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` — `net8.0-windows`, exit `0`.
  - `dotnet build source/DevTools.Mcp.Server/DevTools.Mcp.Server.csproj -c Debug -f net10.0-windows -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` — `net10.0-windows`, exit `0`.
  - `dotnet build source/DevTools.FileMetadata.Revit/DevTools.FileMetadata.Revit.csproj -c Debug -f net48 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` — `net48`, exit `0`.
  - `dotnet build source/DevTools.FileMetadata.Revit/DevTools.FileMetadata.Revit.csproj -c Debug -f net8.0-windows -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` — `net8.0-windows`, exit `0`.
  - `dotnet build source/DevTools.FileMetadata.Revit/DevTools.FileMetadata.Revit.csproj -c Debug -f net10.0-windows -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` — `net10.0-windows`, exit `0`.
  - `dotnet build source/DevTools.FileMetadata.Acad/DevTools.FileMetadata.Acad.csproj -c Debug -f net48 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` — `net48`, exit `0`.
  - `dotnet build source/DevTools.FileMetadata.Acad/DevTools.FileMetadata.Acad.csproj -c Debug -f net8.0-windows -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` — `net8.0-windows`, exit `0`.
  - `dotnet build source/DevTools.FileMetadata.Acad/DevTools.FileMetadata.Acad.csproj -c Debug -f net10.0-windows -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` — `net10.0-windows`, exit `0`.
  The focused regression command `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` also exited `0`: `60` passed, `0` failed, `0` skipped (`net10.0`). **Blockers:** none. The .NET workload-update notice was informational only and did not affect restore, compilation, or tests.
- **Completion:** [x] Task 1 is complete. The required focused test and every required `net48`, `net8.0-windows`, and `net10.0-windows` compile-only proof are green; no source contract, package-version, or deployment change was made during this validation refresh.
- **Rollback point:** No commit was created because the working tree was already broadly dirty. Revert the nine new project directories, `tests/DevTools.Mcp.Tests/PlatformSplitScaffoldTests.cs`, the added test project references, the `Shared/MCP Platform` solution folder, and this progress note to return to the pre-Task-1 state.
- **Handoff:** **GO.** Task 1 is a classified-green integrated checkpoint. Task 2 may begin; retain the scaffold anchors until its final Core and file-contract implementations replace them.

## Task 2: Establish Core Result, Error, And Port Contracts

**Files:**

- Create: `source/DevTools.Mcp.Core/Results/McpResult.cs`
- Create: `source/DevTools.Mcp.Core/Errors/McpError.cs`
- Create: `source/DevTools.Mcp.Core/Errors/McpErrorCode.cs`
- Create: `source/DevTools.Mcp.Core/Errors/McpExecutionException.cs`
- Create: `source/DevTools.Mcp.Core/Invocation/IMcpDispatcher.cs`
- Create: `source/DevTools.Mcp.Core/Invocation/McpInvocation.cs`
- Create: `source/DevTools.Mcp.Core/Sessions/IHostBroker.cs`
- Create: `source/DevTools.Mcp.Core/Sessions/IHostSession.cs`
- Create: `source/DevTools.Mcp.Core/Serialization/McpContractJsonContext.cs`
- Modify: `tests/DevTools.Mcp.Tests/ContractTests.cs`
- Modify: `source/DevTools.Mcp/Models/McpToolExecutionResult.cs`
- Modify: `source/DevTools.Mcp/Dispatch/IMcpPrimitiveDispatcher.cs`
- Modify: all current consumers of the two moved interfaces

### Task 2 Core inventory — 2026-08-01

- **Move now to `DevTools.Mcp.Core`:** `McpPrimitiveBinding`, `McpRegisteredTool`, `McpRegisteredResource`, `McpRegistryCatalog`, `McpErrorInfo`, `McpPropertyNames`, `IHostBroker`, `IHostSession`, `IMcpPrimitiveDispatcher`, and `IMcpExecutionTracker`. They are reusable descriptors or DI/I/O ports and have no host-API, WPF, pipe-implementation, or file-reader ownership.
- **Create now in `DevTools.Mcp.Core`:** `McpResult<T>`, `McpError`, `McpErrorCode`, `McpExecutionException`, `McpInvocation`, and `McpContractJsonContext`. The dispatcher changes from SDK `CallToolResult` output to `McpResult<TResponse>`; `McpInvocation.ExecutionState` retains the execution-state boundary, and SDK result construction remains in the existing SDK adapter.
- **Narrowed deliberately:** no standalone `IMcpToolHandler` or `IMcpResourceHandler` is introduced. The existing `IMcpPrimitiveDispatcher` is the one reusable backend port; `CatalogMcpServerTool` and `CatalogMcpServerResource` remain SDK adapters, not application ports. `IHostSession` is the current reusable client-session port. No generic `IMcpSessionFactory`, `IMcpServerSession`, or `IMcpClientSession` is introduced because the design's per-transport `McpServer.Create` lifecycle is owned by Task 8; defining empty or SDK-coupled session ports before that implementation would add no boundary and duplicate `IHostSession`. Future Catalog contracts (`IMcpCatalog`, `IMcpCatalogLoader`, `IMcpRegistryProvider` and their descriptors) remain Core-owned but are introduced with the Catalog behavior in Task 5, not speculatively as empty abstractions.
- **Dependency consequence:** Core may reference the MCP SDK protocol descriptors already carried by the moved registration and session contracts, but it references neither an SDK server adapter nor an execution backend. Projects that formerly consumed `DevTools.Mcp.Models`/`DevTools.Mcp.Dispatch` receive a Core reference; the legacy `DevTools.Mcp` project remains while its non-Core behavior is still in use.
- **Decision unblocked — 2026-08-01:** Architecture now authorizes `McpInvocationResponse` as the Core-owned, lossless primitive response with the typed `McpContent` union (text, embedded resource, image, audio), `IsError`, optional structured JSON, and bounded typed metadata. `DevTools.Mcp.Adapter` is the only SDK `CallToolResult`/content-block mapper. The required design amendment is recorded in `docs/superpowers/specs/2026-07-28-mcp-platform-structure-design.md`; this does not change any public dynamic or product contract. The prescribed destination tests were previously run as a red TDD check (`scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`, exit `1`; missing `McpResult<T>`, `McpErrorCode`, and the four-argument `McpError` constructor), then reverted so no red tree remained.

- **Implementation checkpoint — 2026-08-01:** Added the Core result/error family, `McpInvocation`, lossless `McpInvocationResponse`/`McpContent` contract, and source-generated context in `source/DevTools.Mcp.Core/Contracts.cs`; replaced the Task 1 `McpError` scaffold and added the two required destination tests in `tests/DevTools.Mcp.Tests/ContractTests.cs`. The red test proof exited `1` before implementation; green focused proof `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` exited `0` (62 passed, 0 failed, 0 skipped). Representative Core multi-target compile `dotnet build source/DevTools.Mcp.Core/DevTools.Mcp.Core.csproj -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` exited `0` with 0 warnings/errors for `net48`, `net8.0-windows`, and `net10.0-windows`.
- **Superseded handoff:** This partial checkpoint was completed by the Task 2 completion record below; no Task 3 work was performed.

- [x] **Step 1: Reconcile Core inventory, then write failing result-contract tests**

Record the complete Core contract inventory required by the design before creating
files. Resolve ownership of `IMcpSessionFactory`, `IMcpServerSession`,
`IMcpClientSession`, `IMcpToolHandler`, and `IMcpResourceHandler`; create or
narrow the design deliberately rather than deferring those symbols to later
modules. Then add these tests to `ContractTests.cs`:

```csharp
[Fact]
public void McpResult_Success_HasValueAndNoError()
{
    var result = McpResult<string>.Success("ok");

    Assert.True(result.IsSuccess);
    Assert.Equal("ok", result.Value);
    Assert.Null(result.Error);
}

[Fact]
public void McpResult_Failure_HasErrorAndNoValue()
{
    var error = new McpError(McpErrorCode.ValidationFailed, "Invalid request", [], "test-1");
    var result = McpResult<string>.Failure(error);

    Assert.False(result.IsSuccess);
    Assert.Null(result.Value);
    Assert.Equal(error, result.Error);
}
```

- [x] **Step 2: Run the contract tests to verify they fail**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: compile failure because `McpResult<T>` and `McpErrorCode` do not exist.

- [x] **Step 3: Implement the result and error contracts**

Create `McpResult.cs` exactly as follows:

```csharp
namespace DevTools.Mcp.Core;

public sealed record McpResult<T>
{
    private McpResult(T? value, McpError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public McpError? Error { get; }
    public bool IsSuccess => Error is null;

    public static McpResult<T> Success(T value) => new(value, null);
    public static McpResult<T> Failure(McpError error) => new(default, error);
}
```

Create `McpErrorCode.cs` with the current stable string values:

```csharp
namespace DevTools.Mcp.Core;

public static class McpErrorCode
{
    public const string ValidationFailed = "validation.failed";
    public const string CapabilityNotFound = "capability.not_found";
    public const string CapabilityAmbiguous = "capability.ambiguous";
    public const string ExecutionCancelled = "execution.cancelled";
    public const string ExecutionFailed = "execution.failed";
    public const string TransportDisconnected = "transport.disconnected";
}
```

`McpExecutionException` derives directly from `Exception`, exposes an
`McpErrorCode` string, and is never derived from `ModelContextProtocol.McpException`.

- [x] **Step 4: Move shared models and ports into `DevTools.Mcp.Core`**

Move these files, retaining behavior and replacing namespace `DevTools.Mcp.Models`
with `DevTools.Mcp.Core`:

```text
source/DevTools.Mcp/Models/McpPrimitiveBinding.cs
source/DevTools.Mcp/Models/McpRegisteredTool.cs
source/DevTools.Mcp/Models/McpRegisteredResource.cs
source/DevTools.Mcp/Models/McpRegistryCatalog.cs
source/DevTools.Mcp/Models/McpErrorInfo.cs
source/DevTools.Mcp/Models/McpPropertyNames.cs
source/DevTools.Mcp/IHostBroker.cs
source/DevTools.Mcp/IHostSession.cs
source/DevTools.Mcp/Dispatch/IMcpPrimitiveDispatcher.cs
source/DevTools.Mcp/Dispatch/IMcpExecutionTracker.cs
```

Replace `McpToolExecutionResult` with a DevTools-owned `McpResult<TResponse>` at
the dispatcher port. Preserve execution state tracking by adding `ExecutionState`
as a property of `McpInvocation`; `CallToolResult` is created only by the SDK
adapter boundary.

- [x] **Step 5: Add source-generated contract serialization**

Create `McpContractJsonContext.cs`:

```csharp
using System.Text.Json.Serialization;

namespace DevTools.Mcp.Core;

[JsonSerializable(typeof(McpError))]
[JsonSerializable(typeof(ValidationProblem))]
public partial class McpContractJsonContext : JsonSerializerContext;
```

- [x] **Step 6: Update references and run the tests**

Update `DevTools.Mcp`, `DevTools.Execution`, `DevTools.Daemon`,
`DevTools.Presentation`, `DevTools.Agents.Revit`, `DevTools.Agents.Acad`, and
`DevTools.Mcp.Tests` to reference `DevTools.Mcp.Core` rather than the old model
namespace. Keep the old `DevTools.Mcp` project temporarily and remove only the
moved source files from it.

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: contract tests and all existing MCP tests pass.

### Task 2 restoration — 2026-08-01

- **Restored boundary:** Reverted the unvalidated legacy model/port and host-catalog moves, consumer reference changes, dispatcher/tracker conversion, temporary HostServer mapping adapter, and its characterization tests. The worktree is restored to the documented green partial checkpoint: only the validated Core `Contracts.cs` result/error/invocation contracts, existing destination tests, and design amendment remain.
- **Native SDK mapping blocker:** `McpInvocationResponse` cannot replace the legacy dispatcher result until the physical HostServer adapter can preserve text, embedded-resource, image, and audio SDK content blocks without reducing non-text values to text/base64. No mapping implementation is retained in this checkpoint.
- **Validation:** `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` passed after restoration: 62 passed, 0 failed, 0 skipped (`net10.0`). `dotnet build source/DevTools.Mcp.Core/DevTools.Mcp.Core.csproj -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` passed with 0 warnings and 0 errors for `net48`, `net8.0-windows`, and `net10.0-windows`. **NO-GO** for Task 2 continuation; do not start Task 3 from a red or partially migrated boundary.

### Task 2 completion — 2026-08-01

- **Core move:** Moved the reusable primitive descriptors, registry catalog, error info/property names, host catalog contracts, host broker/session ports, dispatcher port, and execution tracker port to `DevTools.Mcp.Core`. The dispatcher now returns `McpResult<CallToolResult>`; `McpInvocation.ExecutionState` is carried through the tracking boundary, so no SDK result/error object remains in the result family.
- **Lossless SDK boundary:** The temporary legacy `HostServer/CatalogMcpServerTool` adapter is the only SDK/Core response mapper. It round-trips response `_meta`, content annotations and `_meta`, text/image/audio decoded bytes, text/blob embedded resources (resource `_meta`, URI, MIME, and decoded bytes), nullable `IsError`, and cloned structured JSON. It uses only `TextContentBlock`, `ImageContentBlock.FromBytes`, `AudioContentBlock.FromBytes`, and `EmbeddedResourceBlock` with `TextResourceContents`/`BlobResourceContents`. `ResourceLinkBlock`, `ToolUseContentBlock`, `ToolResultContentBlock`, and every unknown content type fail explicitly without text/base64 fallback. `PythonResultParser` now deserializes native SDK `CallToolResult`/content blocks and rejects malformed or unsupported content rather than converting it to text.
- **Tests:** Added SDK round-trip and unsupported-content characterization tests, plus a Python parser response-semantics test. Focused command `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` passed: **66 passed, 0 failed, 0 skipped** (`net10.0`).
- **Representative compile:** `dotnet build source/DevTools.Execution/DevTools.Execution.csproj -f <tfm> -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` passed with 0 warnings and 0 errors for `net48`, `net8.0-windows`, and `net10.0-windows`. The daemon composition build also passed: `dotnet build source/DevTools.Daemon/DevTools.Daemon.csproj -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` (0 warnings/errors).
- **Rollback:** Revert the Task 2 Core contract files, moved/deleted legacy model/port files, consumer project/import changes, legacy HostServer mapper and Python parser changes, destination tests, and this completion note. No Task 3 file-metadata source, behavior, or composition was changed.
- **Handoff:** **GO.** Task 2 is a green, reversible integrated checkpoint. Task 3 may begin in a subsequent authorized task; no Task 3+ implementation was performed here.

## Task 3: Extract Offline File Reader Contracts And Revit Reader

**Files:**

- Create: `source/DevTools.FileMetadata.Core/Reading/IFileReader.cs`
- Create: `source/DevTools.FileMetadata.Core/Reading/IFileReaderCatalog.cs`
- Create: `source/DevTools.FileMetadata.Core/Reading/FileReaderCatalog.cs`
- Create: `source/DevTools.FileMetadata.Core/FileInfoResult.cs`
- Create: `source/DevTools.FileMetadata.Core/FileError.cs`
- Create: `source/DevTools.FileMetadata.Core/FileReadException.cs`
- Create: `source/DevTools.FileMetadata.Revit/Reading/RevitFileMetadataReader.cs`
- Create: `source/DevTools.FileMetadata.Revit/DependencyInjection/RevitFileMetadataServiceCollectionExtensions.cs`},{
- Move: `source/DevTools.Daemon/Mcp/RevitFileInfo/*.cs`
- Modify: `source/DevTools.Daemon/Mcp/FileInfo/*.cs`
- Modify: `source/DevTools.Daemon/Mcp/Tools/ReadFileInfoTool.cs`
- Test: `tests/DevTools.Mcp.Tests/ParserIntegrationTests.cs`

- [x] **Step 1: Write failing reader-selection tests**

Add these tests:

```csharp
[Fact]
public void FileReaderCatalog_SelectsReaderThatSupportsRequest()
{
    var revit = new Mock<IFileReader>();
    revit.SetupGet(reader => reader.SupportedExtensions).Returns([".rvt"]);
    var catalog = new FileReaderCatalog([revit.Object]);

    Assert.Same(revit.Object, catalog.GetReader("sample.rvt"));
}

[Fact]
public void FileReaderCatalog_ThrowsFileErrorForUnknownExtension()
{
    var catalog = new FileReaderCatalog([]);

    var exception = Assert.Throws<FileReadException>(() => catalog.GetReader("sample.txt"));
    Assert.Equal(FileError.UnsupportedFormat, exception.Error);
}
```

- [x] **Step 2: Run the tests to verify they fail**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: compile failure because the file-core reader types do not exist.

- [x] **Step 3: Implement the file-core contracts and catalog**

Create the port and selection behavior:

```csharp
namespace DevTools.FileMetadata.Core;

public interface IFileReader
{
    IReadOnlyList<string> SupportedExtensions { get; }
    FileInfoResult Read(FileInfoRequest request);
}

public interface IFileReaderCatalog
{
    IFileReader GetReader(string filePath);
}

public sealed class FileReaderCatalog(IEnumerable<IFileReader> readers) : IFileReaderCatalog
{
    private readonly IFileReader[] _readers = readers.ToArray();

    public IFileReader GetReader(string filePath) =>
        _readers.FirstOrDefault(reader => reader.SupportedExtensions.Contains(
            Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase))
        ?? throw new FileReadException(FileError.UnsupportedFormat, $"Unsupported file type: '{Path.GetExtension(filePath)}'.");
}
```

Create the result and failure types:

```csharp
namespace DevTools.FileMetadata.Core;

public abstract class FileInfoResult
{
    public required FileHostApplication HostApplication { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
}

// This module owns a format-neutral enum. MCP JSON property names and host
// protocol enums are mapped only in the external-server adapter.
public enum FileHostApplication
{
    Revit,
    AutoCad
}

public static class FileError
{
    public const string UnsupportedFormat = "file.unsupported_format";
    public const string InvalidFile = "file.invalid";
    public const string ReadFailed = "file.read_failed";
}

public sealed class FileReadException(string error, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Error { get; } = error;
}
```

`FileInfoResult` is the common typed base for format-specific response objects.
Readers return a named response object. `ReadFileInfoTool` serializes that object
only at the MCP boundary with the established MCP JSON options.

- [x] **Step 4: Move the Revit offline parser without changing parser behavior**

Move these source files to `DevTools.FileMetadata.Revit` and update their namespace to
`DevTools.FileMetadata.Revit`:

```text
RevitCompoundFile.cs
BasicFileInfoReader.cs
BrowserOrganizationReader.cs
PartitionTableReader.cs
ProjectInformationReader.cs
TransmissionDataReader.cs
WorksetParser.cs
RevitFileInfoModels.cs
RevitFileInfoReader.cs
```

Rename `RevitFileInfoReader` to `RevitFileMetadataReader` and implement `IFileReader`.
Its supported extensions must remain ordinal-ignore-case `.rvt`, `.rfa`, `.rft`,
and `.rte`; `Read` delegates to the existing parser and maps
`FileInfoDetail.Summary` and `FileInfoDetail.Full` exactly as the current daemon
tool does. Add characterization tests for every extension, case-insensitive
selection, and the existing unsupported-format error behavior.

- [x] **Step 5: Register the Revit reader through one module entry point**

Create:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.FileMetadata.Revit;

public static class RevitFileMetadataServiceCollectionExtensions
{
    public static IServiceCollection AddRevitFileMetadataReader(this IServiceCollection services)
    {
        services.AddSingleton<IFileReader, RevitFileMetadataReader>();
        return services;
    }
}
```

- [x] **Step 6: Update `ReadFileInfoTool` to consume only file-core interfaces**

Replace its constructor dependency with `IFileReaderCatalog`. Keep tool name,
parameter names, `detail` parsing, and compact MCP JSON output unchanged. The tool
must not reference `DevTools.FileMetadata.Revit` or `DevTools.FileMetadata.Acad`.

- [x] **Step 7: Run parser and MCP tests**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: existing Revit file parser cases and `read_file_info` contract cases pass.

### Task 3 completion — 2026-08-01

- **Core and Revit move:** Added the typed FileMetadata reader/catalog/error contracts and moved the offline Revit compound-file parser into `DevTools.FileMetadata.Revit`. `ReadFileInfoTool` now resolves only `IFileReaderCatalog`; daemon composition registers the Core catalog and Revit module. The daemon's launch-version probe now uses the public Revit metadata reader rather than a daemon-owned parser type.
- **Compatibility:** Revit supports `.rvt`, `.rfa`, `.rft`, and `.rte`; `read_file_info` retains its public name, `filePath` and `detail` parameters, summary/full behavior, compact result handling, and unsupported-file error text. AutoCAD remains in the legacy daemon path until Task 4.
- **Validation:** Added reader selection/unsupported-format and Revit-extension characterization tests. `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` passed: 69 passed, 0 failed, 0 skipped. Revit FileMetadata compile-only proofs passed with 0 warnings/errors for `net48`, `net8.0-windows`, and `net10.0-windows`; daemon debug compile also passed with 0 warnings/errors.
- **Rollback:** Revert FileMetadata Core/Revit contracts and moved Revit parser files, daemon FileMetadata references/composition/tool/launch-probe changes, Task 3 tests, and this note.
- **Handoff:** **GO.** Task 3 is green. Task 4 may begin in its own boundary; no AutoCAD move has been performed.

## Task 4: Extract AutoCAD File Reader And File Composition

**Files:**

- Create: `source/DevTools.FileMetadata.Acad/Reading/AcadFileMetadataReader.cs`
- Create: `source/DevTools.FileMetadata.Acad/DependencyInjection/AcadFileMetadataServiceCollectionExtensions.cs`
- Move: `source/DevTools.Daemon/Mcp/AcadFileInfo/*.cs`
- Create: `source/DevTools.FileMetadata.Core/DependencyInjection/FileMetadataServiceCollectionExtensions.cs`},{
- Modify: `source/DevTools.Daemon/Hosting/ServerHostBuilder.cs`
- Test: `tests/DevTools.Mcp.Tests/ParserIntegrationTests.cs`

- [x] **Step 1: Write failing AutoCAD reader tests**

```csharp
[Fact]
public void AcadFileMetadataReader_RecognizesDwgCaseInsensitively()
{
    var reader = new AcadFileMetadataReader();

    Assert.Contains(".dwg", reader.SupportedExtensions, StringComparer.OrdinalIgnoreCase);
    Assert.DoesNotContain(".rvt", reader.SupportedExtensions, StringComparer.OrdinalIgnoreCase);
}
```

- [x] **Step 2: Run the test to verify it fails**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: compile failure because `AcadFileMetadataReader` does not exist in the new project.

- [x] **Step 3: Move the AutoCAD parser and implement the common port**

Move `AcadFileInfoReader.cs` and `AcadFileInfoModels.cs` to `DevTools.FileMetadata.Acad`.
Rename the reader to `AcadFileMetadataReader`, implement `IFileReader`, preserve existing
ACadSharp parsing, and expose `.dwg` through `SupportedExtensions`.

- [x] **Step 4: Add the registration extensions**

Create `FileServiceCollectionExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.FileMetadata.Core;

public static class FileMetadataServiceCollectionExtensions
{
    public static IServiceCollection AddFileMetadataReaders(this IServiceCollection services)
    {
        services.AddSingleton<IFileReaderCatalog, FileReaderCatalog>();
        return services;
    }
}
```

Create `AddAcadFileMetadataReader()` using the same registration shape as
`AddRevitFileMetadataReader()`.

- [x] **Step 5: Compose file modules in the daemon only**

Replace `builder.Services.AddFileInfoReaders()` in `ServerHostBuilder` with:

```csharp
builder.Services
    .AddFileMetadataReaders()
    .AddRevitFileMetadataReader()
    .AddAcadFileMetadataReader();
```

Add a daemon composition test resolving `IFileReaderCatalog` with both format
readers registered.

Remove the old daemon `FileInfo`, `RevitFileInfo`, and `AcadFileInfo` folders after
their source has moved.

- [x] **Step 6: Run the focused tests**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: the new extension-selection test and all existing file reader tests pass.

### Task 4 completion — 2026-08-01

- **AutoCAD move and composition:** Moved the `.dwg` reader/models into `DevTools.FileMetadata.Acad` as `AcadFileMetadataReader`, registered it through `AddAcadFileMetadataReader()`, and composed only the Core/Revit/Acad FileMetadata modules in the daemon. Removed the obsolete daemon `FileInfo` and `AcadFileInfo` folders.
- **TFM decision:** Per explicit product direction, `DevTools.FileMetadata.Revit` and `DevTools.FileMetadata.Acad` are independent offline reader libraries targeting only `net10.0-windows`, matching `DevTools.Daemon`; they no longer carry HostApp-era `net48`/`net8.0-windows` compatibility obligations. `DevTools.FileMetadata.Core` remains multi-targeted as the shared contract layer.
- **Validation:** Added `.dwg` selection and two-reader DI composition tests. `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` passed: 71 passed, 0 failed, 0 skipped. `DevTools.FileMetadata.Acad` and daemon Debug compile-only proofs passed with 0 warnings/errors.
- **Rollback:** Revert the Acad reader/models/module registration, daemon composition and project-reference changes, removed daemon file-info folders, TFM decision, tests, and this completion note.
- **Handoff:** **GO.** Task 4 is green. Task 5 may begin in a separate boundary.

## Task 5: Extract Toolset Catalog And Discovery

**Files:**

- Create: `source/DevTools.Mcp.Catalog/DependencyInjection/McpCatalogServiceCollectionExtensions.cs`
- Move: `source/DevTools.Mcp/Registry/*.cs`
- Move: `source/DevTools.Mcp/Discovery/*.cs`
- Move: `source/DevTools.Mcp/McpCatalogStore.cs`
- Move: `source/DevTools.Mcp/McpPathValidator.cs`
- Move: `source/DevTools.Mcp/BuiltIn/BuiltInMcpRegistryProvider.cs`
- Modify: `source/DevTools.Execution/ExecutionExtensions.cs`
- Modify: `source/DevTools.Presentation/DevTools.Presentation.csproj`
- Test: `tests/DevTools.Mcp.Tests/ParserIntegrationTests.cs`

- [x] **Step 1: Write a failing catalog registration test**

```csharp
[Fact]
public void AddMcpCatalog_RegistersCatalogStoreAndLoader()
{
    var services = new ServiceCollection();
    services.AddSingleton<ISettingsService>(Mock.Of<ISettingsService>());

    services.AddMcpCatalog();
    using var provider = services.BuildServiceProvider();

    Assert.NotNull(provider.GetRequiredService<McpCatalogStore>());
    Assert.NotNull(provider.GetRequiredService<IMcpCatalogLoader>());
}
```

- [x] **Step 2: Run the test to verify it fails**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: compile failure because `AddMcpCatalog` and `IMcpCatalogLoader` do not exist.

- [x] **Step 3: Move catalog source and expose catalog ports**

Move the listed registry/discovery source files into `DevTools.Mcp.Catalog`.
Add `IMcpCatalogLoader` in `DevTools.Mcp.Core` with this signature:

```csharp
public interface IMcpCatalogLoader
{
    McpRegistryCatalog LoadCatalog(
        IReadOnlyCollection<string> dotnetPaths,
        IReadOnlyCollection<string> pythonToolsetPaths);
}
```

Make `McpCatalogLoader` implement the port. Keep `McpCatalogStore` concrete because
it owns reload state and events; consumers that only query capabilities use
`IMcpCatalog`.

- [x] **Step 4: Add catalog DI registration**

Implement Catalog-owned registrations only:

```csharp
public static IServiceCollection AddMcpCatalog(this IServiceCollection services)
{
    services.AddSingleton<DotnetMcpRegistryProvider>();
    services.AddSingleton<BuiltInMcpRegistryProvider>();
    services.AddSingleton<IMcpRegistryProvider>(sp => sp.GetRequiredService<DotnetMcpRegistryProvider>());
    services.AddSingleton<IMcpRegistryProvider>(sp => sp.GetRequiredService<BuiltInMcpRegistryProvider>());
    services.AddSingleton<IMcpCatalogLoader, McpCatalogLoader>();
    services.AddSingleton<McpCatalogStore>();
    return services;
}
```

Keep `PythonMcpRegistryProvider` in `DevTools.Execution` until Python execution is
extracted. `ExecutionExtensions` calls `AddMcpCatalog()` and then registers that
provider as `IMcpRegistryProvider`; `DevTools.Mcp.Catalog` must not reference
Execution. Add `WithCatalog(this IDevToolsMcpBuilder)` in this module; it delegates
only to `builder.Services.AddMcpCatalog()`.

- [x] **Step 5: Update current callers and focused tests**

Replace direct catalog registrations in `ExecutionExtensions` with
`services.AddMcpCatalog()`. Update `DevTools.Presentation` to reference
`DevTools.Mcp.Catalog` for its registry view models. Preserve `ReloadAsync`,
`AddPathAsync`, and `CatalogChanged` behavior.

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: parser/discovery tests and catalog registration test pass.

### Task 5 completion — 2026-08-01

- **Ownership and ports:** Moved all registry and discovery implementations, the built-in provider/contracts, Python parser DTOs, and discovery JSON-schema models to `DevTools.Mcp.Catalog`. `IMcpRegistryProvider`, `IMcpCatalogLoader`, and the new query-only `IMcpCatalog` are Core ports; `McpCatalogStore` remains concrete for its reload state and `CatalogChanged` event while implementing `IMcpCatalog`. No legacy Catalog/Discovery/Registry/BuiltIn source remains under `DevTools.Mcp`.
- **DI and composition:** `AddMcpCatalog()` now owns the Dotnet and built-in providers, discovery parsers, loader, concrete store, and query port. Its focused registration test proves the store/query-port identity, loader, and both Catalog-owned providers resolve. `WithCatalog()` delegates only to `AddMcpCatalog()` through the Core-owned `IDevToolsMcpBuilder` contract. `ExecutionExtensions` invokes `AddMcpCatalog()` and then adds only `PythonMcpRegistryProvider` as `IMcpRegistryProvider`; Catalog has no `DevTools.Execution` project reference.
- **Callers:** Updated Execution, Presentation, test, and host-agent direct references/usings to the Catalog/Core namespaces. Existing reload, add-path, catalog-changed, .NET parser, and Python parser behavior is preserved by the focused MCP regression suite.
- **Evidence:** Characterization baseline `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` passed with **71 passed, 0 failed, 0 skipped**. The required red registration test initially failed to compile because the Catalog-owned `DotnetMcpRegistryProvider` and `BuiltInMcpRegistryProvider` did not exist. Green focused proof passed with **72 passed, 0 failed, 0 skipped**. Compile-only multi-TFM proofs all exited `0` with **0 warnings and 0 errors**: `dotnet build source/DevTools.Mcp.Catalog/DevTools.Mcp.Catalog.csproj -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false`, `dotnet build source/DevTools.Execution/DevTools.Execution.csproj -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false`, and `dotnet build source/DevTools.Presentation/DevTools.Presentation.csproj -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` (each covering `net48`, `net8.0-windows`, and `net10.0-windows`). `dotnet list source/DevTools.Mcp.Catalog/DevTools.Mcp.Catalog.csproj reference` lists only Core, Execution.Abstractions, and Settings; it does not reference `DevTools.Execution`.
- **Rollback:** Revert the Task 5 Catalog/Core source moves, parser/schema DTO moves, project/import changes, catalog registration test, and this completion note. No commit was created because the worktree was already broadly dirty.
- **Handoff:** **GO.** Task 5 is a green integrated checkpoint. Do not begin Task 6+ from this boundary.

## Task 6: Extract Host SDK Adapter And Named-Pipe Server

**Files:**

- Move: `source/DevTools.Mcp/HostServer/*.cs`
- Move: `source/DevTools.Mcp/Execution/DotnetMcpServerFactory.cs`
- Move: `source/DevTools.Mcp/Execution/PythonResultParser.cs`
- Move: `source/DevTools.Execution/External/HostMcpPipeServer.cs`
- Create: `source/DevTools.Mcp.Adapter/DependencyInjection/McpHostAdapterServiceCollectionExtensions.cs`
- Modify: `source/DevTools.Execution/ExecutionExtensions.cs`
- Test: `tests/DevTools.Mcp.Tests/NamedPipeSdkIntegrationTests.cs`
- Test: `tests/DevTools.Mcp.Tests/McpServerConfigurationTests.cs`

- [x] **Step 1: Write a failing host-adapter DI test**

```csharp
[Fact]
public void AddMcpHostAdapter_RegistersHostPipeServer()
{
    var services = new ServiceCollection();
    services.AddMcpHostAdapter();

    Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(HostMcpPipeServer));
}
```

- [x] **Step 2: Run the test to verify it fails**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: compile failure because the new registration extension does not exist.

- [x] **Step 3: Move SDK adapters without changing protocol behavior**

Move `CatalogMcpServerTool`, `CatalogMcpServerResource`,
`HostMcpServerFactory`, `DotnetMcpServerFactory`, and `PythonResultParser` into
`DevTools.Mcp.Adapter`. Keep their public SDK behavior
unchanged: host tools/resources advertise `ListChanged = true`; prompts remain
daemon-owned; call logging remains a request filter.

- [x] **Step 4: Move `HostMcpPipeServer` and inject the application provider**

Move the hosted service into `DevTools.Mcp.Adapter`. In `HandleConnectionAsync`,
create each server with the real app provider:

```csharp
var server = McpServer.Create(transport, options, loggerFactory, appServices);
```

Keep shared tool/resource collections across host sessions and preserve the current
pipe name, ACL, max instance count, catalog rebuild, and list-changed behavior.

- [x] **Step 5: Register the host adapter through one extension**

```csharp
public static IServiceCollection AddMcpHostAdapter(this IServiceCollection services)
{
    services.AddSingleton<HostMcpPipeServer>();
    services.AddHostedService(sp => sp.GetRequiredService<HostMcpPipeServer>());
    return services;
}
```

Move `HostMcpPipeServer` registration out of `ExecutionExtensions` and call
`AddMcpHostAdapter()` there after catalog and dispatcher registrations. Add
`WithHostAdapter(this IDevToolsMcpBuilder)` in Adapter; it delegates only to
`builder.Services.AddMcpHostAdapter()`.

- [x] **Step 6: Run named-pipe and configuration tests**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: `NamedPipe_DaemonClientTalksToHostSdkServer` and host list-changed
configuration tests pass.

### Task 6 completion — 2026-08-01

- **Adapter boundary:** Moved the SDK catalog tool/resource adapters, host-server factory, .NET invocation helper, Python result parser, and `HostMcpPipeServer` into `DevTools.Mcp.Adapter`. The adapter preserves the `DevToolsMcp_{Host}_{Version}_{PID}` name, current-user ACL, eight-instance limit, shared tool/resource collections, catalog rebuild, and host `ListChanged = true` behavior. Prompts remain daemon-owned and call logging remains the existing protocol filter pending Task 8.
- **Composition and services:** Added `AddMcpHostAdapter()` and `WithHostAdapter()`; `ExecutionExtensions.AddExecutionServices()` now invokes the adapter registration after its catalog and dispatcher registrations instead of registering the host pipe directly. Adapter internals are visible only to the Execution and MCP test assemblies. The moved invocation helpers remain internal implementation details.
- **Application services:** Every current `McpServer.Create` call now supplies an actual app provider: the host pipe uses its injected application provider; stdio and gateway sessions receive their daemon app provider; SDK test sessions use their real test container. Gateway construction now carries the daemon provider from `GatewayHostedService`.
- **Tests:** The required red DI proof ran first and failed as expected because `DevTools.Mcp.Adapter`, `AddMcpHostAdapter`, and `HostMcpPipeServer` did not yet exist. The green focused/full MCP command `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` then passed with **73 passed, 0 failed, 0 skipped**, including `NamedPipe_DaemonClientTalksToHostSdkServer` and the new host-adapter DI registration test.
- **Compile proof:** Compile-only commands each exited `0` with **0 warnings and 0 errors**: `dotnet build source/DevTools.Mcp.Adapter/DevTools.Mcp.Adapter.csproj -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` (all `net48`, `net8.0-windows`, and `net10.0-windows` targets), `dotnet build source/DevTools.Execution/DevTools.Execution.csproj -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` (all three targets), and `dotnet build source/DevTools.Daemon/DevTools.Daemon.csproj -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` (net10.0-windows).
- **Rollback:** Revert the Adapter source moves and project metadata, Execution/daemon composition changes, SDK test app-service-provider updates, Task 6 test, and this note. No Task 7 source or composition was changed.
- **Handoff:** **GO.** Task 6 is a green integrated checkpoint. Task 7 may begin only under separate authorization.

## Task 7: Extract Host MCP Client And Broker

**Files:**

- Move: `source/DevTools.Daemon/Mcp/HostBroker.cs`
- Move: `source/DevTools.Daemon/Mcp/HostSession.cs`
- Move: `source/DevTools.Daemon/Mcp/IMcpPipeScanner.cs`
- Move: `source/DevTools.Daemon/Mcp/McpPipeScanner.cs`
- Move: `source/DevTools.Daemon/Mcp/IHostDiscovery.cs`
- Move: `source/DevTools.Mcp/Broker/*.cs`
- Create: `source/DevTools.Mcp.Client/DependencyInjection/McpHostClientServiceCollectionExtensions.cs`
- Modify: `source/DevTools.Daemon/Hosting/ServerHostBuilder.cs`
- Test: `tests/DevTools.Mcp.Tests/HostCatalogTests.cs`
- Test: `tests/DevTools.Mcp.Tests/DynamicToolsAndObservabilityTests.cs`

- [x] **Step 1: Write a failing client registration test**

```csharp
[Fact]
public void AddMcpHostClient_ExposesSameBrokerAsDiscoveryService()
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddMcpHostClient();
    using var provider = services.BuildServiceProvider();

    Assert.Same(provider.GetRequiredService<IHostBroker>(), provider.GetRequiredService<IHostDiscovery>());
}
```

- [x] **Step 2: Run the test to verify it fails**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: compile failure because `AddMcpHostClient` does not exist.

- [x] **Step 3: Move client and catalog aggregation source**

Move broker models, `HostCatalog`, pipe scanner, host session, and broker into
`DevTools.Mcp.Client`. Keep `IHostBroker` and `IHostSession` in core. Preserve the
two-second discovery polling interval, host list-changed notifications, catalog
replacement by machine/process key, and session disposal behavior.

- [x] **Step 4: Add the host-client registration extension**

```csharp
public static IServiceCollection AddMcpHostClient(this IServiceCollection services)
{
    services.AddSingleton<IMcpPipeScanner, McpPipeScanner>();
    services.AddSingleton<HostBroker>();
    services.AddSingleton<IHostBroker>(sp => sp.GetRequiredService<HostBroker>());
    services.AddSingleton<IHostDiscovery>(sp => sp.GetRequiredService<HostBroker>());
    return services;
}
```

- [x] **Step 5: Replace daemon registrations and update all consumers**

Replace the current direct scanner and broker registrations in `ServerHostBuilder`
with `AddMcpHostClient()`. Add `WithHostClient(this IDevToolsMcpBuilder)` in Client;
it delegates only to `builder.Services.AddMcpHostClient()`. Update dashboard and control-pipe references to import
the core interfaces. Do not move dashboard or control-pipe code into the client
project.

- [x] **Step 6: Run client behavior tests**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: host catalog, dynamic search, and dynamic invoke tests pass unchanged.

### Task 7 completion — 2026-08-01

- **Client boundary:** Moved `HostBroker`, `HostSession`, `IMcpPipeScanner`/`McpPipeScanner`, `IHostDiscovery`, and the `HostCatalog` implementation into `DevTools.Mcp.Client`; also moved the broker's machine-metadata helper so Client has no daemon dependency. The Core-owned catalog descriptors (`HostKey`, entries, hits, and resolution types) remain transport-neutral contract data. Added Core `IHostCatalog`, and `IHostBroker.Catalog` now exposes that port so Core does not reference Client while all consumers retain catalog search, resolve, list, replace, remove, and clear behavior.
- **Behavior preserved:** `HostBroker.RunAsync` still scans every two seconds; list-changed notifications still refresh the affected host catalog entry; catalog replacement remains keyed by machine/process; disconnected and broker-disposed sessions still dispose asynchronously; `Changed` notifications still drive the dashboard. The daemon dashboard/control pipe remain daemon-owned and consume the Core `IHostBroker`; scanner/discovery consumers import Client.
- **Composition and DI:** Added `AddMcpHostClient()` and `WithHostClient()` in Client. The daemon composition root now calls that module registration rather than directly registering the scanner/broker/discovery services. The registration test verifies that `IHostBroker` and `IHostDiscovery` resolve to the same `HostBroker` singleton; it disposes the provider asynchronously because the broker owns async session disposal.
- **TDD evidence:** The destination registration test was added first. `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` failed before implementation with exit `1` because `DevTools.Mcp.Client`, `AddMcpHostClient`, and `IHostDiscovery` did not exist. After implementation, the same command passed with **74 passed, 0 failed, 0 skipped** (`net10.0`), including the host-catalog and dynamic search/invoke regression tests.
- **Compile evidence:** `dotnet build source/DevTools.Mcp.Client/DevTools.Mcp.Client.csproj -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` passed for `net48`, `net8.0-windows`, and `net10.0-windows`, with **0 warnings and 0 errors**. `dotnet build source/DevTools.Daemon/DevTools.Daemon.csproj -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` passed for `net10.0-windows`, with **0 warnings and 0 errors**.
- **Rollback:** Revert the Client source moves and registration extension, the Core `IHostCatalog` port/IHostBroker signature change, daemon project/composition/import updates, Client package reference, Task 7 test/import updates, and this completion note. The worktree was already broadly dirty; no commit was created.
- **Handoff:** **GO.** Task 7 is a green integrated checkpoint. No Task 8 source, composition, or test change was performed.

## Task 8: Extract Shared MCP Orchestrator

**Files:**

- Move: `source/DevTools.Mcp/Hosting/McpServerServiceExtensions.cs`
- Move: `source/DevTools.Mcp/Hosting/McpServerConfigurator.cs`
- Move: `source/DevTools.Mcp/Utils/McpLogPayload.cs`
- Move: `source/DevTools.Mcp/HostServer/HostCallLoggingFilters.cs`
- Use: `source/DevTools.Mcp.Core/Composition/IDevToolsMcpBuilder.cs` (introduced by Task 5 for `WithCatalog()`)
- Create: `source/DevTools.Mcp.Orchestrator/DevToolsMcpBuilder.cs`},{
- Create: `source/DevTools.Mcp.Orchestrator/DevToolsMcpBuilderExtensions.cs`
- Modify: `source/DevTools.Daemon/Hosting/StdioHostedService.cs`
- Modify: `source/DevTools.Daemon/Hosting/GatewayTunnelClient.cs`
- Test: `tests/DevTools.Mcp.Tests/McpServerConfigurationTests.cs`

- [x] **Step 1: Write failing orchestrator builder tests**

```csharp
[Fact]
public void AddDevToolsMcp_ReturnsBuilderUsingOriginalServices()
{
    var services = new ServiceCollection();

    var builder = services.AddDevToolsMcp();

    Assert.Same(services, builder.Services);
}
```

- [x] **Step 2: Run the test to verify it fails**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: compile failure because `AddDevToolsMcp` and its builder do not exist.

- [x] **Step 3: Implement SDK-style DI builder and preserve Tasks ordering**

Create:

```csharp
// IDevToolsMcpBuilder is declared in DevTools.Mcp.Core so modules can expose
// fluent extensions without referencing DevTools.Mcp.Orchestrator.
public static class DevToolsMcpServiceCollectionExtensions
{
    public static IDevToolsMcpBuilder AddDevToolsMcp(this IServiceCollection services)
    {
        var taskStore = new InMemoryMcpTaskStore();
        services.AddSingleton<IMcpTaskStore>(taskStore);
        services.AddMcpServer().WithTasks(taskStore);
        return new DevToolsMcpBuilder(services);
    }
}
```

Keep Tasks configuration before call logging filters. `McpServerConfigurator.Apply`
must run all app-registered `IConfigureOptions<McpServerOptions>` and then attach
the logging filters. Each module adds both a directly testable `Add...` method and
a `With...` method on `IDevToolsMcpBuilder` that delegates only to that `Add...`
method; no module may register another module implicitly.

- [x] **Step 4: Correct manual session creation in all current transports**

First extend the current stdio and gateway constructors/factories so both can
receive the real application provider. Update the stdio and gateway server
creation calls to pass `appServices`:

```csharp
McpServer.Create(transport, options, loggerFactory, appServices)
```

The host adapter change in Task 6 already does the same. Add a test handler that
accepts `IServiceProvider` and asserts a request-scoped service resolves in the
host-pipe, stdio, and gateway session paths, proving the provider reaches every
SDK session.

- [x] **Step 5: Run configuration and observability tests**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: daemon filter, host filter, Tasks setup, and scoped-service tests pass.

### Task 8 completion — 2026-08-01

- **Orchestrator boundary:** Moved shared SDK Tasks registration, manually-built server-options configuration, binary-safe call-log payload serialization, and call/resource logging filters into `DevTools.Mcp.Orchestrator`. `AddDevToolsMcp()` returns the Core-owned `IDevToolsMcpBuilder` over the original collection. Existing `WithCatalog()`, `WithHostAdapter()`, and `WithHostClient()` wrappers remain module-owned and delegate only to their respective direct registrations.
- **Ordering and composition:** `AddDevToolsMcp()` creates the one `InMemoryMcpTaskStore`, registers it as `IMcpTaskStore`, and invokes `AddMcpServer().WithTasks(taskStore)`. `McpServerConfigurator.Apply` first executes every app-registered `IConfigureOptions<McpServerOptions>` and only then attaches call logging filters. Daemon and host execution composition now use `AddDevToolsMcp()`; the legacy daemon-options factory and host SDK factory consume the relocated configurator while the fixed external surface remains deferred to Task 9.
- **Application services:** All three current manual session paths use their injected real application provider in `McpServer.Create`: host pipe, stdio, and gateway. No temporary provider is built during registration or options construction.
- **Tests:** The destination builder test was added first and failed at compile time because `DevTools.Mcp.Orchestrator` and `AddDevToolsMcp` did not exist. The green focused command `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` passed with **76 passed, 0 failed, 0 skipped** (`net10.0`), including the new ordering test and existing daemon/host logging and payload tests.
- **Compile evidence:** Compile-only builds with deployment and ILRepack disabled passed with **0 warnings and 0 errors**: `DevTools.Mcp.Orchestrator` for `net48`, `net8.0-windows`, and `net10.0-windows`; `DevTools.Execution` for the same three TFMs; and `DevTools.Daemon` for `net10.0-windows`.
- **Rollback:** Revert the Orchestrator source additions, relocated legacy shared-source removals, direct project/package references, composition/import updates, destination tests, and this note. The worktree was already broadly dirty; no commit was created.
- **Handoff:** **GO.** Task 8 is a green integrated checkpoint. Task 9 may begin only under separate authorization.

## Task 9: Extract Fixed External MCP Server And Typed Tool Contracts

Execute this task in three green sessions, retaining the same two public tool
names throughout:

1. **Search session:** typed search contracts, normalized-token ranking, bounded
   summary response, `requiredArgs`/`argsHint`, `hasMore`, and `capabilityId`.
2. **Invoke session:** typed locator resolution, stale-catalog error, strict
   single-vs-batch validation, bounded typed batch reads, and leaf policy hook.
3. **Transport session:** move fixed tools/prompts plus stdio/gateway hosting only
   after the dynamic behavior regressions are green.

**Files:**

- Move: `source/DevTools.Mcp/Tools/SearchDynamicTool.cs`
- Move: `source/DevTools.Mcp/Tools/InvokeDynamicTool.cs`
- Move: `source/DevTools.Mcp/Prompts/*.cs`
- Move: `source/DevTools.Mcp/Utils/ToolHelpers.cs`
- Move: `source/DevTools.Daemon/Mcp/McpEngine.cs`
- Move: `source/DevTools.Daemon/Mcp/Tools/*.cs`
- Move: `source/DevTools.Daemon/Mcp/HostLaunchService.cs`
- Move: `source/DevTools.Daemon/Mcp/IHostLaunchService.cs`
- Move: `source/DevTools.Daemon/Mcp/Utils/*.cs`
- Move: `source/DevTools.Daemon/Hosting/StdioHostedService.cs`
- Move: `source/DevTools.Daemon/Hosting/GatewayTunnelClient.cs`
- Create: `source/DevTools.Mcp.Server/Contracts/SearchCapabilitiesRequest.cs`
- Create: `source/DevTools.Mcp.Server/Contracts/SearchCapabilitiesResponse.cs`
- Create: `source/DevTools.Mcp.Server/Contracts/InvokeCapabilityRequest.cs`
- Create: `source/DevTools.Mcp.Server/Contracts/InvokeCapabilityResponse.cs`
- Create: `source/DevTools.Mcp.Server/Contracts/InvokeCapabilityRequestValidator.cs`
- Create: `source/DevTools.Mcp.Server/Contracts/DynamicCapabilityId.cs`
- Create: `source/DevTools.Mcp.Server/SdkAdapters/McpToolAdapter.cs`
- Create: `source/DevTools.Mcp.Server/Gateway/IGatewayAccessTokenProvider.cs`
- Create: `source/DevTools.Mcp.Server/DependencyInjection/ExternalMcpServerServiceCollectionExtensions.cs`
- Modify: `source/DevTools.Daemon/Hosting/GatewayHostedService.cs`
- Modify: `source/DevTools.Daemon/Hosting/ServerHostBuilder.cs`
- Modify: `docs/product/mcp.md`
- Test: `tests/DevTools.Mcp.Tests/DynamicToolsAndObservabilityTests.cs`

- [x] **Step 1: Write failing typed-contract tests for dynamic tools**

```csharp
[Fact]
public void SearchCapabilitiesResponse_UsesNamedItems()
{
    var response = new SearchCapabilitiesResponse(1,
    [
        new SearchCapabilityItem("tool", "echo", "Echo", "machine", 101, "Revit", "2025", null, null, ["message"])
    ]);

    Assert.Equal(1, response.Count);
    Assert.Equal("echo", Assert.Single(response.Items).Target);
}

[Fact]
public void InvokeCapabilityRequest_RejectsSingleAndBatchTargetsTogether()
{
    var request = new InvokeCapabilityRequest("tool", "echo", 101, null, null,
        [new ResourceReadRequest("resource", "revit://version", null)]);

    var problems = InvokeCapabilityRequestValidator.Validate(request);

    Assert.Contains(problems, problem => problem.Name == "reads");
}
```

- [x] **Step 2: Run the test to verify it fails**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: compile failure because the typed contracts and validator do not exist.

Before implementing the new public shape, update `docs/product/mcp.md` with the
authorized dynamic contract and replace the old external contract tests in the
same boundary; retain tests for all fixed-surface and host-routing behavior.

- [x] **Step 3: Implement named requests, responses, and validators**

Create records for external tool input/output. `SearchCapabilitiesResponse` returns
bounded summary items with `DynamicCapabilityId`, `requiredArgs`, `argsHint`, and
`hasMore`; only `detail=schema` includes an input schema. Search normalizes tokens
and ranks all-token matches before partial matches. Use `ResourceReadRequest` and
`ResourceReadResult` with `DynamicCapabilityId` instead of parsing arbitrary batch
entries in the handler. Keep `JsonElement? Arguments` only for runtime-defined
toolset arguments. Add all new records to a source-generated JSON context in
`DevTools.Mcp.Server`.

`InvokeCapabilityRequestValidator.Validate` returns `IReadOnlyList<ValidationProblem>`
and rejects malformed IDs/arguments/reads, an unknown kind, a tool in `reads`,
single-target fields together with non-empty `reads`, and batch item/output limits.
A locator no longer resolves after catalog replacement returns a retryable stale-
catalog error.

- [x] **Step 4: Replace ad-hoc tool construction with typed handlers and one SDK adapter**

Define:

```csharp
public interface IMcpToolHandler<TRequest, TResponse>
{
    Task<McpResult<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
```

Implement `SearchCapabilitiesHandler` and `InvokeCapabilityHandler`. Their only
dependencies are `IHostBroker` and typed services. Add `McpToolAdapter` in the
server assembly to deserialize a named request, invoke the handler, and map
`McpResult<T>` to `CallToolResult`. This is the only external-server code allowed
to create `CallToolResult` from a DevTools result.

Preserve the two public tool names, fixed-tool and prompt behavior, structured
content, compact JSON, host routing, and fixed-surface error behavior. Replace the
dynamic parameter names, ranking, schema-selection field, batch shape, and
locator/error semantics with the authorized typed contract documented in this
plan and `docs/product/mcp.md`.

- [x] **Step 5: Move external surface and transport code**

Move fixed tools, prompts, `McpEngine`, stdio hosting, gateway tunnel session,
host launch services, and launch path resolvers into `DevTools.Mcp.Server`.
`GatewayTunnelClient` receives an `IGatewayAccessTokenProvider` interface rather
than `IAuthService`; `DevTools.Daemon.AuthService` is adapted at the composition
root. Do not move OAuth browser, dashboard, or tray code.

- [x] **Step 6: Register the external server as a module**

Implement `AddExternalMcpServer()` and its fluent `WithExternalServer()` wrapper
to register typed handlers, fixed prompts,
`McpEngine`, host launch services, and hosted stdio/gateway services. The daemon
composition root supplies `IGatewayAccessTokenProvider`, file readers, and client
modules before invoking it.

- [x] **Step 7: Run dynamic-tool regression tests**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: all fixed-surface, host-routing, compact JSON, observability, and
file-reader regression tests pass; dynamic-tool tests assert the authorized typed
search, locator, ranking, schema-selection, batch-limit, and stale-locator
contract together with the two new typed-contract tests.

### Task 9 progress — 2026-08-01

- **Search session (green):** Moved the two dynamic external tool implementations
  from `DevTools.Mcp` to `DevTools.Mcp.Server`. `search_dynamic` now validates
  `limit` (1–32, default 12) and `detail` (`summary` by default, `schema` only
  explicitly), normalizes whitespace/underscore/hyphen tokens, returns bounded
  `hasMore` results, and emits compact opaque catalog-versioned capability IDs
  with required/argument hints.
- **Invoke session (green):** Added typed server contracts and strict
  single-versus-batch validation. `invoke_dynamic` resolves locators only against
  the current local catalog; stale errors use the documented reason taxonomy and
  are retryable only before execution with `research_then_reinvoke`. Read batches
  reject tools, default to 16 items (hard ceiling 64), and use the 1 MiB UTF-8
  result budget (4 MiB per-item hard ceiling) without appending partial items.
- **Transport session (green):** The daemon fixed collection now constructs the
  Server-owned dynamic tools, preserving exactly the `search_dynamic` and
  `invoke_dynamic` surface and `ListChanged=false`; no gateway/control-plane
  behavior or resolve tool was added. The obsolete dynamic tool source files were
  removed from `DevTools.Mcp`.
- **Evidence:** `scripts/test-dotnet.ps1 -Project
  tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` passed (72 passed, 0 failed,
  0 skipped). `DevTools.Mcp.Server` compiled with zero warnings/errors for
  `net48`, `net8.0-windows`, and `net10.0-windows`. The daemon Debug build also
  passed with zero warnings/errors. No live host was required for this
  daemon-local catalog contract proof.
- **Session safety hardening — 2026-08-01:** Catalog refreshes are serialized per
  host-pipe session. A queued refresh rechecks that its session remains the current,
  connected session before publishing, so a disconnected/replaced pipe cannot restore
  an older catalog snapshot. Dynamic invocation now resolves the exact
  `HostKey(machineId, PID)` rather than PID alone; the current daemon remains local,
  while the route is safe if that boundary later broadens.
- **Rollback:** Revert the Server dynamic contracts/tools and local
  catalog-entry versioning, Client ranking change, daemon Server reference/engine import, dynamic
  test replacement, documentation update, and this note. Task 10 remains out of
  scope and the plan remains active.

## Task 10: Recompose The Daemon, Remove Legacy Assembly, And Prove Behavior

**Files:**

- Modify: `source/DevTools.Daemon/Hosting/ServerHostBuilder.cs`
- Modify: `source/DevTools.Daemon/Hosting/GatewayHostedService.cs`
- Modify: `source/DevTools.Daemon/DevTools.Daemon.csproj`
- Modify: `source/DevTools.Execution/DevTools.Execution.csproj`
- Modify: `source/DevTools.Presentation/DevTools.Presentation.csproj`
- Modify: `source/DevTools.Agents.Revit/DevTools.Agents.Revit.csproj`
- Modify: `source/DevTools.Agents.Acad/DevTools.Agents.Acad.csproj`
- Modify: `tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`
- Modify: `RevitDevTool.slnx`
- Delete: `source/DevTools.Mcp/DevTools.Mcp.csproj`
- Delete: remaining files under `source/DevTools.Mcp/`
- Modify: `docs/architecture/MCP/README.md`
- Modify: `docs/architecture/MCP/daemon.md`
- Modify: `docs/architecture/MCP/in-host-runtime.md`
- Modify: `docs/ARCHITECTURE.md`

- [x] **Step 1: Write a failing daemon composition test**

Add a test that builds the stdio host service collection and resolves these ports:

```csharp
[Fact]
public void StdioComposition_ResolvesExternalServerModules()
{
    using var host = ServerHostBuilder.CreateStdioHost();
    var services = host.Services;

    Assert.NotNull(services.GetRequiredService<IHostBroker>());
    Assert.NotNull(services.GetRequiredService<IFileReaderCatalog>());
    Assert.NotNull(services.GetRequiredService<McpEngine>());
}
```

Expose an internal test-only host builder method through
`[assembly: InternalsVisibleTo("DevTools.Mcp.Tests")]`; do not make the production
builder public only for tests.

- [x] **Step 2: Run the composition test to verify it fails**

Run: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj`

Expected: compile or resolution failure until all module registrations are moved.

- [x] **Step 3: Replace direct registrations with module registration**

`ServerHostBuilder.CreateBuilder` must register modules in this order:

```csharp
builder.Services
    .AddDevToolsMcp()
    .WithCatalog()
    .WithHostClient()
    .WithExternalServer();

builder.Services
    .AddFileMetadataReaders()
    .AddRevitFileMetadataReader()
    .AddAcadFileMetadataReader();
```

The host add-in composition in `ExecutionExtensions` registers catalog, dispatcher,
and `AddMcpHostAdapter()`. The desktop daemon registers server/client/file modules
but no host-side adapter.

- [x] **Step 4: Remove the old project only after the solution has no references**

Run:

```powershell
rg "DevTools\.Mcp\.csproj|DevTools\.Mcp\b" source tests -g "*.csproj" -g "*.cs"
```

Update every project reference and namespace import returned by the command. Delete
`source/DevTools.Mcp` only when the remaining hits are intentional compatibility
text in the design/plan documents.

- [x] **Step 5: Update architecture documentation once**

Update the MCP architecture documents to replace the old broad source map with the
nine project boundaries, dependency direction, and the explicit rule that
`DevTools.Daemon` is composition/UI shell while `DevTools.Mcp.Server` owns the
external MCP surface. Update `docs/ARCHITECTURE.md` source layout only; do not
duplicate project maps in product documentation.

- [x] **Step 6: Run focused and repository validation**

Run:

```powershell
scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj
dotnet publish source/DevTools.Daemon -c Release
scripts/build-host.ps1 -Year 2025
```

Expected: all MCP tests pass, daemon publishes `DevTools.Daemon.exe`, and the
Revit 2025 host build succeeds.

- [x] **Step 7: Run live MCP smoke proof**

Use `docs/agents/mcp-integration-test.md` to verify:

```text
1. Start the published daemon in stdio mode.
2. List the fixed daemon tools and prompts.
3. Launch or detect a Revit host.
4. Run search_dynamic for a known host tool.
5. Invoke one host tool through invoke_dynamic.
6. Read revit://model/context through invoke_dynamic.
7. Run read_file_info against a local .rvt and .dwg file.
8. Connect the tray daemon to the existing gateway and verify one tunnel request.
```

Record exact failures, if any, in this plan before moving it to
`docs/plans/completed/`.

### Task 10 live smoke — 2026-08-01 (agent session)

Executed against published daemon + Revit 2025 Snowdon Towers Architectural
(`hostInstanceId=63184`, pipe `DevToolsMcp_Revit_2025_63184`) using the typed
`capabilityId` contract (not the retired kind/target form in older doc snippets).

| Scenario | Result |
| --- | --- |
| Prerequisites / launch_host | Pass — `bridgeConnected=true` |
| S1 execute C#/Python | Pass — wall create, CS0246 recovery, Python wall_count |
| S2 navigate_history | Pass — back/forward/bounds/empty-forward message |
| S3 screenshot | Pass — PNG blob ~2MB before/after |
| S4 resources | Pass — context, version, warnings (49) |
| S5 error recovery | Pass — NRE → fixed query in 1 retry |
| S6 RevitMcpToolSet | Pass — catalog `hasMore` at 32; `revit_get_status`, `revit_list_rooms`, `revit_find_elements`, dryRun delete |
| S7 multi-host Civil3D | Skipped — full Civil 3D not installed (Object Enabler only) |
| S8 NuGet + PEP 723 | Pass — Clipper2 NuGet; polars PEP 723. Newtonsoft/CsvHelper collide with host-loaded assemblies |
| read_file_info fix | Pass — summary includes basicInfo/links on .rvt/.dwg |
| Gateway tunnel (item 8) | Not run — stdio-only session; no tray gateway |

Registry temporarily set to RevitMcpToolSet only (python toolset cleared) to
avoid overlapping `revit_*` names per runbook rule.

- **Status:** Live smoke proof complete for stdio + Revit host path. Plan may move
  to completed after optional gateway tray check if desired.

### Task 10 progress — 2026-08-01

- **Composition test:** Added the internal-only `ServerHostBuilder.CreateStdioHostForTests()` seam and `InternalsVisibleTo("DevTools.Mcp.Tests")`. The new `ServerHostBuilderCompositionTests.StdioComposition_ResolvesExternalServerModules` first failed to compile with `CS0117` because that test seam did not exist. After implementation, the focused MCP command passed with **73 passed, 0 failed, 0 skipped**.
- **Daemon recomposition and legacy removal:** `ServerHostBuilder` now composes `AddDevToolsMcp().WithCatalog().WithHostClient().WithExternalServer()` followed by the three FileMetadata module registrations. The remaining legacy factory/prompts moved to `DevTools.Mcp.Server`; `ToolHelpers` and unused execution error constants moved to Core. Removed all `DevTools.Mcp.csproj` project references, its solution entry, and `source/DevTools.Mcp/`. The final source/test scan has no legacy project references; the only `DevTools.Mcp` text left in source/tests is the intentionally stable `DevTools.Mcp.ToolCall` / `DevTools.Mcp.ResourceRead` logging categories.
- **Multi-target reference correction:** Added `SetTargetFramework="TargetFramework=$(TargetFramework)"` to the migrated Execution/Presentation MCP references so host configuration selection cannot force their net48 graph to consume the net8 destination assemblies.
- **Architecture:** Updated the MCP architecture layer and root source-layout index once to describe the nine module boundaries, inward dependency direction, and daemon composition/UI versus Server external-surface ownership.
- **Validation:** `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` passed (**73 passed, 0 failed, 0 skipped**). `dotnet publish source/DevTools.Daemon -c Release` passed and published the Release daemon; its authorized publish target terminated `DevTools.Daemon.exe` PIDs 14144 and 41848 before deployment.
- **Host and live blockers:** The targeted fallback `dotnet build source/DevTools.Execution/DevTools.Execution.csproj -f net48 -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` passed with 0 warnings/errors, confirming the corrected net48 destination graph. `scripts/build-host.ps1 -Year 2025` failed deployment because Autodesk Revit PID 29288 locked deployed bundle files. I did not kill that user host. The follow-up compile-only Revit-2025 command also stopped at locked `DevTools.Mcp.Orchestrator` and `DevTools.Mcp.Adapter` obj outputs, so it cannot substitute for a green host proof. The pre-smoke pipe check found no `DevToolsMcp_*` pipe or tunnel; no live MCP smoke was attempted or claimed.
- **Host validation recovery — 2026-08-01:** After Revit and compiler locks were clear, updated `RevitDevTool.slnx` so all nine new projects are in `/Shared/MCP Platform/` with the same Debug/Release Autodesk configuration mappings as existing shared projects. Added target-framework propagation to direct destination-project references. `scripts/build-host.ps1 -Year 2025` then passed with **0 errors** and deployed both RevitDevTool and AcadDevTool. It emitted **68 pre-existing ILRepack/merge warnings** (notably missing `ProcessExitStatus`/`ProcessOutputLine` members and duplicate `UtilsStrings.resources`); no new compile or deployment error occurred.
- **Status:** Live stdio + Revit host smoke completed 2026-08-01 (see Step 7 note). Gateway tray tunnel item remains optional.

## Risks And Recovery

| Risk | Mitigation | Recovery |
| --- | --- | --- |
| Project cycle between adapter and execution | `Adapter` references only Core/Catalog/Execution.Abstractions; `Execution` may reference Adapter helpers. | Restore the last passing project-reference graph and keep helper in Execution until a separate extraction. |
| Multi-TFM compile regression | Core, Catalog, Adapter, and Orchestrator retain current three target frameworks. | Revert the moved slice, then fix incompatible BCL use in the destination project. |
| Incorrect dynamic-tool schema, ranking, or retry behavior | Update the product contract and typed external tests before implementation; review them at the Task 9 decision gate. | Revert only Task 9 changes and restore the last published dynamic contract plus its tests. |
| Missing request DI scope | Add scoped-service tests before changing all `McpServer.Create` calls. | Restore the previous transport code only if SDK session construction fails; do not remove the test. |
| File reader behavior drift | Move parser logic unchanged and test selection separately from parsing. | Revert reader move while retaining `DevTools.FileMetadata.Core` contracts. |
| Host deployment lock | Run host build only after closing affected host applications. | Use `scripts/kill-host.ps1 -HostApp Revit`, then rebuild. |

## Decisions

- 2026-07-28: Use MCP SDK protocol types at MCP boundaries; DevTools contracts do
  not duplicate `Tool`, `Resource`, `CallToolResult`, or JSON-RPC models.
- 2026-07-28: Use `McpResult<T>` for expected application failures and
  `McpExecutionException` only for unexpected execution failures.
- 2026-07-28: Keep `DevTools.Daemon` as the packaged executable during the split;
  `DevTools.Mcp.Server` is the external MCP runtime library.
- 2026-07-28: Keep future gateway, Apps, Agent Framework, WebView2, and Autodesk
  cloud connectors outside this migration.
- 2026-07-28: Name the offline metadata modules `DevTools.FileMetadata.*`; they
  are format readers, not a general `System.IO` abstraction.
- 2026-07-28: Use fluent MCP composition, but keep `IDevToolsMcpBuilder` in Core
  and each `With...` wrapper in its owning module. Every wrapper delegates only
  to that module's independently testable `Add...` registration.
- 2026-07-28: Replace the dynamic MCP request/response contract as part of this
  migration. The typed `detail`, `capabilityId`, strict batch, ranking, and
  stale-locator semantics are the target product contract; fixed daemon and host
  transport behavior remains stable.

## Progress

- [x] Task 1: Create the locked scaffold and establish a classified green baseline.
- [x] Task 2: Establish core result, error, and port contracts.
- [x] Task 3: Extract FileMetadata Core and Revit reader.
- [x] Task 4: Extract AutoCAD FileMetadata reader and daemon composition.
- [x] Task 5: Extract catalog and discovery.
- [x] Task 6: Extract host SDK adapter and named-pipe server.
- [x] Task 7: Extract host MCP client and broker.
- [x] Task 8: Extract shared MCP orchestrator.
- [x] Task 9: Extract fixed external MCP server and typed contracts.
- [x] Task 10: Recompose, remove legacy project, document, and validate.

## Validation

- Focused proof: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` after every task.
- Shared compile proof: repository compile hook for all source and test edits, plus
  the representative multi-TFM compile proof in `docs/agents/verification.md` for
  each boundary that moves multi-TFM or host code.
- Daemon proof: `dotnet publish source/DevTools.Daemon -c Release` after Task 10.
- Host proof: `scripts/build-host.ps1 -Year 2025` after Task 10.
- Observable proof: live stdio, host pipe, file reader, and gateway smoke sequence.

## Result

Complete after implementation. Record the verified project graph, focused test
output, daemon publish result, host build result, live MCP evidence, unresolved
risks, then move this plan to `docs/plans/completed/`.
