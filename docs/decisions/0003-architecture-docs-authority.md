# 0003 Layered Documentation Authority

Date: 2026-05-29

## Status

Accepted (updated 2026-07-23 for hybrid harness layout)

## Context

Agents and humans need durable architecture truth without duplicating every
detail in `AGENTS.md`.

## Decision

- `docs/product/` holds current behavior contracts.
- `docs/architecture/<Module>/` holds durable module design.
- `docs/agents/` holds agent workflow, traps, and verification digests.
- `docs/decisions/` holds lasting choices independent of a plan.
- Skills hold domain workflows under `.agents/skills/`; compile verify is
  hook-driven.
- Update only the matching layer when truth changes; link instead of copying.

## Consequences

Positive: progressive disclosure and continuous docs without rewriting AGENTS.

Tradeoffs: authors must choose the correct layer.
