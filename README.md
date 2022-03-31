# RevitDevTool

A ToolSet for Revit Development and Debugging.

## Usage

### Trace Log

Open `Trace Log` DockablePanel and `Enable` the logger. Then you can then use `Trace.TraceInformation()`, `Trace.TraceWarning()`, or `Trace.TraceError()` in your code to record debugging information and monitor the health of your application running.

### Trace Geometry

Turn on the `TraceGeometry` switch in the `RevitDevTools` panel, and use `Trace.Write(GeometryObject)` or `Trace.Write(IEnumerable<GeometryObject>)` to generate transient geometry.

`Transient Geometry` is a read-only temporary primitive that works like Dynamo's preview. `Transient Geometry` cannot be selected, so it cannot be deleted directly. It will be destroyed after the document is closed and will not be saved to the document. Alternatively, you can use `ClearTraceGeometry` to delete the it created by `TraceGeometry`.

## Screenshots

![TraceLog.png](./images/tracelog.png)

## FeatureList

- [x] Trace Log
- [x] Trace Geometry
- [ ] Command Manager