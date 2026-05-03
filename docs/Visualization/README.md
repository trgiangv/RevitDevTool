# Visualization System Architecture

Transient 3D geometry rendering via Revit's `DirectContext3D` API. Geometry is intercepted from log output and rendered without creating model elements.

**Source:** `source/RevitDevTool/Visualization/`

---

## Architecture Overview

```mermaid
flowchart TB
    subgraph Input["Input Channels"]
        CSharp["C#: Trace.Write(curve)"]
        Python["Python: print(curve)"]
    end

    subgraph Logging["Logging Layer"]
        GL["GeometryListener\n(detects geometry types)"]
        Filter{"Is Revit\ngeometry?"}
        Text["→ Text log"]
    end

    subgraph Viz["Visualization Layer"]
        Factory["VisualizationServerFactory\n(select by type)"]
        Polyline["PolylineServer"]
        Face["FaceServer"]
        Solid["SolidServer"]
        Mesh["MeshServer"]
        XYZ["XyzServer"]
        BBox["BoundingBoxServer"]
    end

    subgraph Render["Rendering Layer"]
        Helper["RenderHelper\n(primitive draw calls)"]
        Buffer["RenderingBufferStorage\n(cached tesselation)"]
        DC3D["DirectContext3D\n(GPU)"]
    end

    CSharp --> GL
    Python --> GL
    GL --> Filter
    Filter -->|no| Text
    Filter -->|yes| Factory
    Factory --> Polyline
    Factory --> Face
    Factory --> Solid
    Factory --> Mesh
    Factory --> XYZ
    Factory --> BBox
    Polyline --> Helper
    Face --> Buffer
    Solid --> Buffer
    Mesh --> Buffer
    XYZ --> Helper
    BBox --> Helper
    Helper --> DC3D
    Buffer --> Helper
```

---

## Server-Per-Type Pattern

Each geometry type has a dedicated `VisualizationServer<TGeometry>`:

```mermaid
classDiagram
    class VisualizationServer~T~ {
        +Open()
        +Close()
        +RenderScene()
        #OnDrawFrame()
        -IDirectContext3DServer
    }
    class PolylineVisualizationServer {
        +Tessellate curve → polyline
        +DrawLine() per segment
    }
    class FaceVisualizationServer {
        +Triangulate face
        +DrawTriangles()
    }
    class SolidVisualizationServer {
        +Decompose solid → faces → edges
        +DrawWireframe / DrawTriangles()
    }
    class MeshVisualizationServer {
        +Direct mesh rendering
        +Fastest path
    }
    class XyzVisualizationServer {
        +DrawPoints()
    }
    class BoundingBoxVisualizationServer {
        +DrawWireframeBox()
    }

    VisualizationServer~T~ <|-- PolylineVisualizationServer
    VisualizationServer~T~ <|-- FaceVisualizationServer
    VisualizationServer~T~ <|-- SolidVisualizationServer
    VisualizationServer~T~ <|-- MeshVisualizationServer
    VisualizationServer~T~ <|-- XyzVisualizationServer
    VisualizationServer~T~ <|-- BoundingBoxVisualizationServer
```

| Server | Type | Rendering Method |
|--------|------|-----------------|
| `PolylineVisualizationServer` | `Curve` (Line, Arc, NurbSpline, HermiteSpline) | Tessellated polyline |
| `FaceVisualizationServer` | `Face` (Planar, Cylindrical, Conical, Revolved, Ruled) | Triangulated mesh |
| `SolidVisualizationServer` | `Solid` | Face decomposition → wireframe or filled |
| `MeshVisualizationServer` | `Mesh` | Direct triangle rendering |
| `XyzVisualizationServer` | `XYZ` | Point spheres |
| `BoundingBoxVisualizationServer` | `BoundingBoxXYZ` | Wireframe edges |

---

## Rendering Pipeline

```mermaid
sequenceDiagram
    participant Revit as Revit View
    participant Server as VisualizationServer
    participant Buffer as RenderingBufferStorage
    participant Helper as RenderHelper
    participant DC3D as DirectContext3D

    loop Every Frame
        Revit->>Server: OnDrawFrame()
        Server->>Buffer: Get cached vertices/indices?
        alt Cache hit
            Buffer-->>Server: Cached data
        else Cache miss
            Server->>Server: Tessellate geometry
            Server->>Buffer: Store vertices + indices
        end
        Server->>Helper: DrawTriangles() / DrawLine() / DrawPoints()
        Helper->>DC3D: Issue primitive draw calls
        DC3D->>Revit: GPU renders to viewport
    end
```

**Key points:**
- Transient content: geometry disappears each frame unless redrawn
- Two-pass rendering: opaque pass (depth testing) then transparent pass (blending)
- `RenderingBufferStorage` caches tessellation to avoid recomputation
- Servers subscribe to `DrawFrame` events; unsubscribe on disposal

---

## Integration with Logging

```mermaid
flowchart LR
    Code["Trace.Write(geometry)"] --> GL["GeometryListener\n(in Logging module)"]
    GL -->|"is geometry?"| Factory["VisualizationServerFactory"]
    Factory --> Server["VisualizationServer&lt;T&gt;"]
    Server --> View["3D View"]
    GL -.->|"is text?"| Text["Trace Log Panel"]
```

The `GeometryListener` in the Logging module intercepts all `Trace.Write()` calls. If the value is a Revit geometry type (`Curve`, `Face`, `Solid`, etc.), it routes to Visualization instead of the text log.

---

## Related Modules

- **[Logging Architecture](../Logging/README.md)** — GeometryListener captures geometry from trace output
- **[Execution Architecture](../Execution/README.md)** — Scripts produce geometry via Revit API

---

_Last updated: 2026-05-03_
