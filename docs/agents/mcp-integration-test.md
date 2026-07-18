# MCP Integration Test Workflow

Comprehensive end-to-end testing guide for AI agents operating through MCP against live host processes.

---

## Prerequisites Checklist

Before running any MCP test, verify each step in order:

### 1. Kill Host Processes

```powershell
# Kill all host processes that lock DLLs
Get-Process -Name "Revit" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "acad" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 3
```

### 2. Build Add-in (Latest Code)

```powershell
# Revit — pick your year (2022-2027)
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025

# AutoCAD/Civil3D — pick your year (2022-2027)
dotnet build source/AcadDevTool/AcadDevTool.csproj -c Debug.Autodesk.2026
```

### 3. Publish Daemon (If Code Changed)

Check if any files under `source/DevTools.Daemon/`, `source/DevTools.Mcp/`, or `source/DevTools.Ipc/` have changed since last publish:

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

---

## Test Scenarios

### Scenario 1: Built-in Code Execution

**Goal**: Verify `execute_csharp_code` and `execute_python_code` work with both error recovery and success paths.

```
1. launch_host(hostApp="Revit", filePath="C:\Program Files\Autodesk\Revit 2025\Samples\Snowdon Towers Sample Architectural.rvt")
2. devtools_search(query="execute") → confirm the expected cached targets
3. Execute C# — create wall:
   devtools_invoke(target="tool:execute_csharp_code", arguments={code: <IExternalCommand creating a wall>})
   Expected: "Wall created" or similar success
4. Execute C# — intentional error (missing using):
   devtools_invoke(target="tool:execute_csharp_code", arguments={code: <code without System.Collections.Generic>})
   Expected: [COMPILATION ERROR] with CS0246
5. Execute C# — fix and retry
   Expected: Success
6. Execute Python — query elements:
   devtools_invoke(target="tool:execute_python_code", arguments={code: <query all walls, print count>})
   Expected: Element count printed
7. Execute Python — data analysis with PEP 723:
   devtools_invoke(target="tool:execute_python_code", arguments={code: <script with # /// script requiring polars>})
   Expected: Package auto-installs, results printed
```

**Token estimate**: ~2000 tokens per C# execution (code + response), ~1500 per Python.

### Scenario 2: Navigate History (Undo/Redo)

**Goal**: Verify `navigate_history` with unified contract on both hosts.

```
1. Create 3 transactions in Revit (wall, floor, column)
2. devtools_search(query="navigate_history", kinds=["tool"]) → confirm the cached tool target
3. devtools_invoke(target="tool:navigate_history", arguments={direction="back", steps=2})
   Expected: {navigated: 2, operations: [...], back_remaining: 1, forward_available: 2}
4. devtools_invoke(target="tool:navigate_history", arguments={direction="forward", steps=1})
   Expected: {navigated: 1, forward_available: 1}
5. devtools_invoke(target="tool:navigate_history", arguments={direction="back", steps=99})
   Expected: Bounded to available stack, undo all
6. devtools_invoke(target="tool:navigate_history", arguments={direction="forward", steps=99}) on empty forward stack
   Expected: "Nothing to redo. Forward stack is empty."
```

Repeat with Civil 3D (PID routing via `hostId`):
```
6. Create entities in Civil 3D (line, circle, polyline)
7. devtools_search(query="navigate_history", kinds=["tool"], hostId=<civil3d_pid>) → confirm the target
8. devtools_invoke(target="tool:navigate_history", hostId=<civil3d_pid>, arguments={direction="back", steps=1})
   Expected: {navigated: 1, operations: ["Group of commands"], ...}
```

**Token estimate**: ~300 tokens per navigate call.

### Scenario 3: Vision Capture

**Goal**: Verify `revit://view/screenshot` returns usable image for AI inspection.

```
1. devtools_search(query="revit://view/screenshot", kinds=["resource"]) → confirm the cached resource target
2. devtools_invoke(target="resource:revit://view/screenshot") → expect PNG blob (base64)
3. devtools_search(query="execute_csharp_code", kinds=["tool"]), then devtools_invoke(target="tool:execute_csharp_code", arguments={code: <add wall>})
4. devtools_invoke(target="resource:revit://view/screenshot") → expect different image
5. Compare: AI confirms visual change occurred
```

**Token estimate**: Image is binary (no text tokens consumed for blob), ~500 for metadata.

### Scenario 4: Resource-Driven Development Loop

**Goal**: Full workflow using resources → code → verify → undo cycle.

