# Python Test Scripts

Python scripts demonstrating RevitDevTool's three core modules: **CodeExecute**, **Logging**, and **Visualization**.

All scripts follow the naming convention `*_script.py` to be recognized by the CodeExecute system.

---

## 📊 Dashboard & Data Analysis

### `dashboard_script.py`
Main dashboard application with WebView2 UI.
- **Dependencies**: polars, numpy, openpyxl
- **Features**: Modern web-based dashboard with charts

### `data_analysis_script.py`
Complete analysis workflow combining all three modules.
- **Dependencies**: polars
- **Features**: 
  - Data collection from Revit elements
  - Statistical analysis with Polars
  - Outlier detection
  - Geometry visualization of results

---

## 🎨 Visualization Tests

### `visualization_xyz_script.py`
XYZ point visualization tests.
- Pick single/multiple points
- Generate point grids
- Display points in 3D view

### `visualization_curve_script.py`
Curve and edge visualization tests.
- Pick edges from elements
- Visualize wall location curves
- Generate lines, arcs, splines
- Test polylines and curve loops

### `visualization_solid_script.py`
Solid and face visualization tests.
- Extract solids from elements
- Display faces with area information
- Visualize bounding boxes
- Batch solid visualization

---

## 📝 Logging Tests

### `logging_batch_script.py`
Batch logging performance tests.
- Small batch (100 messages)
- Medium batch (1,000 messages)
- Large batch (10,000 messages - stress test)
- Log level keyword detection
- URL detection and formatting

### `logging_format_script.py`
Log formatting and syntax highlighting tests.
- Log level detection (INFO, WARN, ERROR, FATAL, DEBUG)
- Scalar value formatting
- JSON pretty-printing with syntax highlighting
- Complex nested object formatting
- Exception handling with stack traces

---

## 🧪 Scientific Computing Examples

### `sklearn_script.py`
Machine learning with scikit-learn.
- **Dependencies**: scikit-learn, numpy

### `shapely_script.py`
Geometric operations with Shapely.
- **Dependencies**: shapely

### `trimesh_script.py`
3D mesh processing with trimesh.
- **Dependencies**: trimesh

### `modeless_script.py`
Modeless window example.
- **Dependencies**: (none)

### `module_test_script.py`
High-complexity module reset diagnostics.
- **Dependencies**: (none)
- **Features**:
  - Nested package imports
  - Dynamic plugin loading via `importlib`
  - Function cache state (`lru_cache`)
  - Module-level session markers to verify reset behavior

---

## 🚀 Running Scripts

### In Revit:
1. Open RevitDevTool panel
2. **CodeExecute** tab
3. Load folder containing scripts
4. Click script to execute

### Features:
- **Auto-dependency resolution** - PEP 723 metadata automatically installs packages via UV
- **Hot reload** - Changes detected and scripts reloaded instantly
- **Print redirection** - `print()` outputs to Trace panel with syntax highlighting
- **Geometry visualization** - `print(geometry_object)` displays in 3D view

---

## 📚 Related Files

### C# Demo Files (RevitDevTool.DotnetDemo)
- `XYZVisualization.cs` - Point visualization
- `CurveVisualization.cs` - Curve visualization
- `SolidVisualization.cs` - Solid visualization
- `FaceVisualization.cs` - Face visualization
- `MeshVisualization.cs` - Mesh visualization
- `Log.cs` - Comprehensive logging tests

### Documentation
- [Visualization Module](../../../../docs/Visualization/index.md)
- [Logging Module](../../../../docs/Logging/index.md)
- [CodeExecute Module](../../../../docs/CodeExecute/index.md)
