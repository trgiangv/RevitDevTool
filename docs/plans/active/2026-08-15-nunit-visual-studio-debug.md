# Execution Plan: Visual Studio host Debug via Runner attach

Date: 2026-08-15

## Status

Active

## Outcome

Visual Studio Test Explorer **Debug** hits breakpoints in host NUnit test
bodies. MTP and the in-tree VSTest adapter pass `--debug` /
`--debug-parent-pid`; Runner attaches EnvDTE to the Autodesk host PID after
the control pipe is up and before `nunit/run`.

## Context

- Product: `docs/product/nunit-host-testing.md`
- Decision: `docs/decisions/0016-nunit-native-runtime-and-mtp-first-integration.md` (decision 11 amended)
- MTP has no public `client/attachDebugger` API (testfx#5490)

## Scope

In scope:

- Runner `--debug` / `--debug-parent-pid` and `Debugging/` EnvDTE attach
- MTP `IDebugSession` flag pass-through (no Interop in the nupkg)
- In-tree VSTest `IRunContext.IsBeingDebugged` → same flags

Out of scope:

- Native AOT Runner
- Rider / C# Dev Kit attach
- Host pipe `nunit/debug-ready`
- `IFrameworkHandle2.AttachDebuggerToProcess`

## Approach

After `HostSession.EnsurePipeAsync`, `HostDebugAttachScope` calls
`IVisualStudioAttach.TryAttach` (production: `VisualStudioAttach` via ROT +
EnvDTE). `nunit/run` follows. Dispose detaches. Attach failure warns on stderr
and does not fail the run.

## Risks And Recovery

- Multiple Visual Studio instances: select the DTE whose DebuggedProcesses
  contains `--debug-parent-pid`.
- EnvDTE missing / not VS: warn and continue; manual Attach to Process remains.
- Recovery: omit `--debug`; ordinary run path is unchanged.

## Progress

- [x] Runner CLI, `Debugging/`, attach-before-run in `RunCommand`
- [x] MTP `--debug` flags when `Debugger.IsAttached`
- [x] VSTest adapter `IsBeingDebugged`
- [x] ADR / product / agent docs
- [ ] Live VS Test Explorer Debug breakpoint on `Arithmetic_runs_inside_host` (local; agent cannot drive Test Explorer)

## Decisions

- 2026-08-15: Suppress ADR 0016 separate-IDE-project placement. EnvDTE lives
  in Runner because that is where the host PID exists. MTP does not reference
  Interop.

## Validation

- Compile (pass): `dotnet build source/DevTools.NUnit.Runner/DevTools.NUnit.Runner.csproj -c Debug`; same for MTP and TestAdapter
- MTP tests (pass 24): `scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Mtp.Tests/DevTools.NUnit.Mtp.Tests.csproj`
- TestAdapter tests (pass 6): `scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.TestAdapter.Tests/DevTools.NUnit.TestAdapter.Tests.csproj`
- Runner tests: 39 pass excluding pre-existing flake `NUnitPipeClientTests.RunAsync_caller_cancellation_sends_cancel_for_active_run_id` (fails intermittently in the full suite; passes alone)
- Live: VS Test Explorer Debug on `samples/DevTools.NUnit.SampleTests` `Arithmetic_runs_inside_host` — not run from this session

## Result

Unit/compile proof is green for Runner attach, MTP flags, VSTest flags, and
architecture (no Interop in MTP). Live Test Explorer Debug remains a local
check: breakpoint in `Arithmetic_runs_inside_host` should hit inside the host.
