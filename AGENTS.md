# AGENTS

## Start Here
- Treat `RevitDevTool.slnx` as the source of truth for solution-wide work. The repo root has no `.sln`; CI and build modules target `.slnx`.
- Read the architecture README for the module you are changing before editing core behavior: `docs/CodeExecute/architecture/README.md`, `docs/Logging/architecture/README.md`, `docs/Visualization/architecture/README.md`.
- Trust code and build modules over top-level prose. Some docs still reference old paths like `source/RevitDevTool.PythonDemo` or `source/Samples/...`; the current sample code lives under top-level `Samples/`.

## Repo Shape
- Main add-in: `source/RevitDevTool/`.
- Shared libraries: `source/DevTools.Logging/`, `source/DevTools.Utilities/`, `source/RevitDevTool.Core/`.
- MCP pieces are split: parser library in `source/RevitDevTool.McpParser/`, self-contained server in `source/RevitDevTool.McpServer/`, in-addin runtime host under `source/RevitDevTool/BridgeExecution/` with MCP and test execution scoped underneath.
- Examples and sample toolsets live under `Samples/`, not under `source/`.

## Build And Verification
- Required SDK is .NET `10.0.0` via `global.json`.
- Root build logic lives in `build/Program.cs`; CI does not call `dotnet build` directly.
- CI/package command: `dotnet run -c Release pack` from `build/`.
- Release publish command: `dotnet run -c Release -- pack publish` from `build/`.
- Supported add-in configurations are `Debug.Autodesk.2022` through `Debug.Autodesk.2026` and `Release.Autodesk.2022` through `Release.Autodesk.2026`. `ResolveConfigurationsModule` selects `Release.Autodesk.*` from `RevitDevTool.slnx`.
- For focused compile checks, prefer `dotnet build RevitDevTool.slnx -c "Debug.Autodesk.2025"` or the matching target year instead of guessing `Release R25` style config names from README text.
- Packaging depends on compiled `bin/publish` outputs. `pack` runs clean, compile, bundle creation, MCP server publish, and MSI creation.

## Tests
- .NET test projects are `tests/RevitDevTool.Execution.Tests/` and `tests/RevitDevTool.Server.Tests/`.
- Python parser tests live in `tests/RevitDevTool.PyServer.Tests/` and should be run with `pytest`.
- Some server tests require built sample assets and a prepared Python environment; they are not pure unit tests.
- `ParserIntegrationTests` expects `Samples/McpToolsetDemo/bin/Debug/net8.0/McpToolsetDemo.dll` to already exist, even though the current test helper still looks under `source/Samples/...`.
- `PythonInProcessParserTests` and parser integration tests expect a Pixi Python env at `%APPDATA%\RevitDevTool\pixi-env\.pixi\envs\default`.
- Several tests still search upward for `RevitDevTool.sln` instead of `.slnx`; if a repo-root lookup fails, check that assumption before changing unrelated code.

## Execution System
- The execution tree is orchestrated by `source/RevitDevTool/Execution/Services/ExecutionOrchestrator.cs`.
- Provider selection is DI-based in `source/RevitDevTool/Host.cs`: `AssemblyExecutionProvider` for `.dll`, `ScriptExecutionProvider` for directories.
- Script discovery only surfaces files ending with `*script.py` or `*script.fsx`; a folder without at least one matching entry script is skipped.
- Script discovery also skips many directories by name, including `docs`, `resources`, `bin`, `obj`, `node_modules`, virtualenv folders, and agent folders like `.agent`, `.agents`, `.claude`.

## .NET / F# / Python Quirks
- .NET command execution is by loading `IExternalCommand` implementations from DLLs; on .NET 8+ it uses an unloadable `AssemblyLoadContext`, while .NET Framework builds execute in the current AppDomain with manual unmanaged DLL loading.
- F# script execution compiles `.fsx` into an `IExternalCommand` and then runs it through the same Revit command path. F# compilation has a hard 30 second timeout in `FSharpExecutionStrategy`.
- F# package resolution is custom NuGet installation under `%APPDATA%\RevitDevTool\nuget`, not standard restore.
- Python backend selection is automatic: try Pixi first, then fall back to a pip-based pyRevit CPython provider if Pixi cannot run.
- Pixi env location is `%APPDATA%\RevitDevTool\pixi-env`; content/settings/log roots are versioned under `%APPDATA%\RevitDevTool\<RevitVersion>`.
- Python dependency resolution is driven by PEP 723 metadata parsed by `source/RevitDevTool/Resources/scripts/Parser.py`.
- Pip fallback is designed for locked-down enterprise machines and depends on `pyrevit.exe` being on `PATH` so it can locate `cengines/CPY*/python.exe`.

