# Build Matrix

## Source Of Truth

- Solution: `RevitDevTool.slnx`.
- Build pipeline: `build/Program.cs` and `build/Modules/*`.
- Required SDK: .NET `10.0.0` from `global.json`.

## Supported Host Configurations

| Autodesk year | Configuration suffix | Target framework |
|---------------|----------------------|------------------|
| 2022 | `Autodesk.2022` | `net48` |
| 2023 | `Autodesk.2023` | `net48` |
| 2024 | `Autodesk.2024` | `net48` |
| 2025 | `Autodesk.2025` | `net8.0-windows` |
| 2026 | `Autodesk.2026` | `net8.0-windows` |
| 2027 | `Autodesk.2027` | `net10.0-windows` |

Valid modes are `Debug` and `Release`, so full names look like `Debug.Autodesk.2025`.

## Commands

- Focused host compile: `scripts/agent/build-host.ps1 -Year 2025`.
- Release package: `scripts/agent/pack.ps1`.
- Build pipeline with no args: `dotnet run -c Release` from `build/` compiles selected release configurations.

## Compatibility Rule

Any shared `DevTools.*` change can affect all target frameworks. If code is reachable from 2022-2024, verify .NET Framework compatibility and avoid newer BCL APIs unless the repo already has a compatibility helper or package.
