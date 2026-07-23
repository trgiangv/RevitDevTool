# 0009 Multi-Host Pytest Client

Date: 2026-05-31

## Status

Accepted

## Context

The pytest client used Revit-only CLI/INI names while the pipe protocol was
already multi-host.

## Decision

- Client options use `--host-*` / `host_*` (not `--revit-*`).
- Pipe pattern mirrors C# `InstanceManager`: `DevTools_{Host}_{Version}_{PID}`
  with version as any non-underscore string.
- Unknown host names get a fallback pipe-prefix config; hosts without exe
  discovery still connect via existing pipes.

## Consequences

Positive: one client for Revit, AutoCAD-family, and extensible hosts.

Tradeoffs: wire protocol and C# contracts must stay mirrored.
