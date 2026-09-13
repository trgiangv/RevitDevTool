# 0034 Execution Tests Are Scoped MSTest.Sdk Projects With First-Party Coverage

Date: 2026-09-13

## Status

Accepted

## Context

`tests/DevTools.Execution.Tests` is a god project: ~90 files covering CPython
(pythonnet), IronPython, C# / F# compilers, pytest/IPy pipe, MCP backends, and
orchestrator/package services in one MTP executable.

That packing causes three lasting problems:

1. **Process-global runtimes share one testhost.** pythonnet allows one engine
   per process. Pixi/pip init, uninitialized Skip facts, and NuGet restore all
   serialize through xUnit collections (`PythonRuntimeCollection`,
   `NugetRestoreCollection`). A compiler or pipe failure waits behind a pixi
   download in the same process.
2. **Coverlet cannot measure Execution reliably.** `coverlet.MTP` rewrites
   DLLs in that project's `bin/`. A second testhost on the same output fails
   immediately (`MSB3027` / instrumentation failed). The last owned Coverlet
   merge omitted Execution for that reason (`docs/agents/test-matrix.md`).
3. **xUnit v3 + Coverlet is not the MSTest.Sdk path this repo already uses.**
   `global.json` pins `MSTest.Sdk` **4.4.0**. `tests/DevTools.TestRunner.Tests`
   already runs on that SDK. TestRunner disables Microsoft Code Coverage
   (`TestingExtensionsProfile=None`) so it can stay on the repo-wide
   `coverlet.MTP` feed. Execution is the module that needs a collector that
   does not lock the testhost `bin/`.

0022 keeps repository tests on Microsoft Testing Platform. It does not require
xUnit. Remaining `tests/*.Tests` stay xUnit v3 until a later decision.

## Decision

Replace `tests/DevTools.Execution.Tests` with scoped test projects that match
`source/DevTools.Execution/` areas. Those projects use **MSTest.Sdk 4.4.0**
(already in `global.json`) and **Microsoft.Testing.Extensions.CodeCoverage**
from the SDK **Default** profile. They do not use xUnit or `coverlet.MTP`.

### Project map

| Project | Owns |
|---------|------|
| `DevTools.Execution.Tests.Shared` | Helper library, not a testhost. `IsTestProject=false`. |
| `DevTools.Execution.CSharp.Tests` | C# compiler, directives, cache, CSharp tools, assembly isolation |
| `DevTools.Execution.FSharp.Tests` | F# graph, cache, NuGet, executor |
| `DevTools.Execution.Python.Tests` | CPython providers (pixi / pip / uv), pythonnet init |
| `DevTools.Execution.IronPython.Tests` | DLR runner, pydevd, IPy `sys.__pytest_running__` |
| `DevTools.Execution.Pytest.Tests` | `DevTools_*` pipe, pytest/IPy handlers, contracts, `HostPipeName` |
| `DevTools.Execution.Mcp.Tests` | Primitive dispatcher, Dotnet/BuiltIn backends, connection tracker |
| `DevTools.Execution.Services.Tests` | Orchestrator, watcher, tree, packages, `HostUiHelper` |

Python MCP backends (`PythonMcpToolBackend*`, `PythonMcpRegistryProvider`,
`PythonCodeTool`) live in **Python.Tests** because they initialize pythonnet.
C# / OpenDocument built-in tools stay in CSharp.Tests / Mcp.Tests.

### Framework

- SDK: `Sdk="MSTest.Sdk"` (version from `global.json` `msbuild-sdks`).
- Style: same as `tests/DevTools.TestRunner.Tests` — `[TestClass]`,
  `[TestMethod]`, `[DataRow]`, `TestContext` instance property.
- Runtime skip: `Assert.Inconclusive(reason)` (optional artifact / already
  initialized). Do not `Assert.Fail` when pixi, pydevd, or a dispatcher is
  absent.
- Do not enable xUnit collections. Isolate pythonnet by **project**
  (Python.Tests and Pytest.Tests may each init in their own process). Put
  `[assembly: DoNotParallelize]` on Python.Tests. MSTest default is already
  non-parallel; do not add `[assembly: Parallelize]` on Python.Tests.

### Coverage

Execution scoped projects set `UseMicrosoftCodeCoverage=true`. That:

- selects MSTest.Sdk **Default** profile (Code Coverage + TRX packages);
- sets `EnableMicrosoftTestingExtensionsCodeCoverage=true`;
- **does not** reference `coverlet.MTP`.

Collect with `--coverage`, not `--coverlet`. Shared settings:
`tests/mstest-coverage.xml` (include `DevTools.*`, exclude `*.Tests` and
vendored UI assemblies). Default output format for merges is **cobertura**
(`--coverage-output-format cobertura`).

This is **managed** Microsoft Code Coverage. Do **not** set
`EnableStaticNativeInstrumentation` / `EnableDynamicNativeInstrumentation`
(unmanaged/C++ instrumentation). Do **not** add Coverlet next to it.

Superseded for remaining testhosts by [0035](0035-mstest-sdk-repo-tests.md):
the whole `tests/` tree uses MSTest.Sdk + `--coverage`. TestRunner.Tests now
opts into the same collector.

### InternalsVisibleTo

`source/DevTools.Execution/Properties/AssemblyInfo.cs` lists every new test
assembly plus `DevTools.Execution.Tests.Shared`.

## Alternatives Considered

1. **Keep one xUnit project, split folders only.** Same testhost, same
   pythonnet and Coverlet lock. Rejected.
2. **Split projects but keep xUnit + Coverlet.** Fixes process isolation.
   Does not fix Coverlet `bin/` lock on the Python/Pytest testhosts, and
   ignores the already-pinned MSTest.Sdk.
3. **Convert every `tests/*.Tests` project to MSTest.Sdk now.** Out of scope.
   MCP / Daemon / Hosting stay xUnit until a separate decision.
4. **Microsoft coverage on TestRunner.Tests as well.** TestRunner already
   opted out so Coverlet remains the single collector there. Do not mix both
   collectors in one testhost.

## Consequences

Positive:

- pythonnet, IronPython, compilers, and pipe tests run in separate processes.
- Execution coverage uses the SDK collector (`--coverage`) and can join the
  HTML merge without a second Coverlet testhost on the same `bin/`.
- Agents can compile and run one scope without downloading pixi.

Tradeoffs:

- Two in-repo unit-test frameworks until other modules migrate (closed by
  [0035](0035-mstest-sdk-repo-tests.md)). Commands stay MTP (`dotnet run --project`).
- Mixed “coverage boost” files must be split by method, not moved whole.
- `Assert.*` APIs differ (xUnit `Equal`/`Skip` vs MSTest `AreEqual` /
  `Inconclusive`). Conversion follows TestRunner.Tests.

## Follow-Up

- Implemented: `docs/plans/completed/2026-09-13-execution-test-project-split.md`.
- Remaining `tests/*.Tests` migrated in [0035](0035-mstest-sdk-repo-tests.md).
