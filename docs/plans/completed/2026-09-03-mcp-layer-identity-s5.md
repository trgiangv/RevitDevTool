# Execution Plan: MCP layer consolidation + identity (S5 → strategy gate)

Date: 2026-09-03

## Status

Completed 2026-09-03 — S5 A–D plus listed follow-ups landed. S1/S2 packaging
and remaining SDK-free host contracts are **follow-on**, not this plan.

## Outcome

1. **S5 (this plan):** One owner for MCP ALC identity work (`Catalog/Isolation/`), symmetric
   input/output bridge, ADR text matches code, tests that can fail on merged host, cheap
   dependency/dead-code cleanup. No packaging or kernel change yet.
2. **Strategy gate (human):** Choose S2 (`DevTools.Mcp.Toolset.Abstractions` + `RepackBinariesKeep`)
   vs S1 (toolsets ship MCP; bridge is permanent design). Blocks P1 layer collapse.
3. **P1 (follow-on plan):** `McpInvocationResponse` shrink, Adapter wire split, `Execution ↛ Adapter`.

## Context

- Architecture review: Opus 5 (`082e44a2-2587-4ed8-8c99-e49969e712ea`), 2026-09-03.
- Rollback: `McpMergedContractBinder`, `WithManagedBind`, foreign-bridge removal reverted.
- Rejected permanently (unless new ADR): Pin aliases, TypeForwardedTo stub siblings, stop host
  ILRepack of MCP, kernel merged-contract bind without 0023 amendment.
- ADRs: [0019](../../decisions/0019-ilrepack-and-polyfill-isolated-alc.md),
  [0023](../../decisions/0023-shared-assembly-isolation-kernel.md),
  [0027](../../decisions/0027-mcp-product-surface.md).
- Build: `.agents/skills/build/SKILL.md`.

Orchestrator owns this file (Progress/Decisions). Slices must not edit it.

## Scope

In scope (S5):

- `Catalog/Isolation/` module (foreign types, isolation plan, metadata path collector).
- Symmetric bridge: input `ToolsetInvocationServices` + output serializers.
- ADR/doc factual corrections (identity policy gap).
- Merged-host test fixture (can fail when bind is broken).
- Cheap wins: `SdkInvocationMapper.ToCore`, legacy MRTR meta branch, dispatcher dead work,
  `Execution → Adapter` inversion (Python payload move).

Out of scope (S5):

- `McpInvocationResponse` → `CallToolResult` collapse (P1).
- Toolset packaging change (`ExcludeAssets=runtime` flip) — strategy gate.
- `DevTools.Mcp.Toolset.Abstractions` new project — strategy gate.
- Kernel `AssemblyIsolationPlan` API changes.
- Daemon publish / host deploy.

## Approach

**Fan-out order: S5-A ∥ S5-B ∥ S5-C → S5-D → gate → P1 plan.**

```text
S5-A Docs          ADR 0019/0027 + platform-boundaries (read-only truth)
S5-B Cheap wins    dead code + Python move + dispatcher (no Isolation/ yet)
S5-C Isolation     Catalog/Isolation/ extract + symmetric bridge
S5-D Tests         merged-host fixture + packaging test honesty
        ↓
   Strategy gate (S1 vs S2)
        ↓
   P1 layer split (separate plan)
```

### S5-A. ADR and architecture doc corrections

May write: `docs/decisions/0019-ilrepack-and-polyfill-isolated-alc.md` (§7, Consequences),
`docs/decisions/0027-mcp-product-surface.md` (§5, §7 narrow), `docs/architecture/MCP/platform-boundaries.md`
(toolset load boundary, Adapter layer table).

- State: host ILRepack removes `ModelContextProtocol.Core` assembly identity; `Pin` is name-keyed;
  documented bind is **not implemented** in isolation kernel.
- State: foreign bridge is required when toolset resolves a **private** MCP copy OR when documented
  `ExcludeAssets=runtime` path cannot resolve MCP on repacked host.
- Narrow 0027: no `McpServer` **session on pipe**; `RequestFactory` + `ToolExecutionTransport` for `RequestContext` is allowed.
- Do not claim a strategy is chosen; document S1/S2 as open gate.

Proof: doc diff only; link to this plan.

### S5-B. Cheap wins (no Isolation folder yet)

May write:

- `source/DevTools.Mcp.Adapter/Bridging/SdkInvocationMapper.cs` — delete `ToCore` only.
- `source/DevTools.Mcp.Catalog/Discovery/ToolsetMrtrBridge.cs` — delete legacy `Meta["devtools.inputRequired"]` branch.
- `source/DevTools.Execution/External/Mcp/Dispatchers/McpPrimitiveDispatcher.cs` — move `PythonInvocationPayload.ToJson` into Python branch; collapse duplicate MRTR catches.
- `source/DevTools.Mcp.Adapter/Execution/PythonInvocationPayload.cs` + `PythonResultParser.cs` →
  `source/DevTools.Execution/External/Mcp/Python/` (or equivalent); update `.csproj` refs;
  remove `DevTools.Execution` → `DevTools.Mcp.Adapter` project reference.
