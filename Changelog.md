# 1.4.0

## Added

- **Automatic Floating Window on Trace Events**
  - Floating trace log window automatically opens when trace events occur and no document is open
  - Listener monitors document open/close events to manage window visibility
  - Auto-hides when document is opened to avoid interference

- **External File Logging System**
  - Multiple log file format support:
    - **Plain Text (.log)** - Human-readable format with timestamps
    - **JSON (.json)** - Structured JSON format for parsing
    - **CLEF (.clef)** - Compact Log Event Format for analysis tools like Seq
    - **SQLite (.db)** - Database format for querying and data analysis
  - Configurable rolling intervals (daily, hourly, etc.)
  - File size limit controls
  - Shared file access support for multi-process logging

- **Log Configuration UI**
  - Settings panel for managing log output preferences
  - Enable/disable external file logging
  - Select log format and output path
  - Configure rolling intervals and file management
  - Stack trace inclusion toggle with depth control

- **Enhanced Stack Trace in Logs**
  - Stack traces now show actual caller location (method, file, line number)
  - Automatic filtering of framework and system internals
  - Configurable stack trace depth
  - Displays call chain in readable format (e.g., `MyClass.Execute:42 > Helper.Process:18`)
  - Includes Revit version and active document name in structured log context

## Changed

- **Visualization System**
  - Adjusted default axis length for better scale
  - Optimized geometry rendering pipeline

- **UI Adjustments**
  - Disabled window backdrop (mica/acrylic) to fix RichTextBox overlay issues
  - Adjusted glass frame thickness for better WinForms control integration

## Fixed

- Proper cleanup of trace listener when Revit closes
- Window theme subscription leaks on close

# 1.3.3

## Changed

- Updated manual test case: batch log

## Fixed

- `Clear` method in TraceLog - text not actually clearing and reappearing on next trace event due to `OnIdling` event handler interference
- Curve Test case - cast to `Edge` instead of `Curve`

# 1.3.2

## Added

- DockablePane visibility tracking to automatically subscribe/unsubscribe TraceLog when panel is shown/hidden
- Proper lifecycle management for TraceLog with subscription state tracking

## Changed

- Changed default `AxisLength` in `XyzVisualizationSettings` from 6 to 2 for better default visualization scale
- Changed Settings window startup location from `CenterScreen` to `CenterOwner`
- Improved TraceLog initialization and disposal flow with better state management
- Refactored TraceLog to completely unsubscribe when panel is not visible
- Enhanced theme change handling with proper dispatcher invocation
- Removed unused external event handlers (`AsyncEventHandler`, `AsyncCollectionEventHandler`, `IdlingEventHandler`)
- Updated `.csproj` repack configuration to exclude only UI assemblies

## Fixed

- Prevented multiple Settings windows from opening simultaneously by controlling `OpenSettings` command execution state
- TraceLog resource cleanup and memory management
- Trace listeners registration to ensure persistence across application lifecycle

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
- Trace support for ICollection `<object>` and IEnumerable `<CurveLoop>`
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
