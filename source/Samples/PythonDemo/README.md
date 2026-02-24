# RevitDevTool.PythonDemo - Advanced BIM Dashboard

This module provides an advanced BIM analytics dashboard hosted in Revit using WebView2.

The architecture uses:
- Python backend for Revit data extraction, Polars analytics, and Excel export
- React + TypeScript frontend (`dashboard-ui`) with shadcn-style UI patterns
- WebView2 bridge for data transfer and export commands

## Project Structure

```text
RevitDevTool.PythonDemo/
  main_script.py                  # Entry point only
  pydemo/
    context.py                    # Revit UIApplication/Document context
    runner.py                     # End-to-end orchestration
    contracts/
      payload.py                  # Backend/frontend payload contracts
    data/
      collector.py                # Full-model element collector (no category whitelist)
    analytics/
      polars_engine.py            # Filtering, KPIs, chart data, quality/outlier analytics
    export/
      excel_exporter.py           # Filtered export to xlsx
    presentation/
      webview.py                  # WebView2 host + JS bridge
      dashboard_html.py           # Legacy/fallback html template helper
    visualization/
      charts.py                   # Legacy Plotly chart generators
  dashboard-ui/                   # React + Vite frontend
```

## Runtime Flow

1. `main_script.py` executes `pydemo.runner.main()`.
2. Runner collects all model elements (`collector.py`).
3. Polars layer builds analytics payload and filter options.
4. WebView host loads `dashboard-ui/dist/index.html`.
5. Payload is injected into `window.__BIM_DASHBOARD_INITIAL_DATA`.
6. Frontend sends `export_excel` requests through `window.chrome.webview.postMessage`.
7. Backend exports filtered rows to Excel and sends result event back.

## Frontend (React + shadcn-style)

### Install

```powershell
cd source/RevitDevTool.PythonDemo/dashboard-ui
npm install
```

### Build for Revit host

```powershell
npm run build
```

The build output is expected at:

`source/RevitDevTool.PythonDemo/dashboard-ui/dist`

## Local Quality Gates (Sonar-style)

### Python

```powershell
cd source/RevitDevTool.PythonDemo
uv run --with ruff --with mypy --with radon ruff check pydemo
uv run --with mypy mypy pydemo
```

Configured in `pyproject.toml`:
- Ruff rules + complexity (C90)
- Mypy static typing checks

### Frontend

```powershell
cd source/RevitDevTool.PythonDemo/dashboard-ui
npm run quality
```

`npm run quality` executes:
- `tsc --noEmit`
- `eslint .` (including `eslint-plugin-sonarjs`)

## Key Capabilities

- Full-model analysis without fixed category limits
- Dynamic filters: category/family/type/level/phase/workset + text search
- Visibility controls: hide selected categories and levels
- KPI cards + distribution charts + tabular drilldown
- Export filtered dataset to Excel
- Preset dashboard views (Executive / QA / Coordination)

## Notes

- If `dashboard-ui/dist` is missing, WebView will show a build reminder page.
- Excel exports are written to:
  - `%USERPROFILE%/Documents/RevitDevToolExports` (default)