# 0011 Hybrid Repository-Harness Docs Layout

Date: 2026-07-23

## Status

Accepted

## Context

`repository-harness` core expects a small `AGENTS.md`, `docs/WORKFLOW.md`,
`docs/product/`, `docs/plans/`, and `docs/decisions/`. RevitDevTool already had
deep module docs and agent digests that must remain continuously updated.

## Decision

Adopt a hybrid layout:

- Harness process surfaces: `AGENTS.md` (HARNESS block), `WORKFLOW.md`,
  `product/`, `plans/`, `decisions/`, `templates/`.
- Module architecture moves under `docs/architecture/<Module>/`.
- `docs/agents/` remains the domain task router and digests.
- `docs/ARCHITECTURE.md` and `docs/README.md` are thin maps, not encyclopedias.
- Completion updates only the matching documentation layer.

## Consequences

Positive: compatible with harness updates while preserving domain memory.

Tradeoffs: path churn for existing links; authors must learn the layer map.
