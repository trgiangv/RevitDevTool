# Revit MCP Toolset — Capabilities

45 structured tools for repeatable BIM workflows. Use toolset tools first; fall back to built-in `execute_csharp_code` only for exotic one-offs.

## MCP content types (SDK 2.0)

| Content block | Example tool | Practical value |
|---------------|--------------|-----------------|
| **Text** | `revit_find_elements` | Human-readable JSON summaries for the model |
| **Structured** | `revit_get_model_summary`, `revit_model_digest` | Machine-parseable fields alongside text (counts, IDs) — clients skip JSON parsing |
| **Image** | `revit_capture_view`, built-in `view_screenshot` | Vision verification after edits — inline preview in Cursor/Claude |
| **Embedded resource** | `revit_preview_schedule` | Small CSV/text payloads inline — no `resources/read` or temp-file roundtrip |
| **Resource link** | `revit_model_digest` | Pointer to `revit://model/views` — summary in tool result, full JSON via resource read |
| **Audio** | — | Not used in BIM workflows (reserved for voice/alert demos) |

File-based exports (`revit_export_pdf`, `revit_export_image`) still return **text** with paths — appropriate for large deliverables on disk.

## Decision Tree: Toolset vs God Tool

```
Task requested
├─ Covered by tool catalog below?
│  ├─ YES → Use matching revit_* tool (batch-first, one transaction per call)
│  └─ NO  → Read revit://toolset/capabilities non-goals, then execute_csharp_code
├─ Needs custom geometry logic (walls, stairs, curtain walls)?
│  └─ execute_csharp_code
├─ Needs structural analysis, rebar, IFC, in-place families?
│  └─ execute_csharp_code
├─ Repeatable query → write → export workflow?
│  └─ Toolset (see workflow patterns in revit://toolset/patterns/*)
└─ Unsure? → revit_get_model_summary + revit_find_elements to scope, then pick tool
```

**Built-in companions (not in toolset):** `open_document`, `execute_csharp_code`, `undo_changes`, `revit://api-cheatsheet`, `revit://model/context`.

---

## 1. Model Intelligence (Query + Analysis) — Read-only

| Tool | Description | When to use |
|------|-------------|-------------|
| `revit_get_model_summary` | Project overview: info, categories+counts, warnings, levels, phases, worksets, links | Start of any analysis session; understand model scope |
| `revit_find_elements` | Structured search with composable FilterSpec | Find elements by category, param, level, bbox, workset |
| `revit_read_parameters` | Get params of element(s) with metadata | Inspect values before batch writes; sample a few matches |
| `revit_list_types` | Family, MEP system, view template, title block types | Resolve type IDs before placement or sheet creation |
| `revit_list_category_parameters` | Schedulable parameter names for a category | Discover param names for filters, schedules, color splash |
| `revit_list_rooms` | Rooms with name, number, area, level, department | Room QA, department coloring, area reports |
| `revit_list_links` | Revit links and CAD imports with load status | Coordination setup; verify link paths |

### Multimodal content (SDK 2.0)

| Tool | Content returned | When to use |
|------|------------------|-------------|
| `revit_capture_view` | **Image** (inline PNG) | Vision check after view changes — prefer over `revit_export_image` for agents |
| `revit_preview_schedule` | **Text** + **embedded CSV** | Quick schedule QA without filesystem export |
| `revit_model_digest` | **Text** + **resource link** + **structured** counts | Start documentation flow; follow link to `revit://model/views` |

`revit_get_model_summary` also emits **structuredContent** for reliable parsing of project overview fields.

---

## 2. Element Operations (CRUD)

| Tool | Description | When to use |
|------|-------------|-------------|
| `revit_write_parameters` | Set param values on elements (batch) | Bulk mark updates, comments, property fixes |
| `revit_place_family` | Create family instance(s) at location(s) | Place doors, furniture, equipment from known types |
| `revit_move_elements` | Translate by vector | Reposition elements without custom code |
| `revit_rotate_elements` | Rotate around axis | Orient equipment, rotate groups |
| `revit_delete_elements` | Delete elements (dry-run + confirmation threshold) | Cleanup with safety checks (>50 needs care) |
| `revit_clone_parameters` | Copy param values source → targets | Propagate marks, types, or custom params |
| `revit_swap_type` | Change element type | Retype doors, equipment, families |
| `revit_highlight_elements` | Select elements in Revit UI | Show engineer what the agent found/changed |

---

## 3. MEP Engineering

