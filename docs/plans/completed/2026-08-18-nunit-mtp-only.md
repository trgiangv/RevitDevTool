# Execution Plan: NUnit MTP-Only Stack

Date: 2026-08-18

## Status

Complete

## Baseline

The VSTest-compatible implementation is preserved on branch
`testing/nunit-vstest` at `e32ae590`. Work proceeds on `develop`.

## Global Constraints

- Do not launch, locate, attach to, or contact Revit/AutoCAD during discovery.
- Preserve NUnit 4.6.1 execution semantics and net48/net8/net10 host support.
- Remove `netstandard2.0`; do not remove net48.
- Keep only neutral Testing contracts across runtime/load-context boundaries.
- Do not change Polyfill, add PolySharp, or alter ILRepack flags/policy.
- Use TDD for architecture and behavior changes.
- Commit each completed, verified migration scope.

## Task 1: Remove VSTest Products And Samples

- Add a failing repository architecture test for TestAdapter projects,
  solution/build/package references, VSTest sample directories, and forbidden
  global packages.
- Delete `source/DevTools.NUnit.TestAdapter/` and
  `tests/DevTools.NUnit.TestAdapter.Tests/`.
- Delete `samples/DevTools.NUnit.VSTest.SampleTests/` and
  `samples/DevTools.NUnit.VSTest.Civil3D.SampleTests/`.
- Remove their solution/sample-solution/project/build references.
- Remove VSTest-specific docs and conflict checks.

## Task 2: Run Repository Tests Through xUnit MTP

- Remove `Microsoft.NET.Test.Sdk`, `Microsoft.TestPlatform.ObjectModel`,
  `xunit.runner.visualstudio`, `NUnit3TestAdapter`, and
  `ricaun.RevitTest.TestAdapter` from central versions and projects.
- Configure xUnit v3 test projects as MTP executables without per-project
  VSTest packages.
- Convert the net48 NUnit-based Host test project to xUnit v3/MTP while keeping
  its net48 coverage.
- Update `scripts/test-dotnet.ps1` only if MTP invocation requires it.
- Prove representative net48 and modern tests discover and run through MTP.

## Task 3: Delete Legacy NUnit Wire Compatibility

- Remove `NUnitRequestHandler`, `nunit/*` registration, legacy protocol/DTO/
  JSON/compatibility bridge, legacy pipe client, and legacy CLI mode.
- Make Runner always use the neutral `testing/*` transport.
- Remove legacy discovery activation; MTP/Runner discovery remains local.
- Update bridge-registration and CLI tests to assert only the generic surface.

## Task 4: Make NUnit Runtime Neutral-Contract Only

- Make runtime factories and handles expose `ITestingRuntimeSession` only.
- Convert Runtime event/result mapping directly to neutral contracts.
- Remove `INUnitRuntimeSession` and `DevTools.NUnit.Transport`.
- Preserve NUnit-specific internals inside Runtime/Provider without exposing
  them across the ALC/AppDomain boundary.
- Preserve cancellation, generation isolation, attachments, hierarchy,
  diagnostics, and net48 loader behavior.

## Task 5: Remove Adapter Compatibility Targets

- Remove `netstandard2.0` from Ipc, Testing Abstractions/Transport, NUnit
  Provider, and any other project where it was added for VSTest.
- Remove `NETSTANDARD` conditional branches that become dead.
- Simplify the MTP private package closure and host payload/shared policy.
- Update product/architecture docs once for the new observable ownership.

## Task 6: Verify And Commit The MTP-Only Graph

- Run architecture scans for forbidden projects, packages, protocols, target
  frameworks, source links, and payload names.
- Run focused Testing/NUnit Host/Runtime/Runner/MTP suites through MTP.
- Build touched multi-target projects and Revit/AutoCAD hosts without deploy.
- Pack `RevitDevTool.NUnit` locally and run clean-consumer net48/net8/net10
  restore/load/discovery proof.
- Inspect nupkg and host payload for no VSTest, NUnit Transport, netstandard, or
  duplicate compatibility assets.
- Run whole-change review, fix Critical/Important findings, then commit scoped
  changes on `develop`.

## Progress

- [x] Task 1: remove VSTest products and samples.
- [x] Task 2: migrate repository tests to xUnit MTP.
- [x] Task 3: delete legacy NUnit wire compatibility.
- [x] Task 4: make NUnit Runtime neutral-contract only.
- [x] Task 5: remove adapter compatibility targets.
- [x] Task 6: verify and commit the MTP-only graph.

## Result

`develop` now exposes only the neutral `testing/*` host protocol and the
`RevitDevTool.NUnit` MTP package. The VSTest/TestAdapter baseline remains on
`testing/nunit-vstest`. Repository tests execute through xUnit v3's MTP v2
runner; net48/net8/net10 product targets remain supported and netstandard is
removed from the testing stack.
