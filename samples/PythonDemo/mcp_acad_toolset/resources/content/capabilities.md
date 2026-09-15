# AutoCAD MCP Toolset — Capabilities

Lightweight Python toolset for AutoCAD / Civil 3D / Plant 3D via AcadDevTool.
Use these `acad_*` tools first; fall back to built-in `execute_csharp_code` /
`execute_python_code` for geometry the catalog does not cover.

## Decision Tree

```
Task requested
├─ Status / layers / selection? → acad_get_status, acad_list_layers, acad_get_selection
├─ Find entities by type or layer? → acad_find_entities
├─ Draw LINE / CIRCLE? → acad_draw_line / acad_draw_circle
├─ Show entities in the editor? → acad_highlight_entities
├─ Delete by handle? → acad_erase_entities
└─ Anything else (hatch, dim, blocks, layouts, Civil objects)
    → execute_csharp_code or execute_python_code
```

**Built-in companions (not in this toolset):** `open_document`, `execute_csharp_code`,
`execute_python_code`, `navigate_history`, `view_screenshot`, `acad://python-cheatsheet`.

Coordinates and sizes are in **drawing units**. Handles are hex (with or without `0x`).

## Query (read-only)

| Tool | Description |
|------|-------------|
| `acad_get_status` | Drawing name, path, host version, entity/layer/selection counts |
| `acad_list_layers` | Layer table: off / frozen / locked / color |
| `acad_find_entities` | Model-space search by CLR type name and/or layer |
| `acad_get_selection` | Implied editor selection |

## Drawing (write)

| Tool | Description |
|------|-------------|
| `acad_draw_line` | Create LINE from start/end `[x,y]` or `[x,y,z]` |
| `acad_draw_circle` | Create CIRCLE from center + radius |
| `acad_highlight_entities` | Set implied selection from handles (non-destructive) |
| `acad_erase_entities` | Erase entities by handle |

## Resources

| URI | Description |
|-----|-------------|
| `acad://toolset/capabilities` | This catalog |
| `acad://drawing/context` | Live drawing summary JSON |
| `acad://drawing/layers` | Layer table JSON |
| `acad://drawing/selection` | Current selection JSON |
| `acad://entity/{handle}` | One entity summary |

## Non-goals

Layouts / paper space, blocks/attributes, hatches, dimensions, xrefs, Civil 3D
surfaces/alignments, and Plot. Use `execute_csharp_code` (read `acad://csharp-cheatsheet`)
or `execute_python_code`. After a bad write, `navigate_history(direction="back")`.
