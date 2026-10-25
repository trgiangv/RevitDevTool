# Troubleshooting

## Add-In Failed To Load (crash logs)

When the add-in fails during host startup, RevitDevTool writes a one-shot crash log before the panel appears:

```
%APPDATA%\RevitDevTool\{Year}\Logs\crash_{app}_{ver}_{pid}.log
```

Example (Revit 2025, PID 12345):

```
%APPDATA%\RevitDevTool\2025\Logs\crash_Revit_2025_12345.log
```

Check this file first when the ribbon tab is missing, MCP lists no hosts after a rebuild, or pytest cannot connect immediately after launch.

Rolling session logs use the `log_*` pattern in the same folder. AutoClean removes `log_*` only — `crash_*` files are kept.

## Python Dependencies Do Not Install

Check:

- Pixi can run on the machine (enterprise policy may block third-party executables)
- `%APPDATA%\RevitDevTool\pixi-env` is writable
- On **Plant 3D only**: uv can run, and `%APPDATA%\RevitDevTool\uv-env\{major.minor}` is writable
- Package indexes are reachable (conda-forge + PyPI for Pixi; PyPI for the Plant 3D uv sidecar)
- PEP 723 `# /// script` block is present and valid at the top of the script

If Pixi is blocked on Revit, AutoCAD or Civil 3D, confirm pyRevit is installed so its CPython engine can be used as the fallback. Plant 3D has no fallback: uv must be able to run.

## Python Debugger Does Not Attach

Check:

- Your IDE is attached to the configured port
- The port shown for that instance in RevitDevTool matches your attach configuration
- `debugpy` is installed in the active Python environment (Pixi env, Plant 3D uv sidecar, or fallback)
- Firewall/security policy allows local attach

## pytest Cannot Connect

Check:

1. The host (Revit, AutoCAD, or Civil 3D) is running with RevitDevTool loaded
2. `host_name` and `host_version` in `pyproject.toml` match the running instance
3. Try `uv run pytest --force-launch --host-version 2025 -v`
4. If the add-in failed to load, read the newest startup log (see above)

IronPython tests (`test_*_ipy.py`) use unittest semantics and do not use PEP 723 dependencies. On Revit they prefer the pyRevit engine when it is installed.

## Geometry Does Not Display (Revit)

Check:

- The active view supports DirectContext3D rendering
- Visualization is enabled in settings
- The traced object is a supported Revit geometry type
- The geometry is within visible view range
- The script printed the actual geometry object, not just a string representation

## AI Client Cannot Find Hosts

Check:

- At least one host instance is running with RevitDevTool loaded
- [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) is installed
- If hosts disappeared after a rebuild, read the newest `crash_*` log before assuming the MCP server failed

**MCP client config** should follow the installation instructions for your client and point to the RevitDevTool MCP server.

## MCP Server Logs

The stdio MCP server writes to:

```
%APPDATA%\RevitDevTool\logs\
```

Useful when the AI client times out or `list_processes` returns empty.
