# Visualization System Architecture

Visualization is currently a Revit-host feature. It renders transient geometry through Revit DirectContext3D when Revit geometry objects are written through the logging path.

Last updated: 2026-05-29

---

## Source Map

| Area | Path |
|------|------|
| Revit geometry listener | `source/RevitDevTool/Logging/Listeners/GeometryListener.cs` |
| Server base/contracts | `source/RevitDevTool/Visualization/Contracts/` |
| Concrete servers | `source/RevitDevTool/Visualization/Server/` |
| Render helpers | `source/RevitDevTool/Visualization/Helpers/` |
| Render buffer cache | `source/RevitDevTool/Visualization/Render/RenderingBufferStorage.cs` |
| Settings view models | `source/RevitDevTool/ViewModel/Settings/Visualization/` |
| Settings views | `source/RevitDevTool/View/Settings/Visualization/` |

---

## Flow

```mermaid
flowchart TB
    Script["Script / command\nTrace.Write(geometry)"]
    Listener["GeometryListener\nRevit host"]
    Router["Type routing"]
    Servers["Visualization servers\nCurve, Face, Solid, Mesh, XYZ, BoundingBox"]
    Buffer["RenderingBufferStorage"]
    DC3D["Revit DirectContext3D"]
    Text["Normal text logging"]

    Script --> Listener
    Listener -->|"Revit geometry"| Router
    Listener -->|"not geometry"| Text
    Router --> Servers
    Servers --> Buffer
    Servers --> DC3D
```

Geometry visualization is coupled to Revit API types and DirectContext3D. Other hosts should add their own visualization adapters rather than reuse this implementation directly.

---

## Server-Per-Type Pattern

| Server | Geometry |
|--------|----------|
| `PolylineVisualizationServer` | Revit `Curve` / tessellated polyline |
| `FaceVisualizationServer` | Revit `Face` |
| `SolidVisualizationServer` | Revit `Solid` |
| `MeshVisualizationServer` | Revit `Mesh` |
| `XyzVisualizationServer` | Revit `XYZ` |
| `BoundingBoxVisualizationServer` | Revit `BoundingBoxXYZ` |
| `PlaneVisualizationServer` | Revit plane-like data |

Servers are registered as concrete singletons in Revit host DI. Keep new Revit geometry types aligned with this pattern.

---

## Rendering Pipeline

```mermaid
sequenceDiagram
    participant Revit as Revit View
    participant Server as VisualizationServer
    participant Buffer as RenderingBufferStorage
    participant Helper as RenderHelper
    participant DC3D as DirectContext3D

    Revit->>Server: Draw frame
    Server->>Buffer: Query cached tessellation
    alt Cache miss
        Server->>Server: Tessellate geometry
        Server->>Buffer: Store render data
    end
    Server->>Helper: Build draw primitives
    Helper->>DC3D: Issue draw calls
```

`RenderingBufferStorage` avoids repeated tessellation where possible. Servers are responsible for lifecycle and frame rendering.

---

## Settings

Visualization settings are Revit-host settings. View models live under `source/RevitDevTool/ViewModel/Settings/Visualization/` and are wired by `RevitHostingExtensions.AddApplicationServices()`.

---

## Change Rules

- Do not move Revit API geometry types into shared `DevTools.Logging` or `DevTools.Presentation`.
- Keep host-neutral logging behavior intact for non-geometry trace output.
- Add new Revit geometry rendering as a dedicated server plus settings if needed.
- For non-Revit hosts, create host-specific visualization adapters.

---

## Related Docs

- `docs/Logging/README.md`
- `docs/ai/host-boundaries.md`
- `docs/Execution/README.md`
