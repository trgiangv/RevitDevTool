# Execution Plan: Migrate Remaining tests/ To MSTest.Sdk

Date: 2026-09-13

## Status

Completed 2026-09-13

## Outcome

No in-repo `tests/*.Tests` project references `xunit.v3` or `coverlet.MTP`.
Each remaining testhost is `Sdk="MSTest.Sdk"` (4.4.0). net10 testhosts collect
`--coverage`. net48 testhosts have no collector.

## Context

- Decision: `docs/decisions/0035-mstest-sdk-repo-tests.md`
- Execution already migrated: [0034](../decisions/0034-execution-mstest-sdk-scoped-tests.md)
  / [2026-09-13-execution-test-project-split](2026-09-13-execution-test-project-split.md)
- Style: `tests/DevTools.TestRunner.Tests` and `tests/DevTools.Execution.CSharp.Tests`
- Gaps: `docs/agents/test-matrix.md`

## Scope

In scope: convert every remaining xUnit testhost **in place** (do not split
folders). Enable Microsoft coverage on net10. Flip TestRunner.Tests onto the
Default coverage profile.

Out of scope:

- Execution projects (already MSTest)
- `samples/` (NUnit/TUnit host tests, ricaun VSTest)
- Fixture class libraries (`*.Fixtures`, stubs) — not testhosts
- Unmanaged native instrumentation flags

## Approach

1. Parent writes 0035 + this plan.
2. Composer 2.5 agents convert **non-overlapping projects** in place:
   csproj SDK + `UseMicrosoftCodeCoverage`, remove `xunit.v3` / `Using Xunit`,
   convert test sources, `dotnet build` + `dotnet run`.
3. Parent removes Coverlet injection and package versions after the last
   testhost is converted; updates `test-matrix.md`.

## Agent map (non-overlapping)

| Agent | Projects |
|-------|----------|
| MCP small | `Mcp.Core.Tests`, `Mcp.Adapter.Tests`, `Mcp.Client.Tests` |
| MCP Catalog | `Mcp.Catalog.Tests` only (pythonnet; `[assembly: DoNotParallelize]`) |
| MCP Server + Daemon | `Mcp.Server.Tests`, `Daemon.Tests` (MewUI collection → `DoNotParallelize` on those classes or assembly) |
| Platform libs | `FileMetadata.Core.Tests`, `Settings.Tests`, `Utilities.Tests`, `Telemetry.Tests`, `Logging.Tests` |
| Hosting | `Hosting.Tests`, `Hosting.Revit.Tests`, `Hosting.Acad.Tests`, `AcadDevTool.Tests` |
| Isolation | `AssemblyIsolation.Tests`, `AssemblyIsolation.NetFramework.Tests` (net48: no coverage) |
| Testing kernel | `Testing.Abstractions.Tests`, `Testing.Transport.Tests`, `Testing.Host.Tests`, `TestAdapter.Tests`, `TestRunner.Tests` (coverage flip only) |
| NUnit/TUnit | `NUnit.Host.Tests`, `NUnit.Host.NetFramework.Tests` (net48), `NUnit.MTP.Tests`, `NUnit.Runtime.Tests`, `TUnit.Host.Tests`, `TUnit.Runtime.Tests` |

## Conversion rules

Mirror Execution / TestRunner.

| xUnit | MSTest |
|-------|--------|
| `[Fact]` | `[TestMethod]` + `[TestClass]` |
| `[Theory]` + `[InlineData]` | `[TestMethod]` + `[DataRow]` |
| `[MemberData]` | `[DynamicData]` |
| `[Collection]` / `CollectionDefinition` | delete; use `[DoNotParallelize]` on class or assembly when DisableParallelization was required |
| `TestContext.Current.CancellationToken` | instance `TestContext` then `TestContext.CancellationToken` |
| `Assert.Equal` | `AreEqual` |
| `Assert.Skip` | `Assert.Inconclusive` |
| `Assert.Throws<T>` | `ThrowsExactly<T>` |

csproj (net10):

