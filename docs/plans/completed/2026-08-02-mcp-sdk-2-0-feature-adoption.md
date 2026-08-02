# Execution Plan: MCP C# SDK 2.0 feature adoption

Date: 2026-08-02

## Status

Completed (2026-08-02). Living gap table: [`docs/architecture/MCP/sdk-2-0-gap-matrix.md`](../../architecture/MCP/sdk-2-0-gap-matrix.md).

## Outcome

Adopt remaining **SDK 2.0.0** (`ModelContextProtocol` + `ModelContextProtocol.Extensions.Tasks`)
capabilities that the product stack already depends on but does not fully expose or test:

1. Native multimodal `CallToolResult` end-to-end (single `invoke_dynamic`, not batch JSON).
2. Structured tool output (`OutputSchema` + `StructuredContent`) on daemon fixed tools where JSON text is the main payload.
3. Safe handling of SDK content blocks the adapter currently rejects (`ResourceLinkBlock`).
4. Documented, tested MCP Tasks path for long-running host tools (optional client opt-in).
5. Screenshot defaults sized for remote connectors (1280 px width, Revit **150 DPI** unchanged).

Success is measured by contract tests + integration checklist, not by new external tool names.

## Context

- Package pin: `Directory.Packages.props` → `ModelContextProtocol` **2.0.0**, `ModelContextProtocol.Extensions.Tasks` **2.0.0**.
- Product contract: `docs/product/mcp.md`
- Architecture: `docs/architecture/MCP/tools.md`, `docs/architecture/MCP/README.md`
- SDK reference: sibling `csharp-sdk` repo (`CallToolResult`, `ContentBlock`, `WithTasks`, `UseStructuredContent`).
- Prior work: `invoke_dynamic` single-invoke pass-through for host `CallToolResult` / `ReadResourceResult` (image vision fix).
- Related active plan: `2026-07-26-mcp-agent-efficiency.md` (Phase 1 largely shipped; Tasks deferred there).

### SDK 2.0 surface vs DevTools today

| SDK 2.0 feature | Host adapter | Daemon | `invoke_dynamic` single | Notes |
|-----------------|-------------|--------|-------------------------|-------|
| `TextContentBlock` | ✅ | ✅ | ✅ pass-through | |
| `ImageContentBlock` | ✅ | — | ✅ pass-through | `view_screenshot` |
| `AudioContentBlock` | ✅ | — | ✅ pass-through | No product tool yet |
| `EmbeddedResourceBlock` | ✅ | — | ✅ code path | **No test** |
| `ResourceLinkBlock` | ❌ throw | — | ❌ | Third-party toolsets |
| `ToolUse` / `ToolResult` blocks | ❌ throw | — | ❌ | Sampling-only; not product goal |
| `CallToolResult.IsError` | ✅ | ✅ | ✅ pass-through | |
| `CallToolResult.StructuredContent` | ✅ map | partial | ✅ pass-through | Only `search_dynamic` emits |
| `CallToolResult.Meta` | ✅ map | rare | ✅ pass-through | |
| `Tool.OutputSchema` | parser extracts | none on fixed tools | via `detail=schema` search | |
| `UseStructuredContent` | not used | not used | — | SDK advertises + fills field |
| `Annotations` on content | ✅ round-trip | not set | pass-through | |
| `WithTasks` + extension | configured | configured | — | Default `Optional`; not leveraged |
| `completions` capability | not advertised | not advertised | — | Defer |
| `resources/subscribe` | `Subscribe=false` | — | — | Intentional |

### Screenshot sizing (locked)

Revit `ImageExportOptions` uses both `PixelSize` (width px) and `ImageResolution` (DPI enum).
Lowering width **1920 → 1280** reduces file size and JSON-RPC payload; **DPI stays `DPI_150`**
(default in `ImageExportSettings`). Effective print density is unchanged; fewer pixels sampled.

AutoCAD `CapturePreviewImage` uses explicit pixel dimensions → **1280×720** (16:9, down from 1920×1080).

## Scope

### In scope

**Phase 0 — Screenshot defaults (done in code)**

- Revit `view_screenshot`: `PixelSize = 1280`, `Resolution = DPI_150`.
- Acad `view_screenshot`: `1280×720`, description updated.

**Phase 1 — Contract tests for existing pass-through (≈0.5 session)**

Close test gaps without wire changes:

1. `InvokeDynamic_PassThroughHostReadResourceResult` → `EmbeddedResourceBlock` list.
2. `InvokeDynamic_PassThroughPreservesIsErrorMetaStructuredContent` (fixture returns rich `CallToolResult`).
3. `InvokeDynamic_PassThroughMixedTextAndImageContent`.
4. Stale `docs/architecture/MCP/workflows.md` screenshot path → `view_screenshot` tool.

**Phase 2 — Structured output on daemon fixed tools (≈1 session)**

Use SDK `McpServerToolCreateOptions.UseStructuredContent` + `OutputSchema` (or typed return + SDK schema generation):

