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

`scripts/build-host.ps1` with no arguments stops and deploys both hosts for every year `2022`–`2027` in `Release`. One host builds that host project. Both hosts build `RevitDevTool.slnx` once per year. Parameters and project paths: [build-matrix.md](https://github.com/trgiangv/RevitDevTool/blob/develop/docs/agents/build-matrix.md#host-build-script).

```powershell
scripts/build-host.ps1
scripts/build-host.ps1 -HostApp Revit -Year 2025 -Mode Debug
```

A solution build that does not stop a running host:

```powershell
dotnet build RevitDevTool.slnx -c "Debug.Autodesk.2025"
```

## Package Build

The build pipeline lives in `build/Program.cs`.

Package command:

```powershell
dotnet run --project build -- pack
```

Publish command:

```powershell
dotnet run --project build -- pack publish
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
