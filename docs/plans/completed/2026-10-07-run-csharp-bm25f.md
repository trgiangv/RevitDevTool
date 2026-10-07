# Execution Plan: `codemode` and BM25F

Date: 2026-10-07

## Status

Completed 2026-10-08. Wire name is `code_mode` (renamed from `codemode` after this plan). [0040](../../decisions/0040-bm25f-search-and-hit-annotations.md)
accepted 2026-10-07. Waves 0–3 passed. Live Revit 2025 covered search,
invoke, the image block, and the `readOnly: true` rejection. The session
used the already connected host and did not run `kill-host` or `build-host`.

## Outcome

A client calls one daemon tool, `codemode`, with a C# method body. The
body searches the in-memory catalog (BM25F), describes a tool, invokes host
tools, and reads resources. The model receives only the program's return
value. `search_dynamic` and `invoke_dynamic` are gone from `tools/list`.
Host capabilities stay off that list. No host add-in change.

## Context

- Decision: [0040](../../decisions/0040-bm25f-search-and-hit-annotations.md)
- Product to amend in the surface lane: `docs/product/mcp.md`
- Envelope being amended: [0027](../../decisions/0027-mcp-product-surface.md)
- Rank being replaced: [0039](../../decisions/0039-mcp-flow-audit-sdk-reuse-and-vocabulary.md) decision 9
- Code: `source/DevTools.Daemon/Mcp/Search/`, `Mcp/Tools/InvokeTool.cs`,
  `Mcp/Hosting/McpEngine.cs`
- SDK types already returned by `CallToolPassthroughAsync`:
  `CallToolResult`, `ReadResourceResult`, content blocks in
  `ModelContextProtocol.Protocol`
- Proof: `.agents/skills/build/SKILL.md`, `docs/agents/test-matrix.md`,
  live tail only: `docs/agents/mcp-integration-test.md`

The host is another process. Daemon tests can fake the pipe result. A live
Revit process is not on the critical path of the ranker, the compiler, or
the result mapper.

## Scope

In scope:

- BM25F in `SearchIndex` / `Scoring` (k1 1.2, b 0.75, field weights 4/2/1/1,
  no half-match cutoff)
- `CatalogScript`: `SearchAsync`, `DescribeAsync`, `InvokeAsync`, `ReadAsync`
- Roslyn compile of the method body, fixed references, collectible load
- `InvokeAsync` returns SDK `CallToolResult`. `ReadAsync` returns SDK
  `ReadResourceResult`. `DescribeAsync` returns SDK `Tool`
- Outer `CallToolResult` built from the return value, including image,
  audio, and embedded resource blocks
- MCP tool `codemode` (`code`, `readOnly`). Remove `search_dynamic` and
  `invoke_dynamic` from `McpEngine`
- Prompts and `docs/product/mcp.md` that still teach search-then-invoke
- Headless tests listed under Validation

Out of scope:

- Host add-in, host pipe protocol, `execute_csharp`, `CSharpCompiler`
- `#r` / `#load` resolver, NuGet, file or network APIs in the script
- Lucene, ElBruno.BM25, `Microsoft.CodeAnalysis.CSharp.Scripting`
- Synonym table, stemming, `execute_plan`
- A second mirror of `CallToolResult`

## Approach

Work is lanes with disjoint files. A lane starts when its "after" column
is done. Lanes in the same wave do not edit the same file.

The host process is not required until Wave 3. Waves 0–2 run against a
fake `CallToolResult` / `ReadResourceResult`.

```text
Wave 0 (one owner)     seam: CatalogScript abstracts + result rules
        |                    |
        +----------+---------+----------+
        |          |                    |
Wave 1  Rank       Compile              Map
        |          |                    |
        +----------+--------------------+
                       |
Wave 2              Glue, then Surface
                       |
Wave 3              Live host (serial, one process)
```

Inside one program, two `processId`s may overlap. One `processId` stays
serialized on that host's external event. Wave 2 proves the overlap with
two fake sessions, not two Revit processes.

### File ownership

