# 0008 Document Bridge And Startup Dialogs

Date: 2026-05-31

## Status

Accepted. Dialog-catalog half is **superseded by
[0018](0018-host-identity-and-out-of-process-infrastructure.md)** (per-host
specs). `IDocumentBridge` / `open_document` still stands.

## Context

Opening documents and dismissing startup dialogs needed a shared abstraction
across Revit and AutoCAD.

## Decision

- Shared `IDocumentBridge` with host implementations; in-host `open_document`
  delegates to the active host bridge.
- `StartupDialogResolver` is a generic poller. Dialog catalogs are per-host
  (`RevitStartupDialogSpec` / `AcadStartupDialogSpec`). The merged
  Autodesk keyword bag in this ADR is **superseded by
  [0018](0018-host-identity-and-out-of-process-infrastructure.md)**.

## Consequences

Positive: one tool contract for document open across hosts.

Tradeoffs: keyword lists must be curated carefully per host dialog churn.
