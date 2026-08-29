# MCP Integration Test Workflow

Comprehensive end-to-end testing guide for AI agents operating through MCP against live host processes.

---

## Prerequisites Checklist

Before running any MCP test, verify each step in order:

### 1. Stop Only The Target Host Year

```powershell
# Pick the same product/year used in step 2. Other versions stay running.
scripts/kill-host.ps1 -HostApp Revit -Year 2025
# or: scripts/kill-host.ps1 -HostApp AutoCAD -Year 2026
```

### 2. Build Add-in (Latest Code)

```powershell
# Revit — pick your year (2022-2027)
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025

# AutoCAD/Civil3D — pick your year (2022-2027)
dotnet build source/AcadDevTool/AcadDevTool.csproj -c Debug.Autodesk.2026
```

### 3. Publish Daemon (If Code Changed)

Check if any files under `source/DevTools.Daemon/`, `source/DevTools.Mcp.*/`, or `source/DevTools.Ipc/` have changed since last publish:

```powershell
dotnet publish source/DevTools.Daemon -c Release
```

The publish target auto-kills the running Daemon and deploys to `%AppData%\...\RevitDevTool.bundle\Contents\`.

### 4. Verify MCP Client Configuration

Ensure the AI coding agent (Cursor, Claude Code, Codex, GitHub Copilot) has an MCP entry pointing to the Daemon:

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

### 5. Reload MCP in Client

After Daemon publish, the stdio connection is broken. Reload/restart MCP in the client:

- **Cursor**: Click reload in MCP panel or restart the IDE
- **Claude Code**: `/mcp` command to reconnect
- **Codex**: Restart the session
- **GitHub Copilot**: Reload extensions

---

## Sample Files

### Revit (All Versions 2022–2027)

Path: `C:\Program Files\Autodesk\Revit {Version}\Samples\`

Verified present on all installed versions (2022–2027):

| File | Size | Use Case |
|------|------|----------|
| `Snowdon Towers Sample Architectural.rvt` | 97 MB | Large multi-story arch model (perf, query, CRUD) |
| `Snowdon Towers Sample Structural.rvt` | 28 MB | Structural elements (beams, columns, foundations) |
| `Snowdon Towers Sample HVAC.rvt` | 22 MB | MEP systems (ducts, fittings) |
| `Snowdon Towers Sample Electrical.rvt` | 40 MB | Electrical systems (circuits, panels) |
| `Snowdon Towers Sample Plumbing.rvt` | 42 MB | Pipe systems |
| `Snowdon Towers Sample Facades.rvt` | 53 MB | Curtain walls, panels |
| `Snowdon Towers Sample Site.rvt` | 54 MB | Site/topo elements |
| `BIM_Projekt_Golden_Nugget-Architektur_und_Ingenieurbau.rvt` | 138 MB | Complex mixed-use (stress test) |
| `rac_basic_sample_family.rfa` | 0.4 MB | Family editing workflow |

### AutoCAD Family

#### Base AutoCAD Samples (All Products)

Path: `C:\Program Files\Autodesk\AutoCAD {Version}\Sample\`

Always present with any AutoCAD-family product installation:

| Subfolder | Files | Use Case |
|-----------|-------|----------|
| `en-us\DesignCenter\` | `Home - Space Planner.dwg`, `House Designer.dwg`, `HVAC*.dwg`, `Kitchens.dwg`, `Landscaping.dwg`, etc. | 2D layout, block references, layers |
| `en-us\Dynamic Blocks\` | `Architectural - Imperial.dwg`, `Civil - Imperial.dwg`, etc. | Dynamic blocks, visibility states |
| `Sheet Sets\Architectural\` | `A-01.dwg` through `A-05.dwg`, `Res\*.dwg` | Sheet sets, viewports, architectural details |
| `Sheet Sets\Civil\` | Civil drawings | Civil plan/profile sheets |
| `Sheet Sets\Manufacturing\` | Mechanical drawings | Manufacturing drawings |
| `Mechanical Sample\` | `Mechanical - Xref.dwg`, `Data Extraction*.dwg`, etc. | Xrefs, data extraction, tables |
| `Database Connectivity\` | `Floor Plan Sample.dwg` | Database-linked objects |

#### Civil 3D Specific

Path: `C:\Program Files\Autodesk\AutoCAD {Version}\C3D\Help\Civil Tutorials\Drawings\`

Only present when Civil 3D is installed (vertical product overlay):

| Pattern | Count | Use Case |
|---------|-------|----------|
| `Align-*.dwg` | 15+ | Horizontal alignment design |
| `Assembly-*.dwg` | 5+ | Corridor assembly creation |
| `Corridor-*.dwg` | 5+ | Corridor modeling |
| `Grading-*.dwg` | 5+ | Grading and earthwork |
| `Profile-*.dwg` | 5+ | Vertical profile design |
| `Surface-*.dwg` | 5+ | TIN surface modeling |
| `Pipe-*.dwg` | 5+ | Pipe network design |

#### Map 3D Specific

Path: `C:\Program Files\Autodesk\AutoCAD {Version}\Map\Sample\`

Present when Map 3D is installed (typically bundled with Civil 3D):

| Content | Use Case |
|---------|----------|
| GIS-related drawings | Coordinate systems, feature data |

#### Product-Specific Folder Pattern

Each vertical product has its own subfolder under the AutoCAD install:

| Product | Folder | Notes |
|---------|--------|-------|
| Civil 3D | `C3D\` | Tutorial drawings, templates |
| Plant 3D | `PLNT3D\` | P&ID, 3D piping (may not be installed) |
| Architecture | `ACA\` | Architectural objects (may be empty) |
| Map 3D | `Map\` | GIS samples |

**Fallback**: If product-specific folders are not found, use the base `Sample\` folder — it is always available with any AutoCAD product.

---

## Dynamic Toolset Registration

### Build Toolsets

```powershell
# .NET Toolset for Revit
dotnet build samples/RevitMcpToolSet/RevitMcpToolSet.csproj -c Release.Autodesk.2025

