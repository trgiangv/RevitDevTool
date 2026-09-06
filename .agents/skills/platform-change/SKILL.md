---
name: platform-change
description: >
  Short checklist for platform changes across execution, MCP/IPC, pytest bridge,
  host boundaries, logging/visualization, or packaging. Use when editing
  source/DevTools.*, host projects, or build/ packaging. For compile/deploy proof,
  use `.agents/skills/build/SKILL.md`. For other domains, use the matching skill
  under `.agents/skills/`.
---

# Platform Change

## Before editing

1. Read `docs/WORKFLOW.md` for work shape, then the matching digest only: `docs/agents/execution-system.md`, `mcp-pytest-bridge.md`, `host-boundaries.md`, `build-matrix.md`, **`test-matrix.md` (Current gaps — do not add tests that fight pythonnet / dispatcher / Coverlet testhost lock)**, `docs/product/<domain>.md`, or `docs/architecture/<Module>/README.md`. Host testing structure/release: `docs/architecture/Testing/README.md`.
2. Classify shared vs host-specific before moving code. Classify **which release artifact** the change ships in (installer vs TestAdapter nupkg) before editing pack targets.

## Rules

- Shared `DevTools.*`: no Revit/AutoCAD API types; host adapters own threading, docs, rendering.
- Logging: `StartupTrace` is pre-DI crash dump (`crash_{app}_{ver}_{pid}.log`); `FileLogProcessor` is rolling `log_*`. Do not mix prefixes. AutoClean must not delete `crash_*`.
- MCP/IPC: keep wire contracts in sync (`BridgeMessage`, `PytestContracts`, MCP pipe names).
- Pytest bridge: treat discover and run as separate flows; do not “fix” stale path gaps with unrelated edits.
- Packaging is **two pipelines**. Installer/bundle (`scripts/pack.ps1`, `build/Modules/*`, `PublishRelease.yml`) does not publish NuGet. Adapter nupkg (`scripts/pack-test-adapter.ps1`, `PublishTestAdapter.yml`) does not go through `build/Modules/*`. ILRepack + Polyfill policy is `docs/decisions/0019-ilrepack-and-polyfill-isolated-alc.md`.
- TestAdapter pack/release constraints: `docs/architecture/Testing/README.md`. Do not add a TestAdapter → MTP `ProjectReference`. Host/Runtime/Runner ship in the installer, not the nupkg.
- net48: rely on Polyfill + hook/multi-TFM compile; do not speculative-polyfill.
  `PolyArgumentExceptions` is on globally — prefer `ArgumentNullException.ThrowIfNull`
  over `if is null throw`.

## After editing

- Compile touched projects via build skill; fix reported errors before done.
- Before deploy, stop only the exact host year being tested, for example
  `scripts/kill-host.ps1 -HostApp Revit -Year 2025`, then run `scripts/build-host.ps1`.
  Never stop all Revit versions.
- Adapter pack/publish: `scripts/pack-test-adapter.ps1` (not `scripts/pack.ps1`). Installer/bundle: `scripts/pack.ps1`.
- Add a focused test when contracts/discovery/dispatch change; otherwise document host blockers.
- Update the one matching doc layer (`docs/product/`, `docs/architecture/<Module>/`, `docs/agents/`, or `docs/decisions/`) — not multiple layers by default. Testing pack/release structure lives in `docs/architecture/Testing/`.
