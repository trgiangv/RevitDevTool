# Known Limitations

Things RevitDevTool does not do, or does only under certain conditions, and what to do about each.

## Hosts

| Limitation | What to do |
| --- | --- |
| [Geometry visualization](/docs/logging/Visualization-Overview) works on Revit only | On AutoCAD-family hosts, use logging output (Monitor, File, HTTP) |
| Search Commands and Element Finder are Revit only | None; not yet available on AutoCAD-family hosts |
| Host API stubs are not bundled for every Autodesk version | [Generate `.pyi` stubs](/docs/execution/python/Execution-StubGeneration) from the target assemblies |

## Python

| Limitation | What to do |
| --- | --- |
| CPython needs Pixi, which needs a writable `%APPDATA%` and a reachable conda-forge/PyPI | Ask IT to allow Pixi. If Pixi is blocked, scripts use the pyRevit CPython engine when pyRevit is installed |
| With no Pixi and no pyRevit engine, CPython scripts do not run | Allow Pixi or install pyRevit |
| Plant 3D runs on the host's own CPython (3.13 on Plant 3D 2027), with packages from PyPI through uv. Conda-only packages and packages with no wheel for that version are unavailable | Pick packages that publish wheels for the host's Python version |
| Plant 3D has no pyRevit fallback | If uv cannot run, ask IT to allow it |
| CPython debugging needs `debugpy` and a free debug port | Add `debugpy` to the script's dependencies |

## Testing

| Limitation | What to do |
| --- | --- |
| IronPython tests (`test_*_ipy.py`) use unittest semantics: no pytest fixtures and no PEP 723 dependency install | Use CPython tests when you need fixtures or packages |
| One pytest run cannot mix a CPython and an IronPython `conftest.py` tree | Run them as separate pytest sessions against the same host |
| Tests in one host instance do not run in parallel, because host API calls run on the main thread | Run slow suites in separate hosts if you need parallelism |

## AI tools

| Limitation | What to do |
| --- | --- |
| The MCP server needs the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0); without it, AI tools are unavailable even when a host is running | Install the runtime |
| The server finds only hosts that have loaded RevitDevTool. If the add-in crashes at startup, no MCP pipe exists | Check the `crash_*` logs in [Troubleshooting](/docs/getting-started/Troubleshooting) |