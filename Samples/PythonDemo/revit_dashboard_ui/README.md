# BIM Dashboard UI

React + TypeScript dashboard for visualizing Revit BIM data.

## Development Modes

### 1. Browser Mode (Mock Data)
Run the dashboard in browser with mock data for UI development:

```bash
cd revit_dashboard_ui
npm install
npm run dev
```

Open http://localhost:5173 - the dashboard will use generated mock data.

### 2. Revit Dev Mode (Hot Reload)
For developing with real Revit data and hot reload:

1. Start the dev server:
```bash
npm run dev
```

2. In Python `webview_host.py`, set:
```python
DEV_MODE = True
```

3. Run the dashboard from Revit - it will connect to localhost:5173 and inject real Revit data.

### 3. Production Mode (Built Files)
For production deployment:

1. Build the frontend:
```bash
npm run build
```

2. In Python `webview_host.py`, set:
```python
DEV_MODE = False
```

3. Run from Revit - it will use the built files from `dist/` folder.

## Architecture

```
src/
├── App.tsx                    # Main layout
├── features/
│   ├── charts/               # Bar charts
│   ├── filters/              # Filter panel
│   ├── header/               # Header with actions
│   ├── selection/            # Selection toolbar
│   ├── stats/                # Stats bar
│   └── table/                # Data table
├── lib/
│   ├── bridge-client.ts      # WebView2 bridge (auto-detects mock/real mode)
│   └── mock-data.ts          # Mock data generator
├── providers/
│   ├── bridge-provider.tsx   # Bridge context
│   └── dashboard-provider.tsx # Dashboard state
└── types/                    # TypeScript types
```

## Data Flow

```
┌─────────────────┐     ┌──────────────────┐
│  Browser Mode   │     │   Revit Mode     │
│  (Mock Data)    │     │  (Real Data)     │
└────────┬────────┘     └────────┬─────────┘
         │                       │
         ▼                       ▼
    ┌────────────────────────────────┐
    │         BridgeClient           │
    │  (auto-detects mode)           │
    └────────────────┬───────────────┘
                     │
                     ▼
    ┌────────────────────────────────┐
    │       DashboardProvider        │
    │  (payload, filters, selection) │
    └────────────────┬───────────────┘
                     │
                     ▼
    ┌────────────────────────────────┐
    │         UI Components          │
    └────────────────────────────────┘
```

## Scripts

- `npm run dev` - Start dev server with hot reload
- `npm run build` - Build for production
- `npm run preview` - Preview production build
- `npm run lint` - Run ESLint

## Tech Stack

- React 19 + TypeScript
- Vite
- TailwindCSS v4
- shadcn/ui components
- TanStack Table (virtualized)
- Sonner (toasts)
