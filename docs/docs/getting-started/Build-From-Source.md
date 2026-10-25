# Build From Source

Build from the solution file:

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

Test projects live under `tests/`. Run one with `dotnet run --project tests/<project>/<project>.csproj`.

Some tests need prepared sample assets, a Pixi environment, or a running host. If one fails, check that environment first. Contributor workflow details are in the [repository documentation map](https://github.com/trgiangv/RevitDevTool/blob/develop/docs/README.md).
