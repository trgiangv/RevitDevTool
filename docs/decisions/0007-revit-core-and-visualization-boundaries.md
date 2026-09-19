# 0007 Revit.Core And Visualization Boundaries

Date: 2026-05-31

## Status

Accepted

## Context

Shared-platform boundaries were unclear for Revit helpers and DirectContext3D.

## Decision

- `RevitDevTool.Core` is Revit-only (transactions, dockable panes, image export);
  only the Revit host references it.
- Visualization lives entirely in `source/RevitDevTool/Visualization/`, not in
  shared libraries.
- Select / search APIs, Element Finder, and Command Browser live in
  `RevitDevTool.Tools` ([0036](0036-revit-monitor-link-element-tokens.md)),
  not Core.

## Consequences

Positive: prevents accidental Revit API leakage into shared code.

Tradeoffs: visualization features are unavailable on non-Revit hosts.