| Lane | May edit | Must not edit |
|------|----------|----------------|
| 0 Seam | new `Mcp/Code/CatalogScript.cs` (abstract methods only), new `Mcp/Code/ProgramResult.cs` (empty mapper signature) | `Search/*`, `McpEngine.cs`, `InvokeTool.cs` |
| 1 Rank | `Mcp/Search/*`, `tests/DevTools.Mcp.Client.Tests/ScoringTests.cs` | `Mcp/Code/*`, `McpEngine.cs` |
| 1 Compile | new `Mcp/Code/CodeModeCompiler.cs`, new compiler tests under `tests/DevTools.Daemon.Tests/` | `Search/*`, `ProgramResult.cs`, `McpEngine.cs` |
| 1 Map | `Mcp/Code/ProgramResult.cs`, new mapper tests under `tests/DevTools.Daemon.Tests/` | `Search/*`, `CodeModeCompiler.cs`, `McpEngine.cs` |
| 2 Glue | method bodies on `CatalogScript.cs` | `SearchIndex` internals, compiler emit, `McpEngine.cs` |
| 2 Surface | `McpEngine.cs`, new `Mcp/Tools/RunCSharpTool.cs`, tool-list tests, prompts, `docs/product/mcp.md`, 0027 loop sentence, 0039 status pointer | `Search/*`, compiler, mapper |

`CallToolPassthroughAsync` stays. Surface unregisters the MCP tool. Glue
calls the session method. Nobody rewrites the pipe.

## Tasks

### Wave 0 — seam (serial, no host)

- [x] Accept 0040, or stop.
- [x] Add `CatalogScript` with abstract `SearchAsync`, `DescribeAsync`,
      `InvokeAsync`, `ReadAsync`, and `CancellationToken`. Signatures match
      0040: search returns name, description, process id, primitiveType; describe
      returns SDK `Tool`; invoke returns SDK `CallToolResult`; read returns
      SDK `ReadResourceResult`.
- [x] Add `ProgramResult.ToCallToolResult(object? value)` signature and the
      return table from 0040 as comments. No behavior yet.

Done when both types compile and nothing calls them.

### Wave 1 — three lanes (parallel, no host)

**Rank**

- [x] Replace the weighted sum and the half-match cutoff with BM25F.
      Fields: target 4, name 2, description 1, parameter names 1.
      `k1 = 1.2`, `b = 0.75`. IDF `ln(1 + (N - df + 0.5) / (df + 0.5))`.
      Distinct query tokens once. Score 0 omits the item. No score on the
      wire. Empty query returns nothing.
- [x] Replace `ScoringTests.Score_TargetMatchDominates_AndHalfTokenCutoffApplies`.
      Cases: a filler token does not drop a one-token hit; a high-df token
      loses to a low-df token; a parameter-name token scores above 0.
      Fix `N`, `df`, and field lengths in the fixture.

**Compile**

- [x] Wrap `code` as the body of `Script.RunAsync` on a `CatalogScript`
      subclass. The model does not write the class.
- [x] Emit with `CSharpCompilation` into a collectible load context.
      References: BCL, `CatalogScript`, `ModelContextProtocol`. No
      metadata resolver, no source resolver, no `#r` parser.
- [x] Tests: empty `code` fails; a body that returns `1` yields 1; a body
      that contains `#r` is a C# syntax error; the compiled assembly can
      be unloaded (collectible context, no leftover static root).

**Map**

- [x] Implement `ProgramResult.ToCallToolResult` for SDK values, not a
      second result type.
      `CallToolResult` passes through. A content block or a list of them
      becomes `Content`. `ReadResourceResult` becomes one
      `EmbeddedResourceBlock` per content, same as `InvokeTool` today.
      `string` becomes one `TextContentBlock`. Any other object becomes
      `StructuredContent` plus one JSON text block.
- [x] Tests, all in memory: image-only `CallToolResult` stays an
      `ImageContentBlock`; audio stays audio; a resource blob stays a
      blob; a projected anonymous object has no image block; over 1 MiB
      (count base64) is an error and the payload is absent.

### Wave 2 — glue, then surface (no host)

Glue starts when Wave 1's three lanes have merged. Surface starts when
Glue's `CatalogScript` can run a body against a fake session.

**Glue**

- [x] `SearchAsync` calls the BM25F index. Default primitiveType tool, limit 8.
      Omitted `processId` searches every connected process. Does not open
      a pipe.
- [x] `DescribeAsync` returns the cached SDK `Tool`, including
      `Annotations`. Null hints stay null. No pipe.
- [x] `InvokeAsync` resolves `(processId, tool, name)` to the current id
      and returns the session's `CallToolResult` unchanged, including
      `IsError`. `readOnly: true` throws unless `ReadOnlyHint` is true.
      A missing hint fails closed.
- [x] `ReadAsync` resolves a resource or template and returns
      `ReadResourceResult`. A tool name throws.
- [x] One `processId`: host calls run one at a time. Two `processId`s:
      `Task.WhenAll` overlaps. Test with two fake sessions and a gate
      that records overlap. A thrown body does not roll back the first
      fake invoke.
- [x] Identical `code` may reuse a compilation. Different source compiles
      again. Cache is not a product field.

**Surface** (one owner; many tests name the old tools)

