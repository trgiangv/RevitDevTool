# 0005 GitNexus Indexing Limitation

Date: 2026-07-21

## Status

Accepted

## Context

`npx gitnexus analyze` fails in `scopeResolution` even after ignoring vendor
`libs/` and cleaning `.gitnexus`.

## Decision

- `.gitnexusignore` excludes vendor/generated/runtime folders.
- Until analyzer failure is resolved, agents inspect source directly and do not
  rely on GitNexus graph freshness.

## Consequences

Positive: clear fallback guidance.

Tradeoffs: no graph-assisted navigation until fixed.
