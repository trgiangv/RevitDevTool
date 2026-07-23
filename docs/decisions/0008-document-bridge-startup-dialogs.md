# 0008 Document Bridge And Startup Dialogs

Date: 2026-05-31

## Status

Accepted

## Context

Opening documents and dismissing startup dialogs needed a shared abstraction
across Revit and AutoCAD.

## Decision

- Shared `IDocumentBridge` with host implementations; in-host `open_document`
  delegates to the active host bridge.
- `StartupDialogResolver` uses merged Revit + AutoCAD keywords in default
  options rather than host-specific option branching.

## Consequences

Positive: one tool contract for document open across hosts.

Tradeoffs: keyword lists must be curated carefully per host dialog churn.
