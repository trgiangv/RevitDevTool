# MCP Integration Test Workflow

End-to-end checks an agent runs through MCP against live host processes. Run
this checklist when the Daemon or host MCP wire or tool surface changed and a
host is available.

The Daemon tool list is `list_processes`, `launch_host`, `read_file_info`, and
`code_mode`; the gateway answers `list_machines`. Host tools and resources are
reached from a `code_mode` program. Contract authority: `docs/product/mcp.md`;
patterns: [MCP/workflows.md](../architecture/MCP/workflows.md).

## Calling the host from code_mode

`code_mode` takes `code`, the body of an async C# method, and an optional
`readOnly`. Only the program's `return` value comes back. Inside the program:

- `SearchAsync(query, limit, processId, primitiveType)` ranks catalog entries
  and returns `Name`, `Description`, `ProcessId`, `PrimitiveType`.
  `primitiveType` is `tool` (default), `resource`, or `resource_template`.
- `DescribeAsync(name, processId)` returns the tool's `InputSchema` and annotations.
- `InvokeAsync(name, arguments, processId)` calls a tool and returns its `CallToolResult`.
- `ReadAsync(name, arguments, processId)` reads a resource or resource template.

Tools and resources are addressed by name or URI. Pass `processId` (from
`list_processes` or a search hit) whenever more than one host is connected,
because a name that exists on two processes is rejected as ambiguous.
Arguments can be an anonymous object or a dictionary. Calls to one process run
one at a time inside a program.

Source for `execute_csharp_code` or `execute_python_code` is one C# verbatim
string, with each `"` written as `""`. Take the entry pattern from that tool's
description:

```csharp
var code = @"
<entry pattern from the execute_csharp_code description>
";
var result = await InvokeAsync("execute_csharp_code", new Dictionary<string, object> { ["code"] = code }, 80740);
return new { result.IsError, text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text };
```

Return a projection for text, or return the `ImageContentBlock` /
`ReadResourceResult` itself when the model must see it. Text over 40,000
characters is cut to its start and end, and the reply names the file holding
the full text.

The steps below use a short form: `code_mode: InvokeAsync(...)` means one
`code_mode` program that makes that call and returns its result.

## Prerequisites

1. Build and deploy the target host year. The script stops only that year;
   other running versions stay up.

   ```powershell
   scripts/build-host.ps1 -HostApp Revit -Year 2025
   # or: scripts/build-host.ps1 -HostApp AutoCAD -Year 2026
   ```

   To compile without deploying while that year must stay open:

   ```powershell
   # Revit — pick your year (2022-2027)
   dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployAddin=false -p:ILRepackable=false

   # AutoCAD/Civil3D — pick your year (2022-2027)
   dotnet build source/AcadDevTool/ACadDevTool.csproj -c Debug.Autodesk.2026 -p:DeployAddin=false -p:ILRepackable=false
   ```