| Tool | Description | When to use |
|------|-------------|-------------|
| `revit_place_duct` | Create duct segment | Straight duct runs between two points |
| `revit_place_pipe` | Create pipe segment | Straight pipe runs with system binding |
| `revit_place_conduit` | Create conduit segment | Electrical conduit placement |
| `revit_list_mep_systems` | Enumerate systems/circuits | Verify system assignment after placement |
| `revit_insulate_duct_system` | Apply duct insulation | Post-placement insulation by system |

See `revit://toolset/patterns/mep` for full workflow.

---

## 4. Documentation (Views, Sheets, Schedules)

| Tool | Description | When to use |
|------|-------------|-------------|
| `revit_create_view` | Create floor plan, section, or 3D view | Generate missing views for documentation |
| `revit_create_sheet` | Create drawing sheet | Start sheet package |
| `revit_place_on_sheet` | Place view/schedule on sheet | Build drawing set |
| `revit_create_schedule` | Create and configure schedule | Door/window/room schedules from config |
| `revit_apply_view_template` | Apply or detach view template | Standardize view appearance |
| `revit_list_views` | All views/sheets with metadata | Discover existing views before creating |
| `revit_list_schedule_fields` | Schedulable fields for a category | Build ScheduleConfig field list |
| `revit_activate_view` | Open view in UI (disrupts engineer) | Only when engineer needs visual confirmation |

See `revit://toolset/patterns/documentation` for sheet package workflow.

---

## 5. Visualization & Annotation

| Tool | Description | When to use |
|------|-------------|-------------|
| `revit_color_by_parameter` | Color splash by param value | Department/status visualization in views |
| `revit_clear_overrides` | Clear graphic overrides | Reset view after QA highlighting |
| `revit_place_tags` | Auto-tag elements in view(s) | Batch tagging doors, equipment, etc. |
| `revit_override_colors` | Direct color override on elements | Pinpoint highlight without param-based splash |

---

## 6. Export & Reporting — Read-only (writes to filesystem)

| Tool | Description | When to use |
|------|-------------|-------------|
| `revit_export_pdf` | Export views to PDF | Deliverable drawing export |
| `revit_export_image` | Export views to image | Quick visual snapshots |
| `revit_export_to_excel` | Export element data with FilterSpec | QA spreadsheets, param audits |
| `revit_export_schedule` | Export schedule to CSV/xlsx | Schedule data for external review |

See `revit://toolset/patterns/export` for options and path conventions.

---

## 7. Document Management

| Tool | Description | When to use |
|------|-------------|-------------|
| `revit_save_document` | Save or SaveAs | Persist changes after write operations |
| `revit_close_document` | Close active document | End session (use with caution) |
| `revit_sync_with_central` | Workshared sync | Publish changes in collaborative models |

**Note:** Use built-in `open_document` to open models — not in toolset.

---

## 8. Infrastructure (Grids, Levels, Status)

| Tool | Description | When to use |
|------|-------------|-------------|
| `revit_generate_grids` | Create grid system | New building setup from spacing spec |
| `revit_generate_levels` | Create levels batch | New building level creation |
| `revit_get_status` | Health + worksharing + selection info | Pre-flight check; respect engineer selection |

---

## Non-Goals (use `execute_csharp_code`)

| Domain | Rationale |
|--------|-----------|
| Wall/floor/roof/ceiling/stair creation | Context-dependent geometry |
| Structural framing, rebar, load cases | Analytical model complexity |
| In-place/nested families | Family Editor context |
| Curtain walls, shaft openings | Multi-element creation |
| IFC import/export | Many options, rarely automated |
| Revit link attach/reload | Infrequent, edge cases |
| Detail items, dimensions, filled regions | View-specific detailing |

---

## Constraints

- **Units:** All geometry in feet (internal). See `revit://toolset/units`.
- **Batch-first:** Prefer array inputs; one transaction per tool named `MCP: {tool_name}`.
- **Pagination:** `revit_find_elements` defaults to 500 max; use `offset` for more.
- **Partial success:** Write tools return `success_count` + `failures[]`. See `revit://toolset/errors`.
- **Collaboration:** Respect worksets, borrowed elements, engineer selection (`revit://model/selection`).
- **Paths:** Export paths validated by `PathGuard`; null = temp directory.

## Live Resources for Tool Chaining

| URI | Purpose |
|-----|---------|
| `revit://model/types` | Type IDs for placement and sheets |
| `revit://model/levels` | Level elevations and associated views |
| `revit://model/views` | View/sheet metadata, templates, on-sheet status |
| `revit://model/worksets` | Workset ownership and element counts |
| `revit://model/links` | Link/CAD paths and load state |
| `revit://model/selection` | Engineer's current selection (intent signal) |
| `revit://model/grids` | Grid reference geometry |
