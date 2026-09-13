# Execution Plan: Split Execution God Test Project To MSTest.Sdk

Date: 2026-09-13

## Status

Completed 2026-09-13

## Outcome

`tests/DevTools.Execution.Tests` is replaced by scoped MSTest.Sdk 4.4.0
projects. Each project compiles and `dotnet run`s independently. Execution line
coverage is collected with `--coverage` (Microsoft.Testing.Extensions.CodeCoverage),
not `--coverlet`. Optional pixi / pydevd / dispatcher fixtures Skip
(`Assert.Inconclusive`), they do not Fail.

## Context

- Decision: `docs/decisions/0034-execution-mstest-sdk-scoped-tests.md`
- Architecture: `docs/architecture/Execution/README.md`
- Gaps: `docs/agents/test-matrix.md` (Execution Coverlet lock)
- In-repo MSTest style: `tests/DevTools.TestRunner.Tests`
- SDK pin: `global.json` → `MSTest.Sdk` 4.4.0
- Prior split pattern: `docs/plans/active/2026-09-04-mcp-test-project-split.md`

## Scope

In scope:

- Scaffold Shared + seven MSTest.Sdk test projects, slnx, InternalsVisibleTo
- Convert xUnit facts to MSTest (no xUnit package on new projects)
- Split mixed coverage-boost files by owning scope
- First-party coverage XML + Directory.Build.props opt-out of Coverlet
- Delete empty god project
- Update `docs/agents/test-matrix.md` + `verification.md` + build skill (one layer)

Out of scope:

- Converting other xUnit test projects
- Enabling Microsoft coverage on `DevTools.TestRunner.Tests`
- Live host E2E (`mcp-integration-test.md`)
- Raising Coverlet % on remaining modules
- Unmanaged native instrumentation flags

## Approach

1. Parent writes 0034, this plan, props, coverage XML, Shared helpers (public),
   empty csproj + slnx + InternalsVisibleTo.
2. Composer 2.5 agents copy **non-overlapping** source files into their project,
   convert xUnit → MSTest, compile + `dotnet run` that project. **Do not
   delete** files under `tests/DevTools.Execution.Tests/` (parent deletes last).
3. One mixed-file agent extracts methods from the four coverage-boost files
   into **new** files in the owning projects (no overlap with other agents’
   filenames).
4. Parent deletes the god project, updates agent docs, runs each new project.

## Target map

### `DevTools.Execution.Tests.Shared` (library)

- `ExecutionTestHelpers.cs` — public helpers. Namespace stays
  `DevTools.Execution.Tests`.

### `DevTools.Execution.CSharp.Tests`

- `CSharpDirectiveParserTests.cs`
- `CSharpCompilerTests.cs`
- `CompileScriptSymbolsTests.cs`
- `CSharpCodeToolTests.cs`
- `CSharpCodeToolSuccessTests.cs`
- `CSharpExecutionStrategyTests.cs`
- `CSharpCompilationCacheTests.cs`
- `AssemblyIsolation/ScriptAssemblyIsolationTests.cs`
- `AssemblyIsolation/CommandAssemblyIsolationTests.cs`
- `AssemblyExecutionStrategyTests.cs`
- `AssemblyExecutionProviderTests.cs`

Keep isolation fixture ProjectReferences (`IsolationEntry`,
`IsolationSibling`, `PrivateSystemNamedDependency`) with
`ReferenceOutputAssembly=false`.

### `DevTools.Execution.FSharp.Tests`

- `FSharpDependencyResolverExtendedTests.cs`
- `FSharpExecutionTests.cs`
- `FSharpCompilationCacheTests.cs`
- `FSharpNugetManagerTests.cs`

Do not copy `NugetRestoreCollection.cs`. Assembly already has
`[assembly: DoNotParallelize]`.

### `DevTools.Execution.Python.Tests`

- `PythonDebuggerTests.cs`
- `PythonNativeEnvironmentTests.cs`
- `PipEnvironmentTests.cs`
- `PythonMcpToolBackendInvokeTests.cs`
- `PythonJsonSerializerTests.cs`
- `PythonInitializerTests.cs`
- `PythonMcpRegistryProviderTests.cs`
- `PipEnvironmentProviderTests.cs`
- `UvEnvironmentProviderExtendedTests.cs`
- `PythonDepsManagerHeadlessTests.cs`
- `PixiEnvironmentSmokeTests.cs` (drop `[CollectionDefinition]`; keep tests)
- `PythonExecutionStrategyTests.cs`
- `PythonPackageStoresTests.cs`
- `PixiPackageStoreParseTests.cs`
- `PythonCodeToolTests.cs`
- `PythonMcpToolBackendResultTests.cs`
- `PyEnvironmentProviderStdlibTests.cs`
- `PipCengineTests.cs`
- `UvHostCaptureTests.cs`
- `UvArgsTests.cs`
- `UvEnvironmentProviderTests.cs`
- `PixiArgsTests.cs`
- `PixiEnvironmentProviderTests.cs`
- `Fixtures/pep723_sample.py`, `Fixtures/pep723_no_deps.py` (already copied)

