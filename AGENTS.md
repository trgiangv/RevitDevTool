# AGENTS

Last verified: 2026-07-21

## Repository Contract
- This repo is a reusable .NET host/dev-tool platform for CAD/BIM applications, not a Revit-only add-in. Revit and AutoCAD are current hosts; future hosts may include Tekla, Bentley, or other .NET-capable platforms.
- `RevitDevTool.slnx` is the solution source of truth. The repo root has no `.sln`; build logic and CI target `.slnx`.
- Keep shared platform behavior in `source/DevTools.*`. Keep host API dependencies in host projects such as `source/RevitDevTool/` and `source/AcadDevTool/`.

## Harness Governance
- Repo-owned harness is the source of truth: `AGENTS.md`, `docs/agents/`, and `.agents/skills/`.
- Do not add tool-specific rule adapters (`.cursorrules`, `.clauderules`, etc.) — point tools at this harness instead.

## Read First
- Start with `docs/agents/index.md` for task routing.
- For core behavior, read the matching module README before editing: `docs/Execution/README.md`, `docs/MCP/README.md`, `docs/PyTest/README.md`, `docs/Logging/README.md`, or `docs/Visualization/README.md`.
- Domain skills only (do not invent a build skill): read the matching `.agents/skills/*/SKILL.md` when the task fits.
- Trust current code and build modules over older prose. Stale path references are listed in **Common Traps** below.

## Compile Verify (hooks first)
- Cursor project hooks under `.cursor/hooks/` track `.cs`/`.csproj`/`.xaml` edits and compile on agent **stop**. Do not call ad-hoc MSBuild for routine verify — wait for / rely on the hook, then fix reported errors.
- Revit API projects (`UseRevit`): hook builds `Debug.Autodesk.2022`, `2025`, and `2027` (compile-only: no deploy, no ILRepack).
- AutoCAD API projects (`UseAutoCad`): same year matrix, compile-only.
- **Hook uses 3 representative years (one per TFM: net48 / net8 / net10), not the full 6-year matrix.** All `2022`–`2027` configs remain supported for manual/CI builds — see Build Matrix.
- Shared / multi-TFM `DevTools.*`: hook builds `Debug` (all TargetFrameworks). Packaging edits under `build/`/`install/` switch the pending mode to `Release`.
- Manual deploy (kills host locks): `scripts/kill-host.ps1` then `scripts/build-host.ps1 -Year 2025`.
- Focused manual compile-only (when hook did not run):  
  `dotnet build <csproj> -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:IsRepackable=false`

## Build Matrix
- Required SDK: .NET `10.0.0` via `global.json`.
- Supported Autodesk configurations: `Debug.Autodesk.2022`–`2027` and `Release.Autodesk.2022`–`2027` (all six years).
- Autodesk 2022-2024 → `net48`; 2025-2026 → `net8.0-windows`; 2027 → `net10.0-windows`. Shared libs multi-target; Polyfill covers most modern C# on net48 — prefer compile feedback over speculative API reviews.
- CI/package: `scripts/pack.ps1` or `dotnet run -c Release pack` from `build/`.
- Daemon publish: `dotnet publish source/DevTools.Daemon -c Release` (csproj kills running instance and deploys to bundle Contents).

## Verification
- Prefer scripts in `scripts/`. Do not guess commands.
- .NET tests: `scripts/test-dotnet.ps1`
- Python parser tests (this repo): `scripts/test-python.ps1`
- **Client pytest (sibling repo `RevitDevTool.PyTest`)**: always `uv run pytest` from that repo root. Never search system Python, never bare `pytest` without uv.
  ```powershell
  cd c:\Users\truon\source\repos\RevitDevTool.PyTest
  uv run pytest -v
  uv run pytest tests/Revit/<file>.py -v
  ```
- Collect logs: `scripts/collect-logs.ps1`
- Kill host before deploy: `scripts/kill-host.ps1 [-HostApp All|Revit|AutoCAD]`
- Passing smoke tests are not strong proof. Add focused tests when behavior changes; document host/Pixi blockers instead of unrelated edits.

## Repo Shape
- Revit host: `source/RevitDevTool/`. Revit-only helpers: `source/RevitDevTool.Core/`.
- AutoCAD host: `source/AcadDevTool/`.
- Shared platform: `source/DevTools.*` (Execution, Execution.Abstractions, Ipc, Mcp, Logging, Presentation, Settings, Telemetry, UI, Utilities, Daemon).
- Samples under `samples/`; build under `build/`; agent scripts under `scripts/`.

## Host Boundaries
- Default: features are sharable. Host API / threading / rendering stay in host projects.
- Shared execution depends on abstractions (`IHostContextExecutor`, discovery/runner bridges). `RevitDevTool.Core` is Revit-only.
- Visualization (DirectContext3D) lives in `source/RevitDevTool/Visualization/`. Daemon is host-agnostic.

## Common Traps
Stale paths — some tests/docs still reference these; current locations are:
- Solution: `RevitDevTool.slnx` (not a root `.sln`, not `RevitDevTool.sln`).
- Samples: `samples/` (not `source/samples/`).
- Embedded scripts: `source/DevTools.Execution/Resources/scripts/` (not `source/RevitDevTool/Resources/scripts`).
- Test-specific stale paths: `docs/agents/known-test-gaps.md`.

Other traps:
- Parser integration tests expect `%APPDATA%\RevitDevTool\pixi-env\.pixi\envs\default`.
- ILRepack disabled for 2027 host projects (isolated context).

## Change Rules
- After edits, let the compile hook report errors; fix from that feedback. Only manually build when verifying deploy or packaging.
- Scale tests to risk. Update `docs/*/README.md` or `docs/agents/*.md` when architecture/workflow changes — not every layer by default.
- Preserve user changes. Do not revert unrelated dirty work.
