# 0004 Hook-First Compile Harness

Date: 2026-07-21

## Status

Superseded (2026-08-03) — see build skill below.

## Context

Build skills created round-trips and duplicated matrix knowledge already owned
by scripts and project configuration. Cursor stop hooks were added to automate
compile on agent stop.

## Decision (historical)

- Removed thin `*-change/review` skills; added `platform-change`.
- `.cursor/hooks/` tracked edits and compiled on agent stop.
- Deploy stayed manual via `scripts/kill-host.ps1` + `scripts/build-host.ps1`.

## Superseded by

- **2026-08-03:** Removed `.cursor/hooks/` — agents compile explicitly via
  `.agents/skills/build/SKILL.md` (symptom → command table, no hidden stop hook).
- Routine verify is intentional `dotnet build` on touched csproj, not deferred to IDE lifecycle.

## Consequences (still valid)

- Deploy stays manual via scripts.
- In-repo tests via `scripts/test-dotnet.ps1` / `scripts/test-python.ps1`; see `known-test-gaps.md`.
