# PythonDemo - Developer Guide

This guide covers development workflow for creating demo scripts and extending the dashboard application.

---

## 🛠️ Development Setup

### Prerequisites
- **UV** - Python package manager ([install guide](https://docs.astral.sh/uv/getting-started/installation/))
- **Node.js 18+** and npm (only for dashboard frontend development)
- **Visual Studio Code** (recommended) with Python extension

### Basic Environment Setup

```powershell
cd source/RevitDevTool.PythonDemo

# Install dependencies (uv automatically creates .venv if needed)
uv sync

# Verify installation
uv run python -c "import polars; print('Polars OK')"
```

### Dashboard Frontend Setup (Optional)

Only needed if developing dashboard UI:

```powershell
cd source/RevitDevTool.PythonDemo/revit_dashboard_ui

# Install dependencies
npm install

# Build for production
npm run build
```

---

## 🔍 Quality Gates

### Python Quality Checks

Run before committing Python code:

```powershell
cd source/RevitDevTool.PythonDemo

# Lint with Ruff (auto-fix available)
uv run --with ruff ruff check commands/
uv run --with ruff ruff check --fix commands/  # Auto-fix

# Or check specific script
uv run --with ruff ruff check commands/data_analysis_script.py
```

**Configured in `pyproject.toml`:**
- Ruff: 100+ rules including complexity (mccabe C90)
- Line length: 120 characters

**Common Issues:**
- **F401 (unused import):** Remove or add `# noqa: F401` if intentional
- **C901 (complex function):** Refactor into smaller functions

### Frontend Quality Checks (Dashboard Only)

```powershell
cd source/RevitDevTool.PythonDemo/revit_dashboard_ui

# Run all checks
npm run quality

# Individual checks
npm run typecheck     # TypeScript compiler
npm run lint          # ESLint + SonarJS
```

---

## 📝 Creating Demo Scripts

### Script Template

Create new file in `commands/*_script.py`:

```python
# /// script
# dependencies = [
#     "polars==1.38.1",  # Optional: declare dependencies
# ]
# ///

"""
Script Description
Demonstrates [feature] using [module].
"""

# Access Revit context
uiapp = __revit__  # type: ignore
uidoc = uiapp.ActiveUIDocument
doc = uidoc.Document

def main():
    """Main function"""
    print("Starting demo...")
    
    # Your code here
    
    print("Demo complete!")

if __name__ == "__main__":
    main()
```

### Example: Visualization Script

```python
# /// script
# dependencies = []
# ///

"""Test XYZ Point Visualization"""

from Autodesk.Revit.DB import XYZ
from Autodesk.Revit.UI.Selection import ObjectType

uiapp = __revit__  # type: ignore
uidoc = uiapp.ActiveUIDocument

def test_pick_point():
    """Pick single point and visualize"""
    try:
        print("Pick a point...")
        ref = uidoc.Selection.PickObject(ObjectType.PointOnElement)
        point = ref.GlobalPoint
        
        print(f"Selected point: {point}")
        print(point)  # Visualize in 3D view
        
    except Exception as e:
        print(f"ERROR: {e}")

if __name__ == "__main__":
    test_pick_point()
```

### Example: Data Analysis Script

```python
# /// script
# dependencies = ["polars==1.38.1"]
# ///

"""Analyze Wall Dimensions"""

import polars as pl
from Autodesk.Revit.DB import FilteredElementCollector, Wall, BuiltInParameter

uiapp = __revit__  # type: ignore
doc = uiapp.ActiveUIDocument.Document

def main():
    # Collect data
    walls = FilteredElementCollector(doc).OfClass(Wall).ToElements()
    
    data = []
    for wall in walls:
        area = wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)
        data.append({
            "Id": wall.Id.IntegerValue,
            "Area": area.AsDouble() if area else 0
        })
    
    # Analyze with Polars
    df = pl.DataFrame(data)
    print(f"Total walls: {len(df)}")
    print(f"Total area: {df['Area'].sum():.2f} sq ft")
    print(f"Average area: {df['Area'].mean():.2f} sq ft")
    
    # Find outliers
    threshold = df['Area'].quantile(0.95)
    outliers = df.filter(pl.col("Area") > threshold)
    print(f"\nLarge walls (>{threshold:.2f} sq ft): {len(outliers)}")

if __name__ == "__main__":
    main()
```

---

## 🏗️ Extension Points

### A. Adding Demo Scripts

**Process:**
1. Create `my_feature_script.py` in `commands/`
2. Add PEP 723 dependencies if needed
3. Test in Revit via CodeExecute UI
4. Document in `commands/README.md`

**Naming Convention:**
- `visualization_*_script.py` - Geometry visualization demos
- `logging_*_script.py` - Logging feature demos
- `data_analysis_*_script.py` - Data workflow demos
- `*_script.py` - Other demos (must end in `_script.py`)

### B. Extending Dashboard Analytics

**1. Add Custom KPI (`revit_dashboard/analytics/polars_engine.py`)**

```python
def calculate_custom_kpi(df: pl.DataFrame) -> dict:
    """Calculate custom KPI metric."""
    return {
        "custom_metric": df.select(
            pl.col("some_column").filter(...).count()
        ).item()
    }

def build_analytics_payload(elements: list[dict]) -> dict:
    df = pl.DataFrame(elements)
    
    # Add your custom analytics
    custom_kpi = calculate_custom_kpi(df)
    
    return {
        "kpi": {**existing_kpi, **custom_kpi},
        # ... rest of payload
    }
```

**2. Update Payload Contract (`contracts/payload.py`)**

```python
from typing import TypedDict

class KpiMetrics(TypedDict):
    total_elements: int
    unique_families: int
    custom_metric: int  # Add your new metric
```

**3. Update Frontend Types (`dashboard-ui/src/types/dashboard.ts`)**

```typescript
export interface KpiMetrics {
  total_elements: number;
  unique_families: number;
  custom_metric: number;  // Add matching TypeScript type
}
```

**4. Display in UI (`dashboard-ui/src/components/KpiCard.tsx`)**

```tsx
<Card>
  <CardHeader>Custom Metric</CardHeader>
  <CardContent>{kpi.custom_metric}</CardContent>
</Card>
```

### Adding New Filters

**1. Collect Filter Options (`analytics/polars_engine.py`)**

```python
def get_filter_options(df: pl.DataFrame) -> dict:
    return {
        # Existing filters
        "categories": df["category"].unique().sort().to_list(),
        
        # Add new filter
        "custom_property": df.filter(
            pl.col("custom_property").is_not_null()
        )["custom_property"].unique().sort().to_list()
    }
```

**2. Add Filter State (`dashboard-ui/src/App.tsx`)**

```tsx
const [filters, setFilters] = useState({
  categories: [],
  families: [],
  customProperty: [],  // Add new filter state
});
```

**3. Add Filter UI Component**

```tsx
<FilterSelect
  label="Custom Property"
  options={filterOptions.custom_property}
  value={filters.customProperty}
  onChange={(value) => setFilters({...filters, customProperty: value})}
/>
```

**4. Apply Filter Logic**

```tsx
const filteredElements = useMemo(() => {
  return rawElements.filter(el => {
    if (filters.customProperty.length > 0 && 
        !filters.customProperty.includes(el.custom_property)) {
      return false;
    }
    // ... other filters
    return true;
  });
}, [rawElements, filters]);
```

### Adding New Charts

**1. Generate Chart Data (`analytics/polars_engine.py`)**

```python
def get_custom_distribution(df: pl.DataFrame) -> dict:
    return (
        df.group_by("custom_dimension")
        .agg(pl.count().alias("count"))
        .sort("count", descending=True)
        .to_dicts()
    )
```

**2. Add to Payload**

```python
"charts": {
    "category_distribution": [...],
    "custom_distribution": get_custom_distribution(df)
}
```

**3. Create Chart Component (`dashboard-ui/src/components/CustomChart.tsx`)**

```tsx
import { BarChart, Bar, XAxis, YAxis, Tooltip } from 'recharts';

export function CustomChart({ data }: { data: Array<{custom_dimension: string, count: number}> }) {
  return (
    <BarChart data={data} width={400} height={300}>
      <XAxis dataKey="custom_dimension" />
      <YAxis />
      <Tooltip />
      <Bar dataKey="count" fill="#8884d8" />
    </BarChart>
  );
}
```

---

## 🧪 Testing Strategies

### Testing Demo Scripts

**Basic Script Testing:**

1. **Run in CodeExecute UI** - Use the script executor panel in Revit
2. **Check console output** - Verify print statements and results
3. **Inspect geometry visualization** - If script uses `print(geometry_object)`

**Example Test Workflow:**

```python
# 1. Add debug prints
def main():
    print("Starting test...")
    walls = FilteredElementCollector(doc).OfClass(Wall)
    print(f"Found {walls.GetElementCount()} walls")  # Verify collection
    
    # 2. Process in small batches
    for wall in list(walls)[:5]:  # Test with first 5 only
        print(f"Processing wall {wall.Id}")
        # ... your logic
    
    print("Test complete!")

# 3. Run via CodeExecute UI
# 4. Check output in Python console
```

**Dependency Testing:**

```python
# Test if packages installed correctly
try:
    import polars as pl
    print(f"Polars version: {pl.__version__}")
except ImportError as e:
    print(f"ERROR: {e}")
    print("Install with: # dependencies = ['polars==1.38.1']")
```

### Testing Dashboard Application

**Manual Backend Testing:**

```python
# Test collector independently
from pydemo.data.collector import collect_all_elements
from pydemo.context import get_revit_context

ctx = get_revit_context()
elements = collect_all_elements(ctx.active_document)
print(f"Collected {len(elements)} elements")
```

**Frontend Testing:**

```powershell
cd dashboard-ui

# Development server (uses mock data)
npm run dev

# Build and test in Revit
npm run build
# Then run dashboard command in Revit
```

**Type Safety:**

```powershell
npm run typecheck  # Catches TypeScript errors before runtime
npm run lint      # Catches common bugs and code smells
```

---

## 🔧 Debugging

### Debugging Demo Scripts

**1. Print Debugging (Primary Method):**

```python
# Debug element collection
walls = FilteredElementCollector(doc).OfClass(Wall)
print(f"Found {walls.GetElementCount()} walls")  # Check count

for wall in walls:
    print(f"Wall ID: {wall.Id}")  # Inspect each element
    print(f"  Type: {wall.WallType.Name}")
    print(f"  Family: {wall.WallType.FamilyName}")
```

**2. Geometry Visualization (Built-in Feature):**

```python
# Visualize geometry in 3D view
from Autodesk.Revit.DB import Line, XYZ

line = Line.CreateBound(XYZ(0,0,0), XYZ(10,10,0))
print(line)  # Draws line in active 3D view
```

**3. Data Inspection:**

```python
import polars as pl

df = pl.DataFrame(data)
print(df.head(10))      # First 10 rows
print(df.describe())    # Statistics
print(df.schema)        # Column types
```

**4. Exception Handling:**

```python
def main():
    try:
        # Your code
        result = process_elements()
        print(f"Success: {result}")
    except Exception as e:
        print(f"ERROR: {e}")
        import traceback
        traceback.print_exc()  # Full error trace

if __name__ == "__main__":
    main()
```

### Debugging Dashboard Application

**Python Backend Debugging:**

```python
from pydemo.context import get_revit_context

ctx = get_revit_context()
print(f"Active document: {ctx.active_document.Title}")  # Console output
```

**File Logging for Dashboard:**

```python
import logging

logging.basicConfig(
    filename="C:/Temp/pydemo.log",
    level=logging.DEBUG,
    format="%(asctime)s - %(message)s"
)

logging.debug("Collector started")
```

**Frontend Debugging in WebView2:**

**1. Enable DevTools** - In `presentation/webview.py`:

```python
# Add before loading HTML
webview.CoreWebView2.Settings.AreDevToolsEnabled = True
```

Press **F12** in WebView2 to open DevTools.

**2. Console Logging:**

```typescript
console.log("Filters updated:", filters);
console.table(filteredElements.slice(0, 10));  // Table view
```

**3. React DevTools:**

WebView2 supports Chrome extensions. Install React DevTools extension.

---

## 📦 Build and Deployment

### Frontend Build

```powershell
cd dashboard-ui

# Production build
npm run build

# Output: dashboard-ui/dist/
# - index.html (loaded by WebView2)
# - assets/*.js (bundled React app)
# - assets/*.css (bundled styles)
```

**Build Optimization:**

- **Code splitting:** Vite automatically splits vendor chunks
- **Tree shaking:** Unused code removed
- **Minification:** JavaScript and CSS compressed

### Distribution

PythonDemo is bundled with RevitDevTool:

1. Frontend built to `dashboard-ui/dist/`
2. Python backend in `pydemo/` package
3. All packaged in RevitDevTool installer

**User Requirements:**
- Edge WebView2 Runtime (usually pre-installed on Windows 11)
- No separate Python installation needed (embedded in RevitDevTool)

---

## 🎨 Code Style Guidelines

### Demo Scripts

**Naming:**
- Use `*_script.py` suffix (e.g., `visualization_curve_script.py`)
- Use descriptive prefixes: `visualization_`, `logging_`, `data_analysis_`, etc.

**Structure:**

```python
# /// script
# dependencies = ["package==version"]
# ///

"""Brief script description."""

from Autodesk.Revit.DB import FilteredElementCollector

uiapp = __revit__  # type: ignore
uidoc = uiapp.ActiveUIDocument
doc = uidoc.Document

def main():
    """Main function with clear purpose."""
    # Your code here
    print("Results...")

if __name__ == "__main__":
    main()
```

**Best Practices:**
- Keep scripts simple and focused on one feature
- Add comments explaining Revit API usage
- Use `print()` for output and visualization
- Handle exceptions with try/except
- No strict type checking required (scripts are educational)

### Dashboard Python Code

- **Follow PEP 8** (enforced by Ruff)
- **Type hints required** for all functions
- **Docstrings** for public functions (Google style)
- **Max line length:** 120 characters
- **Max function complexity:** 10 (C90 metric)

**Example:**

```python
def collect_all_elements(doc: Document) -> list[dict[str, Any]]:
    """Collect all model elements from the Revit document.
    
    Args:
        doc: The active Revit document.
        
    Returns:
        List of element dictionaries with metadata.
    """
    collector = FilteredElementCollector(doc).WhereElementIsNotElementType()
    return [extract_element_data(el) for el in collector]
```

### TypeScript / React (Dashboard UI)

- **Follow Airbnb style** (enforced by ESLint)
- **Functional components** with hooks
- **Named exports** for components
- **Props interfaces** for all components
- **Max cognitive complexity:** 15 (SonarJS)

**Example:**

```typescript
interface FilterSelectProps {
  label: string;
  options: string[];
  value: string[];
  onChange: (value: string[]) => void;
}

export function FilterSelect({ label, options, value, onChange }: FilterSelectProps) {
  return (
    <div>
      <label>{label}</label>
      <select multiple value={value} onChange={e => onChange(Array.from(e.target.selectedOptions, o => o.value))}>
        {options.map(opt => <option key={opt} value={opt}>{opt}</option>)}
      </select>
    </div>
  );
}
```

---

## 🚀 Performance Optimization

### Demo Script Performance

**Efficient Element Collection:**

```python
# Good: Filter early with Revit API
collector = FilteredElementCollector(doc)\
    .OfClass(Wall)\
    .WhereElementIsNotElementType()

# Bad: Collect everything then filter in Python
all_elements = FilteredElementCollector(doc)
walls = [e for e in all_elements if isinstance(e, Wall)]
```

**Batch Processing:**

```python
# Process large collections in chunks
elements = list(collector)
chunk_size = 100

for i in range(0, len(elements), chunk_size):
    chunk = elements[i:i+chunk_size]
    print(f"Processing chunk {i//chunk_size + 1}...")
    # Process chunk
```

**Avoid Repeated Lookups:**

```python
# Good: Cache element type
element_type = element.GetTypeId()
type_name = doc.GetElement(element_type).Name

# Bad: Multiple lookups
name1 = doc.GetElement(element.GetTypeId()).Name
name2 = doc.GetElement(element.GetTypeId()).Name  # Duplicate lookup
```

### Dashboard Backend Performance

**Collector Optimization:**

```python
# Good: Single pass through elements
collector = FilteredElementCollector(doc).WhereElementIsNotElementType()
elements = [extract_element_data(el) for el in collector]

# Bad: Multiple passes
for category in categories:
    collector = FilteredElementCollector(doc).OfCategory(category)
    # ... (slower for large models)
```

**Polars Optimization:**

```python
# Good: Lazy evaluation with efficient queries
df = pl.scan_ipc("elements.arrow").filter(
    pl.col("category") == "Walls"
).select(["element_id", "family"]).collect()

# Bad: Eager evaluation with multiple steps
df = pl.read_ipc("elements.arrow")
df = df.filter(df["category"] == "Walls")
df = df.select(["element_id", "family"])
```

### Dashboard Frontend Performance

**Memoization:**

```typescript
// Expensive computation memoized
const filteredElements = useMemo(() => {
  return rawElements.filter(applyFilters);
}, [rawElements, filters]);  // Only recomputes when dependencies change
```

**Virtualization (future enhancement):**

For 50,000+ element models, use `react-window` for table virtualization.

---

## 📚 Further Reading

### Python + Revit API
- [RevitAPI Docs](https://www.revitapidocs.com/)
- [Polars User Guide](https://pola-rs.github.io/polars/user-guide/)

### Frontend
- [React Documentation](https://react.dev/)
- [Vite Guide](https://vitejs.dev/guide/)
- [TypeScript Handbook](https://www.typescriptlang.org/docs/)

### WebView2
- [WebView2 Reference](https://learn.microsoft.com/en-us/microsoft-edge/webview2/)

---

## 🤝 Contributing Checklist

### For Demo Scripts:

- [ ] Script ends with `_script.py` suffix
- [ ] PEP 723 dependencies declared (if needed)
- [ ] Script has docstring explaining purpose
- [ ] Tested in Revit via CodeExecute UI
- [ ] Added entry to `commands/README.md`
- [ ] Uses `print()` for output/visualization
- [ ] Exception handling added for robustness

### For Dashboard Features:

- [ ] Python code passes `ruff check` (no Mypy required)
- [ ] Frontend code passes `npm run quality`
- [ ] New features have TypeScript types
- [ ] Complex functions have docstrings/comments
- [ ] Tested manually in Revit
- [ ] Updated architecture docs if design changed
- [ ] No hardcoded paths or credentials

---

_Last updated: 2026-02-14_
