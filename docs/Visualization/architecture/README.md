# Visualization Architecture

Technical documentation for the geometry rendering system.

---

## 📚 Documentation

### [00-overview.md](00-overview.md)
Quick reference with component table and supported geometry types

### [01-developer-guide.md](01-developer-guide.md)  
Complete architecture guide:
- DirectContext3D integration
- Server pattern and lifecycle
- Geometry tessellation
- Performance optimization
- Custom renderer implementation

---

## 🎯 Quick Navigation

### For Users
- **[Visualization-Overview (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/Visualization-Overview)** - User guide and examples
- **[Geometry Types (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/Visualization-GeometryTypes)** - Supported types reference
- **[Trace Geometry (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/Visualization-TraceGeometry)** - Usage examples

### For Developers
- **[System Overview](00-overview.md)** - Component reference table
- **[Developer Guide](01-developer-guide.md)** - Complete architecture
- **[Source Code](../../../source/RevitDevTool/Visualization/)** - Implementation

### Server Implementations
All located in `source/RevitDevTool/Visualization/Server/`:
- **PolylineVisualizationServer.cs** - Curve rendering
- **FaceVisualizationServer.cs** - Face triangulation
- **SolidVisualizationServer.cs** - Solid decomposition
- **MeshVisualizationServer.cs** - Direct mesh rendering
- **XyzVisualizationServer.cs** - Point spheres
- **BoundingBoxVisualizationServer.cs** - Wireframe boxes

---

## 🏗️ Architecture Overview

```
┌──────────────────────────────────────────────────────┐
│           User Code                                  │
│  Trace.Write(curve), Trace.Write(solid), etc.      │
└──────────────┬───────────────────────────────────────┘
               │
        ┌──────▼──────┐
        │GeometryList │
        │   ener      │ (Logging module)
        └──────┬──────┘
               │
        ┌──────▼──────────────────┐
        │  Type Detection &       │
        │  Server Selection       │
        └──┬───┬───┬───┬───┬───┬──┘
           │   │   │   │   │   │
    ┌──────▼┐  │   │   │   │   │
    │Polyline│ │   │   │   │   │
    │Server  │ │   │   │   │   │
    └────┬───┘ │   │   │   │   │
         │  ┌──▼──┐│   │   │   │
         │  │Face ││   │   │   │
         │  │Servr││   │   │   │
         │  └──┬──┘│   │   │   │
         │     │ ┌─▼──┐│   │   │
         │     │ │Solid│   │   │
         │     │ │Servr│   │   │
         │     │ └──┬─┘│   │   │
         │     │    │┌─▼──┐│   │
         │     │    ││Mesh││   │
         │     │    │└─┬──┘│   │
         │     │    │  │┌──▼──┐│
         │     │    │  ││XYZ  ││
         │     │    │  │└──┬──┘│
         │     │    │  │   │┌──▼────┐
         │     │    │  │   ││BBox   │
         │     │    │  │   │└──┬────┘
         └─────┴────┴──┴───┴───┴─────────┐
                                          │
                ┌─────────────────────────▼─┐
                │   DirectContext3D         │
                │   (Revit Native Render)   │
                └───────────────────────────┘
```

---

## 🔑 Key Concepts

### Server Pattern
Each geometry type has a dedicated `VisualizationServer<TGeometry>` that:
- Discovers and stores geometry objects
- Implements DirectContext3D rendering interface
- Manages lifecycle (creation, update, disposal)

### DirectContext3D Integration
Servers integrate with Revit's rendering pipeline via `IDirectContext3DServer`:
- `CanExecute()` - Check if geometry exists
- `RenderScene()` - Emit draw calls
- Automatic view update triggers

### Two-Pass Rendering
1. **Opaque pass:** Render solid geometry with depth testing
2. **Transparent pass:** Render translucent geometry with blending

### Tessellation Pipeline
Complex geometry converted to triangles:
- **Curves** → Polyline vertices
- **Faces** → Triangulated mesh
- **Solids** → Face decomposition → Triangles

---

## 📖 Related Documentation

- **[Logging Architecture](../../Logging/architecture/)** - Output routing and geometry interception
- **[CodeExecute Architecture](../../CodeExecute/architecture/)** - Script execution integration

---

_Last updated: 2026-02-14_

- **[User Guide](../index.md)** - How to visualize geometry
- **[Geometry Types](../Geometry-Types.md)** - Geometry handling reference
- **[Performance](../Performance.md)** - Optimization strategies

---

**For System Developers:** Start with [01-System-Design.md](01-System-Design.md)  
**For Custom Renderers:** See [02-Server-Pattern.md](02-Server-Pattern.md)  
**For Geometry Processing:** Check [03-Geometry-Processing.md](03-Geometry-Processing.md)
