# 1.3.1

## Fixed

- Removed `PixiEditor.ColorPicker` to avoid conflicts between `Microsoft.Xaml.Behaviors.Wpf` and other add-ins.

# 1.3.0

## Added

- Settings integration 
  - Visualization options
  - Theme management options
- Suppor `Outline` object

## Fixed

- Revit 2026 context isolation
- Ensure Trace.Listeners are re-added if removed by other add-ins

# 1.2.0

## Added

- DirectContext3D visualization support for geometry rendering
- Support for visualizing multiple geometry types (Solid, Face, Curve, XYZ, CurveLoop, Outline)
- Trace support for ICollection`<object>` and IEnumerable`<CurveLoop>`
- Serilog sink for WinForms RichTextBox with theme support
- Nice3Point packages integration
- New designed icons and button icons

## Fixed

- Multiple geometries visualization
- Face normal vector visualization
- SolidVisualizationServer rendering issues
- VisualizationServerController startup
- Exception handling when retrieving bounding box of solids
- Skip solids with zero volume in bounding box calculation

## Changed

- Improved theme management and resource handling
- Enhanced TraceLog with RichTextBox using dynamic theme and max log lines
- Optimized bounding box calculations by transforming corners individually
- Streamlined buffer disposal and clearing methods
- Refactored XYZ rendering methods with updated color settings and axis length support
- Updated PolylineVisualizationServer rendering
- Improved safe server registration
- Better start/stop mechanism for Visualization server

## Performance

- Disposed unmanaged resources properly

## Documentation

- Added RenderHelper in-depth documentation
- Updated README with Acknowledgments section
- Refined geometry visualization details in README
- Streamlined table of contents and updated Python references for Revit 2025

# 1.1.1

- Listen to Console.WriteLine
- Auto start listening on revit start

# 1.1.0

- Use single DockablePanel
- Simplified codebase
- UI ehancements

# 1.0.0

Initial release. Enjoy!
