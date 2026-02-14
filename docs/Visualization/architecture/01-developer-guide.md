# Visualization Architecture

## Overview

The visualization system renders Revit geometry objects transiently in the 3D view using DirectContext3D API. Geometry is captured from log messages and rendered without creating actual Revit elements.

**Prerequisites:** .NET 10 SDK, Visual Studio 2026 (or Rider, C# Dev Kit for .NET)

## Architecture Flow

```
Application Code
    ↓  Trace.Write(geometry)
GeometryListener (Logging)
    ↓  Collects & triggers event
VisualizationServerFactory
    ↓  Dispatches by type
VisualizationServer (Curve/Face/Solid/Mesh/XYZ/Box)
    ↓  Tessellates & converts
RenderHelper
    ↓  Low-level draw calls
Revit DirectContext3D
    ↓  GPU rendering
```

## Core Components

### VisualizationServer (Abstract Base)

**Source:** `VisualizationServer.cs` in `RevitDevTool.Visualization` namespace

Base class for all geometry renderers. Manages DirectContext3D lifecycle and provides drawing infrastructure.

**Key responsibilities:**
- Override `OnDrawFrame()` to implement rendering logic
- Access `DrawContext` during frame rendering
- Manage subscription to `DrawFrame` events
- Clean up resources via `Open()` / `Close()` lifecycle

**Typical implementation pattern:**
1. Receive geometry object via event or property
2. Convert/tessellate geometry to render primitives
3. In `OnDrawFrame()`, use `RenderHelper` to issue draw commands
4. DirectContext3D renders primitives to view

### VisualizationServerFactory

**Source:** `VisualizationServerFactory.cs` in `RevitDevTool.Visualization` namespace

Dispatches geometry objects to appropriate server implementations based on type:

| Geometry Type | Server Implementation |
|---------------|----------------------|
| `Curve` | `PolylineVisualizationServer` |
| `Face` | `FaceVisualizationServer` |
| `Solid` | `SolidVisualizationServer` |
| `Mesh` | `MeshVisualizationServer` |
| `XYZ` | `XyzVisualizationServer` |
| `BoundingBoxXYZ` | `BoundingBoxVisualizationServer` |

### Server Implementations

**PolylineVisualizationServer** - Renders curves as polylines
- Tessellates curve to line segments
- Draws using `RenderHelper.DrawLine()`

**FaceVisualizationServer** - Renders surfaces  
- Tessellates face to triangles
- Draws using `RenderHelper.DrawTriangles()`

**SolidVisualizationServer** - Renders 3D solids
- Extracts edges from solid
- Draws wireframe or filled faces

**MeshVisualizationServer** - Renders pre-tessellated meshes
- Uses mesh vertices/triangles directly
- Fastest rendering path for complex geometry

**XyzVisualizationServer** - Renders points
- Draws points using `RenderHelper.DrawPoints()`

**BoundingBoxVisualizationServer** - Renders bounding boxes
- Draws wireframe box using `RenderHelper.DrawWireframeBox()`

### RenderHelper

**Source:** `RenderHelper.cs` in `RevitDevTool.Visualization` namespace

Low-level drawing API that wraps DirectContext3D primitive calls.

**Key methods:**
```csharp
public static class RenderHelper
{
    void DrawLine(XYZ start, XYZ end, ColorWithTransparency color);
    void DrawTriangles(IList<XYZ> vertices, IList<int> indices);
    void DrawPoints(IList<XYZ> points, ColorWithTransparency color);
    void DrawWireframeBox(BoundingBoxXYZ box);
}
```

### RenderGeometryHelper

**Source:** `RenderGeometryHelper.cs` in `RevitDevTool.Visualization` namespace

Geometry processing utilities for tessellation and conversion.

**Key responsibilities:**
- Tessellate curves to polylines
- Tessellate faces to triangle meshes
- Extract edges from solids
- Convert between coordinate systems

### RenderingBufferStorage

**Source:** `RenderingBufferStorage.cs` in `RevitDevTool.Visualization` namespace

Caches tessellated mesh data to avoid recomputing geometry every frame.

**Key features:**
- Store vertex/index buffers
- Invalidate on geometry changes
- Memory management for large meshes

## Rendering Pipeline

### Per-Frame Flow

1. **Revit** calls `OnDrawFrame()` on registered `VisualizationServer`
2. **Server** retrieves geometry object to render
3. **Tessellation** converts geometry to primitives (if not cached)
4. **RenderHelper** issues draw commands to DirectContext3D
5. **DirectContext3D** sends primitives to GPU
6. **GPU** renders to Revit viewport

### Transient Rendering

DirectContext3D content is **transient** - it disappears after each frame unless redrawn. Servers must:
- Subscribe to `DrawFrame` events
- Redraw geometry every frame while active
- Unsubscribe when visualization is complete

## Usage Pattern

```csharp
// In application code - geometry is logged
var curve = Line.CreateBound(XYZ.Zero, new XYZ(10, 0, 0));
Trace.Write(curve);  // Triggers GeometryListener

// GeometryListener captures and creates server
var server = VisualizationServerFactory.Create(curve);
server.Open();  // Start rendering
// ... geometry visible in Revit view ...
server.Close();  // Stop rendering
server.Dispose();
```

## Performance Considerations

- **Tessellation**: Cache tessellated geometry in `RenderingBufferStorage` to avoid recomputing every frame
- **Draw Calls**: Batch primitives to minimize DirectContext3D calls
- **Memory**: Dispose servers when visualization complete
- **Complexity**: Use LOD (Level of Detail) for complex geometry

## Integration Points

- **Logging System**: `GeometryListener` in Logging module triggers visualization
- **Settings**: Color, transparency, and rendering options from `ISettingsService`
- **DirectContext3D**: Revit API for hardware-accelerated transient graphics
