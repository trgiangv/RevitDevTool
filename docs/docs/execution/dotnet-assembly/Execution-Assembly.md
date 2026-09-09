# .NET Assembly

.NET assembly execution is for compiled add-ins and commands.

Scripts, MCP toolsets, and compiled commands each load in isolated assembly contexts: Revit 2025+ uses a collectible AssemblyLoadContext; Revit 2022–2024 on .NET Framework uses net48 scoped resolve.

Use it when you are developing a compiled command and want to run it from RevitDevTool during iteration.

In Revit, this fills the same practical role as Revit Add-In Manager: build a command assembly, load it, and run commands without packaging a full add-in workflow each time. For many development workflows, RevitDevTool can replace Add-In Manager while also giving you the surrounding logging, scripting, visualization, and tool workflow.

## Running the Assembly

In Revit, assembly execution is centered around Revit command contracts such as `IExternalCommand`. In other hosts, the same idea applies to that host's command/tool assembly model.

Typical workflow:

1. Build a command assembly.
2. Add its folder to RevitDevTool settings.
3. Let the command tree discover the assembly.
4. Run the command from the panel or command browser.

## Debugging

Attach a .NET debugger to the running host process (`Revit.exe`, `acad.exe`, …), set breakpoints in your compiled source, then run the command from RevitDevTool.

| IDE | Workflow |
| --- | --- |
| **Visual Studio** | Debug → Attach to Process → select host → Run command |
| **VS Code** | C# Dev Kit → Attach to Process → select host → Run command |

![Assembly debugger](/images/execution/AssemblyDebugger.png)

*Placeholder — replace with a product screenshot.*

For shared attach guidance (including C# and F# scripts), see [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

## Version Notes

| Autodesk version | Runtime behavior |
| --- | --- |
| 2022-2024 | .NET Framework / `net48`; no unloadable AssemblyLoadContext |
| 2025-2026 | .NET 8; unloadable AssemblyLoadContext can be used |
| 2027 | .NET 10; follows modern .NET path |

The legacy .NET Framework path needs extra care around assembly loading and unmanaged DLL resolution.

## Samples

Current samples:

```text
samples/CSharpDemo/
samples/AcadCSharpDemo/
samples/McpToolsetDemo/
samples/RevitMcpToolSet/
```

## User Guidance

When building external assemblies:

- target the correct Autodesk year/runtime;
- keep host API references aligned with the target year;
- test against the actual host when command behavior depends on host API context.

For build commands, see [Build From Source](/docs/getting-started/Build-From-Source).

## Related

- [Execution Overview](/docs/execution/Execution-Overview)
- [Attach Debugger](/docs/execution/python/Execution-PythonDebugging)
- [C# Scripts](/docs/execution/csharp-script/Execution-CSharp)