### `DevTools.Execution.IronPython.Tests`

- `IronPythonDebuggerTests.cs`
- `IronPythonRunnerExtendedTests.cs`
- `IronPythonPytestRunningTests.cs`
- `IronPythonExecutionTests.cs`
- `PydevdInstallerTests.cs`

### `DevTools.Execution.Pytest.Tests`

- `IpyTestExecutionServiceTests.cs`
- `DevToolsPipeServerDisconnectTests.cs`
- `PytestRequestHandlerPrepareTests.cs`
- `PytestRequestHandlerRunTests.cs`
- `PytestRunnerScriptTests.cs`
- `PytestDependencyServiceTests.cs`
- `PytestExecutionServiceTests.cs`
- `PytestRunRequestParseTests.cs`
- `IpyTestPathTests.cs`
- `HostPipeNameTests.cs`
- `PytestRequestHandlerTests.cs`
- `InstanceRequestHandlerTests.cs`
- `DevToolsPipeServerTests.cs`
- `IpyTestRequestHandlerTests.cs`
- `PytestPathResolverTests.cs`
- `PytestBridgeFramingTests.cs`
- `IpyTestDriverIoPathsTests.cs`
- `PytestPep723BoundaryTests.cs`
- `PytestCaseResultStdoutTests.cs`
- `BridgeHandlerRegistrationTests.cs`
- `ScriptExecutionStrategyFactoryTests.cs`
- `ScriptExecutionProviderTests.cs`
- `ScriptExecutionProviderExtendedTests.cs`

Pytest.Tests may initialize pythonnet in **this** process. That is allowed.
Do not put uninitialized-engine facts here.

### `DevTools.Execution.Mcp.Tests`

- `DotnetMcpToolBackendInvokeTests.cs`
- `DotnetMcpToolBackendTests.cs`
- `BuiltInMcpToolBackendTests.cs`
- `McpPrimitiveDispatcherTests.cs`
- `OpenDocumentToolTests.cs`
- `McpPipeConnectionTrackerTests.cs`

### `DevTools.Execution.Services.Tests`

- `HostUiHelperTests.cs`
- `NugetPackageStoreTests.cs`
- `TreeNodeOperationsTests.cs`
- `FileWatcherServiceExtendedTests.cs`
- `PackageServiceTests.cs`
- `ExecutionOrchestratorExtendedTests.cs`
- `ExecutionOrchestratorFileChangeTests.cs`
- `TreeStateManagerTests.cs`
- `NetworkServiceTests.cs`
- `FileWatcherServiceTests.cs`
- `ExecutionOrchestratorTests.cs`

### Mixed files (dedicated agent — new filenames only)

Source (do not edit in place except to leave a stub comment if needed; prefer
extract-into-new-file only):

- `ExecutionCoverageFinalTests.cs`
- `ExecutionCoverageBoostTests.cs`
- `ExecutionCoverageStabilizerTests.cs`
- `AdditionalCoverageTests.cs`

| Methods (prefix) | Destination new file |
|------------------|----------------------|
| `CSharpDirectiveParser_*`, `CSharpCompiler_*` | `CSharp.Tests/CSharpCoverageTests.cs` |
| `FSharpCompilationCache_*`, `FSharpExecutionStrategy_*` | `FSharp.Tests/FSharpCoverageTests.cs` |
| `PythonEmbedded_*`, `PythonDepsManager_*`, `PipEnvironmentProvider_*`, `Pixi*`, `PipAndUv*`, `PythonExecutionStrategy_*`, `PythonCodeTool_*`, `ResolveDependencies*`, `InstallDependencies*`, `RefreshImportCache_*` | `Python.Tests/PythonCoverageTests.cs` |
| `DevToolsPipeServer_StartAsync_*` | `Pytest.Tests/PipeServerCoverageTests.cs` |
| `DotnetMcpToolBackend_*`, `BuiltInMcpToolBackend_*`, `NullDocumentBridge_*`, `McpConnectState_*`, `McpExecutionTracker_*` | `Mcp.Tests/McpCoverageTests.cs` |
| `PackageVersionChecker_*`, `PackageTreeNodes_*`, `PackageService_*`, `NugetManager_*`, `NetworkService_*`, `ExecutionGuardContext_*` | `Services.Tests/ServicesCoverageTests.cs` |

## MSTest conversion rules

Mirror `tests/DevTools.TestRunner.Tests`.

