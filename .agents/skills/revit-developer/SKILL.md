---
name: revit-developer
description: Develop, test, and explore Revit API using RevitDevTool MCP (execute code, undo, vision) combined with rvtdocs-mcp (search API docs, browse namespaces, compare versions). Use when writing Revit API code, debugging Revit add-ins, exploring API patterns, testing against live Revit instances, or when the user mentions Revit API, Revit MCP, execute in Revit, or search Revit docs.
---

# Revit Developer Workflow

Two MCP servers work together for complete Revit API development:

| Server | Role | Tools |
|--------|------|-------|
| `revitdevtool-mcp` | Execute code in live Revit | `execute_csharp_code`, `execute_python_code`, `navigate_history`, resources |
| `rvtdocs-mcp` | Explore API documentation in detail | `rvtdocs_search`, `rvtdocs_scan`, `rvtdocs_diff`, `rvtdocs_fetch` |

## Core Pattern: Explore → Code → Execute → Verify

```
1. rvtdocs_search("wall create curve")     → discover API
2. rvtdocs_fetch(url)                       → read method signature
3. read revit://csharp-cheatsheet           → get code patterns (once per session)
4. read revit://model/context               → get live model state
5. execute_csharp_code(code)                → run in Revit
6. view_screenshot                          → verify visually
7. navigate_history(direction="back")       → undo if wrong
```

## API Discovery (rvtdocs-mcp)

### Search: Vague query → ranked results

```
rvtdocs_search(query="structural rebar placement", year="2025", limit=15)
→ classes, methods, properties with FQN + URL
```

Always search first when unsure which API exists. Namespaces appear at top.

### Scan: Browse namespace or class

```
rvtdocs_scan(target="Autodesk.Revit.DB.Structure", types="class")
→ 126 classes in that namespace
```

Use after search reveals a namespace. Filter with `types="class,method,property"`.

### Diff: Compare versions

```
rvtdocs_diff(from_year="2025", to_year="2027", scope="Autodesk.Revit.DB.Electrical")
→ {added: [...], removed: [...], summary: {addedCount: 6, removedCount: 18}}
```

Use for migration, compatibility checking, or finding new APIs.

### Fetch: Get full documentation

```
rvtdocs_fetch(url="https://rvtdocs.com/2025/Autodesk.Revit.DB.Wall.Create")
→ method table, parameters, remarks
```

Only fetch when you need detailed signatures or documentation text.

## Code Execution (revitdevtool)

### Prerequisites

1. Revit must be running with DevTools add-in loaded
2. `list_host_instances` confirms connection
3. Read `revit://csharp-cheatsheet` once per session for patterns

### C# Execution

```csharp
// Always wrap in IExternalCommand pattern
// Revit provides: commandData (ExternalCommandData)
using Autodesk.Revit.DB;
var doc = commandData.Application.ActiveUIDocument.Document;
using (var tx = new Transaction(doc, "Create Wall"))
{
    tx.Start();
    // ... API calls
    tx.Commit();
}
```

Key: `execute_csharp_code` compiles and runs an `IExternalCommand.Execute` body.

### Python Execution

```python
# __revit__ is injected (UIApplication)
doc = __revit__.ActiveUIDocument.Document
# PEP 723 for external deps:
# /// script
# dependencies = ["polars>=1.0"]
# ///
import polars as pl
```

### Error Recovery

| Error Type | Action |
|-----------|--------|
| `[COMPILATION ERROR]` | Fix code, retry |
| `[RUNTIME ERROR]` | Read error details, fix logic, retry |
| `[ROLLBACK]` | Constraint violation auto-resolved, check warnings |

After runtime errors: `navigate_history(direction="back", steps=1)` to undo.

### NuGet / PEP 723

```csharp
// C# — NuGet reference
#r "nuget: NetTopologySuite, 2.6.0"
```

```python
# Python — PEP 723 deps (auto-installed)
# /// script
# dependencies = ["pandas>=2.0"]
# ///
```

## Resources (read once per session)

| Resource | When to read |
|----------|-------------|
| `revit://csharp-cheatsheet` | Before first C# execution |
| `revit://python-cheatsheet` | Before first Python execution |
| `revit://model/context` | Before each operation (live state) |
| `revit://model/warnings` | After operations to check issues |
| `revit://version` | To confirm API version |
| `view_screenshot` | To verify visual results (1280 px PNG MCP image via single `invoke_dynamic`) |

## Multi-Host

When multiple Revit instances or Revit + AutoCAD are connected:

```
list_host_instances → see all PIDs
search_dynamic(query="execute", hostInstanceId=<PID>) → capabilityId
invoke_dynamic(capabilityId=<id>, arguments={...})
```

## Common Workflows

### Write code for unknown API

1. `rvtdocs_search` with vague terms
2. `rvtdocs_scan` the discovered namespace
3. `rvtdocs_fetch` for specific method signatures
4. Read `revit://csharp-cheatsheet` for patterns
5. Write and execute code
6. Verify with screenshot

### Check version compatibility

1. `rvtdocs_diff(from_year="2024", to_year="2027", scope="...")`
2. Identify added/removed APIs
3. Use `#if REVIT2025_OR_GREATER` for conditional compilation

### Debug failing code

1. Read error message from `execute_csharp_code` response
2. `rvtdocs_search` for correct API usage
3. `rvtdocs_fetch` for parameter details
4. Fix and retry
5. `navigate_history(direction="back")` if state is corrupted

### Explore new domain

1. `rvtdocs_search("electrical")` → discover namespace
2. `rvtdocs_scan("Autodesk.Revit.DB.Electrical", types="class")` → 78 classes
3. Pick interesting class → `rvtdocs_fetch` for details
4. Write exploratory code to test understanding

## Useful NuGet Packages

Not included in Revit SDK — use `#r "nuget:"` to add at runtime:

| Package | Version | Use Case |
|---------|---------|----------|
| `Clipper2` | 2.0.0 | Polygon boolean (union/intersect/diff), offsetting, triangulation |
| `NetTopologySuite` | 2.6.0 | 2D spatial operations, WKT/WKB, topology predicates, buffering |
| `MathNet.Numerics` | 6.0.0-beta2 | Linear algebra, matrix ops, interpolation, statistics |
| `CsvHelper` | 33.0.1 | CSV import/export for schedules, data analysis |
| `MiniExcel` | 1.45.0 | Excel read/write without Office interop |

### Usage in execute_csharp_code

```csharp
#r "nuget: Clipper2, 2.0.0"
#r "nuget: NetTopologySuite, 2.6.0"
using Clipper2Lib;
using NetTopologySuite.Geometries;

// Clipper2: polygon offset for wall centerline operations
var paths = new Paths64();
var solution = Clipper.InflatePaths(paths, offset, JoinType.Miter, EndType.Polygon);

// NTS: spatial predicates, buffering
var factory = new GeometryFactory();
var polygon = factory.CreatePolygon(coordinates);
var buffered = polygon.Buffer(1.0);
```

## Token Efficiency

| Action | Tokens |
|--------|--------|
| rvtdocs_search (20 results) | ~500 |
| rvtdocs_scan (full namespace) | ~1500 |
| rvtdocs_diff (full comparison) | ~1500 |
| rvtdocs_fetch (page content) | ~1000-3000 |
| C# code execution | ~2000 |
| Model context read | ~200 |
| Cheatsheet (one-time) | ~800 |
| Screenshot | 0 (binary) |

Typical explore+code+verify loop: ~5000-8000 tokens total.