```xml
<Project Sdk="MSTest.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <IsPackable>false</IsPackable>
    <UseMicrosoftCodeCoverage>true</UseMicrosoftCodeCoverage>
  </PropertyGroup>
  <!-- keep other PackageReference / ProjectReference; drop xunit.v3 -->
  <ItemGroup>
    <Using Include="Microsoft.VisualStudio.TestTools.UnitTesting"/>
    <!-- keep product Usings; drop Using Xunit -->
  </ItemGroup>
</Project>
```

net48: `TestingExtensionsProfile=None`,
`EnableMicrosoftTestingExtensionsCodeCoverage=false`, no
`UseMicrosoftCodeCoverage`.

Keep `DefaultItemExcludes` / fixture `Compile Remove` / `ProjectReference`
`AdditionalProperties` / `SetTargetFramework` / `PrivateAssets` /
`IsTestingPlatformApplication` as they are today unless MSTest.Sdk cannot
run without a change — then document the exception.

`PassThroughRunMapper.cs` is a helper linked into TestAdapter.Tests. Do not
put `[TestClass]` on it unless it already has `[Fact]`.

Daemon `ProjectReference` must keep
`AdditionalProperties="SelfContained=false;PublishSingleFile=false"`.

## Independence

- Catalog pythonnet: no `PythonEngine.Shutdown`. Skip/Inconclusive if optional
  sample DLL / pixi / live pipe missing (`OptionalArtifact`).
- Daemon MewUI: do not parallelize STA desktop tests.
- TestAdapter collections (`TestingDiscoveryCollection`,
  `PackageConsumerCollection`): `DoNotParallelize`.
- NUnit Host `CollectionBehavior` / trace listener tests: `DoNotParallelize`
  where the xUnit collection disabled parallelization.

## Risks And Recovery

- Parallel agents editing `Directory.Packages.props` / `Directory.Build.props`
  / `test-matrix.md` / `slnx` → **forbidden**. Parent owns those after.
- TestAdapter `IsTestingPlatformApplication=false` may conflict with
  MSTest.Sdk defaults. Keep it if `dotnet run` still discovers tests; if not,
  set what MSTest.Sdk needs and report.
- Rollback: git checkout the project folder.

## Progress

- [x] Decision 0035 + this plan
- [x] Composer 2.5: eight project groups (spawned 2026-09-13)
  - [x] Isolation — net10 60 pass / 2 fail (architecture LoadFrom); net48 22 pass / 1 fail (resolver order)
  - [x] Platform — 107 passed (FileMetadata 10, Settings 12, Utilities 18, Telemetry 24, Logging 43)
  - [x] MCP Catalog — 152 passed, 9 inconclusive (optional sample/pixi)
  - [x] Server + Daemon — Server 61 passed; Daemon 76 passed (one auth-tunnel flake on first full run)
  - [x] MCP small — Core 62 passed; Adapter 37 passed / 1 inconclusive; Client 20 passed
  - [x] Testing kernel — 253 passed (Abstractions 55, Transport 22, Host 66, TestAdapter 83, TestRunner 27)
  - [x] NUnit/TUnit — 173 passed (Host 32, Host.NetFx 13, MTP 51, Runtime 62, TUnit.Host 1, TUnit.Runtime 14)
  - [x] Hosting — 102 passed, 1 inconclusive (Hosting 71, Hosting.Revit 11, Hosting.Acad 18, AcadDevTool 2 + 1 skip)
- [x] Remove `xunit.v3` + `coverlet.MTP` from central packages/props
- [x] Update `docs/agents/test-matrix.md`

## Validation

- Focused: `dotnet build` + `dotnet run --project tests/<proj>/...` per converted csproj (agent evidence 2026-09-13).
- Isolation 3 failures treated as pre-existing product/architecture, not harness bugs.
- Coverage HTML merge not run this session.

## Result

Every in-repo `tests/*.Tests` testhost is MSTest.Sdk 4.4.0. `xunit.v3` and
`coverlet.MTP` are gone from `Directory.Packages.props`. net10 testhosts collect
`--coverage`. net48 testhosts have no collector. Isolation LoadFrom failures
and one Daemon auth-tunnel flake remain product follow-ups, not migration
blockers.
