# RevitDevTool

Bring standard development ecosystems into CAD/BIM hosts — without replacing them.

<div style="vertical-align: center;">
    <a href="https://github.com/trgiangv/RevitDevTool/releases/latest"><img src="https://img.shields.io/badge/Revit-2022--2027-blue.svg?style=flat-square" alt="RevitVersion"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/releases/latest"><img src="https://img.shields.io/badge/AutoCAD-2022--2027-orange.svg?style=flat-square" alt="AutoCADVersion"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/releases/latest"><img src="https://img.shields.io/github/v/release/trgiangv/RevitDevTool?style=flat-square" alt="Release"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/releases/latest"><img src="https://img.shields.io/github/downloads/trgiangv/RevitDevTool/total?style=flat-square" alt="Downloads"></a>
</div>

**The development platform for BIM and CAD.** Run scripts and add-ins in the live session, test them in-process, and let AI assistants work with your open model, all from one add-in.

- **Run code** — CPython¹, IronPython², C# `.csx`, F# `.fsx`, and compiled .NET `.dll`³
- **Debug** — breakpoints in VS Code or PyCharm for CPython and IronPython, your IDE for .NET code
- **Test in the host** — pytest, NUnit, TUnit and MSTest inside a real Revit or AutoCAD session
- **Connect AI** — [MCP](https://modelcontextprotocol.io/) for Cursor, Claude, GitHub Copilot, Codex and more, extensible with your own Python or C# toolsets
- **See what happens** — color-coded logs with stack traces, plus 3D geometry preview in Revit

<sub>¹ CPython 3.14 with inline dependencies (PEP 723 + [Pixi](https://pixi.sh/)) · ² pyRevit-compatible · ³ hot reload</sub>

## Supported hosts

| Host                                    | Versions  | Run code | Test | AI (MCP) | Geometry preview |
| --------------------------------------- | --------- | :------: | :--: | :------: | :--------------: |
| **Revit**                               | 2022–2027 |    ✓     |  ✓   |    ✓     |        ✓         |
| **AutoCAD**, **Civil 3D**, **Plant 3D** | 2022–2027 |    ✓     |  ✓   |    ✓     |        —         |

Other AutoCAD-based products load the same add-in. Plant 3D embeds its own CPython, so it uses the host's Python version and installs packages with uv from PyPI.

## Quick start

**1. Install.** Download `RevitDevTool-Setup.exe` from [Releases](https://github.com/trgiangv/RevitDevTool/releases/latest) and run it. It installs for the current user and needs no administrator rights.

**2. Write a script.** Create `hello_script.py` in an empty folder. Dependencies go in the header:

```python
# /// script
# dependencies = ["polars"]
# ///

import polars as pl
from Autodesk.Revit import DB

doc = __revit__.ActiveUIDocument.Document
walls = DB.FilteredElementCollector(doc).OfClass(DB.Wall)

df = pl.DataFrame({"name": [w.Name for w in walls]})
print(df.group_by("name").len())
```

**3. Run it.** Open the RevitDevTool panel in Revit (or the DevTools panel in AutoCAD), add the folder, click the script, and read the output in the Trace panel. Dependencies install on first run.

Next: [run your first script](https://trgiangv.github.io/RevitDevTool/docs/getting-started/installation/#run-your-first-script).

## Documentation

Full guides live on the **[documentation site](https://trgiangv.github.io/RevitDevTool/)**.

| I want to…                         | Read                                                                                                                                                                                                       |
| ---------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Install and run a first script     | [Installation](https://trgiangv.github.io/RevitDevTool/docs/getting-started/installation/) · [First script](https://trgiangv.github.io/RevitDevTool/docs/getting-started/installation/#run-your-first-script)           |
| Choose a script runtime            | [Execution Overview](https://trgiangv.github.io/RevitDevTool/docs/execution/Execution-Overview/)                                                                                                           |
| Use Python packages                | [Python Dependencies](https://trgiangv.github.io/RevitDevTool/docs/execution/Python-Dependencies/)                                                                                                         |
| Debug Python in VS Code or PyCharm | [Attach Debugger](https://trgiangv.github.io/RevitDevTool/docs/execution/python/Execution-PythonDebugging/)                                                                                                |
| Write tests that run in the host   | [Testing Overview](https://trgiangv.github.io/RevitDevTool/docs/testing/Testing-Overview/)                                                                                                                 |
| Connect an AI assistant            | [MCP Protocol](https://trgiangv.github.io/RevitDevTool/docs/mcp/MCP-Protocol/)                                                                                                                             |
| Customize logging and 3D preview   | [Log Level](https://trgiangv.github.io/RevitDevTool/docs/logging/Logging-Overview/) · [Geometry Visualization](https://trgiangv.github.io/RevitDevTool/docs/logging/Visualization-Overview/)              |
| Fix a problem                      | [Troubleshooting](https://trgiangv.github.io/RevitDevTool/docs/getting-started/Troubleshooting/) · [Known Limitations](https://trgiangv.github.io/RevitDevTool/docs/getting-started/Known-Limitations/)    |

## Samples

| Sample                                                                                                                                          | Contents                                                                                                         |
| ----------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| [`samples/PythonDemo/commands`](samples/PythonDemo/commands)                                                                                    | Data analysis (Polars), WebView2 dashboard, logging formats, 3D geometry preview, scikit-learn, shapely, trimesh |
| [`samples/PythonDemo/mcp_toolset`](samples/PythonDemo/mcp_toolset)                                                                              | Custom MCP toolset in Python                                                                                     |
| [`samples/CSharpDemo`](samples/CSharpDemo) · [`CSharpScriptDemo`](samples/CSharpScriptDemo) · [`FSharpScriptDemo`](samples/FSharpScriptDemo)    | .NET add-in, C# script and F# script                                                                             |
| [`samples/McpToolsetDemo`](samples/McpToolsetDemo) · [`RevitMcpToolSet`](samples/RevitMcpToolSet)                                               | Custom MCP toolsets in C#                                                                                        |
| `samples/DevTools.NUnit.SampleTests` and its TUnit / MSTest siblings                                                                            | In-host .NET tests                                                                                               |

Walkthroughs: [Examples](https://trgiangv.github.io/RevitDevTool/docs/examples/Examples-Overview/).

## Contributing

Contributions are welcome. Start with the [repository documentation map](docs/README.md), which separates product behavior, architecture and contributor workflow. Keep shared platform code in `DevTools.*` and host-specific code in `RevitDevTool` or `AcadDevTool`.

```powershell
git clone https://github.com/trgiangv/RevitDevTool.git
cd RevitDevTool

# stop the host, then build and deploy
scripts/build-host.ps1 -HostApp Revit -Year 2025

# or build the solution without touching a running host
dotnet build RevitDevTool.slnx -c "Debug.Autodesk.2025"
```

Requires the .NET SDK pinned in `global.json`. Configurations run from `Debug.Autodesk.2022` to `Release.Autodesk.2027`. More: [Build From Source](https://trgiangv.github.io/RevitDevTool/docs/getting-started/Build-From-Source/).

## Get help

- [Documentation](https://trgiangv.github.io/RevitDevTool/)
- [Bug reports](https://github.com/trgiangv/RevitDevTool/issues)
- [Questions and ideas](https://github.com/trgiangv/RevitDevTool/discussions)

## Acknowledgments

- [RevitLookup](https://github.com/lookup-foundation/RevitLookup) — DirectContext3D implementation reference
- [RevitDevTool (original)](https://github.com/Zhuangkh/RevitDevTool) — project inspiration
- [RevitAddinManager](https://github.com/chuongmep/RevitAddinManager) — add-in hot reload patterns
- [pyRevit](https://github.com/pyrevitlabs/pyRevit) and [RevitPythonShell](https://github.com/architecture-building-systems/revitpythonshell) — Python scripting in Revit
- [RevitToolkit](https://github.com/Nice3point/RevitToolkit) — Revit context for test projects
- [Pixi](https://pixi.sh/) — package management
- [Python.NET](https://github.com/pythonnet/pythonnet) — Python and .NET bridge

## License

MIT — see [LICENSE](LICENSE).
