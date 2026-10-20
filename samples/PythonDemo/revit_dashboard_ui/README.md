# BIM Dashboard UI

React page for the open Revit document. Revit hosts it in WebView2. A browser session uses mock data.

## Browser

```powershell
pnpm install
pnpm dev
```

Open http://localhost:5173.

## Revit

`DEV_MODE` in `revit_dashboard/presentation/host.py` selects the page:

| Value | Page |
|-------|------|
| `True` | This dev server (`http://localhost:5173`) |
| `False` | `dist/` from `pnpm build` |

Run `commands/dashboard_ipy_script.py` from RevitDevTool. Close an already-open dashboard window before expecting a new host build.

## Check

```powershell
pnpm check
pnpm build
```

## Stack

React 19, TypeScript, Vite+, Semi Design, Apache ECharts, and `lucide-react`. Product layout is in `design.md`.
