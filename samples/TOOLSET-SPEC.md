# Revit MCP Toolset Specification

Single source of truth for both `RevitMcpToolSet` (C#) and `PythonDemo/mcp_toolset` (Python) implementations.

Both toolsets MUST implement the same tool surface with identical intent, parameters, and response shapes. Only one toolset is loaded at a time per host instance.

Last updated: 2026-08-02

---

## Design Principles

1. **Engineer + Agent collaboration**: Agent assists engineer on the same model, same undo stack. Tools must be safe for concurrent use — never modify unrelated elements.
2. **God tool as escape hatch**: Built-in `execute_csharp_code` handles exotic one-offs. Toolset covers repeatable 80% workflows.
3. **Structured over free-form**: Tools accept structured params (element IDs, geometry specs, filter criteria). Agent doesn't need to generate boilerplate.
4. **Batch-first**: Prefer array inputs over single-element tools. One tool call = one transaction named `"MCP: {tool_name}"` for targeted undo.
5. **Safe by default**: Read-only tools never mutate. Write tools default to explicit scope (IDs or criteria, never whole model). Export tools validate paths via `PathGuard`.
6. **Partial-success reporting**: Write tools return `success_count` + `failures[]` with per-element error details.
7. **Collaboration-aware**: Tools respect worksets, selection state, and borrowed elements.
8. **Packaging:** .NET toolsets ILRepack like other add-ins (`ILRepackable`) but MCP is compile-only (`ExcludeAssets=runtime`). Host Catalog reflects and invokes; collectible ALC binds `ModelContextProtocol*` from the host load context. Do not ship MCP siblings. Do not gate ILRepack on Autodesk year. Merge policy: `docs/decisions/0019-ilrepack-and-polyfill-isolated-alc.md`. Host wire types: `docs/decisions/0012-host-mcp-spec-engine.md`. Keep returning `CallToolResult` / low-level `InputRequiredException` as usual.

---

## Packaging (.NET toolsets)

| Rule | Why |
|------|-----|
| `ILRepackable=true` like other add-ins | One merge pipeline ([0019](../docs/decisions/0019-ilrepack-and-polyfill-isolated-alc.md)) |
| MCP `ExcludeAssets=runtime` | Compile attributes only — host reflects; ALC shares host MCP |
| Do not year-gate `ILRepackable` | Isolated ALC uses Polyfill net4-only + sidecar, not skip-pack |

Do **not** invent a private MCP package version — pin centrally in `Directory.Packages.props`.

---
## Naming Convention

- Tool names: `revit_{domain}_{action}` — e.g. `revit_find_elements`, `revit_place_duct`
- All snake_case, `revit_` prefix mandatory
- Resource URIs: `revit://toolset/{topic}` for static patterns, `revit://model/{topic}` for live queries
- Resource template URIs: `revit://element/{elementId}`, `revit://schedule/{scheduleId}/preview`
- Prompt names: `revit_{purpose}`

---

## Resource Templates

Parameterized reads for one element or schedule without a dedicated tool. Discovered via
`search_dynamic` with `kinds=["resource_template"]`; resolved with `invoke_dynamic` and
template arguments (or batched in `reads[]` alongside fixed resources).

| URI template | MIME | Purpose |
|--------------|------|---------|
| `revit://element/{elementId}` | `application/json` | Compact element summary: id, category, family/type, level, pinned, workset, bounding box |
| `revit://schedule/{scheduleId}/preview` | `text/csv` | CSV header + first 30 body rows for a `ViewSchedule` (prefer over `revit_preview_schedule` when batching) |

**Chaining example** (after `revit_find_elements` returns IDs):

```
1. search_dynamic(query="element", kinds=["resource_template"])
   → capabilityId for revit://element/{elementId}
2. invoke_dynamic(capabilityId=<element_template_id>, arguments={elementId: 12345})
   → JSON element summary
3. invoke_dynamic(reads=[
     { capabilityId: <capabilities_id> },
     { capabilityId: <selection_id> },
     { capabilityId: <element_template_id>, arguments: { elementId: 12345 } }
   ])
   → batch: toolset scope + engineer selection + element detail in one round-trip
```

Use fixed `revit://model/*` resources for unparameterized snapshots (levels, selection, types).
Use templates when you already have element or schedule IDs from a tool result.

---

## Shared Schemas

### ToolError (standard error in `failures[]`)

```json
{
  "elementId": 12345,
  "code": "constraint_violation | element_borrowed | element_pinned | group_member | param_readonly | type_mismatch | not_found",
  "message": "Human-readable description",
  "recoverable": true,
  "suggestedAction": "release workset | unpin element | use undo_changes"
}
```

### FilterSpec (composable, OR/AND logic)

Adopted from Python toolset's proven 12-type discriminated union:

```json
{
  "filters": [
    { "type": "category", "names": ["Walls", "Doors"], "inverted": false },
    { "type": "parameter_string", "parameter_name": "Mark", "operator": "contains", "value": "EXT" },
    { "type": "parameter_numeric", "parameter_name": "Area", "operator": "greater_than", "value": 100.0 },
    { "type": "parameter_has_value", "parameter_name": "Comments", "has_value": true },
    { "type": "level", "level_name": "Level 1" },
    { "type": "class", "class_names": ["Wall", "FamilyInstance", "Room"] },
    { "type": "bounding_box", "min_point": [0,0,0], "max_point": [100,100,50] },
    { "type": "view", "view_name": null },
    { "type": "element_type", "is_type": false },
    { "type": "workset", "workset_name": "Shared Levels and Grids" },
    { "type": "phase", "phase_name": "New Construction" },
    { "type": "exclusion", "element_ids": [12345, 67890] }
  ],
  "logic": "and"
}
```

**String operators**: `equals`, `not_equals`, `contains`, `not_contains`, `begins_with`, `ends_with`
**Numeric operators**: `equals`, `not_equals`, `greater_than`, `less_than`, `greater_or_equal`, `less_or_equal`
**Bounding box modes**: `inside` (default), `intersecting`

### Units Convention

- **All geometry values in feet** (Revit internal units)
- **Exception**: `thickness_mm` in insulation tools (explicit suffix)
- **Colors**: `[r, g, b]` as integers 0–255
- **Angles**: degrees (not radians)
- Element IDs: `long` (int64) — supports both pre-2024 (`IntegerValue`) and 2024+ (`Value`)

---

## Tool Surface (42 tools)

### 1. Model Intelligence (Query + Analysis)

| Tool | Description | RO |
|------|-------------|-----|
| `revit_get_model_summary` | Project overview: info, categories+counts, warnings count, levels, phases, worksets, links | Y |
| `revit_find_elements` | Structured element search with composable FilterSpec | Y |
| `revit_read_parameters` | Get all params of element(s) with metadata | Y |
| `revit_list_types` | Available types: family, MEP system, view template, title block | Y |
| `revit_list_category_parameters` | Schedulable parameter names for a category | Y |
| `revit_list_rooms` | Rooms with name, number, area, level, department, location | Y |
| `revit_list_links` | Revit links and CAD imports with load status | Y |

#### `revit_find_elements` params:

```json
{
  "filters": "FilterSpec (see above)",
  "selectedOnly": false,
  "includeTypes": false,
  "includeInstances": true,
  "maxResults": 500,
  "offset": 0,
  "fields": ["id", "category", "family", "type", "level", "name", "workset", "bbox"]
}
```

**Return**:
```json
{
  "count": 150,
  "truncated": false,
  "elements": [
    { "id": 12345, "category": "Walls", "family": "Basic Wall", "type": "Generic - 200mm",
      "level": "Level 1", "name": "Wall 1", "workset": "Shared Levels", "bbox": { "min": [...], "max": [...] } }
  ]
}
```

#### `revit_read_parameters` params:

`elementIds: long[]`, `paramNames?: string[]` (null = all)

**Return per element**:
```json
{
  "id": 12345,
  "params": [
    { "name": "Mark", "value": "W-01", "storage": "String", "writable": true, "builtin": false, "isShared": true }
  ]
}
```

---

### 2. Element Operations (CRUD)

| Tool | Description | RO |
|------|-------------|-----|
| `revit_write_parameters` | Set param values on elements | N |
| `revit_place_family` | Create family instance(s) at location(s) | N |
| `revit_move_elements` | Translate by vector | N |
| `revit_rotate_elements` | Rotate around axis | N |
| `revit_delete_elements` | Delete elements (with confirmation threshold) | N |
| `revit_clone_parameters` | Copy param values source → targets | N |
| `revit_swap_type` | Change element type | N |
| `revit_highlight_elements` | Select elements in Revit UI (for engineer visibility) | N |

#### `revit_write_parameters`:

```json
{
  "elementIds": [123, 456, 789],
  "updates": [
    { "param_name": "Mark", "value": "EXT-01" },
    { "param_name": "Comments", "value": "Updated by agent" }
  ]
}
```

#### `revit_place_family`:

```json
{
  "familyName": "M_Single-Flush",
  "typeName": "0915 x 2134mm",
  "placements": [
    { "x": 10.0, "y": 5.0, "z": 0.0, "rotation": 90.0, "levelName": "Level 1", "hostId": 54321 }
  ],
  "properties": { "Mark": "D-01" }
}
```

- `hostId`: optional — for face-hosted families (doors in walls, etc.)
- `rotation`: degrees, default 0
- Returns `{ created[]: { id, location }, failures[]? }`

#### `revit_delete_elements`:

```json
{
  "elementIds": [123, 456],
  "dryRun": false
}
```

- When `dryRun: true`, returns what WOULD be deleted without executing
- When count > 50, requires explicit confirmation (returns warning if not `dryRun`)

---

### 3. MEP Engineering

| Tool | Description | RO |
|------|-------------|-----|
| `revit_place_duct` | Create duct segment | N |
| `revit_place_pipe` | Create pipe segment | N |
| `revit_place_conduit` | Create conduit segment | N |
| `revit_list_mep_systems` | Enumerate systems/circuits | Y |
| `revit_insulate_duct_system` | Apply duct insulation | N |

#### Geometry specs:

```
DuctSpec {
  ductTypeId: long
  systemTypeId: long
  levelId: long
  start: [x, y, z]         // feet
  end: [x, y, z]           // feet
  width?: double            // feet (rectangular)
  height?: double           // feet (rectangular)
  diameter?: double         // feet (round)
  slope?: double            // ratio (e.g. 0.01 = 1%)
}

PipeSpec {
  pipeTypeId: long
  systemTypeId: long
  levelId: long
  start: [x, y, z]
  end: [x, y, z]
  diameter: double          // feet
  slope?: double            // ratio
}

ConduitSpec {
  conduitTypeId: long
  systemTypeId: long        // ← was missing
  levelId: long
  start: [x, y, z]
  end: [x, y, z]
  diameter: double          // feet
}
```

#### `revit_list_mep_systems`:

`kind: "duct" | "pipe" | "electrical" | "all"`

**Return**:
```json
{
  "systems": [
    { "id": 123, "name": "Supply Air 1", "type": "Supply Air", "element_count": 45, "classification": "Supply" }
  ]
}
```

---

### 4. Documentation (Views, Sheets, Schedules)

| Tool | Description | RO |
|------|-------------|-----|
| `revit_create_view` | Create floor plan, section, or 3D view | N |
| `revit_create_sheet` | Create drawing sheet | N |
| `revit_place_on_sheet` | Place view/schedule on sheet | N |
| `revit_create_schedule` | Create and configure schedule | N |
| `revit_apply_view_template` | Apply or detach template | N |
| `revit_list_views` | All views/sheets with metadata | Y |
| `revit_list_schedule_fields` | Schedulable fields for a category | Y |
| `revit_activate_view` | Open view in UI (caution: disrupts engineer) | N |

#### `revit_create_view` params by type:

```
floor_plan: { levelName, viewName?, templateName? }
section:    { min: [x,y,z], max: [x,y,z], directionAngle: degrees, depth?, viewName? }
3d:         { viewName?, templateName?, isBoundingBox?: bool }
```

#### `ScheduleConfig`:

```json
{
  "categoryName": "Doors",
  "scheduleName": "Door Schedule",
  "fields": ["Mark", "Width", "Height", "Level", "Comments"],
  "sortRules": [{ "field": "Level", "ascending": true }, { "field": "Mark", "ascending": true }],
  "filterRules": [{ "field": "Width", "operator": "greater_than", "value": "900", "isNumeric": true }],
  "groupRules": [{ "field": "Level", "showHeader": true, "showFooter": true }]
}
```

---

### 5. Visualization & Annotation

| Tool | Description | RO |
|------|-------------|-----|
| `revit_color_by_parameter` | Color splash by param value | N |
| `revit_clear_overrides` | Clear graphic overrides | N |
| `revit_place_tags` | Auto-tag elements in view(s) | N |
| `revit_override_colors` | Direct color override on elements | N |

#### `revit_color_by_parameter`:

```json
{
  "categoryName": "Rooms",
  "parameterName": "Department",
  "viewId": null,
  "useGradient": false,
  "colors": ["#FF0000", "#00FF00", "#0000FF"]
}
```

- `viewId`: null = active view

#### `revit_place_tags`:

```json
{
  "taggingData": [
    { "viewId": 123, "elementIds": [456, 789] }
  ]
}
```

- Per-view element pairs (from C# pattern — more precise than flat arrays)

---

### 6. Export & Reporting

| Tool | Description | RO |
|------|-------------|-----|
| `revit_export_pdf` | Export views to PDF | Y |
| `revit_export_image` | Export views to image | Y |
| `revit_export_to_excel` | Export element data with filters | Y |
| `revit_export_schedule` | Export schedule to CSV/xlsx | Y |

#### `revit_export_to_excel`:

```json
{
  "filters": "FilterSpec",
  "parameters": ["Mark", "Level", "Area", "Comments"],
  "outputPath": "C:\\Export\\walls.xlsx"
}
```

- `parameters`: null = all available
- `outputPath`: null = auto-generated in temp; validated by `PathGuard`

**Note**: Export tools are `readOnlyHint: true` (no model mutation) but write to filesystem.

---

### 7. Document Management

| Tool | Description | RO |
|------|-------------|-----|
| `revit_save_document` | Save or SaveAs | N |
| `revit_close_document` | Close active document | N |
| `revit_sync_with_central` | Workshared sync | N |

**Note**: `open_document` is NOT in the toolset — use built-in `open_document` tool.

#### `revit_sync_with_central`:

```json
{
  "comment": "Agent: updated door marks",
  "compact": false,
  "relinquishAll": false,
  "saveLocalBefore": true
}
```

---

### 8. Infrastructure (Grids, Levels)

| Tool | Description | RO |
|------|-------------|-----|
| `revit_generate_grids` | Create grid system | N |
| `revit_generate_levels` | Create levels batch | N |
| `revit_get_status` | Health + worksharing + selection info | Y |

#### `revit_get_status` return:

```json
{
  "healthy": true,
  "documentTitle": "Project1.rvt",
  "filePath": "F:\\Project1.rvt",
  "worksharingEnabled": true,
  "centralPath": "\\\\server\\Project1.rvt",
  "activeWorkset": "Shared Levels and Grids",
  "selectionCount": 3,
  "version": "2025"
}
```

---

## Non-Goals (use `execute_csharp_code`)

These domains are intentionally excluded from the toolset. Use the built-in god tool:

| Domain | Rationale |
|--------|-----------|
| Wall/floor/roof/ceiling/stair creation | Highly context-dependent geometry, better as free-form code |
| Structural framing (beams, columns, foundations) | Complex analytical model binding |
| Rebar, analytical model, load cases | Specialized structural analysis |
| In-place families, nested families | Requires Family Editor context |
| Curtain walls, shaft openings | Complex multi-element creation |
| IFC import/export | Rarely automated, many options |
| Energy/structural analysis | Specialized post-processing |
| Revit link attach/reload | Infrequent, many edge cases |
| Detail items, dimensions, filled regions | View-specific detailing |

Agent should reference `revit://toolset/capabilities` to confirm scope before attempting.

---

## Resources

### Relationship to built-in resources

Built-in resources (from `DevTools.Agents.Revit`) provide **human-readable markdown** context:
- `revit://api-cheatsheet` — C# API patterns for god tool
- `revit://model/context` — markdown model snapshot
- `revit://model/warnings` — active warnings
- `revit://version` — version + compatibility notes
- `revit://view/screenshot` — visual verification

Toolset resources provide **machine-readable JSON** for tool chaining. No duplication — different format, different purpose.

### Static Resources (embedded patterns)

| URI | Description |
|-----|-------------|
| `revit://toolset/capabilities` | Full tool catalog: when-to-use, constraints, decision tree (toolset vs god tool) |
| `revit://toolset/patterns/query` | FilterSpec composition examples, spatial queries, performance tips |
| `revit://toolset/patterns/mep` | MEP workflow: type discovery → system binding → segment placement → validation |
| `revit://toolset/patterns/documentation` | Sheet package workflow: views → sheets → viewports → templates → export |
| `revit://toolset/patterns/export` | Export options: PDF/image config, path conventions, batch patterns |
| `revit://toolset/errors` | Standard error codes, recovery patterns, retry guidance |
| `revit://toolset/units` | Unit conversion reference: feet ↔ mm/m, display vs internal |

### Live Resources (query Revit runtime)

| URI | Description | Format |
|-----|-------------|--------|
| `revit://model/types` | Family types, MEP system types, view templates, title blocks | JSON |
| `revit://model/levels` | Levels with elevations and associated views | JSON |
| `revit://model/views` | Views/sheets with metadata (type, template, on-sheet) | JSON |
| `revit://model/worksets` | Worksets with editability, owner, element counts | JSON |
| `revit://model/links` | Revit links and CAD imports with paths and load state | JSON |
| `revit://model/selection` | Currently selected elements (engineer intent) | JSON |
| `revit://model/grids` | Grid names, IDs, and geometry for reference | JSON |

---

## Prompts

| Name | Arguments | Purpose |
|------|-----------|---------|
| `revit_toolset_workflow` | `task` (required), `domain?: string` | Generate optimal multi-step tool call sequence |
| `revit_batch_operation` | `operation`, `criteria`, `updates?` | Generate batch param/transform execution plan |
| `revit_coordination_check` | `categories[]`, `tolerance?`, `rules?` | Clash/interference check with structured output schema |
| `revit_undo_recovery` | `failed_tool`, `error_context` | Recovery plan after tool failure — integrate `undo_changes` |
| `revit_worksharing_guide` | `operation` | Sync/relinquish/borrow etiquette for concurrent use |
| `revit_god_tool_decision` | `task` | Decision tree: use toolset tool vs `execute_csharp_code` |

---

## Workflow Patterns

### Pattern 1: Model Analysis → Report

```
revit_get_status → revit_find_elements (FilterSpec) → revit_export_to_excel
```

### Pattern 2: MEP Placement

```
revit_list_types(kind="mep_system") → [resource: patterns/mep] → revit_place_duct/pipe → revit_list_mep_systems (verify)
```

### Pattern 3: Documentation Package

```
revit_list_views → revit_create_view (sections) → revit_create_sheet → revit_place_on_sheet → revit_apply_view_template → revit_export_pdf
```

### Pattern 4: Batch Parameter Update

```
revit_find_elements → revit_read_parameters (sample) → revit_write_parameters (batch) → [on failure: undo_changes]
```

### Pattern 5: Coordination Check

```
revit_find_elements(bbox) → [prompt: coordination_check] → execute_csharp_code (clash) → revit_color_by_parameter (highlight)
```

### Pattern 6: Quality Assurance

```
revit_get_model_summary → revit_find_elements (flagged) → revit_read_parameters → write_parameters / delete_elements → [on failure: undo_changes]
```

### Pattern 7: Color-by-Department (common request)

```
revit_list_category_parameters("Rooms") → revit_color_by_parameter("Rooms", "Department")
```

### Pattern 8: Schedule Creation

```
revit_list_schedule_fields("Doors") → revit_create_schedule(config) → revit_create_sheet → revit_place_on_sheet
```

---

## Implementation Notes

### C# (`RevitMcpToolSet`)
- `[McpServerToolType]` + `[McpServerTool(Name, Title, ReadOnly)]` attributes
- `RevitContext` from Nice3point.Revit.Toolkit
- Single `Transaction` per tool call, named `"MCP: {tool_name}"`
- DTOs in `Data/` folder matching spec schemas
- `OperationOutcome` for partial-success aggregation
- `PathGuard` for all filesystem paths
- `ElementIdExtensions` for cross-version element IDs (int vs long)
- Resources: `[McpServerResourceType]` + `[McpServerResource]` attributes (SDK native)
- Prompts: `[McpServerPromptType]` + `[McpServerPrompt]` attributes (SDK native)

### Python (`mcp_toolset`)
- FastMCP `@mcp.tool(annotations=ToolAnnotations(...))` decorator
- `RevitContext` from host builtins
- `run_transaction(doc, "MCP: {tool_name}", operation)` helper
- Pydantic DTOs for structured input/output
- PEP 723 dependencies: `polars`, `xlsxwriter`
- Resources: `@mcp.resource()` decorator
- Prompts: `@mcp.prompt()` decorator

### Transaction Boundaries

Each write tool = exactly one transaction:
- `Transaction.Start("MCP: revit_write_parameters")`
- On success → `Commit()` → return result
- On Revit failure → auto-resolved by ExecutionGuard or → `RollBack()` → return error
- On exception → `RollBack()` → return error with `ToolError`

Multi-element tools that span views/levels use `TransactionGroup` with per-view inner transactions.

### Performance Guidelines

- `find_elements`: default 500 max, support `offset` for pagination
- `read_parameters` on >100 elements: chunk internally (50/batch)
- `color_by_parameter` on >10000 elements: warn in response
- `export_to_excel` on large datasets: stream to file, return path only

---

## Future Considerations (P2+)

- Structural read tools: `revit_list_structural_types`, `revit_find_structural_elements`
- Room management: `revit_rename_rooms`, `revit_place_rooms`
- Text note tools: `revit_list_text_notes`, `revit_change_text_case`
- Material tools: `revit_clone_material`
- Pagination: cursor-based for very large result sets
- Streaming progress: for long-running batch operations
- Design options support in FilterSpec
- IFC export tool (when demand justifies)
