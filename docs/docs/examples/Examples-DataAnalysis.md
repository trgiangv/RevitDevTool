# Example: Data Analysis with Polars

**Demo:** Collect Revit data → Analyze with Polars → Visualize results
**Script:** [`data_analysis_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/data_analysis_script.py)
**Time:** 2-3 minutes

---

## What It Does

Complete workflow combining all three modules:
1. **Code Execution** - Auto-install Polars via PEP 723
2. **Logging** - Structured output with syntax highlighting
3. **Visualization** - Display outliers in 3D view

---

## Key Pattern: PEP 723 Dependencies

```python
# /// script
# dependencies = [
#     "polars==1.38.1",
# ]
# ///
```

**When you click Execute:**
- Code Execution checks if `polars` is installed
- If missing, Pixi installs it automatically (~5 seconds). On Plant 3D the same metadata uses the uv sidecar instead.
- Script runs with full access to Polars DataFrame API

---

## Key Pattern: Revit Data Collection

```python
from Autodesk.Revit.DB import FilteredElementCollector, Wall

doc = __revit__.ActiveUIDocument.Document
walls = FilteredElementCollector(doc).OfClass(Wall).ToElements()

data = []
for wall in walls:
    data.append({
        "Id": wall.Id.IntegerValue,
        "Name": wall.Name,
        "Area": wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble(),
    })
```

**Output in Trace Panel:**
```
Collecting wall data...
Collected 127 walls
```

---

## Key Pattern: DataFrame Analysis

```python
import polars as pl

df = pl.DataFrame(data)

# Statistical analysis
stats = df.select(pl.col("Area").describe())
print(stats)

# Find outliers (area > 2 std deviations)
mean_area = df["Area"].mean()
std_area = df["Area"].std()
outliers = df.filter(pl.col("Area") > mean_area + 2 * std_area)
```

**Output in Trace Panel:**
```
Wall Area Statistics:
┌──────────┬────────────┐
│ statistic│ value      │
├──────────┼────────────┤
│ count    │ 127.0      │
│ mean     │ 245.8      │
│ std      │ 78.3       │
│ min      │ 12.5       │
│ max      │ 892.1      │
└──────────┴────────────┘

Found 3 outlier walls
```

---

## Key Pattern: Geometry Visualization

```python
# Get walls from outlier IDs
outlier_ids = [ElementId(id) for id in outliers["Id"]]
outlier_walls = [doc.GetElement(id) for id in outlier_ids]

# Print geometry → auto-visualized in 3D
for wall in outlier_walls:
    curve = wall.Location.Curve
    print(f"Wall {wall.Id}: Area={wall_area:.1f} sq ft")
    print(curve)  # Appears in 3D view with color
```

**Result:**
- Outlier walls highlighted in 3D view
- Trace Panel shows IDs + areas
- Clear visual feedback for analysis

---

## Try It Yourself

1. **Open** RevitDevTool panel
2. **Navigate** to Code Execution tab
3. **Load** folder: `samples/PythonDemo/commands/`
4. **Click** `data_analysis_script.py`
5. **Watch** dependencies install (first run only)
6. **See** results in Trace Panel + 3D view

**Full source:** [`data_analysis_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/data_analysis_script.py)

---

## Related Examples

- **Logging tests** → [`logging_format_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/logging_format_script.py)
- **Visualization tests** → [`visualization_curve_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/visualization_curve_script.py)
- **Dashboard UI** → [`dashboard_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/dashboard_script.py)
