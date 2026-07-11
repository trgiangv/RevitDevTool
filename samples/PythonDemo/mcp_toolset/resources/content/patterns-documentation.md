# Documentation Package Workflow

Sheet package workflow: views → sheets → viewports → templates → export.

## Overview

```
1. Inventory          → revit_list_views / revit://model/views
2. Create views       → revit_create_view
3. Apply templates    → revit_apply_view_template
4. Create sheets      → revit_create_sheet
5. Place on sheet     → revit_place_on_sheet
6. Create schedules   → revit_create_schedule (optional)
7. Export             → revit_export_pdf
```

---

## Step 1: Inventory Existing Views

```
revit_list_views(includeSheets=true, includeTemplates=true)
```

Or `revit://model/views` for JSON with template and on-sheet metadata.

---

## Step 2: Create Floor Plan

```json
{
  "viewType": "floor_plan",
  "levelName": "Level 1",
  "viewName": "Level 1 - Architectural",
  "templateName": "Architectural Plan"
}
```

---

## Step 3: Create Section

```json
{
  "viewType": "section",
  "min": [0, 0, 0],
  "max": [100, 50, 30],
  "directionAngle": 0,
  "depth": 50,
  "viewName": "Section A-A"
}
```

- `directionAngle`: 0 = North/+Y, 90 = West/-X (degrees)
- Bounding box in feet

---

## Step 4: Create 3D View

```json
{
  "viewType": "3d",
  "viewName": "3D Overview",
  "templateName": "3D View Template",
  "isBoundingBox": true
}
```

---

## Step 5: Apply View Template

```json
{
  "viewId": 123456,
  "templateName": "Architectural Plan"
}
```

Detach template:

```json
{ "viewId": 123456, "templateName": null }
```

Resolve template names from `revit://model/types` → `viewTemplates`.

---

## Step 6: Create Sheet

```json
{ "titleBlockId": 78901 }
```

Omit `titleBlockId` to use first available title block. Get IDs from `revit://model/types` → `titleBlocks`.

---

## Step 7: Place View on Sheet

```json
{
  "sheetId": 456789,
  "viewOrScheduleId": 123456,
  "position": [1.0, 1.0]
}
```

- `position`: [x, y] in feet on sheet; omit for origin placement
- Works for views and schedules

---

## Step 8: Create Schedule (Optional)

First discover fields:

```
revit_list_schedule_fields(categoryName="Doors")
```

Then create:

```json
{
  "categoryName": "Doors",
  "scheduleName": "Door Schedule",
  "fields": ["Mark", "Width", "Height", "Level", "Comments"],
  "sortRules": [
    { "field": "Level", "ascending": true },
    { "field": "Mark", "ascending": true }
  ],
  "filterRules": [
    { "field": "Width", "operator": "greater_than", "value": "900", "isNumeric": true }
  ],
  "groupRules": [
    { "field": "Level", "showHeader": true, "showFooter": true }
  ]
}
```

Place schedule on sheet with same `revit_place_on_sheet` call.

---

## Step 9: Export PDF

```json
{
  "viewIds": [123456, 234567],
  "directory": "C:\\Export\\Package",
  "combineIntoSingle": true
}
```

- `viewIds`: null = active view only
- `directory`: null = temp directory (PathGuard validated)
- `combineIntoSingle`: true = one PDF, false = one per view

---

## Full Example Sequence

```
revit_list_views(includeSheets=true)
→ revit_create_view(floor_plan, Level 1)
→ revit_apply_view_template(viewId, "Architectural Plan")
→ revit_create_sheet(titleBlockId from revit://model/types)
→ revit_place_on_sheet(sheetId, viewId, position)
→ revit_list_schedule_fields("Doors")
→ revit_create_schedule(config)
→ revit_place_on_sheet(sheetId, scheduleId)
→ revit_export_pdf(viewIds, directory, combineIntoSingle=true)
```

---

## Cautions

| Concern | Guidance |
|---------|----------|
| `revit_activate_view` | Disrupts engineer's active view — avoid unless requested |
| Template mismatch | Match template ViewType to target view type |
| Sheet numbers | Auto-assigned; use `revit_list_views` to find sheet IDs |
| Unplaced views | Check `onSheet` in `revit://model/views` before export |
