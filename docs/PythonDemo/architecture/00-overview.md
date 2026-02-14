# PythonDemo - System Overview

## Module Purpose

PythonDemo is a **collection of demonstration scripts** showcasing RevitDevTool's three core modules (CodeExecute, Logging, Visualization). The project contains:

### 1. Demo Scripts Collection (`commands/`)
12+ example scripts organized by module:
- **Visualization**: `visualization_xyz_script.py`, `visualization_curve_script.py`, `visualization_solid_script.py`
- **Logging**: `logging_format_script.py`, `logging_batch_script.py`
- **Data Analysis**: `data_analysis_script.py`
- **Dashboard**: `dashboard_script.py` (entry point for full dashboard app)
- **Scientific Computing**: `sklearn_script.py`, `shapely_script.py`, `trimesh_script.py`
- **UI Patterns**: `modeless_script.py`

### 2. Dashboard Application (`revit_dashboard/`)
Production-grade BIM analytics dashboard:
- Full-model data extraction from Revit
- High-performance analytics using Polars dataframes
- Modern web UI embedded in Revit via WebView2
- Bidirectional communication between Python backend and React frontend
- Excel export for filtered datasets

---

## Architecture

### High-Level Structure

```
RevitDevTool.PythonDemo/
├─ commands/               ← Demo scripts (12+ examples)
│  ├─ dashboard_script.py       Entry to full dashboard
│  ├─ data_analysis_script.py   Data workflow demo
│  ├─ visualization_*_script.py Geometry rendering tests
│  ├─ logging_*_script.py       Log format tests
│  └─ *_script.py               Other examples
│
├─ revit_dashboard/        ← Dashboard application backend
│  ├─ runner.py                 Orchestration
│  ├─ data/                     Data collection
│  ├─ analytics/                Polars analytics engine
│  ├─ export/                   Excel export
│  └─ presentation/             WebView2 bridge
│
└─ revit_dashboard_ui/     ← React frontend
   └─ dist/                     Built assets for WebView2
```

### Dashboard Application Components

