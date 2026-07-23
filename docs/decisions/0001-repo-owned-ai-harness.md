# 0001 Repo-Owned AI Harness

Date: 2026-05-29

## Status

Accepted

## Context

Coding agents need a single repository-owned instruction surface rather than
tool-specific rule adapters that drift apart.

## Decision

- `AGENTS.md` is the entry contract and router.
- `docs/agents/` contains deterministic agent digests.
- `.agents/skills/` holds domain workflows only. Routine compile is
  `.cursor/hooks/` (stop verify).
- Tool-specific files should be thin adapters that point back to the repo-owned
  harness.

## Consequences

Positive: one authority for agents across tools.

Tradeoffs: harness docs must stay current when workflow changes.
