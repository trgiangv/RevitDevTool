# 0004 Hook-First Compile Harness

Date: 2026-07-21

## Status

Accepted

## Context

Build skills created round-trips and duplicated matrix knowledge already owned
by scripts and project configuration.

## Decision

- Removed `revit-build`, `net48-compat-review`, `pyrevit-ironpython`, and thin
  `*-change/review` skills; added `platform-change`.
- `.cursor/hooks/` tracks edits and on agent stop compiles: Revit/AutoCAD API →
  Autodesk 2022/2025/2027; shared → Debug/Release.
- Deploy stays manual via `scripts/kill-host.ps1` + `scripts/build-host.ps1`.
- Client pytest path is `uv run pytest` in `RevitDevTool.PyTest` only.

## Consequences

Positive: fewer agent tokens on routine compile; consistent matrix.

Tradeoffs: agents must wait for hook feedback instead of inventing MSBuild.