2. If anything under `source/DevTools.Daemon/`, `source/DevTools.Mcp.*/`, or
   `source/DevTools.Ipc/` changed since the last publish, publish the Daemon. The
   publish target stops the running Daemon and deploys to
   `%AppData%\...\RevitDevTool.bundle\Contents\`.

   ```powershell
   dotnet publish source/DevTools.Daemon -c Release
   ```

3. Check that the MCP client (Cursor, Claude Code, Codex, GitHub Copilot) points at the Daemon:

   ```json
   {
     "mcpServers": {
       "revitdevtool": {
         "command": "C:\\Users\\{user}\\AppData\\Roaming\\Autodesk\\ApplicationPlugins\\RevitDevTool.bundle\\Contents\\DevTools.Daemon.exe",
         "args": ["--stdio"]
       }
     }
   }
   ```

4. A Daemon publish breaks the stdio connection, so reconnect MCP in the client:
   Cursor (reload in the MCP panel or restart), Claude Code (`/mcp`), Codex
   (restart the session), GitHub Copilot (reload extensions).

## Sample files

### Revit

Path: `C:\Program Files\Autodesk\Revit {Version}\Samples\`, present in 2022–2027 installs.

| File | Size | Use |
|------|------|-----|
| `Snowdon Towers Sample Architectural.rvt` | 97 MB | Large multi-story model (perf, query, CRUD) |
| `Snowdon Towers Sample Structural.rvt` | 28 MB | Beams, columns, foundations |
| `Snowdon Towers Sample HVAC.rvt` | 22 MB | Ducts, fittings |
| `Snowdon Towers Sample Electrical.rvt` | 40 MB | Circuits, panels |
| `Snowdon Towers Sample Plumbing.rvt` | 42 MB | Pipe systems |
| `Snowdon Towers Sample Facades.rvt` | 53 MB | Curtain walls, panels |
| `Snowdon Towers Sample Site.rvt` | 54 MB | Site and topo |
| `BIM_Projekt_Golden_Nugget-Architektur_und_Ingenieurbau.rvt` | 138 MB | Complex mixed use (stress test) |
| `rac_basic_sample_family.rfa` | 0.4 MB | Family editing |

### AutoCAD family

Base samples, present with any AutoCAD-family product, under
`C:\Program Files\Autodesk\AutoCAD {Version}\Sample\`:

| Subfolder | Files | Use |
|-----------|-------|-----|
| `en-us\DesignCenter\` | `Home - Space Planner.dwg`, `House Designer.dwg`, `HVAC*.dwg`, `Kitchens.dwg`, `Landscaping.dwg`, … | 2D layout, block references, layers |
| `en-us\Dynamic Blocks\` | `Architectural - Imperial.dwg`, `Civil - Imperial.dwg`, … | Dynamic blocks, visibility states |
| `Sheet Sets\Architectural\` | `A-01.dwg` to `A-05.dwg`, `Res\*.dwg` | Sheet sets, viewports, details |
| `Sheet Sets\Civil\` | Civil drawings | Plan and profile sheets |
| `Sheet Sets\Manufacturing\` | Mechanical drawings | Manufacturing drawings |
| `Mechanical Sample\` | `Mechanical - Xref.dwg`, `Data Extraction*.dwg`, … | Xrefs, data extraction, tables |
| `Database Connectivity\` | `Floor Plan Sample.dwg` | Database-linked objects |

Civil 3D, only when installed, under `C:\Program Files\Autodesk\AutoCAD {Version}\C3D\Help\Civil Tutorials\Drawings\`:

| Pattern | Count | Use |
|---------|-------|-----|
| `Align-*.dwg` | 15+ | Horizontal alignments |
| `Assembly-*.dwg` | 5+ | Corridor assemblies |
| `Corridor-*.dwg` | 5+ | Corridor modeling |
| `Grading-*.dwg` | 5+ | Grading and earthwork |
| `Profile-*.dwg` | 5+ | Vertical profiles |
| `Surface-*.dwg` | 5+ | TIN surfaces |
| `Pipe-*.dwg` | 5+ | Pipe networks |

Other vertical products have their own subfolder under the AutoCAD install:
`C3D\` (Civil 3D), `PLNT3D\` (Plant 3D, P&ID and 3D piping, may not be
installed), `ACA\` (Architecture, may be empty), `Map\` (Map 3D GIS samples
under `Map\Sample\`, usually bundled with Civil 3D). When a product folder is
missing, use the base `Sample\` folder.

## Dynamic toolset registration

Build the toolsets:

```powershell
# .NET Toolset for Revit
dotnet build samples/RevitMcpToolSet/RevitMcpToolSet.csproj -c Release.Autodesk.2025

# Python Toolset (no build needed — interpreted)
# Revit: samples/PythonDemo/mcp_toolset/
# AutoCAD: samples/PythonDemo/mcp_acad_toolset/
```

Register them in `%AppData%\RevitDevTool\{Version}\Settings\McpRegistryConfig.json`:

```json
{
  "dotnetToolsetPaths": [
    "C:\\Users\\{user}\\source\\repos\\RevitDevTool\\samples\\RevitMcpToolSet\\bin\\Release.Autodesk.2025\\RevitMcpToolSet.dll"
  ],
  "pythonToolsetPaths": [
    "C:\\Users\\{user}\\source\\repos\\RevitDevTool\\samples\\PythonDemo\\mcp_toolset"
  ]
}
```

AutoCAD and Civil 3D use `%AppData%\RevitDevTool\{Version}\Settings\AcadMcpRegistryConfig.json`
(same root, separate file so it does not collide with Revit's):

```json
{
  "dotnetToolsetPaths": [],
  "pythonToolsetPaths": [
    "C:\\Users\\{user}\\source\\repos\\RevitDevTool\\samples\\PythonDemo\\mcp_acad_toolset"
  ]
}
```

Restart the host after editing either file; the registry is not hot-reloaded.

Register one of `PythonDemo/mcp_toolset` and `samples/RevitMcpToolSet` at a
time. Both expose the same `revit_*` names with different schemas (snake_case vs
camelCase arguments), so catalog resolve becomes ambiguous.

## Scenarios

Run mutating calls on one host one at a time (for example, not creating
geometry and calling `navigate_history` concurrently).

### Scenario 1: Built-in code execution

Goal: `execute_csharp_code` and `execute_python_code` succeed and recover from errors.

```
1. launch_host(hostApp="Revit", filePath="C:\Program Files\Autodesk\Revit 2025\Samples\Snowdon Towers Sample Architectural.rvt")
2. code_mode: SearchAsync("execute code")
   Expected: execute_csharp_code and execute_python_code with the Revit processId
