# Execution Plan: MCP `run_command`

Date: 2026-11-03

## Status

Completed

## Outcome

Inside `code_mode`, an agent can call the host built-in `run_command(path, entry?)`
to run a DLL entry it just built (Revit `IExternalCommand` full class name, AutoCAD
`TypeFullName.MethodName`) or an entry script. The run uses the same isolation and
execution path as a click in Command Execution. Exact input only; a mismatch runs
nothing.

## Context

- Decision: [0056](../../decisions/0056-mcp-run-command.md)
- Product surface: [0027](../../decisions/0027-mcp-product-surface.md),
  [`docs/product/mcp.md`](../../product/mcp.md)
- Pattern to copy: `source/DevTools.Execution/External/Mcp/BuiltIn/CSharpCodeTool.cs`
  and its registration in `source/DevTools.Execution/ExecutionExtensions.cs`
- Run path: `ICommandDiscovery`, `AssemblyExecutionStrategy`,
  `IScriptExecutionStrategyFactory`, `ScriptExecutionProvider.GetExecutionMode`
- Test pattern: `tests/DevTools.Execution.Mcp.Tests/OpenDocumentToolTests.cs`

## Scope

In scope:

- Shared script entry-mode helper; `ScriptExecutionProvider` uses it.
- `RunCommandTool` (`IBuiltInMcpTool`), `McpSpecKeys.Tool.RunCommand = "run_command"`,
  DI registration.
- Focused tests for the tool.
- Docs: product contract, MCP tools map, integration checklist, test matrix.

Out of scope:

- Command Execution tree, pane busy state, saved paths, search policy.
- Command arguments, dialog suppression.

## Approach

Two tracks run in parallel in this workspace. Their files do not overlap.

### Track A: code (one agent)

Files:

- New `source/DevTools.Execution/Providers/ScriptEntryMode.cs`: a static helper
  that returns the `ExecutionMode` for an entry script path, or none for an
  unsupported file. The rule is the one in `ScriptExecutionProvider` today
  (`_ipy_script.py` → IronPython, `.py` → Python, `.fsx` → FSharp,
  `.csx` → CSharp).
- `source/DevTools.Execution/Providers/ScriptExecutionProvider.cs`: use the
  helper. Behavior unchanged.
- New `source/DevTools.Execution/External/Mcp/BuiltIn/RunCommandTool.cs`.
- `source/DevTools.Mcp.Catalog/Core/Protocol/McpSpecKeys.cs`: add `RunCommand`.
- `source/DevTools.Execution/ExecutionExtensions.cs`: register the tool.
- New `tests/DevTools.Execution.Mcp.Tests/RunCommandToolTests.cs`.

Tool contract (ADR 0056, decisions 2, 5, 6):

- `path` empty, missing file or folder → error, nothing runs.
- `.dll`: `entry` required. Check it with an ordinal comparison against
  `ICommandDiscovery.ParseCommands(path)` `FullClassName`. Match → run through
  `AssemblyExecutionStrategy` (it marshals to `IHostContextExecutor`). No match →
  error naming path and entry, with no suggestions.
- Folder: `path` is the script root (like a Script root in Command Execution,
  so sibling modules and relative imports resolve). `entry` required: the
  script path relative to the folder, or an absolute path inside it. The file
  must exist, lie inside the folder, and match the discovery entry rule
  (`*script.py`, `*_ipy_script.py`, `*script.fsx`, `*script.csx`). Mode from
  `ScriptEntryMode`; `IScriptExecutionStrategyFactory.Create(mode,
  fullScriptPath, folder)`.
- A single script file as `path` → error telling the agent to pass the folder
  as `path` and the script as `entry`; nothing runs.
- Other file types → error.
- Result: status, `message`, exception type and message, elapsed ms. Failure
  prefixes match `CSharpCodeTool` (`McpSpecKeys.Result.Runtime`, `.Rollback`),
  and success appends `ExecutionGuardContext.RollbackSummary` the same way.
  Include the host log file path if an existing API exposes it; if none
  exists, leave it out and record that under Decisions.
- `Destructive = true`, `OpenWorld = true`. Description: the draft in ADR 0056
  decision 6.

