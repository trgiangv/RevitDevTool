# Execution Plan: Close Known Test Gaps

Date: 2026-09-04

## Status

Completed

## Outcome

Gaps in `docs/agents/test-matrix.md` that can be proven without a live
Revit/AutoCAD process have focused tests (or a product fix that makes an
existing test independent). Live-host E2E stays documented, not faked.

## Context

- Gaps: `docs/agents/test-matrix.md`
- ADR 0017: `docs/decisions/0017-nunit-host-test-output-routing.md`
- Pytest digest: `docs/agents/mcp-pytest-bridge.md`

## Scope

In scope (headless / in-process):

- NUnit Trace/Debug capture suite-order (`TestingRunTraceScope` + spike/runtime)
- Pytest bridge: parse, handler routing, pipe accept/round-trip, progress vs batch
- Execution: dispatcher, open_document, orchestrator load, C# directive graph
- `HostUiHelper.RunOnMainThread` runs inline when dispatcher is null so
  `McpConnectState` is testable without UI
- Built-in MCP registry catalog
- File watcher debounce; `execute_csharp_code` empty/compile-fail;
  `execute_python_code` empty; packed-host layout Skip unless ILRepack

Out of scope (remain documented):

- Live host `execute_*` / daemon → deployed host → Revit API
- ILRepack year matrix / daemon hot-reload
- Pixi AppData cold wipe
- Cross-process pytest client (`RevitDevTool.PyTest` repo)

## Approach

Composer 2.5 agents with non-overlapping files. Parent updated
`test-matrix.md` after proof.

## Risks And Recovery

- `HostUiHelper` inline fallback: host still initializes dispatcher first;
  only pre-UI / xUnit paths change. `ArgumentNullException` uses classic throw
  for net48 (`ThrowIfNull` missing).
- Pipe tests: unique `Guid` pipe names; always Stop/Dispose.
- Do not add pythonnet to Core/Adapter/Server tests.

## Progress

- [x] NUnit Trace capture (`TestingRunTraceScope` restore + Runtime e2e markers)
- [x] Pytest bridge unit tests (parse, handlers, pipe round-trip)
- [x] Execution dispatcher/orchestrator/open_document + HostUiHelper inline
- [x] Catalog built-in registry
- [x] File watcher + execute_* empty/compile-fail; packed-host layout Skip
- [x] Update test-matrix.md from evidence

## Validation

| Project | Result |
|---------|--------|
| `DevTools.UI` Debug (net10/net8/net48) | compile succeeded |
| `DevTools.Testing.Abstractions.Tests` | 27 passed |
| `DevTools.NUnit.Runtime.Tests` | 36 passed |
| `DevTools.NUnit.Host.Tests` | 30 passed (packed layout Skip-or-pass) |
| `DevTools.Execution.Tests` | 151 total — 130 passed, 21 skipped (opt-in pixi/pip) |
| Catalog registry filter | 10 passed, 1 skipped (sample DLL) |
