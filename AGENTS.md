# AGENTS

## Repository Contract
- This repo is a reusable .NET host/dev-tool platform for CAD/BIM applications, not a Revit-only add-in. Revit and AutoCAD are current hosts; future hosts may include Tekla, Bentley, or other .NET-capable platforms.
- `RevitDevTool.slnx` is the solution source of truth. The repo root has no `.sln`; build logic and CI target `.slnx`.
- Repo-owned harness is the source of truth. Every AI coding tool should use `AGENTS.md`, `docs/ai/`, and `.agents/skills/`; do not add tool-specific rule adapters.
- Keep shared platform behavior in `source/DevTools.*`. Keep host API dependencies in host projects such as `source/RevitDevTool/` and `source/AcadDevTool/`.

## Read First
- Start with `docs/ai/index.md` for task routing.
- For core behavior, read the matching module README before editing: `docs/Execution/README.md`, `docs/MCP/README.md`, `docs/PyTest/README.md`, `docs/Logging/README.md`, or `docs/Visualization/README.md`.
- For task-specific checklists, use `.agents/skills/*/SKILL.md`.
- Trust current code and build modules over older prose. Some tests/docs still reference old paths such as `source/Samples/...`, `source/RevitDevTool/Resources/scripts`, or `RevitDevTool.sln`.

## Repo Shape
- Revit host: `source/RevitDevTool/`.
- AutoCAD host: `source/AcadDevTool/`.
- Shared platform libraries: `source/DevTools.Execution/`, `source/DevTools.Logging/`, `source/DevTools.McpParser/`, `source/DevTools.McpServer/`, `source/DevTools.Presentation/`, `source/DevTools.Settings/`, `source/DevTools.Telemetry/`, `source/DevTools.UI/`, and `source/DevTools.Utilities/`.
- Samples and demo toolsets live under `Samples/`, not under `source/`.
- Build automation lives under `build/`; agent wrapper scripts live under `scripts/agent/`.

## Build Matrix
- Required SDK: .NET `10.0.0` via `global.json`.
- Supported Autodesk configurations: `Debug.Autodesk.2022` through `Debug.Autodesk.2027` and `Release.Autodesk.2022` through `Release.Autodesk.2027`.
- Autodesk 2022-2024 target `net48`; 2025-2026 target `net8.0-windows`; 2027 targets `net10.0-windows`.
- Focused host build: `scripts/agent/build-host.ps1 -Year 2025`.
- CI/package build: `scripts/agent/pack.ps1` or `dotnet run -c Release pack` from `build/`.
- **Kill host process before deploy builds**: Running Revit/AutoCAD locks the DLL. Before any build that deploys to the addin folder, kill the host process first: `Get-Process -Name "Revit" -ErrorAction SilentlyContinue | Stop-Process -Force`. See `.agents/skills/revit-build/SKILL.md` for details.

## Verification
- Do not guess commands. Prefer scripts in `scripts/agent/`.
- Current test projects are shallow and partly smoke/placeholder coverage. Passing tests are useful but not strong proof. Use engineering judgment to add focused tests or manual verification notes when a change touches real behavior.
- Focused compile: `scripts/agent/build-host.ps1 -Year <2022-2027> [-Mode Debug|Release]`.
- .NET tests: `scripts/agent/test-dotnet.ps1`.
- Python parser tests: `scripts/agent/test-python.ps1`.
- Frontend sample quality gate: from `Samples/PythonDemo/revit_dashboard_ui/`, run `npm run quality`.
- If a test fails due to known stale paths or missing local Pixi/Revit state, document that explicitly instead of changing unrelated code.

## Host Boundaries
- Shared execution depends on abstractions such as `IHostContextExecutor`, `ICommandDiscovery`, `ICommandRunner`, script bridges, and debugger bridges.
- Host projects provide implementations for Revit, AutoCAD, and future .NET hosts.
- New host support should add host-specific projects, adapters, and packaging rules without leaking host APIs into shared `DevTools.*` libraries.
- Revit DirectContext3D visualization belongs in the Revit host. Other hosts should use their own rendering adapters.

## Common Traps
- Do not use repo-root `.sln`; use `RevitDevTool.slnx`.
- Do not assume paths under `source/Samples/`; current samples are under `Samples/`.
- Embedded execution scripts currently live under `source/DevTools.Execution/Resources/scripts/`, but some tests still look under `source/RevitDevTool/Resources/scripts/`.
- `PythonInProcessParserTests` and parser integration tests expect `%APPDATA%\RevitDevTool\pixi-env\.pixi\envs\default`.
- Some tests still search for `RevitDevTool.sln` instead of `.slnx`.
- ILRepack is disabled for 2027 host projects because the Autodesk isolated context breaks repacked assemblies.

## Change Rules
- If editing shared libraries used by Autodesk 2022-2024, check .NET Framework compatibility and build at least one `Debug.Autodesk.2022` or `Debug.Autodesk.2024` configuration when relevant.
- Scale tests to risk. Do not add heavy test infrastructure just for appearance, but do add small meaningful tests when existing coverage only proves the code compiles or handles a happy path.
- If changing execution, MCP, pytest bridge, logging/visualization, packaging, or startup behavior, read the corresponding `docs/ai/*.md` digest and skill checklist first.
- Architecture docs are engineering source of truth for future agents. When adding an important feature or changing overall architecture, update the matching `docs/*/README.md` and/or `docs/ai/*.md` so later agents can build on the decision.
- When changing module architecture, update exactly the layer that changed: module README for deep architecture, `docs/ai/` for agent workflow, or a skill for task checklist. Do not duplicate the same rule across all layers unless all layers are affected.
- Preserve user changes. Do not revert unrelated dirty work.
