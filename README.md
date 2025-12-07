# RevitDevTool

Autodesk Revit plugin project organized into multiple solution files that target versions 2021 - 2026.

## Table of content

<!-- TOC -->
* [RevitDevTool](#revitdevtool)
  * [Table of content](#table-of-content)
  * [Overview](#overview)
    * [📊 Real-Time Trace Logging](#-real-time-trace-logging)
    * [🎨 3D Geometry Visualization](#-3d-geometry-visualization)
    * [📁 External File Logging](#-external-file-logging)
    * [🚀 Auto-Open Floating Window](#-auto-open-floating-window)
  * [Usage](#usage)
    * [🎯 Getting Started](#-getting-started)
    * [📊 Trace Log](#-trace-log)
      * [Features](#features)
      * [How to Use](#how-to-use)
      * [For Python/IronPython Users](#for-pythonironpython-users)
    * [🎨 Trace Geometry - Beautiful 3D Visualization](#-trace-geometry---beautiful-3d-visualization)
      * [Supported Geometry Types](#supported-geometry-types)
      * [Key Features](#key-features)
      * [How to Use Geometry Visualization](#how-to-use-geometry-visualization)
      * [Python/IronPython Geometry Visualization](#pythonironpython-geometry-visualization)
    * [🛠️ Advanced Usage Examples](#-advanced-usage-examples)
      * [Example 1: Debugging Element Geometry](#example-1-debugging-element-geometry)
      * [Example 2: Visualizing Analysis Results](#example-2-visualizing-analysis-results)
      * [Example 3: Python Script Integration](#example-3-python-script-integration)
    * [🎛️ User Interface Features](#-user-interface-features)
    * [🔧 Language Support](#-language-support)
    * [💡 Best Practices](#-best-practices)
    * [🔍 Troubleshooting](#-troubleshooting)
  * [Acknowledgments](#acknowledgments)
<!-- TOC -->

## Overview

RevitDevTool is a comprehensive debugging and visualization toolkit for Autodesk Revit that helps developers and users trace, visualize, and debug their Revit applications. The tool provides two main capabilities: **Trace Logging** and **Geometry Visualization** with beautiful canvas rendering.

### 📊 Real-Time Trace Logging
Capture and view all trace output with color-coded log levels directly in Revit
![Trace Logging in Action](images/RevitDevTool_StackTrace.gif)

Watch as all `Trace.TraceInfomation()`, `Trace.TraceWarning()`, and `Trace.TraceError` calls appear in real-time with color-coded severity levels.

### 🎨 3D Geometry Visualization
Visualize any Revit geometry (curves, faces, solids, meshes) in real-time
![Geometry Visualization](images/RevitDevTool_TraceGeometry.gif)

Simply call `Trace.Write(geometry)` to visualize any Revit geometry object - curves, faces, solids, meshes, and bounding boxes rendered directly in your 3D view.

### 📁 External File Logging
Export logs to multiple formats: Plain Text, JSON, CLEF, SQLite Database
![File Logging Configuration](images/RevitDevTool_SaveLog.gif)

Configure and export your logs to multiple formats including plain text (.log), JSON (.json), CLEF (.clef) for Seq integration, or SQLite database (.db) for advanced querying.

### 🚀 Auto-Open Floating Window
Automatically shows trace window when events occur with no document open
![Auto-Open Floating Window](images/RevitDevTool_WindowBehavior.gif)

The trace window automatically opens when trace events occur and no document is open, helping you catch startup issues and initialization problems.

---

## Usage

### 🎯 Getting Started

1. **Install RevitDevTool**: Use the MSI installer or place the Autodesk bundle in your Revit add-ins folder
2. **Launch Revit**: The tool will automatically register and appear in the **External Tools** panel
3. **Open Trace Panel**: Click the **"Trace Panel"** button in the ribbon to show/hide the dockable panel

### 📊 Trace Log

The Trace Log provides a real-time, color-coded logging interface directly within Revit, similar to Visual Studio's output window.

#### Features

- **Real-time logging** with configurable log levels (Debug, Information, Warning, Error, Fatal)
- **Color-coded output** for easy identification of different log types
- **Console redirection** - captures `Console.WriteLine()` output
- **Auto-start listening** on Revit startup
- **Cascadia Mono font** for better readability
- **Clear function** to reset the log output

#### How to Use

1. **Enable Logging**: Open the `Trace Log` DockablePanel and ensure the logger is `enabled`
2. **Configure Log Level**: Select your desired minimum log level from the dropdown (Debug, Information, Warning, Error, Fatal)
3. **Start Logging**: Use any of the following methods in your C# code:

```csharp
// Information logging
Trace.TraceInformation("Application started successfully");

// Warning logging  
Trace.TraceWarning("Element not found, using default values");

// Error logging
Trace.TraceError("Failed to process element: " + exception.Message);

// Debug logging
Debug.WriteLine("Debug information: " + debugInfo);

// Console output (automatically captured)
Console.WriteLine("This will appear in the trace log");
```

#### For Python/IronPython Users

```python
import clr
# For Revit 2025 onward, use these references:
clr.AddReference("System.Diagnostics.TraceSource")
clr.AddReference("System.Console")
from System.Diagnostics import Trace
from System import Console

# Use the same Trace methods
Trace.TraceInformation("Python script executed")
Trace.TraceWarning("Warning from Python")
Trace.TraceError("Error in Python script")

# Console output is also captured
print("This will appear in the trace log")
Console.WriteLine("Direct console output from Python")
```

### 🎨 Trace Geometry - Beautiful 3D Visualization

The Geometry Visualization system allows you to display transient geometry directly in the Revit 3D view, similar to Dynamo's preview functionality but integrated into your development workflow.

#### Supported Geometry Types

- **Faces** - Surface geometry from Revit elements
- **Curves** - Lines, arcs, splines, and complex curve geometry  
- **Solids** - 3D solid geometry with volume
- **Meshes** - Triangulated mesh geometry
- **Points (XYZ)** - Individual points or point collections
- **Bounding Boxes** - Element bounding box visualization
- **Collections** - Multiple geometry objects at once

#### Key Features

- **Transient Display** - use Revit's DirectContext3D for rendering which inspired by RevitLookup
- **Automatic Cleanup** - Geometry is removed when document closes
- **Manual Control** - Use `ClearGeometry` to remove geometry on demand, `Clear Log` to clear log messages
- **Performance Optimized** - Efficient rendering/Disposal of geometry
- **Supports Mixed Geometry Types** - Trace collections of different geometry types in one call

#### How to Use Geometry Visualization

1. **Enable Geometry Tracing**: Ensure the logger is started (geometry tracing is automatically enabled)

2. **Trace Single Geometry Objects**:

```csharp
// Trace a face
Face face = GetSomeFace();
Trace.Write(face);

// Trace a curve
Curve curve = GetSomeCurve();
Trace.Write(curve);

// Trace a solid
Solid solid = GetSomeSolid();
Trace.Write(solid);

// Trace a mesh
Mesh mesh = GetSomeMesh();
Trace.Write(mesh);

// Trace a point
XYZ point = new XYZ(10, 20, 30);
Trace.Write(point);

// Trace a bounding box
BoundingBoxXYZ bbox = element.get_BoundingBox(null);
Trace.Write(bbox);
```

3. **Trace Multiple Geometry Objects**:

```csharp
// Trace multiple faces
var faces = new List<Face> { face1, face2, face3 };
Trace.Write(faces);

// Trace multiple curves
var curves = selectedElements.SelectMany(e => GetCurvesFromElement(e));
Trace.Write(curves);

// Trace multiple solids
var solids = elements.SelectMany(e => e.GetSolids());
Trace.Write(solids);

// Mixed geometry types
var geometries = new List<GeometryObject> { face, curve, solid };
Trace.Write(geometries);
```

#### Python/IronPython Geometry Visualization

```python
import clr
clr.AddReference("RevitAPI")
# For Revit 2025 onward, use these references:
clr.AddReference("System.Diagnostics.TraceSource")
clr.AddReference("System.Console")

from Autodesk.Revit.DB import *
from System.Diagnostics import Trace
from System.Collections.Generic import List

# Trace individual geometry
face = GetSomeFace()  # Your method to get a face
Trace.Write(face)

# Trace collections
curves = List[Curve]()
curves.Add(curve1)
curves.Add(curve2)
Trace.Write(curves)

# Trace points
point = XYZ(10, 20, 30)
Trace.Write(point)
```

### 🛠️ Advanced Usage Examples

#### Example 1: Debugging Element Geometry

```csharp
foreach (Element element in selectedElements)
{
    Trace.TraceInformation($"Processing element: {element.Id}");
    
    var solids = element.GetSolids();
    if (solids.Any())
    {
        Trace.Write(solids);
        Trace.TraceInformation($"Found {solids.Count} solids");
    }
    else
    {
        Trace.TraceWarning($"No solids found for element {element.Id}");
    }
}
```

#### Example 2: Visualizing Analysis Results

```csharp
// Visualize structural analysis results
var analysisPoints = CalculateStressPoints(beam);
Trace.Write(analysisPoints);

// Show critical areas
var criticalFaces = GetCriticalFaces(beam);
Trace.Write(criticalFaces);

Trace.TraceInformation($"Analysis complete: {analysisPoints.Count} points analyzed");
```

#### Example 3: Python Script Integration

```python
# Python script for geometry analysis
import clr
clr.AddReference("RevitAPI")
# For Revit 2025 onward, use these references:
clr.AddReference("System.Diagnostics.TraceSource")
clr.AddReference("System.Console")
from Autodesk.Revit.DB import *
from System.Diagnostics import Trace

def analyze_walls(walls):
    Trace.TraceInformation("Starting wall analysis...")
    
    for wall in walls:
        # Get wall geometry
        geometry = wall.get_Geometry(Options())
        
        for geo_obj in geometry:
            if isinstance(geo_obj, Solid) and geo_obj.Volume > 0:
                # Visualize the solid
                Trace.Write(geo_obj)
                
                # Log information
                Trace.TraceInformation(f"Wall {wall.Id}: Volume = {geo_obj.Volume}")

# Usage
selected_walls = [doc.GetElement(id) for id in uidoc.Selection.GetElementIds()]
analyze_walls(selected_walls)
```

### 🎛️ User Interface Features

- **Dockable Panel**: Integrated seamlessly with Revit's interface
- **Responsive Design**: Works with Revit's light and dark themes (2024+)
- **Resizable**: Minimum 300x400 pixels, can be resized as needed
- **Keyboard Shortcuts**: Standard copy/paste functionality in the log view
- **Auto-scroll**: Automatically scrolls to show new log entries
- **Log Level Filtering**: Real-time filtering of log messages

### 🔧 Language Support

RevitDevTool works with multiple programming languages and scripting environments:

- **C#** - Full support through .NET Trace API
- **VB.NET** - Full support through .NET Trace API  
- **Python/IronPython** - Full support via pyRevit, RevitPythonShell, or IronPython scripts
- **F#** - Support through .NET interop
- **Any .NET Language** - Works with any language that can access System.Diagnostics.Trace

### 💡 Best Practices

1. **Use Appropriate Log Levels**: Use Information for general status, Warning for non-critical issues, Error for problems
2. **Clear Geometry Regularly**: Use the Clear Geometry button to avoid cluttering the view
3. **Meaningful Messages**: Include element IDs, counts, and relevant context in log messages  
4. **Performance Considerations**: Large geometry collections may impact performance
5. **Document Workflow**: Use logging to document your script's progress and results

### 🔍 Troubleshooting

- **No Geometry Visible**: Ensure the logger is enabled and you can reset by toggling the `Start/Stop Listener`
- **Geometry Persists**: Use "Clear Geometry" button
- **Missing Logs**: Check that the log level is set appropriately for your trace calls
- **Performance Issues**: Reduce the number of geometry objects traced simultaneously

## Acknowledgments

Special thanks to:

- [**RevitLookup**](https://github.com/lookup-foundation/RevitLookup) - For the beautiful DirectContext3D implementation that powers our geometry visualization
- [**RevitDevTool (Original)**](https://github.com/Zhuangkh/RevitDevTool) - For the original idea and inspiration for this project
