# 0002 Host-Agnostic Platform Direction

Date: 2026-05-29

## Status

Accepted

## Context

Treating the product as Revit-only blocked AutoCAD and future .NET hosts.

## Decision

- The project is a reusable host/dev-tool platform, not Revit-only.
- Revit and AutoCAD are current hosts.
- Shared `DevTools.*` libraries remain host-neutral unless a host API dependency
  is unavoidable.

## Consequences

Positive: shared features land once; hosts adapt at the boundary.

Tradeoffs: host-only features must be explicitly classified and documented.