## Logging, Visualization, Memory
- Logging is wired in `Host.ConfigureLogging()` and uses monitor logging + file logging + HTTP logging; monitor output is backed by Scintilla via `DevTools.Logging` abstractions.
- The add-in logging bridge registers trace listeners dynamically through `source/RevitDevTool/Logging/LoggingService.cs`.
- Geometry visualization is not separate from logging: `GeometryListener` intercepts traced geometry and routes it to DirectContext3D visualization servers instead of text output.
- DirectContext3D servers are concrete singletons in `source/RevitDevTool/Visualization/Server/`; keep new geometry rendering aligned with that server-per-geometry-type pattern.
- Memory monitor is a real feature, not demo UI. `MemoryViewModel` samples process working set, private memory, and GC heap and is toggled by `GeneralConfig.IsMemoryEnabled`.

## MCP Registry
- MCP registry UI and runtime hosting are part of the main add-in service graph in `Host.cs`, not separate tooling.
- `.NET` MCP discovery parses registered assemblies through `DotnetMcpToolRegistryProvider`.
- Python MCP discovery parses toolset directories through `PythonMcpToolRegistryProvider` and pre-resolves dependencies for each matching Python MCP entry file before catalog load.
- If you change MCP parsing or sample toolsets, verify both parser libraries and the in-addin registry flow.

## Frontend Sample
- The only JS/TS app in the repo is `Samples/PythonDemo/revit_dashboard_ui/`.
- Its quality gate is `npm run quality` (`typecheck` then `lint`); build is `npm run build`.

## .NET Development Best Practices
Follow these principles to reduce cognitive load and improve maintainability:

### Naming
- Use meaningful, pronounceable names: `startDate` not `d` or `modTime`
- Avoid Hungarian notation: `counter` not `iCounter`
- Use consistent capitalization: PascalCase for types/methods, camelCase for variables
- Avoid magic strings/numbers: extract to `const` or `enum`

