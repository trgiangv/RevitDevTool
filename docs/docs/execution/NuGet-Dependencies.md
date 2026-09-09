# NuGet Dependencies

RevitDevTool supports NuGet references for C# scripts, F# scripts, and compiled .NET projects. This page is the shared guide for resolving packages, keeping host API references aligned, and diagnosing restore problems.

## Where NuGet applies

| Workflow | Syntax or source | Best use |
| --- | --- | --- |
| C# script | `#r "nuget: Package, Version"` | Add a library to a `.csx` without creating a project |
| F# script | `#r "nuget: Package, Version"` | Reference libraries from an `.fsx` script |
| .NET assembly | Project `PackageReference` | Build a repeatable add-in or command assembly |

Python dependencies use PEP 723 metadata and PyPI/conda-forge instead. See [Modern Python Scripting](/docs/execution/python/Execution-Python).

## C# and F# scripts

Declare the package directly in the script:

```csharp
#r "nuget: Humanizer, 2.14.1"
```

```fsharp
#r "nuget: MathNet.Numerics, 5.0.0"
```

The shared resolver restores packages under `%APPDATA%\RevitDevTool\nuget`. Keep versions explicit while debugging or sharing a script so another machine resolves the same dependency graph.

## Compiled assemblies

Use normal project references and target the Autodesk year you are building for:

```xml
<ItemGroup>
  <PackageReference Include="Your.Package" Version="1.2.3" />
</ItemGroup>
```

Autodesk Revit API assemblies are compile-time references, not NuGet packages managed by the script resolver. Align the API reference, target framework, and host year before investigating package errors.

## Resolution rules

- Pin versions for reproducible commands and examples.
- Prefer packages compatible with the host target framework.
- Avoid bringing a second copy of an assembly already loaded by the Autodesk host.
- Keep transitive dependencies compatible with the host's loaded versions.
- Re-run the command after changing a package declaration so the resolver can rebuild its cache.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Package cannot be found | Verify the package id, version, network access, and configured feeds |
| Compile reports duplicate types or assemblies | Remove a conflicting package or align it with the host-loaded version |
| Script works in one host year only | Check the Autodesk API references and target framework |
| Breakpoints do not bind after restore | Keep the original source path and attach the debugger to the host process |

For debugging across C# scripts, F# scripts, .NET assemblies, and CPython, see [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

## Related

- [C# Scripts](/docs/execution/csharp-script/Execution-CSharp)
- [F# Scripts](/docs/execution/fsharp-script/Execution-FSharp)
- [.NET Assembly](/docs/execution/dotnet-assembly/Execution-Assembly)
- [Attach Debugger](/docs/execution/python/Execution-PythonDebugging)
