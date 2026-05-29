# Execution System Change

Use when editing execution providers, strategies, node models, package handling, file watching, or host execution adapters.

## Checklist

- Read `docs/ai/execution-system.md` and `docs/Execution/README.md`.
- Classify the change as provider, strategy, orchestrator, watcher, package service, or host adapter.
- Keep host-thread and host-API behavior in host adapters.
- Preserve script discovery rules for `*script.py`, `*script.fsx`, `*script.csx`, and `_ipy_script.py`.
- Check Python/Pixi, F#/NuGet, and C#/Roslyn side effects when touching shared script code.
- Existing tests may only cover happy paths. Add a focused unit/contract test for discovery rules, node merging, package parsing, or strategy selection when those behaviors change.
- Update `docs/Execution/README.md` or `docs/ai/execution-system.md` when changing execution architecture, modes, provider/strategy responsibilities, or host-adapter boundaries.
- Run `scripts/agent/build-host.ps1 -Year 2025` or the most relevant target year.