| Tool | Return type today | Target |
|------|-------------------|--------|
| `search_dynamic` | Text JSON + `StructuredContent` summary | **Document**: clients should prefer `StructuredContent` for machine parsing; keep text for human/log compatibility. Optionally add `OutputSchema` on wire tool definition. |
| `read_file_info` | `ToolHelpers.Result(FileInfoResult)` text only | Typed return + `UseStructuredContent`; `detail=summary\|full` reflected in schema. Text block may remain as compact JSON **or** drop duplicate text once StructuredContent is stable (decision below). |
| `list_host_instances` | Text JSON | Structured summary (`count`, `instances[]` with pid/app/version only). |
| `invoke_dynamic` errors | Text JSON envelope | Keep text envelope; **do not** add StructuredContent on errors (stale/validation) in this phase. |

Implementation notes:

- Daemon tools use `McpServerTool.Create(handler, options)` — refactor handlers that currently build `CallToolResult` manually to return POCOs where structured output is desired; SDK fills `StructuredContent` and `OutputSchema`.
- `invoke_dynamic` pass-through for host success paths already preserves host `StructuredContent` — no change.
- Update `McpLogPayload` if log shape changes (prefer logging `StructuredContent` when present and non-binary).

**Phase 3 — `ResourceLinkBlock` boundary (≈0.5–1 session)**

SDK `ToAIContent` returns `null` for `ResourceLinkBlock` (client skips). DevTools adapter **throws** today.

Options (pick one in implementation — default **A**):

| Option | Behavior |
|--------|----------|
| **A (default)** | Add `McpResourceLinkContent` to `McpInvocationResponse`; mapper round-trip; `invoke_dynamic` pass-through if host returns link in `CallToolResult`. |
| B | At adapter boundary, resolve link → `EmbeddedResourceBlock` via `ReadResource` (extra I/O; may fail for remote URIs). |
| C | Leave throw but document “toolsets must not return resource_link from tools”. |

Deliverables:

- `McpResourceLinkContent` record + mapper cases.
- Contract test round-trip for `ResourceLinkBlock`.
- Reject or convert policy documented in `docs/architecture/MCP/tools.md`.

**Phase 4 — MCP Tasks for long-running tools (≈1 session)**

Already registered: `AddDevToolsMcp().WithTasks(InMemoryMcpTaskStore)` applies to **daemon and host** via `McpServerConfigurator`.

| Item | Action |
|------|--------|
| Execution mode | Configure `McpTasksOptions.ExecutionModeSelector`: `execute_csharp_code` (and optionally `execute_python_code`) → `Optional`; infrastructure + `search_dynamic` / `invoke_dynamic` → `Synchronous`. |
| Client path | Document: cloud connectors must opt in with `io.modelcontextprotocol/tasks` in `_meta` to get `CreateTaskResult`; otherwise synchronous (current behavior). |
| Host session | **Keep** sync `CallToolAsync` on `HostSession` for catalog invoke — task polling is daemon-client concern, not host-broker concern. |
| Binary results | Integration test: daemon task completes with `ImageContentBlock` in final `CallToolResult` (synthetic fast tool or mock). |
| Product doc | Add Tasks subsection to `docs/product/mcp.md` (extension id, opt-in, which tools are task-capable). |

Out of scope for Phase 4: `CallToolWithPollingAsync` on `HostSession`, gateway changes, required-task mode.

**Phase 5 — Docs, agent guidance, remote limits (≈0.5 session)**

- `docs/product/mcp.md`: structured output consumer rules; Tasks; screenshot 1280/DPI; batch `reads[]` remains JSON-only (no vision).
- `docs/architecture/MCP/tools.md`: content-block matrix; ResourceLink policy.
- `.agents/skills/revit-developer/SKILL.md`: `view_screenshot` 1280, prefer single `invoke_dynamic` for vision.
- Gateway note: `McpGateway` `MAX_BODY_BYTES` = **1 MiB** on **client POST**; large screenshot responses may still work on response path — document practical ~1 MiB JSON budget for cloud clients; 1280 PNG target &lt; ~800 KiB typical.

### Out of scope

- Batch `invoke_dynamic(reads[])` native multimodal pass-through (JSON envelope stays).
- `completions` capability / URI template completion on host.
- `resources/subscribe` and resource update notifications to external clients.
- Sampling (`ToolUse` / `ToolResult` content blocks) and server-side elicitation.
- Cursor / IDE inline image preview (client UI).
- `outputPath` optional parameter on `view_screenshot` (follow-up if users need disk file).
- McpGateway response size limit changes (separate McpGateway repo).
- Python MCP SDK parity (python-sdk repo).

