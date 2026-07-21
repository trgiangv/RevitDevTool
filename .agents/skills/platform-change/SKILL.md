---
name: platform-change
description: >
  Short checklist for platform changes across execution, MCP/IPC, pytest bridge,
  host boundaries, logging/visualization, or packaging. Use when editing
  source/DevTools.*, host projects, or build/ packaging — not for routine compile
  (hooks handle that). For other domains, use the matching skill under
  `.agents/skills/`.
---

# Platform Change

## Before editing

1. Read the matching digest only: `docs/agents/execution-system.md`, `mcp-pytest-bridge.md`, `host-boundaries.md`, `build-matrix.md`, or module `docs/*/README.md`.
2. Classify shared vs host-specific before moving code.

## Rules

- Shared `DevTools.*`: no Revit/AutoCAD API types; host adapters own threading, docs, rendering.
- MCP/IPC/pytest bridge: keep wire contracts in sync (C# + Python client when shape changes).
- Pytest bridge: treat discover and run as separate flows; do not “fix” stale path gaps with unrelated edits.
- Packaging: `build/Modules/*` + `RevitDevTool.slnx` are source of truth; ILRepack stays off for 2027 hosts.
- net48: rely on Polyfill + hook/multi-TFM compile; do not speculative-polyfill.

## After editing

- Let the stop-hook compile; fix reported errors.
- Deploy only with `scripts/kill-host.ps1` + `scripts/build-host.ps1`.
- Add a focused test when contracts/discovery/dispatch change; otherwise document host blockers.
- Update the one doc layer future agents will read (module README **or** `docs/agents/` — not both by default).
