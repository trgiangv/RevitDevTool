# Build From Source

The solution source of truth is:

```text
RevitDevTool.slnx
```

The repo root intentionally does not rely on a classic `.sln` file.

## SDK

The required .NET SDK is defined in `global.json`.

Current requirement:

```text
.NET SDK 10.0.0
```

## Build Matrix

Supported Autodesk build configurations:

```text
Debug.Autodesk.2022
Debug.Autodesk.2023
Debug.Autodesk.2024
Debug.Autodesk.2025
Debug.Autodesk.2026
Debug.Autodesk.2027
Release.Autodesk.2022
Release.Autodesk.2023
Release.Autodesk.2024
Release.Autodesk.2025
Release.Autodesk.2026
Release.Autodesk.2027
```

Framework mapping:

| Autodesk generation | Target framework |
| --- | --- |
| 2022-2024 | `net48` |
| 2025-2026 | `net8.0-windows` |
| 2027 | `net10.0-windows` |

## Focused Build

For local compile checks, build the `.slnx` with the matching Autodesk configuration:

```powershell
dotnet build RevitDevTool.slnx -c "Debug.Autodesk.2025"
```

If the repo has the agent helper scripts available, this is equivalent to:

```powershell
scripts/agent/build-host.ps1 -Year 2025
```

## Package Build

The build pipeline lives in `build/Program.cs`.

Package command:

```powershell
cd build
dotnet run -c Release pack
```

Publish command:

```powershell
cd build
dotnet run -c Release -- pack publish
```

Packaging depends on compiled `bin/publish` outputs and includes bundle creation, Daemon publishing, and installer creation.

## Tests

Current test projects are useful for smoke validation and focused behavior checks, but they are not complete end-to-end proof of host behavior.

Known test areas:

- execution and Python bridge tests under `tests/DevTools.Execution.Tests/`
- MCP daemon and dynamic-tool tests under `tests/DevTools.Mcp.Server.Tests/` and `tests/DevTools.Daemon.Tests/`
- telemetry tests under `tests/DevTools.Telemetry.Tests/`
- MTP adapter tests under `tests/DevTools.NUnit.MTP.Tests/`
- external event test hosts under `tests/RevitDevTool.ExternalEvent.App/`

Some tests rely on prepared sample assets, a Pixi environment, or a live host bridge. Treat failures as signals to inspect context, not as always-isolated unit test failures.