| xUnit | MSTest |
|-------|--------|
| implicit test class | `[TestClass]` |
| `[Fact]` | `[TestMethod]` |
| `[Theory]` + `[InlineData]` | `[TestMethod]` + `[DataRow]` |
| `[MemberData]` | `[DynamicData]` |
| `[Collection(...)]` | delete; Python/FSharp already `DoNotParallelize` |
| `IAsyncLifetime.InitializeAsync` | `[TestInitialize] public async Task ...` |
| `IDisposable.Dispose` / `DisposeAsync` | `[TestCleanup]` |
| `TestContext.Current.CancellationToken` | instance `public TestContext TestContext { get; set; }` then `TestContext.CancellationToken` (see `RunnerTests`) |
| `Assert.Equal` | `Assert.AreEqual` |
| `Assert.NotEqual` | `Assert.AreNotEqual` |
| `Assert.True` / `False` | `Assert.IsTrue` / `IsFalse` |
| `Assert.Null` / `NotNull` | `Assert.IsNull` / `IsNotNull` |
| `Assert.Empty` | `Assert.IsEmpty` (MSTest 4) or `Assert.AreEqual(0, …Count)` |
| `Assert.Skip(reason)` | `Assert.Inconclusive(reason)` |
| `Assert.Throws<T>` | `Assert.ThrowsExactly<T>` |
| `Assert.Contains` (string) | `Assert.Contains` |
| `Assert.Contains` (collection) | `CollectionAssert.Contains` or `Assert.Contains` predicate overload |
| `using Xunit;` | remove; csproj already has `Using` MSTest |

Keep `namespace DevTools.Execution.Tests`. Do not rename types.

`UseMicrosoftCodeCoverage` is already set on the csproj. Do not add
`xunit.v3` or `coverlet.MTP`.

## Independence rules

- Missing pixi / embed CPython / pydevd extract / host dispatcher →
  `Assert.Inconclusive` with the same hint the xUnit test used.
- Unique pipe names (`Guid`). No shared mutable catalog.
- Do not `PythonEngine.Shutdown`. Uninitialized facts Skip if
  `PythonEngine.IsInitialized`.
- Do not clear `HostUiHelper.HostDispatcher`.

## Risks And Recovery

- InternalsVisibleTo mismatch → CS0122. Parent owns AssemblyInfo.
- Coverlet + Microsoft coverage in one testhost → CS/load conflict. Props
  skip `coverlet.MTP` when `UseMicrosoftCodeCoverage=true`.
- Parallel agents editing `RevitDevTool.slnx` / `Directory.Build.props` /
  `test-matrix.md` → **forbidden**. Parent owns those.
- Parallel `git mv` of the god folder → **forbidden**. Copy into the new
  folder; leave the source in place.
- Rollback: delete new `tests/DevTools.Execution.*.Tests*` folders; restore
  slnx / AssemblyInfo / props from git; god project remains until deleted.

## Progress

- [x] Inventory and target map
- [x] Decision 0034
- [x] Scaffold csproj / Shared / slnx / InternalsVisibleTo / coverage XML / props
- [x] Composer 2.5: convert scoped files (7 agents, spawned 2026-09-13)
  - [x] CSharp.Tests — 44 passed
  - [x] FSharp.Tests — 21 passed (18 converted + 3 `FSharpCoverageTests`)
  - [x] IronPython.Tests — 20 passed
  - [x] Pytest.Tests — 84 passed
  - [x] Mcp.Tests — 25 passed
  - [x] Services.Tests — 72 passed (plus restored `ExecutionGuardContextTests`)
  - [x] Python.Tests — 128 passed, 2 inconclusive (uninitialized engine)
- [x] Composer 2.5: split mixed coverage files — 52 methods into 6 new files
- [x] Delete `tests/DevTools.Execution.Tests`
- [x] Update `docs/agents/test-matrix.md` + `verification.md` + build skill
- [x] Compile and run each new project (agent evidence + Services re-run 78 passed)

## Decisions

- 2026-09-13: Execution tests use MSTest.Sdk Default coverage (`--coverage`),
  not Coverlet. Recorded in 0034.
- 2026-09-13: Pytest.Tests may init pythonnet; Python uninitialized facts
  stay in Python.Tests only.
- 2026-09-13: Script discovery provider tests live in Pytest.Tests (execution
  tree / script roots), not Services.

## Validation

- Focused proof (2026-09-13):
  - CSharp.Tests 44 passed
  - FSharp.Tests 21 passed
  - IronPython.Tests 20 passed
  - Pytest.Tests 84 passed
  - Mcp.Tests 25 passed (then `McpCoverageTests` usings fixed)
  - Services.Tests **78 passed** after restoring `ExecutionGuardContextTests`
  - Python.Tests 128 passed, 2 inconclusive
- Integration: none (no product wire change).
- Repository-required checks: compile + `dotnet run` on each scoped csproj.

## Result

`tests/DevTools.Execution.Tests` is deleted. Execution unit tests are scoped
MSTest.Sdk 4.4.0 projects. Coverage flag is `--coverage` (not Coverlet).
Uninitialized pythonnet facts remain Inconclusive after pixi init in the
same testhost. Microsoft coverage HTML merge for Execution is still a follow-up
(not run in this session).
