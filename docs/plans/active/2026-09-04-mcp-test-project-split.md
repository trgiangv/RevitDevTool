# Execution Plan: Split MCP God Test Project

Date: 2026-09-04

## Status

Active

## Outcome

`tests/DevTools.Mcp.Tests` is replaced by module-scoped test projects that match
`source/DevTools.Mcp.*`. Optional fixtures skip instead of failing the suite.
Misplaced pytest/settings/file-metadata cases leave MCP. No remaining test
depends on another test’s leftover action.

## Context

- Product: `docs/product/mcp.md`
- Architecture: `docs/architecture/MCP/README.md`
- Gaps: `docs/agents/test-matrix.md`
- Verify: `docs/agents/verification.md`, `.agents/skills/build/SKILL.md`

God-project evidence: one csproj references Daemon (self-contained),
Presentation, Execution, FileMetadata.Revit/Acad, pythonnet, Moq, Bogus;
~180 cases; `Assert.Fail` when sample DLL / pixi / live Revit pipe is absent;
`PlatformSplitScaffoldTests` is a 2026-07-28 migration leftover;
`ContractTests` / `McpServerConfigurationTests` mix Core, Catalog, Adapter,
FileMetadata, Execution, and Server.

## Scope

In scope:

- Split into `DevTools.Mcp.{Core,Catalog,Adapter,Client,Server}.Tests`
- `DevTools.Daemon.Tests` for Daemon composition / control JSON
- `DevTools.Settings.Tests` for theme JSON
- Move pytest/IPy/pipe-name/connection-tracker into `DevTools.Execution.Tests`
- Move FileMetadata reader facts into `DevTools.FileMetadata.Core.Tests`
- Optional artifacts: `Assert.Skip`, not `Assert.Fail`
- Delete empty `DevTools.Mcp.Tests` after the move
- Update InternalsVisibleTo + agent verify docs (one layer)

Out of scope:

- Live host E2E automation (`mcp-integration-test.md` stays manual)
- Rewriting product MCP behavior
- Completed-plan path rewrites

## Approach

1. Scaffold new csproj + slnx + InternalsVisibleTo.
2. Split mixed files (`ContractTests`, `McpServerConfigurationTests`,
   `PlatformSplitScaffoldTests`, `GeneralConfigThemeJsonTests`) at the source.
3. Move remaining whole files with Composer 2.5 agents (non-overlapping).
4. Independence: skip missing samples/pixi/live pipe; disable parallelization
   for pythonnet and static MRTR stubs.
5. Compile + `dotnet run` each new project. Delete the god project.

## Target map

| Project | Owns |
|---------|------|
| `DevTools.Mcp.Core.Tests` | Result/error/protocol/JSON encoders, schema, ToolHelpers FileInfo serialize |
| `DevTools.Mcp.Catalog.Tests` | Store/loader/parsers/invoker/ALC/isolation; pythonnet only here |
| `DevTools.Mcp.Adapter.Tests` | Host wire, handler, JSON-RPC, conformance, host named-pipe |
| `DevTools.Mcp.Client.Tests` | Passthrough surface, pipe scanner, SDK named-pipe/stream |
| `DevTools.Mcp.Server.Tests` | search/invoke harness, daemon options, log payload |
| `DevTools.Daemon.Tests` | `ServerHostBuilder` composition, Control JSON, launch-host source guards |
| `DevTools.Settings.Tests` | `GeneralConfig` theme JSON + Settings UI-ref guard |
| `DevTools.Execution.Tests` | pytest framing, IPy paths, `HostPipeName`, `McpConnectState` |
| `DevTools.FileMetadata.Core.Tests` | FileReader catalog / Revit / Acad facts |

## Independence rules

- Missing `McpToolsetDemo` / `RevitMcpToolSet` / pixi / live `DevToolsMcp_Revit_*` → `Assert.Skip` with the build/launch hint.
- `DotnetToolsetMrtrStubs.ResetBindings()` at the start of every binder test; collection `DisableParallelization`.
- `PythonInProcessParserTests`: collection `DisableParallelization`; skip if pixi python is absent.
- Each test creates its own harness/catalog/pipe name (`Guid`); no shared mutable catalog across facts.
- Live host test stays opt-in skip, not a CI failure.

## Risks And Recovery

- InternalsVisibleTo mismatch → compile CS0122. Update AssemblyInfo to the new test assembly names.
- Daemon test project pulls SelfContained RID → set Daemon `ProjectReference` `AdditionalProperties="SelfContained=false;PublishSingleFile=false"` and do **not** set SelfContained on other test projects.
- Parallel agents touching `RevitDevTool.slnx` → parent owns slnx.
- Rollback: restore `tests/DevTools.Mcp.Tests` from git; drop new project folders.

## Progress

- [x] Inventory and target map
- [x] Scaffold csproj / slnx / InternalsVisibleTo
- [x] Split mixed files
- [x] Composer 2.5: move remaining files + skip conversion
- [x] Delete `DevTools.Mcp.Tests`
- [x] Update `docs/agents/test-matrix.md` + `verification.md` + build skill
- [x] Compile and run each new project

## Validation

- Focused proof (2026-09-04):
  - Core 25 passed
  - Catalog 111 (102 passed, 9 skipped optional fixtures)
  - Adapter 35 passed
  - Client 7 passed
  - Server 36 passed
  - Daemon 10 passed
  - Settings 6 passed
  - FileMetadata.Core 9 passed
  - Execution 102 (80 passed, 22 skipped opt-in)
- Integration: none required (no product wire change).
- Repository-required checks: compile + `dotnet run` on each new test csproj.

## Result

`tests/DevTools.Mcp.Tests` is gone. MCP tests are module-scoped. Optional sample/pixi/live-pipe/ILRepack/pythonnet fixtures Skip instead of failing the suite.
