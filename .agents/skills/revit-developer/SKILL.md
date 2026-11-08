---
name: revit-developer
description: >
  Write, run, and verify Revit API code against a live Revit through RevitDevTool
  MCP (execute code, undo, screenshots), using rvtdocs-mcp to search, browse, and
  compare the API docs. Use when writing or debugging Revit API code or add-ins,
  executing code in Revit, or looking up Revit API members and version changes.
---

# Revit Developer Workflow

Two MCP servers work together:

| Server | Role | Tools |
|--------|------|-------|
| `revitdevtool` | Run code in live Revit | `list_processes`, `launch_host`, `read_file_info`, `code_mode` |
| `rvtdocs-mcp` | Revit API documentation | `rvtdocs_search`, `rvtdocs_scan`, `rvtdocs_diff`, `rvtdocs_fetch` |

`revitdevtool` is the server key in the MCP client config that the RevitDevTool
settings panel shows; a client may use another key. The server lists only those
four tools. The Revit tools
(`execute_csharp_code`, `execute_python_code`, `navigate_history`,
`view_screenshot`) and resources (`revit://model/context`, …) are reached from
a `code_mode` program, a C# async method body with these calls:

| Call | Returns |
|------|---------|
| `SearchAsync(query, limit, processId, primitiveType)` | Ranked `Name`, `Description`, `ProcessId`, `PrimitiveType` (`tool`, `resource`, `resource_template`) |
| `DescribeAsync(name, processId)` | The tool's `InputSchema` and annotations |
| `InvokeAsync(name, arguments, processId)` | The host `CallToolResult` |
| `ReadAsync(name, arguments, processId)` | The `ReadResourceResult` |

Only the program's `return` value reaches you, so return a projection, or the
image block when you need to see it.

## Loop: explore, code, execute, verify

```
1. rvtdocs_search("wall create curve")      → discover API
2. rvtdocs_fetch(url)                        → read method signature
3. code_mode: DescribeAsync("execute_csharp_code") → entry pattern is in its description
4. code_mode: ReadAsync("revit://model/context")   → live model state
5. code_mode: InvokeAsync("execute_csharp_code", {code}) → run in Revit
6. code_mode: InvokeAsync("view_screenshot")        → verify visually
7. code_mode: InvokeAsync("navigate_history", {direction:"back"}) → undo if wrong
```

## API discovery (rvtdocs-mcp)

Search first when unsure which API exists; namespaces rank at the top.

```
rvtdocs_search(query="structural rebar placement", year="2025", limit=15)
→ classes, methods, properties with FQN + URL
```

Browse a namespace or class the search revealed; filter with `types="class,method,property"`.

```
rvtdocs_scan(target="Autodesk.Revit.DB.Structure", types="class")
→ 126 classes in that namespace
```

Compare versions for migration or compatibility.

```
rvtdocs_diff(from_year="2025", to_year="2027", scope="Autodesk.Revit.DB.Electrical")
→ {added: [...], removed: [...], summary: {addedCount: 6, removedCount: 18}}
```

Fetch the full page only when you need signatures or remarks.

```
rvtdocs_fetch(url="https://rvtdocs.com/2025/Autodesk.Revit.DB.Wall.Create")
→ method table, parameters, remarks
```

## Code execution (revitdevtool)

Revit must be running with the DevTools add-in; `list_processes` confirms the
connection and gives the `processId`. The `execute_csharp_code` /
`execute_python_code` descriptions carry the entry pattern.

From `code_mode`, the host source is one C# verbatim string, with each `"`
written as `""`:

```csharp
var code = @"
<entry pattern from the execute_csharp_code description>
";
var result = await InvokeAsync("execute_csharp_code", new Dictionary<string, object> { ["code"] = code }, 80740);
return new { result.IsError, text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text };
```

`execute_csharp_code` compiles and runs an `IExternalCommand.Execute` body:

```csharp
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

Python gets `__revit__` (UIApplication) injected; PEP 723 headers install dependencies:

```python
doc = __revit__.ActiveUIDocument.Document
# /// script
# dependencies = ["polars>=1.0"]
# ///
import polars as pl
```

C# takes NuGet references with `#r "nuget: NetTopologySuite, 2.6.0"`.

| Result | Action |
|--------|--------|
| `[COMPILATION ERROR]` | Fix the code and retry |
| `[RUNTIME ERROR]` | Read the details, fix the logic, retry; undo with `InvokeAsync("navigate_history", new { direction = "back", steps = 1 }, pid)` |
| `[ROLLBACK]` | A constraint violation was auto-resolved; check warnings |

## Resources and screenshot

Read resources with `ReadAsync(uri, null, pid)`:

| Resource | When to read |
|----------|-------------|
| `revit://model/context` | Before each operation (live state) |
| `revit://model/warnings` | After operations, to check issues |
| `revit://version` | To confirm the API version |
| `revit://csharp-cheatsheet`, `revit://python-cheatsheet` | Once per session, before writing code |

The screenshot is the `view_screenshot` tool, not a resource. It captures the
active view as a 1280 px PNG; return the image block so you can see it:

```csharp
var shot = await InvokeAsync("view_screenshot", null, 80740);
return shot.Content.OfType<ImageContentBlock>().First();
```

## Several hosts

With several Revit instances, or Revit and AutoCAD, connected, the same tool
name exists on each process, and a call without `processId` is rejected as
ambiguous:

```
list_processes → see all PIDs
code_mode → SearchAsync, then InvokeAsync or ReadAsync with the chosen processId
```

## Version compatibility

1. `rvtdocs_diff(from_year="2024", to_year="2027", scope="...")`
2. Find the added and removed APIs.
3. Guard with `#if REVIT2025_OR_GREATER` for conditional compilation.

## NuGet packages for geometry and data

Not in the Revit SDK; add with `#r "nuget:"`:

| Package | Version | Use |
|---------|---------|-----|
| `Clipper2` | 2.0.0 | Polygon boolean (union/intersect/diff), offsetting, triangulation |
| `NetTopologySuite` | 2.6.0 | 2D spatial operations, WKT/WKB, topology predicates, buffering |
| `MathNet.Numerics` | 6.0.0-beta2 | Linear algebra, matrix ops, interpolation, statistics |
| `CsvHelper` | 33.0.1 | CSV import/export for schedules, data analysis |
| `MiniExcel` | 1.45.0 | Excel read/write without Office interop |

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
