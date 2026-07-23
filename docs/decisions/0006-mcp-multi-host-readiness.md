# 0006 MCP Multi-Host Readiness

Date: 2026-05-31

## Status

Accepted

## Context

MCP tooling was evolving from Revit-centric naming to a multi-host platform.

## Decision

- Host discovery is generic via `InstanceManager` and host pipes.
- Standalone/daemon tools and in-host runtime default to sharable behavior.
- Every new MCP feature should be sharable by default unless host API forces
  otherwise.

## Consequences

Positive: AutoCAD and future hosts can reuse the same MCP path.

Tradeoffs: host-specific toolsets may still be missing for some hosts.