### Track B: docs (one agent)

Files:

- `docs/product/mcp.md`: one bullet for `run_command` (inputs, exact match,
  fresh isolation per run, modal-dialog limit).
- `docs/architecture/MCP/tools.md`: a row in the built-in tools table.
- `docs/agents/mcp-integration-test.md`: one live checklist step (build a
  sample command DLL, run it through `code_mode`, check an unknown entry runs
  nothing).
- `docs/agents/test-matrix.md`: the new test class, and the live-only part
  (real Revit/AutoCAD run).

### Track C: integration (coordinator, after A and B)

- Compile and run the tests again, review the diff, live MCP check when a host
  is available, close this plan, move ADR 0056 to Accepted.

## Risks And Recovery

- Both tracks compile nothing in common (Track B is docs only), so there is no
  build lock contention. Only Track A runs `dotnet build`/`dotnet run`.
- A command with modal UI blocks the call: documented limit, not handled.
- Recovery: the change is additive. Remove the registration line to take the
  tool out of the catalog.

## Progress

- [x] ADR 0056 written and renamed.
- [x] Track A: helper, tool, key, registration, tests.
- [x] Track B: docs.
- [x] Track C: diff review, headless re-run (Mcp.Tests 52/52, ScriptEntryModeTests
  16/16), compile Acad 2025 and Revit 2022 (net48) green.
- [x] Track C: live check (Scenario 12) on Revit 2025, plan closed, ADR 0056
  Accepted.

## Decisions

- 2026-11-03: Two parallel tracks, not more. The helper and the tool share
  `DevTools.Execution`, and two agents building it at once would contend for
  `obj/` locks, so they stay in one track.
- 2026-11-03: Scripts run from a folder root with `entry` relative to it, not
  from a single script file, so sibling modules resolve as they do for a
  Script root in Command Execution.
- 2026-11-03: The result has no log file path. No API exposes the current
  host log file: `FileLogProcessor` builds the rolling file name inside the
  ZLogger provider, and only the log folder is in settings. Output stays in the
  log pane and host log.
- 2026-11-03: `ScriptEntryMode` holds both the entry file rule (discovery
  patterns) and the mode rule; `ScriptExecutionProvider` takes its watch
  patterns and modes from it.

## Validation

- Focused proof: `dotnet run --project tests/DevTools.Execution.Mcp.Tests/DevTools.Execution.Mcp.Tests.csproj -- --filter RunCommandToolTests`,
  plus the test project that covers `ScriptExecutionProvider`.
- Compile: `dotnet build source/DevTools.Execution/DevTools.Execution.csproj -c Debug`,
  `source/DevTools.Mcp.Catalog`, and `source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployAddin=false -p:ILRepackable=false`.
- Live: `docs/agents/mcp-integration-test.md` step for `run_command` when a host
  is running.

## Result

Landed 2026-11-03. Verified:

- Headless: `DevTools.Execution.Mcp.Tests` 52/52 (`RunCommandToolTests` 23),
  `ScriptEntryModeTests` 16/16. Compile green for Revit 2025, Revit 2022
  (net48), and AutoCAD 2025.
- Live, Revit 2025 (PID 11384), through daemon `code_mode`, with
  `samples/CSharpDemo` built `Debug.Autodesk.2025`:
  - `SearchAsync("run built IExternalCommand entry from dll")` returned only
    `run_command`, and `DestructiveHint` is true.
  - `CSharpDemo.BatchTraceLogCmd` returned `Succeeded` in 557 ms with no model
    open. The command inherits `ExternalCommand`, so discovery finds an
    inherited `IExternalCommand`. Its trace output went to the host log.
  - `CSharpDemo.NoSuchCmd`, the short name `BatchTraceLogCmd`, and a missing
    `entry` each returned an error, and nothing ran.

Limitations:

- The live check did not run a script folder entry. Script resolution is
  covered by unit tests only.
- The result has no log file path (see Decisions).
- Seen during the check, not caused by this change: both `code_mode` calls
  completed in the daemon (6.5 s and 7.2 s per the daemon log), but the
  claude.ai connector did not receive the MCP Task results. The cause is in the
  task result relay between the gateway and the client.
