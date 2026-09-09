# F# Scripts

Use F# scripts for quick experiments when you prefer F# syntax and interactive-style scripting.

Entry suffix:

```text
*script.fsx
```

Examples:

```text
samples/FSharpScriptDemo/main_script.fsx
samples/FSharpScriptDemo/selection_script.fsx
```

## NuGet References

Declare package references with F# `#r` directives:

```fsharp
#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: Newtonsoft.Json"
#load "Helpers.fsx"
```

`#r "nuget: …"` resolves through `FSharpDependencyResolver` and the shared `NugetManager`. Restored packages and restore graphs live under:

```text
%APPDATA%\RevitDevTool\nuget
```

When NuGet or file `#r` directives must be rewritten, the graph is copied under a temp cache with `#line` mapping so diagnostics and debugging point back to your original files.

![F# NuGet resolution](/images/execution/FsxNuget.png)

*Placeholder — replace with a product screenshot.*

## Host Version Symbols

F# scripts receive the same host-year symbols as C# scripts (`REVIT`, `AUTOCAD`, `{HOST}{year}`, `{HOST}{year}_OR_GREATER`) via FSI `--define:` arguments from `CompileScriptSymbols`.

## Debugging

F# scripts run through FSI with `--debug+` and sequence-point mapping back to the original `.fsx` paths. Attach the debugger to the host process, set breakpoints in your `.fsx` files, then run from RevitDevTool.

For attach settings shared with C# scripts and assemblies, see [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

## Editing

For a better VSCode editing experience, install [Ionide for F#](https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp).

## Related

- [Execution Overview](/docs/execution/Execution-Overview)
- [Attach Debugger](/docs/execution/python/Execution-PythonDebugging)
- [C# Scripts](/docs/execution/csharp-script/Execution-CSharp)
- [.NET Assembly](/docs/execution/dotnet-assembly/Execution-Assembly)