# Python Toolset (no build needed — interpreted)
# Located at: samples/PythonDemo/mcp_toolset/
```

### Register in McpRegistryConfig.json

Edit `%AppData%\RevitDevTool\{Version}\Settings\McpRegistryConfig.json`:

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

For AutoCAD/Civil3D (same structure, different version folder):
```json
{
  "dotnetToolsetPaths": [],
  "pythonToolsetPaths": [
    "C:\\Users\\{user}\\source\\repos\\RevitDevTool\\samples\\PythonDemo\\mcp_toolset"
  ]
}
```

**Important**: After changing `McpRegistryConfig.json`, restart the host application for changes to take effect.

**Registry rule**: Register **one** overlapping dynamic toolset at a time. Do not enable both
`PythonDemo/mcp_toolset` and `samples/RevitMcpToolSet` when they expose the same `revit_*` tool
names — schemas diverge (Python uses snake_case args; C# sample uses camelCase) and catalog
resolve becomes ambiguous.

---

## Test Scenarios

### Scenario 1: Built-in Code Execution

**Goal**: Verify `execute_csharp_code` and `execute_python_code` work with both error recovery and success paths.

```
1. launch_host(hostApp="Revit", filePath="C:\Program Files\Autodesk\Revit 2025\Samples\Snowdon Towers Sample Architectural.rvt")
2. search_dynamic(query="execute") → note capabilityId for execute_csharp_code / execute_python_code
3. Execute C# — create wall:
   invoke_dynamic(capabilityId=<id>, arguments={code: <IExternalCommand creating a wall>})
   Expected: "Wall created" or similar success
4. Execute C# — intentional error (missing using):
   invoke_dynamic(capabilityId=<csharp_id>, arguments={code: <code without System.Collections.Generic>})
   Expected: [COMPILATION ERROR] with CS0246
5. Execute C# — fix and retry
   Expected: Success
6. Execute Python — query elements:
   invoke_dynamic(capabilityId=<python_id>, arguments={code: <query all walls, print count>})
   Expected: Element count printed