- [x] Register `codemode` on `McpEngine`. Arguments: `code`, `readOnly`
      (default false). `Destructive = true`, `OpenWorld = true`.
      Description is the instruction in 0040, with `SearchAsync`,
      `DescribeAsync`, `InvokeAsync`, `ReadAsync`, and the SDK type names.
- [x] Remove `search_dynamic` and `invoke_dynamic` from the external
      collection. Rename `list_host_instances` to `list_processes`. Leave
      `list_machines`, `list_processes`,
      `launch_host`, `read_file_info`.
- [x] Update tests that assert the tool list or call `search_dynamic`:
      `ServerHostBuilderCompositionTests`, `TaskSelectionTests`,
      `DynamicToolsAndObservabilityTests`, `StructuredOutputTests`,
      and the server harness. A request that still passes `id` fails
      validation.
- [x] Headless `codemode`: body calls `SearchAsync`, `DescribeAsync`,
      and `InvokeAsync` twice on a fake catalog; the tool result is the
      projection; the fake host payloads are not the tool result.
- [x] `docs/product/mcp.md`, daemon `revit_code` / `acad_code` prompts,
      and `samples/RevitMcpToolSet/Prompts/ToolsetPrompts.cs`: the model
      calls `codemode`. Do not copy the BM25F formula into
      `docs/architecture/`.
- [x] 0027 loop sentence and 0039 status pointer, as 0040 follow-up says.

### Wave 3 — live host (serial)

One host year. Do not start until Wave 2's headless suite is green.
Stop only that year (`scripts/kill-host.ps1`), then `scripts/build-host.ps1`
if the already-deployed add-in is older than the daemon under test. This
plan does not change host code; rebuild only to load the current pipe.

- [x] `codemode` finds a read-only host tool through `SearchAsync` and
      returns a small projection.
- [x] A host tool that returns an image comes back as `ImageContentBlock`
      when the body returns that block.
- [x] `readOnly: true` rejects a tool whose hint is not true.
- [x] Checklist: `docs/agents/mcp-integration-test.md` for the tool list
      change. Record the host year and pid.

## Risks And Recovery

- Two lanes edit `CatalogScript.cs`. Recovery: Wave 0 lands the abstracts;
  Glue is the only later editor. Rank, Compile, and Map do not touch it
  after Wave 0.
- Tool-list tests fail as soon as Surface removes the old names. Recovery:
  Surface owns those tests in the same change. Do not merge Surface
  without them.
- A collectible context that captures a static delegate never unloads.
  Recovery: the compile test asserts unload. Do not cache the delegate
  on a static field; cache the compilation by source text only.
- Live image proof needs a running host. It does not block Waves 0–2.
  Skip it in CI the way other live MCP checks are skipped.
- Rollback: revert the Surface change and the tools reappear. Rank and
  compiler can stay dark if nothing calls them. Do not ship the tool
  list change without Glue.

## Progress

- [x] Wave 0 seam
- [x] Wave 1 Rank
- [x] Wave 1 Compile
- [x] Wave 1 Map
- [x] Wave 2 Glue
- [x] Wave 2 Surface
- [x] Wave 3 live host

## Decisions

- 2026-10-07: Follow 0040. No new product choices in this plan.
- 2026-10-07: Host code stays put. Parallel work is daemon-only until
  Wave 3.

## Validation

- Focused proof: `dotnet test` on `DevTools.Mcp.Client.Tests` (rank) and
  `DevTools.Daemon.Tests` (compile, map, glue, tool list). Server harness
  tests that assert tool names move with Surface.
- Integration: headless `codemode` against a fake session. No live pipe.
- Repository-required checks: compile touched csproj via the build skill
  after each lane merges.
- Live: Wave 3 only. Not a merge gate for Waves 0–2.

## Result

Waves 0–3 match the checked tasks above.

Live session 2026-10-07, without `scripts/kill-host.ps1` or
`scripts/build-host.ps1`: Revit 2025, processId 20936, Snowdon Towers
Sample HVAC. `SearchAsync` listed the host catalog. `InvokeAsync` on
`revit_get_status` returned a small projection (`ReadOnlyHint` true).
Seven read-only tools in one body took about 1.0 s per pass because one
processId runs one host call at a time. `view_screenshot` returned an
`ImageContentBlock` (`image/png`, 1669 ms). With `readOnly: true`,
`execute_csharp_code` (`ReadOnlyHint` null) threw in 0.13 ms:
`'execute_csharp_code' is not marked read-only.` The same body still
called `revit_get_status`. `docs/agents/mcp-integration-test.md` already
names the daemon tool list and marks `search_dynamic` / `invoke_dynamic`
as the previous loop.