3. Execute C# — create wall:
   code_mode: InvokeAsync("execute_csharp_code", {code: <IExternalCommand creating a wall>}, pid)
   Expected: "Wall created" or similar success
4. Execute C# — intentional error (missing using):
   code_mode: InvokeAsync("execute_csharp_code", {code: <code without System.Collections.Generic>}, pid)
   Expected: [COMPILATION ERROR] with CS0246
5. Execute C# — fix and retry
   Expected: Success
6. Execute Python — query elements:
   code_mode: InvokeAsync("execute_python_code", {code: <query all walls, print count>}, pid)
   Expected: Element count printed
7. Execute Python — data analysis with PEP 723:
   code_mode: InvokeAsync("execute_python_code", {code: <script with # /// script requiring polars>}, pid)
   Expected: Package auto-installs, results printed
```

### Scenario 2: Navigate history

Goal: `navigate_history` follows one contract on both hosts.

```
1. Create 3 transactions in Revit (wall, floor, column)
2. code_mode: InvokeAsync("navigate_history", {direction:"back", steps:2}, pid)
   Expected: {navigated: 2, operations: [...], back_remaining: 1, forward_available: 2}
3. code_mode: InvokeAsync("navigate_history", {direction:"forward", steps:1}, pid)
   Expected: {navigated: 1, forward_available: 1}
4. code_mode: InvokeAsync("navigate_history", {direction:"back", steps:99}, pid)
   Expected: Bounded to available stack, undo all
5. code_mode: InvokeAsync("navigate_history", {direction:"forward", steps:99}, pid) on empty forward stack
   Expected: "Nothing to redo. Forward stack is empty."
```

Repeat on Civil 3D, passing that host's `processId`:

```
6. Create entities in Civil 3D (line, circle, polyline)
7. code_mode: InvokeAsync("navigate_history", {direction:"back", steps:1}, civil_pid)
   Expected: {navigated: 1, operations: ["Group of commands"], ...}
```

Revit undo is synchronous and returns exact stack state. AutoCAD undo queues
commands, so its stack counts are estimates.

### Scenario 3: Vision capture

Goal: the `view_screenshot` host tool returns an image an agent can inspect.

```
1. code_mode: var r = await InvokeAsync("view_screenshot", null, pid);
   return r.Content.OfType<ImageContentBlock>().First();
   Expected: PNG image block, 1280 px wide
2. Execute code that changes geometry (add wall)
3. Repeat step 1 → expect a different image
4. Compare: AI confirms visual change occurred
```

Return the `ImageContentBlock` itself; a projected object would carry only text.

### Scenario 4: Resource-driven development loop

Goal: resources, code, verify, undo in one cycle.

```
1. code_mode: SearchAsync("model context", primitiveType: "resource")
   Expected: revit://model/context, revit://model/warnings, revit://version, cheatsheets
2. code_mode: ReadAsync("revit://csharp-cheatsheet", null, pid) (once per session)
3. code_mode: ReadAsync("revit://model/context", null, pid) → get levels, categories, units
4. code_mode: ReadAsync("revit://version", null, pid) → confirm API version
5. Plan: create 3-story building structure
6. Execute C# code (level by level) via code_mode: InvokeAsync("execute_csharp_code", {code:...}, pid)
7. code_mode: InvokeAsync("view_screenshot", null, pid), return the image block → verify
8. If wrong: code_mode: InvokeAsync("navigate_history", {direction:"back"}, pid) → retry
9. code_mode: ReadAsync("revit://model/warnings", null, pid) → check for constraint violations
```

Steps 2 to 4 can be one program that reads all three and returns them together.

### Scenario 5: Error recovery

Goal: measure retries until a recoverable error succeeds (target under 3).

```
1. Execute deliberately wrong code (bad API usage)
   → [RUNTIME ERROR]
2. Read error → fix → retry
   → [COMPILATION ERROR] (missing using)
3. Fix using → retry
   → Success
```

Record retries until success, token cost per retry, and the error categories seen.

### Scenario 6: Dynamic toolset (RevitMcpToolSet)

Goal: the toolset's CRUD, query, MEP, and export tools work. Needs
`McpRegistryConfig.json` set and the host restarted.

```
1. Restart host after McpRegistryConfig.json change → host reloads catalog (`McpPipeServer` / store); Daemon `HostConnections` picks up `list_changed` on refresh
2. code_mode: SearchAsync("revit", limit: 50, processId: pid), return the names
   → expect 32+ revit_* tools from RevitMcpToolSet
3. code_mode: InvokeAsync("revit_find_elements", {filters:{filters:[{type:"category",names:["Walls"]}]}}, pid) → element IDs
4. code_mode: InvokeAsync(<status or info tool>, ...) → parameters / health as applicable
5. code_mode: InvokeAsync(<create or place tool>, ...) → new element when testing mutating tools
6. code_mode: InvokeAsync(<modify tool>, ...) → update when testing mutating tools
7. code_mode: InvokeAsync("revit_delete_elements", {elementIds:[...], dryRun:true}, pid) → preview delete
8. code_mode: InvokeAsync(<export tool>, ...) → file output when testing export
```

Call `DescribeAsync(name, pid)` first when a tool's arguments are not obvious.

### Scenario 7: Multi-host

Goal: routing between Revit and Civil 3D connected at the same time. Civil 3D
runs as `acad.exe` like AutoCAD; the pipe name tells them apart, and `processId`
is the host PID.

```
1. launch_host(hostApp="Revit", filePath=<sample>)
2. launch_host(hostApp="Civil3D", versionNumber="2026")
3. list_processes → confirm both connected (distinct processId / hostApp)
4. code_mode: SearchAsync("execute code") → the same names once per host, each with its processId
5. code_mode: InvokeAsync("execute_csharp_code", {...}) without processId
   Expected: error that the name matches more than one process
6. Execute on Revit: code_mode: InvokeAsync("execute_csharp_code", {...}, revit_pid)
7. Execute on Civil3D: code_mode: InvokeAsync("execute_python_code" or "execute_csharp_code", {...}, civil_pid)
8. navigate_history on each host independently, passing that host's processId
```

Large models (over 50 MB) take 30–60 s to open; the `launch_host` timeout allows for it.

### Scenario 8: NuGet and PEP 723 packages

Goal: external dependencies resolve in both languages. Both run through
`code_mode` like Scenario 1.

```
# C# — NuGet
execute_csharp_code with: #r "nuget: Newtonsoft.Json, 13.0.3"
Expected: Downloads, compiles, runs

# Python — PEP 723
execute_python_code with:
  # /// script
  # dependencies = ["polars>=1.0"]
  # ///
  import polars as pl
  ...
Expected: Auto-installs polars, executes
```

### Scenario 9: Resource templates

Goal: parameterized `resource_template` discovery, a single read, and several
reads in one program. Needs `RevitMcpToolSet` registered, the host restarted,
and Snowdon Towers (or similar) open with known element IDs.

```
1. code_mode: SearchAsync("element", primitiveType: "resource_template")
   Expected: hit named revit://element/{elementId}
2. code_mode: ReadAsync("revit://element/{elementId}", {elementId: <known_wall_id>}, pid)
   Expected: application/json with id, category, level, boundingBox
3. code_mode: SearchAsync("schedule", primitiveType: "resource_template")
   Expected: hit named revit://schedule/{scheduleId}/preview
4. code_mode: ReadAsync("revit://schedule/{scheduleId}/preview", {scheduleId: <known_schedule_id>}, pid)
   Expected: text/csv with header row + preview rows
5. Several reads (fixed + template) in one code_mode program:
   var caps = await ReadAsync("revit://toolset/capabilities", null, pid);
   var selection = await ReadAsync("revit://model/selection", null, pid);
   var element = await ReadAsync("revit://element/{elementId}", new { elementId = <known_id> }, pid);
   return new { caps, selection, element };
   Expected: three results in one response
```

Template arguments use the template's parameter names (`elementId`,
`scheduleId`). Catalog names are resolved on every call, so a refreshed catalog
needs no new search.

### Scenario 10: Bulk delete warning (dryRun)

Goal: `revit_delete_elements` returns a structured warning and a `dryRun`
preview for bulk delete. The samples use a warning-first policy aligned with
Python, so the call returns a normal result rather than asking for input. Needs
a model with deletable elements, or synthetic IDs in an empty model.

```
1. code_mode: SearchAsync("delete elements") → revit_delete_elements
2. code_mode: InvokeAsync("revit_delete_elements", { elementIds: [<51+ ids>], dryRun: false }, pid)
   Expected: CallToolResult with structured warning, deleted_count=0, short text summary (not empty)
3. code_mode: InvokeAsync("revit_delete_elements", { elementIds: [<ids>], dryRun: true }, pid)
   Expected: structured preview (dryRunResults / count), no mutation
```

With `UseStructuredContent=true` the text is one summary line and the full
payload is in `structuredContent`. Return the `CallToolResult` itself to see
both; `code_mode` counts the structured JSON toward the 40,000-character text limit.

### Scenario 11: Low-level MRTR (no live step)

Goal: an isolated .NET toolset can return `input_required` through the host
hop. There is no live step for it. `code_mode` has no way to hand input back
to a host tool, so an `InputRequiredResult` from the host ends the program with
"Host tools/call returned InputRequiredResult." MRTR is not a product workflow
([0027](../decisions/0027-mcp-product-surface.md)); the `test_mrtr_confirm`
stub and the bridge mapping are covered headlessly in
`tests/DevTools.Mcp.Catalog.Tests` (`DotnetToolsetMrtrStubs`,
`PythonToolsetMrtrBridgeTests`).

### Scenario 12: Run a built command (`run_command`)

Goal: `run_command` runs an exact DLL entry through the Command Execution tree,
runs nothing on an unknown entry, and lists and removes roots
([0056](../decisions/0056-mcp-run-command.md), [0057](../decisions/0057-command-execution-shared-by-panel-and-mcp.md)).

Needs Revit open (a document is optional for `BatchTraceLogCmd`). Build the
sample for that year with `dotnet build samples/CSharpDemo -c Debug.Autodesk.<Year>`
and pass the absolute path of `samples/CSharpDemo/bin/Debug.Autodesk.<Year>/CSharpDemo.dll`.

```
1. code_mode: SearchAsync("run_command") → InvokeAsync with
   {path: "<...>\CSharpDemo.dll", entry: "CSharpDemo.BatchTraceLogCmd"}
   Expected: Succeeded with elapsed ms; trace lines in the log pane and host log;
   CSharpDemo.dll appears once in the Command Execution tree, BatchTraceLogCmd marked last executed
2. Same call with entry: "CSharpDemo.NoSuchCmd"
   Expected: error naming path and entry, no suggestions, no new log output
3. {action: "list"}
   Expected: roots include CSharpDemo.dll with CSharpDemo.BatchTraceLogCmd among its entries
4. {action: "remove", path: "<...>\CSharpDemo.dll"}
   Expected: Removed; the root leaves the panel and is not reloaded after a host restart
5. {action: "add", path: "<folder with hello_ipy_script.py>"} twice
   Expected: Added, then AlreadyLoaded; then {path: <folder>, entry: "hello_ipy_script.py"} Succeeded
6. Create later_ipy_script.py in that folder, then run it without add
   Expected: Succeeded (file watcher or reload-on-miss picks it up)
```

Each root change is written to `ExecutionConfig.json` at once, from both the
panel and MCP, so roots survive a host that `scripts/build-host.ps1` stops.

For AutoCAD, use a command DLL you built and pass `entry` as `TypeFullName.MethodName`.

### Scenario 13: Register and reload a toolset (`add_tools`)

Goal: `add_tools` registers, reloads after a rebuild, and removes a toolset
([0058](../decisions/0058-mcp-toolsets-shared-by-panel-and-add-tools.md)).

Needs Revit open. Build `samples/McpToolsetDemo -c Debug.Autodesk.<Year>` (it
merges its own MCP SDK, [0019](../decisions/0019-ilrepack-and-polyfill-isolated-alc.md)
S1) and copy the DLL to a scratch folder.

```
1. {path: "<scratch>\McpToolsetDemo.dll"}
   Expected: Added with ping_infrastructure among tools; path in McpRegistryConfig.json at once
2. Next code_mode program: InvokeAsync("ping_infrastructure"), then "test_forwarder_calltoolresult"
   Expected: "pong", then "forwarder-spike-ok" (the same program as step 1 reports the tool as not found: the
   daemon has not re-listed yet)
3. Change PingInfrastructure's return value, rebuild, copy over the scratch DLL, repeat step 1
   Expected: Reloaded; the DLL was not locked; the next program returns the new value
4. {action: "remove", path: <same>}, then {action: "list"}
   Expected: Removed; list no longer holds it; a host restart does not reload it
```

## Order for a full pass

1. Prerequisites: build, publish, connect.
2. Scenario 1, code execution.
3. Scenario 2, navigate history.
4. Scenario 3, vision capture.
5. Scenario 4, development loop.
6. Scenario 5, error recovery.
7. Scenario 6, dynamic toolset (config and restart).
8. Scenario 7, multi-host routing.
9. Scenario 8, package install.
10. Scenario 9, resource templates (config and restart).
11. Scenario 12, `run_command` exact and unknown entry.
12. Repacked-host toolset identity: register `McpToolsetDemo` and invoke
    `test_forwarder_calltoolresult`; the `CallToolResult` must bridge without
    foreign-type errors. This is a live step because the MSTest testhost ships
    `ModelContextProtocol.Core` as a sibling DLL, so `McpMergedHostIdentityTests`
    in `DevTools.Mcp.Catalog.Tests` reports Inconclusive there
    ([0019](../decisions/0019-ilrepack-and-polyfill-isolated-alc.md)).

## Measuring

Prefer the agent's own token metrics: Cursor (billing dashboard and chat
header), Claude Code (`--usage` or session stats), Codex (`usage.total_tokens`
in the API response), GitHub Copilot (activity log). Otherwise estimate about
one token per 3.5 characters of input plus output JSON; screenshots cost no text
tokens.

```
| Scenario | Tool Call | Input tokens | Output tokens | Total | Retries |
|----------|-----------|-------------|---------------|-------|---------|
| S1.3     | execute_csharp_code | ~600 | ~200 | ~800 | 0 |
```

For each run, report total session tokens, tool calls (success and failed),
retries per scenario, wall-clock time, and retries divided by total calls
(target under 0.3).

`revit://model/context` latency: after a host deploy, open Snowdon Towers and
run three sequential `code_mode` programs that each call
`ReadAsync("revit://model/context", null, pid)` (no parallel calls). Take `durationMs` from the Daemon or host `resources/read` log lines and
check that element counts match earlier output for the same model.

Targets:

| Measure | Target | Acceptable |
|---------|--------|------------|
| `revit://model/context` cold read (host side, excludes Daemon MCP connect) | < 2000 ms | |
| `revit://model/context` warm read | < 500 ms each | |
| `find_elements` text summary on the structured path | < 120 chars | |
| One program reading 3 resources (capabilities, context, selection) | < 4 KiB JSON | |
| Compilation error recovery | 1 retry | 2 retries |
| Runtime error recovery | 1–2 retries | 3 retries |
| Rollback and retry | 1 retry | 2 retries |
| Undo precision | Exact count | ±1 overshoot |
| Visual verify confidence | 1 screenshot | 2 screenshots |

## Troubleshooting

| Symptom | Cause and fix |
|---------|---------------|
| Build fails on locked DLLs | The host year is running. Use `scripts/build-host.ps1`, which stops that year first. |
| `list_processes` empty after a host rebuild | The add-in may have thrown at startup. Read the newest `%APPDATA%\RevitDevTool\{Year}\Logs\crash_*` before suspecting MCP or the Daemon ([verification.md](verification.md#diagnostic-logs)). |
| Tools gone after a Daemon publish | The publish stopped the running Daemon; reconnect MCP in the client. |
| Registry change has no effect | Restart the host. |
| Client shows tools other than the four Daemon tools | Its tool schema cache is stale after a Daemon publish. Trust the live `tools/list` (`code_mode` takes `code` and `readOnly`) and force a full MCP reconnect. |
| `code_mode` says a name matches more than one process | Two connected hosts expose that name. Pass `processId`. |
