# MEP Placement Workflow

Structured workflow for duct, pipe, and conduit placement using toolset tools.

## Overview

```
1. Discover types     → revit_list_types / revit://model/types
2. Pick IDs           → ductTypeId, systemTypeId, levelId
3. Place segments     → revit_place_duct / revit_place_pipe / revit_place_conduit
4. Validate           → revit_list_mep_systems
5. Insulate (optional)→ revit_insulate_duct_system
```

---

## Step 1: Type Discovery

List MEP system types:

```
revit_list_types(kind="mep_system")
```

Or read live resource `revit://model/types` for `mepSystemTypes` with IDs.

For duct/pipe/conduit **curve types** (not system types), list family types:

```
revit_list_types(kind="family", category="Ducts")
revit_list_types(kind="family", category="Pipes")
revit_list_types(kind="family", category="Conduit")
```

---

## Step 2: Resolve Level

Get level ID from `revit://model/levels` or `revit_get_model_summary`:

```json
{ "id": 311, "name": "Level 1", "elevation": 0.0 }
```

---

## Step 3: Place Duct Segment

```json
{
  "ductTypeId": 45821,
  "systemTypeId": 12345,
  "levelId": 311,
  "start": [10.0, 5.0, 12.0],
  "end": [50.0, 5.0, 12.0],
  "width": 2.0,
  "height": 1.5
}
```

Round duct — use `diameter` instead of `width`/`height`:

```json
{
  "ductTypeId": 45821,
  "systemTypeId": 12345,
  "levelId": 311,
  "start": [10.0, 5.0, 12.0],
  "end": [50.0, 5.0, 12.0],
  "diameter": 1.5
}
```

All coordinates and dimensions in **feet**.

---

## Step 4: Place Pipe Segment

```json
{
  "pipeTypeId": 33456,
  "systemTypeId": 23456,
  "levelId": 311,
  "start": [10.0, 8.0, 10.0],
  "end": [40.0, 8.0, 10.0],
  "diameter": 0.5,
  "slope": 0.01
}
```

`slope` is a ratio (0.01 = 1% grade).

---

## Step 5: Place Conduit Segment

```json
{
  "conduitTypeId": 44556,
  "systemTypeId": 34567,
  "levelId": 311,
  "start": [5.0, 3.0, 10.0],
  "end": [30.0, 3.0, 10.0],
  "diameter": 0.25
}
```

---

## Step 6: Verify Systems

```
revit_list_mep_systems(kind="duct")
revit_list_mep_systems(kind="pipe")
revit_list_mep_systems(kind="electrical")
revit_list_mep_systems(kind="all")
```

Expected return:

```json
{
  "systems": [
    {
      "id": 123,
      "name": "Supply Air 1",
      "type": "Supply Air",
      "element_count": 45,
      "classification": "Supply"
    }
  ]
}
```

Confirm `element_count` increased after placement.

---

## Step 7: Insulate Duct System (Optional)

After verifying system assignment:

```json
{
  "systemId": 123,
  "insulationTypeId": 56789,
  "thickness_mm": 25
}
```

Note: `thickness_mm` is the exception — explicit millimeters suffix.

---

## Common Pitfalls

| Issue | Recovery |
|-------|----------|
| Wrong system type ID | Re-list types; duct systems ≠ pipe systems |
| Level mismatch | Check `revit://model/levels` elevation vs Z coordinate |
| Units confusion | All placement coords in feet; only insulation uses mm |
| Fitting gaps | Toolset places straight segments only; use `execute_csharp_code` for fittings |
| Borrowed elements | Check workset ownership via `revit://model/worksets` |

## Full Example Sequence

```
revit_list_types(kind="mep_system")
→ pick systemTypeId=12345, ductTypeId=45821
→ revit://model/levels (pick levelId=311)
→ revit_place_duct({ ductTypeId, systemTypeId, levelId, start, end, width, height })
→ revit_list_mep_systems(kind="duct") — verify element_count
→ [on failure] undo_changes
```