```
1. devtools_search(query="revit://csharp-cheatsheet", kinds=["resource"]) → confirm the cached resource targets
2. devtools_invoke(target="resource:revit://csharp-cheatsheet") (once per session)
3. devtools_invoke(target="resource:revit://model/context") → get levels, categories, units
4. devtools_invoke(target="resource:revit://version") → confirm API version
5. Plan: create 3-story building structure
6. devtools_search(query="execute_csharp_code", kinds=["tool"]) → confirm the tool target
7. devtools_invoke(target="tool:execute_csharp_code", arguments={code: <level-by-level code>})
8. devtools_invoke(target="resource:revit://view/screenshot") → verify
9. If wrong: devtools_search(query="navigate_history", kinds=["tool"]), then devtools_invoke(target="tool:navigate_history", arguments={direction="back"}) → retry
10. devtools_invoke(target="resource:revit://model/warnings") → check for constraint violations
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
1. devtools_search(query="execute_csharp_code", kinds=["tool"]) → confirm the cached tool target
2. devtools_invoke(target="tool:execute_csharp_code", arguments={code: <deliberately wrong API usage>})
   → [RUNTIME ERROR]
3. Read error → fix → devtools_invoke(target="tool:execute_csharp_code", arguments={code: <fixed code>})
   → [COMPILATION ERROR] (missing using)
4. Fix using → devtools_invoke(target="tool:execute_csharp_code", arguments={code: <corrected code>})
   → Success
5. Token cost: iteration_1 + iteration_2 + iteration_3
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
1. devtools_search(query="revit") → confirm toolset targets are cached
2. devtools_search(query="revit") → expect 42+ targets from RevitMcpToolSet
3. devtools_invoke(target="tool:revit_find_elements", arguments={category="Walls"}) → element IDs
4. devtools_invoke(target="tool:revit_get_element_info", arguments={elementIds=[...]}) → parameters, geometry
5. devtools_invoke(target="tool:revit_create_element", arguments={category="Walls", ...}) → new element
6. devtools_invoke(target="tool:revit_modify_parameters", arguments={elementId=..., parameters={...}}) → update
7. devtools_invoke(target="tool:revit_delete_elements", arguments={elementIds=[...]}) → delete
8. devtools_invoke(target="tool:revit_export_data", arguments={format="xlsx", ...}) → file output
```

### Scenario 7: Multi-Host Simultaneous

**Goal**: Verify routing between Revit and Civil 3D connected simultaneously.

```
1. launch_host(hostApp="Revit", filePath=<sample>)
2. launch_host(hostApp="Civil3D")
3. devtools_search() → confirm both local host PIDs
4. devtools_search(query="execute") → see target candidates and PIDs
5. Execute on Revit: devtools_invoke(target="tool:execute_csharp_code", hostId=<revit_pid>, ...)
6. Execute on Civil3D: devtools_invoke(target="tool:execute_python_code", hostId=<civil3d_pid>, ...)
7. devtools_search(query="navigate_history", kinds=["tool"]) → identify both host targets, then devtools_invoke(target="tool:navigate_history", hostId=<pid>, arguments={direction="back", steps=1}) on each host independently
```

### Scenario 8: NuGet + PEP 723 Package Install

**Goal**: Verify external dependency resolution in both languages.

```
# C# — NuGet
devtools_search(query="execute_csharp_code", kinds=["tool"]), then devtools_invoke(target="tool:execute_csharp_code", arguments={code: <script with #r "nuget: Newtonsoft.Json, 13.0.3">})
Expected: Downloads, compiles, runs

# Python — PEP 723
devtools_search(query="execute_python_code", kinds=["tool"]), then devtools_invoke(target="tool:execute_python_code", arguments={code: <script below>}) with:
  # /// script
  # dependencies = ["polars>=1.0"]
  # ///
  import polars as pl
  ...
Expected: Auto-installs polars, executes
```

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

### Baseline Estimates

| Workflow | Estimated Tokens | Notes |
|----------|-----------------|-------|
| Read cheatsheet (once) | 800 | Cache for session |
| Read model context | 200 | Per-operation, live state |
| C# execution (create) | 2000 | Code + structured response |
| Python execution (query) | 1500 | Code + print output |
| Navigate history | 300 | JSON response |
| Screenshot | 0 (text) | Binary blob, no text tokens |
| Error + retry cycle | 4000 | 2 iterations average |
| Full create-verify-undo loop | 5000 | Code + screenshot + undo |
| Complete 5-step workflow | 12000-15000 | End-to-end complex task |

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

---

## Notes for Agents

- **Always kill host before build** — running host locks DLLs, build will fail
- **Daemon publish kills the running instance** — client must reload MCP after
- **McpRegistryConfig changes require host restart** — not hot-reloadable
- **Civil 3D uses `acad.exe`** — the V2 MCP pipe name is only `DevTools.Mcp.v2.{pid}`, so it does not identify host type or version. Use `devtools_search` host metadata and the daemon-local PID `hostId` to distinguish AutoCAD-family hosts. The descriptor-bearing `DevTools_{Host}_{Version}_{PID}` name belongs only to the separate direct pytest lane.
- **Large models (>50MB) take 30-60s to open** — `launch_host` timeout accounts for this
- **AutoCAD undo is async** — `navigate_history` on AutoCAD queues commands, stack counts are estimates
- **Revit undo is synchronous** — exact stack state returned immediately
- **`hostId` is the local PID** — use `devtools_search` to discover candidates
