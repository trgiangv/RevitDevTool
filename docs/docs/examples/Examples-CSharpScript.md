# Example: C# Script Demo

**Scripts:** [`main_script.csx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/CSharpScriptDemo/main_script.csx) · [`wpf_script.csx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/CSharpScriptDemo/wpf_script.csx)

Use these examples when you want to test a small C# Revit command without creating a full compiled add-in project.

## What It Shows

- referencing Revit API assemblies from a script;
- loading a helper script with `#load`;
- defining an `IExternalCommand` class;
- showing a Revit `TaskDialog`;
- selecting an element;
- tracing bounding box and point geometry for visualization/logging (`main_script.csx`);
- a minimal WPF surface from a script (`wpf_script.csx`).

## Run

1. Open Revit 2025 or adjust the `#r` paths in the script to match your installed Revit version.
2. Add `samples/CSharpScriptDemo/` as a script folder.
3. Run `main_script.csx` or `wpf_script.csx`.
4. For `main_script.csx`, select an element when prompted.
5. Review the trace panel and any temporary geometry output.

## What To Edit

Common edits:

- Change Revit API reference paths for another Revit version.
- Add more helper functions under `Helpers/`.
- Replace the selected element logic with a collector.
- Trace different geometry objects to test visualization.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Revit API reference not found | update `#r` paths for your Revit version |
| script compiles but selection fails | run in an open document and select a valid element |
| no visualization | confirm the traced object is supported geometry |
| command not discovered | file name must end with `script.csx` |

## Related

- [C# Scripts](/docs/execution/csharp-script/Execution-CSharp)
- [.NET Assembly](/docs/execution/dotnet-assembly/Execution-Assembly)