7. Execute Python — data analysis with PEP 723:
   invoke_dynamic(capabilityId=<python_id>, arguments={code: <script with # /// script requiring polars>})
   Expected: Package auto-installs, results printed
```

**Token estimate**: ~2000 tokens per C# execution (code + response), ~1500 per Python.

### Scenario 2: Navigate History (Undo/Redo)

**Goal**: Verify `navigate_history` with unified contract on both hosts.

```
1. Create 3 transactions in Revit (wall, floor, column)
2. invoke_dynamic(capabilityId=<navigate_id>, arguments={direction:"back", steps:2})
   Expected: {navigated: 2, operations: [...], back_remaining: 1, forward_available: 2}
3. invoke_dynamic(capabilityId=<navigate_id>, arguments={direction:"forward", steps:1})
   Expected: {navigated: 1, forward_available: 1}
4. invoke_dynamic(capabilityId=<navigate_id>, arguments={direction:"back", steps:99})
   Expected: Bounded to available stack, undo all
5. invoke_dynamic(capabilityId=<navigate_id>, arguments={direction:"forward", steps:99}) on empty forward stack
   Expected: "Nothing to redo. Forward stack is empty."
```

Repeat with Civil 3D (resolve `navigate_history` via `search_dynamic` filtered by that host's `hostInstanceId`):
```
6. Create entities in Civil 3D (line, circle, polyline)
7. invoke_dynamic(capabilityId=<civil_navigate_id>, arguments={direction:"back", steps:1})
   Expected: {navigated: 1, operations: ["Group of commands"], ...}
```

**Token estimate**: ~300 tokens per navigate call.

### Scenario 3: Vision Capture

**Goal**: Verify `revit://view/screenshot` returns usable image for AI inspection.

```
1. search_dynamic(query="screenshot") → capabilityId for revit://view/screenshot
2. invoke_dynamic(capabilityId=<screenshot_id>) → expect PNG blob
3. Execute code that changes geometry (add wall)
4. invoke_dynamic(capabilityId=<screenshot_id>) → expect different image
5. Compare: AI confirms visual change occurred
```

**Token estimate**: Image is binary (no text tokens consumed for blob), ~500 for metadata.

### Scenario 4: Resource-Driven Development Loop

**Goal**: Full workflow using resources → code → verify → undo cycle.

```
1. search_dynamic → obtain capabilityIds for cheatsheet / model/context / version / execute / screenshot / warnings / navigate
2. invoke_dynamic(capabilityId=<cheatsheet_id>) (once per session)
3. invoke_dynamic(capabilityId=<context_id>) → get levels, categories, units
4. invoke_dynamic(capabilityId=<version_id>) → confirm API version
5. Plan: create 3-story building structure
6. Execute C# code (level by level) via invoke_dynamic(capabilityId=<csharp_id>, arguments={code:...})
7. invoke_dynamic(capabilityId=<screenshot_id>) → verify
8. If wrong: invoke_dynamic(capabilityId=<navigate_id>, arguments={direction:"back"}) → retry
9. invoke_dynamic(capabilityId=<warnings_id>) → check for constraint violations
```

**Token estimate**: 
- Cheatsheet: ~800 tokens (one-time)
- Model context: ~200 tokens per read
- Code execution: ~2000 tokens per call
- Screenshot: ~0 text tokens (binary)
- Full loop (5 iterations): ~12,000 tokens

### Scenario 5: Error Recovery Loop

**Goal**: Measure retry efficiency and recovery capability.

```
1. Execute deliberately wrong code (bad API usage)
   → [RUNTIME ERROR]
2. Read error → fix → retry
   → [COMPILATION ERROR] (missing using)
3. Fix using → retry
   → Success
4. Token cost: iteration_1 + iteration_2 + iteration_3
   Goal: < 3 retries for any recoverable error
```

**Metrics to record**:
- Retries until success
- Token cost per retry
- Error category distribution

### Scenario 6: Dynamic Toolset (RevitMcpToolSet)

**Goal**: Test the full 42-tool spec with CRUD, query, MEP, export.

**Prerequisite**: McpRegistryConfig.json configured + host restarted.

```
1. Restart host after McpRegistryConfig.json change → HostBroker refreshes catalog automatically
2. search_dynamic(query="revit_") → expect 32+ tools (hasMore) from RevitMcpToolSet; note capabilityIds
3. invoke_dynamic(capabilityId=<find_id>, arguments={filters:{filters:[{type:"category",names:["Walls"]}]}, maxResults:5}) → element IDs
4. invoke_dynamic(capabilityId=<status_or_info_id>, ...) → parameters / health as applicable
5. invoke_dynamic(capabilityId=<create_or_place_id>, ...) → new element when testing mutating tools
6. invoke_dynamic(capabilityId=<modify_id>, ...) → update when testing mutating tools
7. invoke_dynamic(capabilityId=<delete_id>, arguments={elementIds:[...], dryRun:true}) → preview delete
8. invoke_dynamic(capabilityId=<export_id>, ...) → file output when testing export
```

**Wire note:** After the platform-split contract, always `search_dynamic` → `capabilityId` → `invoke_dynamic`. Do not pass retired `kind`/`target`/`hostInstanceId` invoke fields.

### Scenario 7: Multi-Host Simultaneous

**Goal**: Verify routing between Revit and Civil 3D connected simultaneously.

```
1. launch_host(hostApp="Revit", filePath=<sample>)
2. launch_host(hostApp="Civil3D", versionNumber="2026")
3. list_host_instances → confirm both connected (distinct processId / hostApp)
4. search_dynamic(query="execute") → see registrations with hostInstanceId per host; pick capabilityIds per host
5. Execute on Revit: invoke_dynamic(capabilityId=<revit_csharp_id>, arguments={...})
6. Execute on Civil3D: invoke_dynamic(capabilityId=<civil_python_or_csharp_id>, arguments={...})
7. navigate_history on each host independently via that host's capabilityId
```

### Scenario 8: NuGet + PEP 723 Package Install

**Goal**: Verify external dependency resolution in both languages.

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

### Scenario 9: Resource Templates

**Goal**: Verify parameterized `resource_template` discovery, single read, and batch `reads[]`.

**Prerequisite**: `RevitMcpToolSet` registered in `McpRegistryConfig.json` + host restarted; Snowdon Towers (or similar) open with known element IDs.

```
1. search_dynamic(query="element", kinds=["resource_template"])
   Expected: hit for revit://element/{elementId} with capabilityId and argsHint (elementId)
2. invoke_dynamic(capabilityId=<element_template_id>, arguments={elementId: <known_wall_id>})
   Expected: application/json with id, category, level, boundingBox
3. search_dynamic(query="schedule", kinds=["resource_template"])
   Expected: hit for revit://schedule/{scheduleId}/preview
4. invoke_dynamic(capabilityId=<schedule_template_id>, arguments={scheduleId: <known_schedule_id>})
   Expected: text/csv with header row + preview rows
5. Batch read (fixed + template):
   search_dynamic → note capabilityIds for revit://toolset/capabilities, revit://model/selection, element template
   invoke_dynamic(reads=[
     { capabilityId: <capabilities_id> },
     { capabilityId: <selection_id> },
     { capabilityId: <element_template_id>, arguments: { elementId: <known_id> } }
   ])
   Expected: three results in one response; no mutating tools in reads[]
```

**Wire note:** Template arguments use the parameter names from the template (`elementId`, `scheduleId`). Stale `capabilityId` after catalog refresh returns `stale_capability` — re-run `search_dynamic`.

### Scenario 10: Bulk delete warning (dryRun)

**Goal**: Verify `revit_delete_elements` returns structured warning + `dryRun` preview for bulk delete — **not** MRTR elicitation through isolated toolsets/Gateway.

**Product note**: MRTR pass-through exists at daemon/client (`CallToolPassthroughAsync`, `inputResponses` on wire) but bulk delete in samples uses **warning-first** policy aligned with Python. Do not expect `input_required` from `invoke_dynamic` for `revit_delete_elements`.

**Prerequisite**: Model with deletable elements (or synthetic IDs in empty model).

```
1. search_dynamic(query="delete_elements")
2. invoke_dynamic(capabilityId=<delete_id>, arguments={ elementIds: [<51+ ids>], dryRun: false })
   Expected: CallToolResult with structured warning, deleted_count=0, short text summary (not empty)
3. invoke_dynamic(capabilityId=<delete_id>, arguments={ elementIds: [<ids>], dryRun: true })
   Expected: structured preview (dryRunResults / count), no mutation
```

**Token note**: One text summary line; full payload in `structuredContent` when `UseStructuredContent=true`.

### Scenario 11 (optional): ALC low-level MRTR demo stub

**Goal**: Live spike for isolated .NET toolset low-level MRTR (`test_mrtr_confirm`) — **not**
product delete confirm and **not** high-level `ElicitAsync`.

**Prerequisite**: Build and register `McpToolsetDemo` (not `RevitMcpToolSet` for this scenario):

```powershell
dotnet build samples/McpToolsetDemo/McpToolsetDemo.csproj -c Debug.Autodesk.2025
```

Add to `%AppData%\RevitDevTool\{Version}\Settings\McpRegistryConfig.json`:

```json
{
  "dotnetToolsetPaths": [
    "C:\\Users\\{user}\\source\\repos\\RevitDevTool\\samples\\McpToolsetDemo\\bin\\Debug.Autodesk.2025\\McpToolsetDemo.dll"
  ]
}
```

Restart host after registry change.

**Requires** MRTR-capable external client (protocol `2026-07-28` + elicitation handler).
Daemon `invoke_dynamic` pass-through only — no auto-retry on daemon→host hop.

```
1. search_dynamic(query="test_mrtr_confirm")
   Expected: capabilityId for tool name test_mrtr_confirm
2. invoke_dynamic(capabilityId=<id>)
   Expected: input_required (elicitation confirm) or soft text if client lacks MRTR
3. Client fulfills elicitation; retry invoke_dynamic with inputResponses + echoed requestState
   Expected: text "confirmed"
```

**Gate:** G1-a factory `IsAugmentedWith` bind is landed (unit T-ALC-* green). Rebuild/redeploy
host with updated Catalog + register `McpToolsetDemo` before running this scenario.

**Contrast Scenario 10:** bulk delete stays warning-first (`dryRun` preview); do not expect
`input_required` from `revit_delete_elements`.

---

## Token Usage Measurement

### How to Measure

1. **Preferred: Use coding agent's native metrics**
   - **Cursor**: Session token count shown in billing dashboard and chat header
   - **Claude Code**: `--usage` flag or session stats in output
   - **Codex**: API response includes `usage.total_tokens`
   - **GitHub Copilot**: Activity log shows token consumption

2. **Fallback: Estimate from payload size**
   - Approximate: 1 token ≈ 4 characters (English) or ≈ 3 characters (code)
   - For tool calls: `(input_json_chars + output_json_chars) / 3.5`
   - For resources: count the response body characters / 3.5
   - For images (screenshots): 0 text tokens — binary blob handled natively by vision models

3. **Recording format**
   ```
   | Scenario | Tool Call | Input tokens | Output tokens | Total | Retries |
   |----------|-----------|-------------|---------------|-------|---------|
   | S1.3     | execute_csharp_code | ~600 | ~200 | ~800 | 0 |
   ```

### Baseline Estimates (2026-07-26, post Phase 1)

| Workflow | Estimated Tokens | Notes |
|----------|-----------------|-------|
| `search_dynamic` full catalog | ~1,000 | `detail=summary` default |
| `search_dynamic` with schemas | ~2,500 | pass `detail=schema` |
| `read_file_info` summary | ~300 | `detail=summary` default |
| `read_file_info` full | ~1,500 | pass `detail=full` |
| Read cheatsheet (once) | ~2,100 | Cache for session; Phase 3 split pending |
| Read model context | ~400 | Per-operation, live state |
| `revit://model/context` cold read | — | Target &lt; 2s host-side (excludes daemon MCP connect) |
| `revit://model/context` warm read | ~20ms measured | Target &lt; 500ms (3× sequential on Snowdon Towers) |
| C# execution (create) | 2000 | Code + structured response |
| Python execution (query) | 1500 | Code + print output |
| Navigate history | 300 | JSON response |
| Screenshot | 0 (text) | Binary blob, no text tokens |
| Error + retry cycle | 4000 | 2 iterations average |
| Full create-verify-undo loop | 5000 | Code + screenshot + undo |
| Complete 5-step workflow | 12000-15000 | End-to-end complex task |
| `find_elements` text summary (structured path) | &lt; 120 chars | `UseStructuredContent` one-line text |
| Batch 3 resource `reads[]` | &lt; 4 KiB JSON | capabilities + context + selection |
| Delete confirm MRTR | 0 extra text | Elicitation replaces warning prose |

### Phase 2 — `revit://model/context` latency

After host deploy (`scripts/build-host.ps1 -Year 2025`), open Snowdon Towers and run
**3 sequential** resource reads (no parallel calls):

```
invoke_dynamic(capabilityId=<context_id>)
```

Record `durationMs` from daemon or host `resources/read` log lines:

| Read | Target |
|------|--------|
| 1 (cold, after idle) | &lt; 2000ms |
| 2–3 (warm) | &lt; 500ms each |

Verify element counts match pre-optimization output for the same model.

### Agent sequencing

- Do **not** parallelize mutating `invoke_dynamic` tool calls on the same `hostInstanceId`
  (e.g. create geometry + `navigate_history` concurrently).

### What to Report

After each test run, record:
- **Total session tokens** (from agent metrics)
- **Tool calls count** (success + failed)
- **Retry count** per scenario
- **Time to completion** (wall clock)
- **Recovery efficiency**: `retries / total_calls` ratio (target: < 0.3)

---

## Recovery Metrics

Target thresholds for healthy agent operation:

| Metric | Target | Acceptable |
|--------|--------|------------|
| Compilation error recovery | 1 retry | 2 retries |
| Runtime error recovery | 1-2 retries | 3 retries |
| Rollback + retry | 1 retry | 2 retries |
| Undo precision | Exact count | ±1 overshoot |
| Visual verify confidence | 1 screenshot | 2 screenshots |

---

## Test Execution Order

Recommended sequence for a full integration pass:

1. **Prerequisites** — kill, build, publish, connect
2. **Scenario 1** — Basic code execution (C# + Python)
3. **Scenario 2** — Navigate history
4. **Scenario 3** — Vision capture
5. **Scenario 4** — Full development loop
6. **Scenario 5** — Error recovery metrics
7. **Scenario 6** — Dynamic toolset (requires config + restart)
8. **Scenario 7** — Multi-host routing
9. **Scenario 8** — External package install
10. **Scenario 9** — Resource templates (requires toolset config + restart)

---

## Notes for Agents

- **Always kill host before build** — running host locks DLLs, build will fail
- **`list_host_instances` empty after a host rebuild** — add-in may have thrown during startup. Read newest `%APPDATA%\RevitDevTool\{Year}\Logs\crash_*` before assuming MCP/Daemon. Session logs are `log_*` (see `docs/agents/verification.md`)
- **Daemon publish kills the running instance** — client must reload MCP after
- **McpRegistryConfig changes require host restart** — not hot-reloadable
- **Civil 3D uses `acad.exe`** — same process name as AutoCAD, differentiated by pipe name
- **Large models (>50MB) take 30-60s to open** — `launch_host` timeout accounts for this
- **AutoCAD undo is async** — `navigate_history` on AutoCAD queues commands, stack counts are estimates
- **Revit undo is synchronous** — exact stack state returned immediately
- **`hostInstanceId` is the PID** — use `list_host_instances` or `search_dynamic` hits to discover; invoke with the hit's `capabilityId`
- **Cursor tool schema cache** — after daemon publish/reload, agent-side `GetMcpTools` may still show retired `kind`/`target`/`includeSchema` fields while live `tools/list` is correct. Trust runtime/`tools/list` (`capabilityId`, `detail`); force a full MCP reconnect if schemas stay stale
- **Dynamic contract** — `docs/product/mcp.md` is authority: `search_dynamic` → `capabilityId` → `invoke_dynamic` (no `kind`/`target` invoke fields)