- Tests: remove/update callers of `ToCore`; trim `AlcInputRequiredBridgeTests` legacy meta test if branch deleted.

Must not touch: `ToolsetResultSerializer` foreign branches, `McpToolsetIsolationPlan`, isolation kernel.

Proof:

```powershell
dotnet build source/DevTools.Mcp.Adapter/DevTools.Mcp.Catalog.csproj -c Debug
dotnet build source/DevTools.Execution/DevTools.Execution.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
dotnet run --project tests/DevTools.Mcp.Tests -p:SelfContained=false -- --filter "ContractTests|AlcInputRequiredBridgeTests|PythonToolsetMrtrBridgeTests|PythonResultParser|McpJsonRpcTests|ToolsetInvokerTests"
```

### S5-C. Catalog `Isolation/` module (ONE owner)

May write under `source/DevTools.Mcp.Catalog/Isolation/`:

| File | Responsibility |
|------|----------------|
| `McpToolsetIsolationPlan.cs` | move from Discovery |
| `McpToolsetContext.cs` | move from Discovery |
| `McpToolsetContextManager.cs` | move from Discovery |
| `MetadataAssemblyPathCollector.cs` | move from Discovery |
| `ForeignMcpTypes.cs` | all `IsForeign*` + `IsHostContract(Type)` |
| `ForeignMcpReader.cs` | reflection reads, `BagObject`, PascalCase tolerance |
| `README.md` | why folder exists; ADR ownership |

May update:

- `ToolsetResultSerializer.cs` — delegate foreign paths to `ForeignMcpReader`; keep host `MapHost*` temporarily.
- `ToolsetMrtrBridge.cs` — delegate foreign exception path to `ForeignMcpReader`.
- `ToolsetInvocationServices.cs` — use `ForeignMcpTypes.IsHostContract` instead of `== typeof`.
- `ToolsetArgumentBinder.cs` — input-side foreign service injection if needed for symmetric MRTR.
- Namespace: `DevTools.Mcp.Catalog.Isolation`; keep `using` aliases or re-export from Discovery if needed for minimal call-site churn.

Must not: change `AssemblyIsolationPlan`, ILRepack targets, toolset csproj packaging.

Proof:

```powershell
dotnet build source/DevTools.Mcp.Catalog/DevTools.Mcp.Catalog.csproj -c Debug
dotnet run --project tests/DevTools.Mcp.Tests -p:SelfContained=false -- --filter "McpToolsetIsolationTests|AlcCallToolResultBridgeReproTests|AlcInputRequiredBridgeTests|ToolsetResultSerializerTests|ToolsetInvokerTests"
```

### S5-D. Test honesty (merged host)

May write:

- `tests/DevTools.Mcp.Tests/Isolation/McpMergedHostIdentityTests.cs` (or extend
  `DevTools.AssemblyIsolation.Tests` with MCP-specific fixture).
- Fixture: ILRepack two small test assemblies OR load built `RevitDevTool.dll` from
  `bin/Debug.Autodesk.2025` when present; assert `McpToolsetIsolationPlan` share key behavior.
- Update `McpSharedRuntimePackagingTests`: split **layout** tests from **identity** tests;
  identity test must `Skip` or `Fail` with clear message when run in xunit process without merged host
  (document false-green finding from review).
- Optional: `[Trait("Category", "HostIdentity")]` for live host checklist.

Must not: change samples packaging (`ExcludeAssets=runtime`) until strategy gate.

Proof:

```powershell
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false
dotnet run --project tests/DevTools.Mcp.Tests -p:SelfContained=false -- --filter "McpMergedHostIdentityTests|McpSharedRuntimePackagingTests"
```

## Strategy gate (human, blocks P1)

| Option | Summary | Identity | Packaging change |
|--------|---------|----------|------------------|
| **S2** | `DevTools.Mcp.Toolset.Abstractions` + `RepackBinariesKeep` | CLR exact for new dialect | Host keeps abstractions DLL beside merge |
| **S1** | Toolsets copy-local MCP | Foreign bridge = design | `ExcludeAssets=runtime` removed from samples |

Record decision in this plan **Decisions** before opening P1 plan.

## P1 preview (out of scope until gate)

- Delete `McpInvocationResponse` / `McpContent` (0027 §8 amendment).
- Move `InvocationResponseEncoder` + `SdkInvocationMapper.ToSdk` → `Adapter/Wire/`.
- `IMcpCatalogReader` port; drop concrete `McpCatalogStore` from Adapter.
- Rename `Core.Results.McpErrorCode` → `McpFailureCode`.

## Risks and recovery

| Risk | Mitigation |
|------|------------|
| S5-C + S5-B merge conflict on `ToolsetMrtrBridge` | B only deletes legacy meta; C owns file structure |
| False-green tests remain | S5-D explicit skip/fail + trait |
| S2/S1 undecided blocks packaging tests | Gate section; S5-D tests layout vs identity separately |
| net48 isolation asymmetry | Run `DevTools.AssemblyIsolation.NetFramework.Tests` after S5-C |