## Decisions (locked)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Screenshot width | 1280 px | Smaller wire payload; vision models sufficient |
| Revit DPI | `DPI_150` | Independent of pixel width; matches `ImageExportSettings` default |
| Acad capture | 1280×720 | Same 16:9 as prior 1920×1080 |
| Single invoke multimodal | Pass-through `CallToolResult` | Shipped; tests in Phase 1 |
| Batch reads | JSON envelope only | Bounded daemon budget; vision uses single tool invoke |
| ResourceLink | Option A unless evidence of need for B | Minimal surprise; matches SDK client skip behavior |
| Tasks default | `Optional` per tool selector | Backward compatible; opt-in for capable clients |
| Structured duplicate text | Phase 2: keep text **until** StructuredContent validated in integration test | Avoid breaking text-only clients |

## Approach

```text
Phase 0 (1280) ──► Phase 1 tests ──► Phase 2 structured daemon tools
                                        │
Phase 3 ResourceLink ◄──────────────────┤
                                        │
Phase 4 Tasks selector + test ◄─────────┘
        │
        ▼
Phase 5 docs + integration re-measure
```

Phase 2 and 3 can run in parallel after Phase 1. Phase 4 depends on stable pass-through tests.

## File touch map (expected)

| Area | Files |
|------|-------|
| Screenshot | `DevTools.Agents.Revit/Tools/ViewScreenshotTool.cs`, `DevTools.Agents.Acad/Tools/ViewScreenshotTool.cs` |
| Tests | `tests/DevTools.Mcp.Tests/DynamicToolsAndObservabilityTests.cs`, `ContractTests.cs`, new `StructuredOutputTests.cs` (optional) |
| Structured tools | `DevTools.Daemon/Mcp/Tools/ReadFileInfoTool.cs`, `ListHostInstancesTool.cs`, `SearchDynamicTool.cs` |
| ResourceLink | `DevTools.Mcp.Core/Contracts.cs`, `CatalogMcpServerToolResponseMapper` |
| Tasks | `DevTools.Mcp.Orchestrator/DevToolsMcpBuilderExtensions.cs` |
| Docs | `docs/product/mcp.md`, `docs/architecture/MCP/tools.md`, `workflows.md`, skill |

## Risks and recovery

| Risk | Mitigation |
|------|------------|
| Structured output breaks Cursor/connector clients that only read `Content` | Keep text JSON in Phase 2; integration test with `mcp-integration-test.md` |
| `ResourceLinkBlock` from catalog DLL crashes host invoke | Phase 3 mapper + test before exposing new toolsets |
| Task + `execute_csharp_code` doubles complexity | Only `Optional`; sync path remains default |
| Screenshot still &gt; 1 MiB on wide schedules | Revit `MaxAspectRatio` clamp already 2.0; document schedule views |
| Recovery | Each phase is independently shippable; revert phase commits if integration fails |

## Progress

- [x] Phase 0: 1280 screenshot defaults (Revit + Acad).
- [x] Phase 1: Pass-through contract tests + workflows doc fix.
- [x] Phase 2: Structured output on `read_file_info`, `list_host_instances`, `search_dynamic` OutputSchema.
- [x] Phase 3: `ResourceLinkBlock` in `McpInvocationResponse` + mapper.
- [x] Phase 4: Tasks `ExecutionModeSelector` + tests.
- [x] Phase 5: Product/architecture docs + skill update.

## Validation

### Focused proof

```powershell
# From RevitDevTool repo root
dotnet test tests/DevTools.Mcp.Tests --filter "FullyQualifiedName~DynamicToolsAndObservabilityTests|FullyQualifiedName~ContractTests"
```

After Phase 2:

```powershell
dotnet test tests/DevTools.Mcp.Tests --filter "FullyQualifiedName~StructuredOutput"
```

### Integration proof

Follow `docs/agents/mcp-integration-test.md`:

1. `search_dynamic` → `view_screenshot` → single `invoke_dynamic` → confirm `image` block, file size &lt; ~1 MiB JSON on wire (log or proxy).
2. `read_file_info` with `detail=summary` → client reads `StructuredContent` fields.
3. (Phase 4) `execute_csharp_code` with tasks opt-in → poll to completion.

### Repository checks

- Cursor compile hook after `.cs` edits (automatic).
- No new external MCP tool names.

## Result

Verified 2026-08-02:

- **43** MCP tests pass (`DynamicToolsAndObservabilityTests`, `ContractTests`, `StructuredOutputTests`, `McpServerConfigurationTests`).
- Single `invoke_dynamic` pass-through: image, embedded resource, mixed content, `IsError`/`Meta`/`StructuredContent`.
- Daemon structured output: `search_dynamic`, `list_host_instances`, `read_file_info` with `UseStructuredContent` + wire `OutputSchema`.
- `ResourceLinkBlock` round-trip via `McpResourceLinkContent`.
- Tasks: `DevToolsMcpTaskExecutionModes` — sync infra/dynamic, Optional `execute_csharp_code`.
- Screenshot defaults: Revit 1280 px @ DPI_150; Acad 1280×720.

**Unresolved:** live integration re-measure (Gateway payload size); Cursor tool-card image preview; batch `reads[]` JSON-only multimodal; end-to-end MCP Tasks poll with real `execute_csharp_code`.
