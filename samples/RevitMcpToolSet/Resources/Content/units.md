# Units Reference

Revit MCP Toolset unit conventions. **Misunderstanding units is the #1 cause of placement errors.**

## Core Rule

**All geometry values in tool parameters are in feet** — Revit's internal database units.

| Quantity | Toolset unit | Exception |
|----------|--------------|-----------|
| Coordinates (x, y, z) | feet | — |
| Distances, widths, heights, diameters | feet | — |
| Bounding boxes | feet | — |
| Sheet positions | feet | — |
| Angles / rotation | degrees | — |
| Colors | [r, g, b] integers 0–255 | — |
| Insulation thickness | — | `thickness_mm` (explicit mm) |
| Element IDs | long (int64) | — |

---

## Conversion Table

| From | To | Multiply by |
|------|-----|-------------|
| feet | millimeters | × 304.8 |
| feet | meters | × 0.3048 |
| feet | inches | × 12 |
| millimeters | feet | ÷ 304.8 |
| meters | feet | ÷ 0.3048 |
| inches | feet | ÷ 12 |

### Common Values

| Description | Feet | mm | Meters |
|-------------|------|-----|--------|
| 1 foot | 1.0 | 304.8 | 0.3048 |
| 1 meter | 3.28084 | 1000 | 1.0 |
| 200mm wall | 0.656 | 200 | 0.2 |
| 3m ceiling | 9.843 | 3000 | 3.0 |
| 10ft grid spacing | 10.0 | 3048 | 3.048 |

---

## Display vs Internal

Revit stores all geometry internally in **feet**. The UI may display in mm, m, or fractional inches based on project settings.

```
Internal (API/toolset):  10.0 feet
Display (UI, mm project): 3048 mm
Display (UI, m project):  3.048 m
```

Check display units via built-in `revit://model/context` or:

```csharp
doc.GetUnits().GetFormatOptions(SpecTypeId.Length)
```

**Never convert toolset inputs to display units** — always provide feet.

---

## Conversion Examples

### Placing a door 3m from origin along X

```
3 meters = 3 / 0.3048 = 9.8425 feet
placement: { "x": 9.8425, "y": 0.0, "z": 0.0 }
```

### Duct 600mm × 400mm cross-section

```
width:  600 / 304.8 = 1.969 feet
height: 400 / 304.8 = 1.312 feet
```

### Level elevation (from revit://model/levels)

Level elevations in live resources are in **feet** (internal). Convert for human display:

```
elevation_feet = 12.0
elevation_mm = 12.0 × 304.8 = 3657.6 mm
```

---

## Common Pitfalls

| Pitfall | Correct approach |
|---------|------------------|
| Passing mm coordinates to `revit_place_duct` | Convert to feet first |
| Using UI-displayed values directly | Read internal values or convert |
| Mixing degrees and radians | Toolset uses degrees only |
| Negative Z for below-grade | Use negative feet values (e.g., -10.0) |
| Area in sq meters | FilterSpec numeric filters use internal sq feet |
| Slope as percentage | Use ratio: 1% = 0.01 |

---

## FilterSpec Coordinates

Bounding box filters use feet:

```json
{
  "type": "bounding_box",
  "min_point": [0, 0, 0],
  "max_point": [32.8, 32.8, 10],
  "mode": "inside"
}
```

The example max_point ~32.8 feet ≈ 10 meters.

---

## Quick Reference for Agents

1. **Input to tools:** always feet (except `thickness_mm`)
2. **Output from tools:** elevations, bboxes, coordinates in feet
3. **Human-facing text:** convert feet → mm/m for display only
4. **When unsure:** read `revit://model/levels` and compare Z values
