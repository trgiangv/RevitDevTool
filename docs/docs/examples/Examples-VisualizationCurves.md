# Example: Curve Visualization

**Demo:** Visualize curves and edges in 3D view
**Script:** [`visualization_curve_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/visualization_curve_script.py)
**Time:** 1-2 minutes

---

## What It Demonstrates

- Pick edges from elements
- Extract location curves from walls
- Generate lines, arcs, and splines
- Display geometry in 3D view without creating model elements

---

## Key Pattern: Pick & Visualize Edge

```python
from Autodesk.Revit.UI.Selection import ObjectType

print("Select an edge...")
ref = uidoc.Selection.PickObject(ObjectType.Edge, "Select an edge")

elem = doc.GetElement(ref)
edge = elem.GetGeometryObjectFromReference(ref)

if edge:
    print(edge)  # Auto-visualized in 3D view
```

**Result:**
- User picks edge in Revit
- Edge highlighted in 3D view
- Color-coded for visibility

---

## Key Pattern: Wall Location Curves

```python
from Autodesk.Revit.DB import FilteredElementCollector, Wall

walls = FilteredElementCollector(doc).OfClass(Wall).ToElements()

print(f"Visualizing {len(walls)} wall location curves...")
for wall in walls:
    curve = wall.Location.Curve
    print(curve)  # Each curve appears in 3D
```

**Result:**
- All wall centerlines displayed
- No model elements created
- Clearable with "Clear" button

---

## Key Pattern: Generate Geometry

```python
from Autodesk.Revit.DB import XYZ, Line, Arc

# Create lines
origin = XYZ(0, 0, 0)
end_point = XYZ(10, 10, 0)
line = Line.CreateBound(origin, end_point)
print(line)

# Create arc
center = XYZ(0, 0, 0)
radius = 5
arc = Arc.Create(center, radius, 0, Math.PI, XYZ.BasisX, XYZ.BasisY)
print(arc)
```

**Result:**
- Geometry generated programmatically
- Displayed immediately
- Useful for debugging calculations

---

## Try It Yourself

1. **Load** folder: `samples/PythonDemo/commands/`
2. **Execute** `visualization_curve_script.py`
3. **Pick** edges when prompted (or ESC to skip)
4. **See** geometry in 3D view

**Full source:** [`visualization_curve_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/visualization_curve_script.py)

---

## Related Examples

- **XYZ points** → [`visualization_xyz_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/visualization_xyz_script.py)
- **Solids & faces** → [`visualization_solid_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/visualization_solid_script.py)
- **Data analysis** → [`data_analysis_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/data_analysis_script.py)
