# Execution Plan: Visual Studio host Debug via Runner attach

Date: 2026-08-15

## Status

Complete (2026-08-27). Executable is `DevTools.TestRunner`. Policy:
[0025](../../decisions/0025-runner-owned-visual-studio-host-attach.md).

## Outcome

Visual Studio Test Explorer **Debug** hits breakpoints in host test bodies.
MTP passes `--debug` / `--debug-parent-pid`; Runner attaches EnvDTE to the
Autodesk host PID after the control pipe is up and before `testing/run`.
Cancel during host boot kills only a host this run spawned.

## Context

- Product: `docs/product/host-testing.md`
- Decision: 0016 decision 11, amended by 0025
- MTP has no public `client/attachDebugger` API (testfx#5490)

## Scope (as executed)

In scope:

- Runner `--debug` / `--debug-parent-pid` and `Debugging/` EnvDTE attach
- MTP `IDebugSession` flag pass-through (no Interop in the nupkg)
- Spawned-host cancel on testhost exit (`DebugHostLifetime`,
  `HostLaunchWaiter.TerminateIfIncomplete`)
- Collapse `RunTestingAsync` into CLI `ExecuteAsync` + `TestPipeClient`

Out of scope (not product follow-up):

- Native AOT Runner
- Runner attach for other IDEs
- Host pipe `debug-ready`
- `IFrameworkHandle2.AttachDebuggerToProcess`

## Result

- Live VS Test Explorer Debug: Revit 2025, `DevTools.TUnit.SampleTests`
  `Named_basis_length_is_one` — host attached, symbols loaded, test body ran.
  Output “Revit.exe has exited with code 0” is EnvDTE **Detach**.
- Coordinator is session-only (`ExecuteAsync`). CLI owns `testing/run`.
- Policy tests: `DevTools.TestRunner.Core.Tests` 8 passed;
  `DevTools.TestRunner.Tests` 12 passed via
  `dotnet run --project tests/…/*.csproj -c Debug`.
