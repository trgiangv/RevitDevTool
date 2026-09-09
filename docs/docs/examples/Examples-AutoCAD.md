# Example: AutoCAD C# Demo

AutoCAD-family illustration — not a separate RevitDevTool host product. Platform features: [AutoCAD](/docs/hosts/Hosts-AutoCAD).

**Sample:** [`AcadCSharpDemo/`](https://github.com/trgiangv/RevitDevTool/tree/main/samples/AcadCSharpDemo)

Use this example to get started with AutoCAD C# commands through RevitDevTool.

## What It Shows

- AutoCAD C# command sample structure;
- editor command examples;
- logging examples for AutoCAD host integration.

## Run / Try

1. Build the sample for the matching Autodesk configuration.
2. Load it through the AutoCAD host workflow available in your local setup.
3. Run the sample editor/logging commands.
4. Inspect AutoCAD output and RevitDevTool/DevTools logging behavior where available.

## Expectations

Visualization (DirectContext3D) is Revit-only and not available in AutoCAD. All other execution, logging, MCP, and testing features are shared.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| command does not load | confirm AutoCAD version/configuration matches the build |
| logging output missing | confirm the AutoCAD host integration is loaded |
| Revit-only feature missing | this is expected unless documented for AutoCAD |

## Related

- [AutoCAD](/docs/hosts/Hosts-AutoCAD)
- [Feature Overview](/docs/getting-started/Platform-v3.0-Overview)
