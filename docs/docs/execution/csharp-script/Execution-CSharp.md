# C# Scripts

Use C# scripts when you want a short typed snippet without creating a full add-in project.

Entry suffix:

```text
*script.csx
```

Example:

```text
samples/CSharpScriptDemo/main_script.csx
```

## NuGet References

Declare package references with Roslyn `#r` directives:

```csharp
#r "nuget: Newtonsoft.Json, 13.0.3"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPI.dll"
#load "./Helpers/DocumentHelper.csx"
```

`#r "nuget: …"` resolves through the shared NuGet resolver (`NugetManager`). Packages restore under `%APPDATA%\RevitDevTool\nuget`. Host API paths can be rewritten by the host bridge.

`#load` pulls additional `.csx` files into the script graph. Each file can declare its own `#r` and `#load` directives.

## Host Version Symbols

Scripts receive the same preprocessor symbols as host add-in projects (`REVIT2025`, `REVIT2025_OR_GREATER`, `AUTOCAD2026`, and so on) via `CompileScriptSymbols`. Use `#if REVIT2025_OR_GREATER` in `.csx` the same way you would in a compiled add-in.

## Debugging

C# scripts compile with **Debug** emit and a **portable PDB** so an attached debugger can bind breakpoints to the original `.csx` source.

Workflow:

1. Open the script folder in Visual Studio or VS Code (C# Dev Kit recommended).
2. Set breakpoints in your `.csx` files.
3. Attach the debugger to the running host process (`Revit.exe`, `acad.exe`, …).
4. Run the script from RevitDevTool.

![C# script debugger](/images/execution/CsxDebugger.png)

*Placeholder — replace with a product screenshot.*

For attach settings shared with F# scripts and assemblies, see [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

## Samples

```text
samples/CSharpScriptDemo/
samples/AcadCSharpDemo/   (AutoCAD-family)
```

## Related

- [Execution Overview](/docs/execution/Execution-Overview)
- [Attach Debugger](/docs/execution/python/Execution-PythonDebugging)
- [F# Scripts](/docs/execution/fsharp-script/Execution-FSharp)
- [.NET Assembly](/docs/execution/dotnet-assembly/Execution-Assembly)