### Functions
- Do one thing only: if you need `if`, the function likely has multiple responsibilities
- Keep arguments to 2 or fewer; use objects for more (records are idiomatic in modern C#)
- Use default arguments instead of null checks where appropriate
- Avoid boolean flags in parameters: split into separate methods
- Encapsulate conditionals: `if (user.IsActive())` not `if (user.Status == "active")`

### Classes & OOP
- Prefer composition over inheritance (especially for testability)
- Use method chaining (fluent extensions) for expressive code
- Keep members private/protected; expose via properties when needed
- Apply SRP: one class = one reason to change

### SOLID Principles
- **S**ingle Responsibility: one stakeholder/user concern per class
- **O**pen/Closed: open for extension, closed for modification
- **L**iskov Substitution: subtypes must be substitutable for base
- **I**nterface Segregation: small, focused interfaces
- **D**ependency Inversion: depend on abstractions, not concretions

### Cognitive Load Reducers
- Early returns: check invalid conditions first, reduce nesting
- Introduce intermediate variables for complex conditions with meaningful names
- Prefer deep modules (simple interface, rich functionality) over shallow ones
- Avoid premature abstraction; extract only when duplication exists
- Prefer descriptive error codes over generic numeric codes

### Dependency Injection
- Use DI throughout (this repo uses `Host.cs` for service registration)
- Avoid Singleton; use scoped services where appropriate
- Inject abstractions via constructor

### Error Handling
- Prefer exceptions for truly exceptional cases
- Use `Result` pattern or similar for operations that may fail expectedly
- Don't swallow exceptions without logging

### Testing
- Write tests that reveal intent, not just coverage
- Follow AAA: Arrange, Act, Assert
- One concern per test

### Common Anti-Patterns to Avoid
- God classes with too many responsibilities
- Premature "clean architecture" layers (hexagonal/onion for small projects)
- Over-abstraction: `FactoryFactory`, `HelperHelper`
- Tight framework coupling in core business logic
- Magic numbers or strings throughout code

## References
- [clean-code-dotnet](https://github.com/thangchung/clean-code-dotnet) - Clean Code concepts adapted for .NET
- [cognitive-load](https://github.com/zakirullin/cognitive-load) - Cognitive load is what matters (reducing extraneous complexity)
- [A Philosophy of Software Design](https://web.stanford.edu/~ouster/cgi-bin/book.php) - Deep modules, information hiding

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **RevitDevTool** (13700 symbols, 35864 relationships, 300 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.

> If any GitNexus tool warns the index is stale, run `npx gitnexus analyze` in terminal first.

## Always Do

- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `gitnexus_impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `gitnexus_detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `gitnexus_query({query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `gitnexus_context({name: "symbolName"})`.

## When Debugging

1. `gitnexus_query({query: "<error or symptom>"})` — find execution flows related to the issue
2. `gitnexus_context({name: "<suspect function>"})` — see all callers, callees, and process participation
3. `READ gitnexus://repo/RevitDevTool/process/{processName}` — trace the full execution flow step by step
4. For regressions: `gitnexus_detect_changes({scope: "compare", base_ref: "main"})` — see what your branch changed

## When Refactoring

- **Renaming**: MUST use `gitnexus_rename({symbol_name: "old", new_name: "new", dry_run: true})` first. Review the preview — graph edits are safe, text_search edits need manual review. Then run with `dry_run: false`.
- **Extracting/Splitting**: MUST run `gitnexus_context({name: "target"})` to see all incoming/outgoing refs, then `gitnexus_impact({target: "target", direction: "upstream"})` to find all external callers before moving code.
- After any refactor: run `gitnexus_detect_changes({scope: "all"})` to verify only expected files changed.

## Never Do

- NEVER edit a function, class, or method without first running `gitnexus_impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `gitnexus_rename` which understands the call graph.
- NEVER commit changes without running `gitnexus_detect_changes()` to check affected scope.

## Tools Quick Reference

| Tool | When to use | Command |
|------|-------------|---------|
| `query` | Find code by concept | `gitnexus_query({query: "auth validation"})` |
| `context` | 360-degree view of one symbol | `gitnexus_context({name: "validateUser"})` |
| `impact` | Blast radius before editing | `gitnexus_impact({target: "X", direction: "upstream"})` |
| `detect_changes` | Pre-commit scope check | `gitnexus_detect_changes({scope: "staged"})` |
| `rename` | Safe multi-file rename | `gitnexus_rename({symbol_name: "old", new_name: "new", dry_run: true})` |
| `cypher` | Custom graph queries | `gitnexus_cypher({query: "MATCH ..."})` |

## Impact Risk Levels

| Depth | Meaning | Action |
|-------|---------|--------|
| d=1 | WILL BREAK — direct callers/importers | MUST update these |
| d=2 | LIKELY AFFECTED — indirect deps | Should test |
| d=3 | MAY NEED TESTING — transitive | Test if critical path |

## Resources

| Resource | Use for |
|----------|---------|
| `gitnexus://repo/RevitDevTool/context` | Codebase overview, check index freshness |
| `gitnexus://repo/RevitDevTool/clusters` | All functional areas |
| `gitnexus://repo/RevitDevTool/processes` | All execution flows |
| `gitnexus://repo/RevitDevTool/process/{name}` | Step-by-step execution trace |

## Self-Check Before Finishing

Before completing any code modification task, verify:
1. `gitnexus_impact` was run for all modified symbols
2. No HIGH/CRITICAL risk warnings were ignored
3. `gitnexus_detect_changes()` confirms changes match expected scope
4. All d=1 (WILL BREAK) dependents were updated

## Keeping the Index Fresh

After committing code changes, the GitNexus index becomes stale. Re-run analyze to update it:

```bash
npx gitnexus analyze
```

If the index previously included embeddings, preserve them by adding `--embeddings`:

```bash
npx gitnexus analyze --embeddings
```

To check whether embeddings exist, inspect `.gitnexus/meta.json` — the `stats.embeddings` field shows the count (0 means no embeddings). **Running analyze without `--embeddings` will delete any previously generated embeddings.**

> Claude Code users: A PostToolUse hook handles this automatically after `git commit` and `git merge`.

## CLI

| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
