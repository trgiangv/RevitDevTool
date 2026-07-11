# Export Patterns

Export options for PDF, image, Excel, and schedule outputs. All export tools are read-only for the model but write to the filesystem.

## Path Conventions

- Paths validated by `PathGuard` — must be writable, no traversal attacks
- `null` / omitted directory → auto-generated in system temp folder
- Use absolute paths on Windows: `C:\\Export\\report.xlsx`
- Returns file paths in response — agent reads files separately

---

## PDF Export (`revit_export_pdf`)

```json
{
  "viewIds": [123456, 234567, 345678],
  "directory": "C:\\Export\\Drawings",
  "combineIntoSingle": true
}
```

| Parameter | Default | Notes |
|-----------|---------|-------|
| `viewIds` | active view | Array of view element IDs |
| `directory` | temp | Output folder |
| `combineIntoSingle` | false | Single PDF vs one per view |

**Return:**
```json
{
  "filePaths": ["C:\\Export\\Drawings\\Project1.pdf"],
  "pageCount": 3
}
```

**Defaults:** 300 DPI, 100% zoom, default paper format.

---

## Image Export (`revit_export_image`)

```json
{
  "viewIds": [123456],
  "format": "png",
  "directory": "C:\\Export\\Images",
  "resolution": 150
}
```

| Parameter | Default | Notes |
|-----------|---------|-------|
| `format` | png | `png`, `jpg`, or `bmp` |
| `resolution` | 150 | DPI |

---

## Excel Export (`revit_export_to_excel`)

```json
{
  "filters": {
    "filters": [
      { "type": "category", "names": ["Walls"] },
      { "type": "level", "level_name": "Level 1" }
    ],
    "logic": "and"
  },
  "parameters": ["Mark", "Level", "Area", "Comments"],
  "outputPath": "C:\\Export\\walls_level1.xlsx"
}
```

| Parameter | Default | Notes |
|-----------|---------|-------|
| `filters` | required | FilterSpec — narrow scope for performance |
| `parameters` | all available | Column list |
| `outputPath` | auto temp path | Full file path including filename |

**Batch pattern:** Export per category or per level to separate files:

```
revit_export_to_excel(filters=walls_L1, outputPath=walls_L1.xlsx)
revit_export_to_excel(filters=doors_L1, outputPath=doors_L1.xlsx)
```

---

## Schedule Export (`revit_export_schedule`)

```json
{
  "scheduleId": 456789,
  "format": "xlsx",
  "outputPath": "C:\\Export\\door_schedule.xlsx"
}
```

| Parameter | Notes |
|-----------|-------|
| `scheduleId` | ViewSchedule element ID |
| `format` | `csv` or `xlsx` |
| `outputPath` | null = temp path |

---

## Batch Export Workflow

```
revit_list_views() → collect viewIds for sheets
→ revit_export_pdf(viewIds, directory, combineIntoSingle=true)
→ revit_find_elements(walls filter) → revit_export_to_excel
→ revit_export_schedule(scheduleId, xlsx)
```

---

## Performance Guidelines

| Tool | Guideline |
|------|-----------|
| `revit_export_to_excel` | Always use FilterSpec; avoid whole-model export |
| `revit_export_pdf` | Batch views with `combineIntoSingle` for deliverables |
| Large datasets | Tool streams to file; response returns path only |
| Image export | Lower resolution (150 DPI) for previews; 300 for print |

---

## Common Pitfalls

| Issue | Recovery |
|-------|----------|
| Invalid path | Use PathGuard-safe absolute paths; check directory exists |
| View not exportable | Templates and sheets have restrictions — use plan/section/3D views |
| Empty Excel | Verify FilterSpec matches elements first with `revit_find_elements` |
| Missing schedule | Create with `revit_create_schedule` before export |