Recovery: revert slice branch; S5 has no kernel or packaging change.

## Progress

- [x] Architecture review (Opus 5).
- [x] Execution plan (this file).
- [x] S5-A Docs ([Docs ADR fixes](c19763fb-4ae8-410b-91e9-45b8ecd29444)).
- [x] S5-B Cheap wins ([Dead code + Python move](3664ff80-dd82-4ac2-98f4-b9f3aa4ac98b)) — 48/48 filtered tests; `Execution → Adapter` csproj ref deferred to P1.
- [x] S5-C Isolation module ([Catalog/Isolation](6000ef0b-da5b-426b-9bc2-dc885eefe01d)) — 32/32 filtered tests; input-side `ForeignMcpWriter` deferred.
- [x] S5-D Test honesty ([Merged host tests](35220f6e-78e9-4de3-b5f4-ceff61633aee)) — 4 pass, 2 skip (xunit false-green documented); live checklist item 11 added.
- [ ] Strategy gate (S1 vs S2) — **human; follow-on**, not this plan.
- [ ] P1 layer split — **follow-on plan**, not this plan.
- [x] Follow-up: split the former dispatcher into a thin router and source-specific
  .NET, Python, and built-in backends.
- [x] Follow-up: fold Python request/result helpers into `PythonMcpToolBackend`.
- [x] Follow-up: replace `ForeignMcpTypes` + `ForeignMcpReader` vocabulary with one
  shape-validating `McpReader` boundary.
- [x] Follow-up: remove `Execution → Adapter`; host compositions register the host
  adapter explicitly.
- [ ] Replace remaining SDK types in Core/Catalog/Adapter host contracts — **follow-on**, not this plan.

## Decisions

- 2026-09-03: S5 before strategy gate — consolidate owner + honest tests/docs without
  committing to S1 or S2 packaging.
- 2026-09-03: Rejected kernel bind and forwarder stubs (user).
- 2026-09-03 (S5-B): Python payload/parser live in `DevTools.Execution.External.Mcp.Python`.
  Full `Execution ↛ Adapter` inversion blocked on `DotnetMcpServerFactory` + `DevToolsPipeServer` in Adapter — P1.
- 2026-09-03 (S5-C): `Catalog/Isolation/` owns foreign MCP identity (output + MRTR read path).
  Symmetric input (`ForeignMcpWriter` / foreign `RequestContext` params) deferred — host contracts injected as host CLR types only.
- 2026-09-03 (follow-up, user authority): S1/S2 packaging does not define the host
  contract. Host contracts will be SDK-free; reflection is confined to the isolated
  .NET toolset backend. “Foreign” is removed from production vocabulary.

## Validation

Repository-required:

```powershell
dotnet build source/DevTools.Mcp.Catalog/DevTools.Mcp.Catalog.csproj -c Debug
dotnet build source/DevTools.Mcp.Adapter/DevTools.Mcp.Adapter.csproj -c Debug
dotnet build source/DevTools.Execution/DevTools.Execution.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
dotnet run --project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj -p:SelfContained=false -- --filter "McpToolsetIsolationTests|AlcCallToolResultBridgeReproTests|ToolsetResultSerializerTests|ToolsetInvokerTests|McpSharedRuntimePackagingTests|McpMergedHostIdentityTests|ContractTests|PythonToolsetMrtrBridgeTests"
dotnet run --project tests/DevTools.AssemblyIsolation.Tests/DevTools.AssemblyIsolation.Tests.csproj -p:SelfContained=false
```

Live (after S5-D): add scenario to `docs/agents/mcp-integration-test.md` — toolset `CallToolResult` on repacked host.

## Result

S5 landed across four slices (A–D). Docs state the ADR 0019 identity gap; `Catalog/Isolation/`
owns foreign MCP detection/bridge; cheap wins removed dead mapper paths; tests no longer
false-green on repacked-host identity in xunit (2 explicit skips + live checklist item 11).

**Proof (orchestrator, 2026-09-03):** combined filter **61 passed, 2 skipped, 0 failed**;
net48 `McpToolsetIsolationPlan` **1/1**.

**Open:** strategy gate S1 vs S2; SDK-free host contracts; reflected request writing
for isolated .NET toolsets.

### Follow-up proof (2026-09-03)

- `DevTools.Execution` Debug multi-TFM: pass, 0 warnings.
- `DevTools.Mcp.Adapter` Debug multi-TFM: pass, 0 warnings.
- `DevTools.Daemon` Debug: pass, 0 warnings.
- `RevitDevTool` Debug.Autodesk.2025 compile-only: pass, 0 warnings.
- `AcadDevTool` Debug.Autodesk.2025 compile-only: pass, 0 warnings.
- MCP backend/isolation/parser/handler filters: 63 passed.
- Host wire/named-pipe/catalog/daemon-invoke filters: 47 passed.
- Execution DI boundary: 1 passed.

The earlier open item `Execution ↛ Adapter` is now complete. Remaining work is the
larger SDK-free contract migration across Core/Catalog/Adapter; this plan deliberately
does not claim that package removal from the host closure is complete.