```
┌─────────────────────────────────────────────────────────────┐
│                       Revit Host                            │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │      Python Backend (revit_dashboard/)               │  │
│  │                                                      │  │
│  │  ┌──────────────┐    ┌─────────────────────────┐   │  │
│  │  │  Collector   │───▶│  Analytics Engine       │   │  │
│  │  │  (Revit API) │    │  (Polars)               │   │  │
│  │  └──────────────┘    └─────────────┬───────────┘   │  │
│  │                                     │               │  │
│  │                                     ▼               │  │
│  │  ┌──────────────────────────────────────────────┐  │  │
│  │  │         Presentation Layer                   │  │  │
│  │  │  ┌────────────────────────────────────────┐  │  │
│  │  │  │  WebView2 Host (C# + Python.NET)      │  │  │
│  │  │  │  ┌──────────────────────────────────┐ │  │  │
│  │  │  │  │  React Frontend (dashboard-ui)   │ │  │  │
│  │  │  │  │  - Data visualization            │ │  │  │
│  │  │  │  │  - Dynamic filters               │ │  │  │
│  │  │  │  │  - Export triggers               │ │  │  │
│  │  │  │  └──────────────────────────────────┘ │  │  │
│  │  │  └────────────────────────────────────────┘  │  │
│  │  └──────────────────────────────────────────────┘  │  │
│  │                       ▲            │                │  │
│  └───────────────────────┼────────────┼────────────────┘  │
│                          │            ▼                   │
│                    Data Injection   postMessage           │
│                                      │                    │
│  ┌──────────────────────────────────┼────────────────┐   │
│  │  Excel Exporter                  │                │   │
│  │  (openpyxl)                      └────────────────┼──▶│
│  └────────────────────────────────────────────────────┘   │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

---

## Component Responsibilities

### A. Demo Scripts (`commands/*.py`)

Each script demonstrates specific RevitDevTool features:

#### Visualization Scripts
- **`visualization_xyz_script.py`** - Point picking and display
- **`visualization_curve_script.py`** - Edge picking, curve generation and rendering  
- **`visualization_solid_script.py`** - Solid extraction and face visualization

**Pattern:**
```python
# Pick geometry
ref = uidoc.Selection.PickObject(ObjectType.Edge)
edge = elem.GetGeometryObjectFromReference(ref)

# Visualize (intercepted by Logging → Visualization)
print(edge)  # Displays in 3D view via DirectContext3D
```

#### Logging Scripts
- **`logging_format_script.py`** - Log level detection, JSON pretty-print, exception formatting
- **`logging_batch_script.py`** - Performance testing with 10,000+ messages

**Pattern:**
```python
print("[INFO] Operation started")     # Detected as Info level
print(json.dumps(data, indent=2))     # Auto pretty-printed with colors
```

#### Data Analysis Scripts
- **`data_analysis_script.py`** - Complete workflow: collect → analyze → visualize

**Pattern:**
```python
# Collect data
walls = FilteredElementCollector(doc).OfClass(Wall)
data = [extract_wall_data(w) for w in walls]

# Analyze with Polars
df = pl.DataFrame(data)
outliers = df.filter(pl.col("Area") > df["Area"].quantile(0.95))

# Visualize outliers
for wall_id in outliers["Id"]:
    wall = doc.GetElement(ElementId(wall_id))
    print(wall.Location.Curve)  # Shows in 3D
```

#### Scientific Computing Scripts
- **`sklearn_script.py`** - Machine learning example
- **`shapely_script.py`** - Geometric operations
- **`trimesh_script.py`** - 3D mesh processing

**Pattern:** All use PEP 723 dependency declarations:
```python
# /// script
# dependencies = ["scikit-learn==1.5.0"]
# ///
```

---

### B. Dashboard Application (`revit_dashboard/`)

Production application with structured backend:

#### 1. Data Collection (`data/collector.py`)
**Purpose:** Extract all elements from active Revit document

**Key Features:**
- Collects **all** model elements (no category whitelist)
- Extracts metadata: Category, Family, Type, Level, Phase, Workset
- Returns structured data ready for analytics

**Output:**
```python
[
  {"element_id": 12345, "category": "Walls", "family": "Basic Wall", ...},
  {"element_id": 67890, "category": "Doors", "family": "Single-Flush", ...},
]
```

#### 2. Analytics Engine (`analytics/polars_engine.py`)
**Purpose:** Transform raw element data into dashboard insights

**Key Features:**
- Polars-based high-performance dataframe operations
- Generate KPI metrics (total elements, unique families, etc.)
- Create distribution charts (category counts, type counts)
- Build filter options from actual model data

**Output (Payload):**
```python
{
  "elements": [...],     # Full element list
  "kpi": {...},          # Key metrics
  "charts": {...},       # Distribution data  
  "filters": {...}       # Available filter values
}
```

#### 3. Excel Exporter (`export/excel_exporter.py`)
**Purpose:** Export filtered datasets to Excel

**Trigger:** Frontend sends `export_excel` message via WebView2

#### 4. WebView2 Host (`presentation/webview.py`)
**Purpose:** Bridge between Python backend and React frontend

**Communication:**
- **Backend → Frontend:** Data injection at page load via `window.__BIM_DASHBOARD_INITIAL_DATA`
- **Frontend → Backend:** JSON messages via `window.chrome.webview.postMessage`
- **Backend → Frontend:** Event responses via `webview.ExecuteScriptAsync`

#### 5. React Frontend (`revit_dashboard_ui/`)
**Tech Stack:** React 18 + TypeScript + Vite

**Features:**
- Dynamic filters (category, family, type, level, phase, workset)
- Text search across element properties
- KPI cards, distribution charts, tabular drilldown
- Export button triggers backend export

---

## Data Flow Patterns

### Pattern 1: Simple Demo Scripts

```
User runs visualization_curve_script.py
  └─▶ Pick geometry via selection
      └─▶ print(geometry_object)
          └─▶ Logging module intercepts
              └─▶ GeometryListener detects Revit geometry type
                  └─▶ Routes to VisualizationServer
                      └─▶ Renders in 3D view via DirectContext3D
```

### Pattern 2: Data Analysis Workflow

```
User runs data_analysis_script.py
  └─▶ Collect Revit elements
      └─▶ Transform to Polars DataFrame
          └─▶ Perform analysis (outlier detection, aggregations)
              └─▶ Print results (formatted by Logging)
                  └─▶ Print outlier geometry (visualized in 3D)
```

### Pattern 3: Dashboard Application

```
User runs dashboard_script.py
  └─▶ revit_dashboard.runner.main()
      ├─▶ collector.collect_all_elements(doc)
      ├─▶ polars_engine.build_analytics_payload(elements)
      └─▶ webview.show_dashboard(payload)
          ├─▶ Load revit_dashboard_ui/dist/index.html
          └─▶ Inject payload into window.__BIM_DASHBOARD_INITIAL_DATA

User interacts with dashboard UI:
  └─▶ Adjust filters → UI updates (client-side)
  └─▶ Click "Export" → postMessage to backend
      └─▶ Excel file generated
          └─▶ Notification sent back to UI
```

---

## Key Design Decisions

### Why Collection of Scripts + Dashboard App?
- **Scripts:** Provide focused examples for learning specific features
- **Dashboard:** Shows production-grade full-application architecture
- **Separation:** Users can study simple scripts without dashboard complexity

### Why Polars instead of Pandas (in dashboard)?
- **Performance:** 5-10x faster for aggregations on large models
- **Lazy evaluation:** Efficient query optimization
- **Modern API:** Better type hints and expression syntax

### Why WebView2 (in dashboard)?
- **Native Chromium:** Full modern web standards support
- **No external server:** Embedded directly in Revit
- **Bidirectional bridge:** Python ↔ JavaScript communication

### Why PEP 723 Dependency Declarations?
- **Inline metadata:** Dependencies declared in script header
- **Auto-installation:** UV automatically installs packages
- **Reproducible:** Locked versions ensure consistency

**Example:**
```python
# /// script
# dependencies = ["polars==1.38.1", "numpy==2.4.2"]
# ///
```

---

## File Structure

```
RevitDevTool.PythonDemo/
├── pyproject.toml              # Python project config + quality tools
├── uv.lock                     # Locked dependencies
│
├── commands/                   # Demo scripts collection
│   ├── README.md               # Scripts documentation
│   ├── dashboard_script.py     # Dashboard entry point
│   ├── data_analysis_script.py # Full workflow example
│   ├── visualization_xyz_script.py
│   ├── visualization_curve_script.py
│   ├── visualization_solid_script.py
│   ├── logging_format_script.py
│   ├── logging_batch_script.py
│   ├── sklearn_script.py       # ML example
│   ├── shapely_script.py       # Geometry ops
│   ├── trimesh_script.py       # Mesh processing
│   └── modeless_script.py      # UI pattern
│
├── revit_dashboard/            # Dashboard application backend
│   ├── runner.py               # Main orchestrator
│   ├── context.py              # Revit API context
│   ├── data/
│   │   └── collector.py        # Element collection
│   ├── analytics/
│   │   └── polars_engine.py    # Analytics engine
│   ├── export/
│   │   └── excel_exporter.py   # Excel export
│   └── presentation/
│       └── webview.py          # WebView2 host
│
└── revit_dashboard_ui/         # React frontend
    ├── package.json            # npm dependencies
    ├── vite.config.ts          # Vite build config
    ├── src/
    │   ├── App.tsx             # Main dashboard component
    │   └── components/         # UI components
    └── dist/                   # Built assets (loaded by WebView2)
```

---

## Dependencies

### Demo Scripts
Each script declares dependencies via PEP 723:
- **Visualization scripts:** No dependencies (pure Revit API)
- **Logging scripts:** No dependencies
- **Data analysis:** `polars==1.38.1`
- **Dashboard:** `polars==1.38.1`, `numpy==2.4.2`, `openpyxl==3.1.5`
- **Scientific:** `scikit-learn`, `shapely`, `trimesh`

### Dashboard Frontend (revit_dashboard_ui/)
- **react:** UI framework
- **typescript:** Type safety
- **vite:** Build tool
- **recharts:** Chart library
- **eslint + sonarjs:** Code quality

---

## Related Documentation

### Demo Scripts
- **[commands/README.md](../../../source/RevitDevTool.PythonDemo/commands/README.md)** - Complete script catalog with features
- **[Examples-Overview (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/Examples-Overview)** - User-friendly examples guide

### Dashboard Application
- **[01-developer-guide.md](01-developer-guide.md)** - Development workflow, quality gates, testing
- **[Examples-Dashboard (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/Examples-Dashboard)** - Dashboard user guide
- **[Examples-DataAnalysis (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/Examples-DataAnalysis)** - Data analysis patterns

---

_Last updated: 2026-02-14_
