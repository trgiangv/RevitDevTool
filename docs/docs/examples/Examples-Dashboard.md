# Example: BIM Dashboard with WebView2

**Demo:** IronPython host plus a React dashboard over the open Revit document
**Script:** [`dashboard_ipy_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/dashboard_ipy_script.py)

![Python Dashboard](/images/examples/PythonDashboard.png)

---

## What It Demonstrates

- A modeless WPF window hosting WebView2
- Revit collection and commands on the Revit thread through `REVIT_CONTEXT`
- A React page that filters, charts, and groups the snapshot
- Excel export through the dashboard window's Save dialog

The page is the product surface. Behavior lives in [`revit_dashboard_ui/design.md`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/revit_dashboard_ui/design.md). Wiring lives in [PythonDemo architecture](https://github.com/trgiangv/RevitDevTool/blob/main/docs/architecture/PythonDemo/README.md).

---

## Layout

```text
samples/PythonDemo/
├── commands/dashboard_ipy_script.py    ← entry
├── revit_dashboard/
│   ├── data/                           ← elements, categories, warnings
│   ├── revit_api/handler.py            ← Revit command bodies
│   └── presentation/host.py            ← window, queue, page replies
└── revit_dashboard_ui/                 ← React + TypeScript + Vite+
```

`dashboard_ipy_script.py` only calls `show_dashboard()`. Python does not chart, aggregate, or build the workbook. The page does that. The host shows `Microsoft.Win32.SaveFileDialog` and writes the bytes.

There is no PEP 723 block and no third-party Python package. The script runs on the pyRevit IronPython engine.

---

## Run in Revit

1. Load the folder `samples/PythonDemo`.
2. Execute `commands/dashboard_ipy_script.py`.

`DEV_MODE` in `revit_dashboard/presentation/host.py` chooses the page:

| `DEV_MODE` | Page |
|------------|------|
| `True` | Vite server at `http://localhost:5173` |
| `False` | Built files under `revit_dashboard_ui/dist/` |

For the dev server, from `samples/PythonDemo/revit_dashboard_ui`:

```powershell
pnpm install
pnpm dev
```

Check and build the page with `pnpm check` and `pnpm build`. The browser at `http://localhost:5173` uses mock data when Revit is not hosting the page.

An already-open dashboard window is activated again and keeps the Python that created it. Close the window and run the script to load host changes.

---

## Related Examples

- **Data analysis** → [`data_analysis_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/data_analysis_script.py)
- **Logging** → [`logging_format_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/logging_format_script.py)
- **Visualization** → [`visualization_curve_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/visualization_curve_script.py)
