# FilterSpec Query Patterns

Composable element search for `revit_find_elements`. All bounding-box coordinates are in **feet** (Revit internal units).

## Basic Structure

```json
{
  "filters": [ /* FilterItem[] */ ],
  "logic": "and"
}
```

- `logic: "and"` — all filters must match (default)
- `logic: "or"` — any filter matches

---

## Pattern 1: Category + Level

Find all walls on Level 1:

```json
{
  "filters": [
    { "type": "category", "names": ["Walls"] },
    { "type": "level", "level_name": "Level 1" }
  ],
  "logic": "and"
}
```

---

## Pattern 2: Parameter String Filter

Doors with Mark containing "EXT":

```json
{
  "filters": [
    { "type": "category", "names": ["Doors"] },
    { "type": "parameter_string", "parameter_name": "Mark", "operator": "contains", "value": "EXT" }
  ],
  "logic": "and"
}
```

**String operators:** `equals`, `not_equals`, `contains`, `not_contains`, `begins_with`, `ends_with`

---

## Pattern 3: Parameter Numeric Filter

Rooms with area > 100 sq ft:

```json
{
  "filters": [
    { "type": "category", "names": ["Rooms"] },
    { "type": "parameter_numeric", "parameter_name": "Area", "operator": "greater_than", "value": 100.0 }
  ],
  "logic": "and"
}
```

**Numeric operators:** `equals`, `not_equals`, `greater_than`, `less_than`, `greater_or_equal`, `less_or_equal`

---

## Pattern 4: Spatial Bounding Box

Elements inside a region (feet):

```json
{
  "filters": [
    { "type": "category", "names": ["Mechanical Equipment", "Ducts"] },
    {
      "type": "bounding_box",
      "min_point": [0, 0, 0],
      "max_point": [100, 80, 30],
      "mode": "inside"
    }
  ],
  "logic": "and"
}
```

- `mode: "inside"` — element bbox fully inside region (default)
- `mode: "intersecting"` — any overlap with region

---

## Pattern 5: Combined OR (Multiple Categories)

Walls OR doors on any level:

```json
{
  "filters": [
    { "type": "category", "names": ["Walls"] },
    { "type": "category", "names": ["Doors"] }
  ],
  "logic": "or"
}
```

---

## Pattern 6: Workset + Phase

Elements in a specific workset and phase:

```json
{
  "filters": [
    { "type": "workset", "workset_name": "Architecture" },
    { "type": "phase", "phase_name": "New Construction" }
  ],
  "logic": "and"
}
```

---

## Pattern 7: Exclusion List

All walls except known IDs:

```json
{
  "filters": [
    { "type": "category", "names": ["Walls"] },
    { "type": "exclusion", "element_ids": [12345, 67890] }
  ],
  "logic": "and"
}
```

---

## Pattern 8: Engineer Selection Scope

Respect engineer's current selection:

```
revit_find_elements({ "filters": { "filters": [], "logic": "and" }, "selected_only": true })
```

Check `revit://model/selection` first to understand intent.

---

## Performance Tips

| Guideline | Detail |
|-----------|--------|
| Default limit | 500 results; set `max_results` and `offset` for pagination |
| Narrow early | Combine `category` + `level` before broad param filters |
| Avoid whole-model scans | Never query without at least one narrowing filter |
| Field selection | Request only needed `fields` to reduce payload |
| Types vs instances | Set `include_types` / `include_instances` explicitly |
| Chunk reads | For >100 elements, sample with `revit_read_parameters` before batch writes |
| Spatial pre-filter | Use `bounding_box` to limit MEP/coordination queries |

## Typical Workflow

```
revit_get_model_summary → revit_find_elements (FilterSpec) → revit_read_parameters (sample) → revit_export_to_excel
```
