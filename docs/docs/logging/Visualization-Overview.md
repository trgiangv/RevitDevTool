# Geometry Visualization

Geometry visualization is currently supported only on Revit through the `DirectContext3D` API. RevitDevTool can display recognized geometry temporarily in the active Revit 3D view; AutoCAD-family hosts support the logging outputs, but not this visualization path.

## How the flow works

Geometry visualization is part of the normal trace pipeline. There is no special visualization command, export format, or model-element workaround:

1. Your add-in or script writes a value through `System.Diagnostics.Trace`.
2. The registered trace listeners receive the value.
3. On Revit, RevitDevTool recognizes supported geometry objects and renders them through `DirectContext3D`.
4. The same trace event can continue to the configured [Log Output](/docs/logging/Observability-Http) targets.

The value must reach the listener as an object. Writing only a formatted string produces a normal log entry, not geometry.

## C#, F#, and .NET add-ins

Use the object overload of `Trace.Write` or `Trace.WriteLine`:

```csharp
using Autodesk.Revit.DB;
using System.Diagnostics;

GeometryObject geometry = element.get_Geometry(new Options()).FirstOrDefault();
if (geometry is not null)
    Trace.WriteLine(geometry); // object overload: log + visualize
```

The same flow applies to F# and compiled .NET add-ins. Keep the geometry object intact; do not interpolate it into a string first:

```csharp
Trace.WriteLine(solid);                 // visualizes a Solid
Trace.WriteLine($"Solid: {solid}");     // text log only
```

## Python and IronPython

Both `Trace` and the normal RevitDevTool `print()` flow can visualize geometry. For a single argument, RevitDevTool keeps the raw Python/.NET object when forwarding `print()` to the trace listeners:

```python
from System.Diagnostics import Trace

print(curve)                  # visualizes the curve
print(solid)                  # visualizes the solid
print(point)                  # visualizes the point

# The explicit .NET API is equivalent:
Trace.WriteLine(curve)
```

Use `print()` for ordinary messages as well:

```python
print("Room analysis started")       # normal log output
print("Curve:", curve)              # message + geometry; default separator
```

The object must not be converted to text before it is passed to `print()` or `Trace`. These forms produce text only:

```python
print(f"Curve: {curve}")            # text only
print(str(curve))                   # text only
print(curve, sep=" | ")             # text only
```

For a Python script executed by RevitDevTool, the flow is:

```text
print(geometry)
    -> RevitDevTool Python print bridge
    -> System.Diagnostics.Trace
    -> registered TraceListeners
    -> Revit geometry display + configured log outputs
```

The registered listeners handle the event in the normal trace pipeline. RevitDevTool renders recognized geometry transiently; the model is not modified and no model element is created.

## Supported geometry

- Curves: `Line`, `Arc`, `Ellipse`, `NurbSpline`, `CurveLoop`, and `PolyLine`
- Faces: planar and analytical Revit faces
- `Solid` objects and their edges/faces
- `Mesh` objects
- `XYZ` points
- `BoundingBoxXYZ`

Collections of supported geometry can also be traced when each item is supported.

## Requirements and behavior

- Revit must have an active 3D view.
- The Trace Log listener must be enabled.
- The minimum [Log Level](/docs/logging/Logging-Overview) must allow the entry through.
- Geometry is transient and exists only for the current session.
- Use **Clear Geometry** in the Trace panel to remove displayed objects.

## Examples

- [C# CurveVisualization.cs](https://github.com/trgiangv/RevitDevTool/blob/main/samples/CSharpDemo/CurveVisualization.cs)
- [C# SolidVisualization.cs](https://github.com/trgiangv/RevitDevTool/blob/main/samples/CSharpDemo/SolidVisualization.cs)
- [Python visualization_curve_script.py](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/visualization_curve_script.py)

## Related

- [Log Level](/docs/logging/Logging-Overview)
- [Log Output](/docs/logging/Observability-Http)
