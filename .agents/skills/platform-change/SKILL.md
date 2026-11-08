---
name: platform-change
description: >
  Checklist for changes to the shared platform: execution, MCP and IPC, the pytest
  bridge, host boundaries, logging, or packaging. Use when editing source/DevTools.*,
  host projects, or build/ packaging.
---

# Platform Change

## Before editing

1. Pick the work shape from `docs/WORKFLOW.md`, then read the one matching digest:
   `docs/agents/execution-system.md`, `mcp-pytest-bridge.md`, `host-boundaries.md`,
   `build-matrix.md`, `docs/product/<domain>.md`, or `docs/architecture/<Module>/README.md`
   (host testing structure and release: `docs/architecture/Testing/README.md`).
   Before adding tests, read `docs/agents/test-matrix.md` Current gaps.
2. Decide whether the code is shared or host-specific, and which release artifact
   ships it (installer or TestAdapter nupkg), before moving code or editing pack targets.

## Rules

- Shared `DevTools.*` uses no Revit or AutoCAD API types; host adapters own threading, documents, and rendering.
- Logging: `StartupTrace` is the pre-DI crash dump (`crash_{app}_{ver}_{pid}.log`) and
  `FileLogProcessor` writes rolling `log_*`. Keep the prefixes separate; AutoClean
  deletes only `log_*` so crash dumps survive.
- MCP and IPC: change wire contracts on both sides together (`BridgeMessage`, `PytestContracts`, MCP pipe names).
- Pytest bridge: discover and run are separate flows; fix each in its own path.
- Packaging is one tool for four products (ADR 0059):
  `dotnet run --project build -- pack <daemon|addin|nettest>`. Each product declares its
  roots, version file, and tag in `build/Products/Product.cs`; `.github/product-paths.yml`
  is generated from it (`build -- paths`). The adapter nupkg is `pack nettest`
  (`PackTestAdapterModule`, no compile dependency, no PowerShell). ILRepack and Polyfill
  policy: `docs/decisions/0019-ilrepack-and-polyfill-isolated-alc.md`.
- TestAdapter pack and release constraints: `docs/architecture/Testing/README.md`. Keep the
  TestAdapter free of `ProjectReference`s to the MTP projects. Host, Runtime, and Runner
  ship in the installer, not the nupkg.
- net48 relies on Polyfill and multi-TFM compile; add a polyfill only when a compile
  fails. `PolyArgumentExceptions` is on globally, so use `ArgumentNullException.ThrowIfNull`.

## After editing

- Compile the touched projects with the build skill and fix what it reports.
- To deploy, stop only the host year under test: `scripts/build-host.ps1 -HostApp Revit -Year 2025`
  stops that year, then builds.
- Packs: `dotnet run --project build -- pack nettest` (adapter), `pack addin` (add-in bundle), `pack daemon` (Daemon installer).
- Add a focused test when contracts, discovery, or dispatch change; otherwise name the host blocker.
- Update the one matching doc layer (`docs/product/`, `docs/architecture/<Module>/`,
  `docs/agents/`, or `docs/decisions/`). Testing pack and release structure lives in
  `docs/architecture/Testing/`.
