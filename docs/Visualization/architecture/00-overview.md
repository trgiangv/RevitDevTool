# Visualization System - Overview

**Real-time geometry rendering using Revit's DirectContext3D for transient display.**

---

## Quick Reference

| Component | Purpose | Source Path |
|-----------|---------|-------------|
| **VisualizationServer** | Base class for renderers | `source/RevitDevTool/Visualization/Contracts/` |
| **Server Implementations** | Specific geometry renderers | `source/RevitDevTool/Visualization/Server/` |
| **RenderHelper** | Drawing primitives | `source/RevitDevTool/Visualization/Helpers/` |
| **RenderGeometryHelper** | Geometry processing | `source/RevitDevTool/Visualization/Helpers/` |
| **RenderingBufferStorage** | Mesh caching | `source/RevitDevTool/Visualization/Render/` |

---

## Supported Geometry Types

| Geometry Type | Server Implementation | Rendering Method |
|--------------|----------------------|------------------|
| **Curve** | `PolylineVisualizationServer.cs` | Tessellated polyline |
| **Face** | `FaceVisualizationServer.cs` | Triangulated mesh |
| **Solid** | `SolidVisualizationServer.cs` | Face decomposition |
| **Mesh** | `MeshVisualizationServer.cs` | Direct triangle rendering |
| **XYZ** | `XyzVisualizationServer.cs` | Point spheres |
| **BoundingBoxXYZ** | `BoundingBoxVisualizationServer.cs` | Wireframe edges |

**Location:** All servers in `source/RevitDevTool/Visualization/Server/`

---

## Documentation

### For Developers
- **[01-developer-guide.md](01-developer-guide.md)** - Complete architecture, DirectContext3D integration, lifecycle, performance optimization

### For Users
- **[Visualization-Overview.md (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/Visualization-Overview)** - User guide and examples
- **[Visualization-GeometryTypes.md (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/Visualization-GeometryTypes)** - Supported geometry reference

---

## Architecture at a Glance

```
User Code: Trace.Write(geometry)
     ↓
GeometryListener (Logging module)
     ↓
Type Router → Selects appropriate server
     ↓
VisualizationServer<T>
     ↓
DirectContext3D → Revit 3D View
```

**Key Design:**
- Each geometry type has dedicated server (`VisualizationServer<TGeometry>`)
- Servers implement `IDirectContext3DServer` for Revit integration
- Two-pass rendering: opaque then transparent geometry
- Buffer management for efficient GPU rendering

---

## Common Tasks

| Task | See |
|------|-----|
| Understand system architecture | [01-developer-guide.md](01-developer-guide.md) |
| Add custom geometry type | [01-developer-guide.md](01-developer-guide.md) - Server Pattern |
| Optimize performance | [01-developer-guide.md](01-developer-guide.md) - Performance Section |
| Change rendering style | [01-developer-guide.md](01-developer-guide.md) - RenderHelper |
| Debug rendering issues | Check `RenderHelper.cs` and server implementations |

---

_Last updated: 2026-02-14_
