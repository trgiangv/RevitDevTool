# 0056 MCP `run_command` runs a built entry point by exact path and name

Date: 2026-11-03

## Status

Accepted (landed 2026-11-03), partly superseded by
[0057](0057-command-execution-shared-by-panel-and-mcp.md): the tool now runs
through the Command Execution tree and also adds, removes, and lists roots. Plan:
[`2026-11-03-mcp-run-command`](../plans/completed/2026-11-03-mcp-run-command.md).
Extends [0027](0027-mcp-product-surface.md): one host built-in tool joins
the catalog behind `code_mode`. Daemon `tools/list` and search
([0040](0040-bm25f-search-and-hit-annotations.md)) are unchanged.

## Context

Agents can compile new code (`execute_csharp_code`, `execute_python_code`) but
cannot run code they just built: a Revit `IExternalCommand` or AutoCAD
`[CommandMethod]` in a DLL, or an entry script. In the dev loop the agent wrote
that code and knows the output path and the type name, so finding the entry is
not the problem. Running it the way a click in Command Execution would is.

The run path already exists and does not need the tree:

- `ICommandRunner.RunCommand(CommandItem)` opens a new isolation session for
  each call, so every run loads the DLL from disk as it is now.
- `AssemblyExecutionStrategy` marshals that call to `IHostContextExecutor`.
- `IScriptExecutionStrategyFactory.Create(mode, scriptPath, rootPath)` builds the
  strategy for `.py`, `_ipy_script.py`, `.fsx`, and `.csx` files.

## Decision

1. **One standalone host built-in tool, `run_command(path, entry?)`**, in
   `DevTools.Execution` and shared by every host (`IBuiltInMcpTool`, registered
   next to `CSharpCodeTool`). It is annotated `Destructive`. It does not read or
   change the Command Execution tree, its saved paths, or its "last executed"
   state. It works like `execute_csharp_code`: input in, one run, result out.

2. **The input is exact. There is no search or listing.**
   - `path` ending in `.dll`: `entry` is required. Revit: the full class name
     (`My.Ns.Command`). AutoCAD: `TypeFullName.MethodName`, the
     `CommandItem.FullClassName` form that `AcadCommandDiscovery` produces. The
     tool checks `entry` against `ICommandDiscovery.ParseCommands(path)` with an
     ordinal comparison, then runs it through `AssemblyExecutionStrategy`.
   - `path` to a folder: the script root, the same as a Script root in Command
     Execution, so sibling modules and relative imports resolve. `entry` is
     required: the script path relative to that folder, or an absolute path
     inside it. The file must be an entry script by the discovery rule of
     `ScriptExecutionProvider` (`*script.py`, `*_ipy_script.py`,
     `*script.fsx`, `*script.csx`). The execution mode comes from that rule,
     which moves to one shared helper.
   - `path` to a single script file is rejected. The error tells the agent to
     pass the working folder as `path` and the script as `entry`. A lone file
     has no root, so its imports and side files would not resolve.
   - Anything else fails and runs nothing: a missing path, an unsupported
     file type, an `entry` that is not an executable command in that DLL, or
     an entry script that is missing, outside the folder, or not an entry
     script. The error names the path and the entry. It does not suggest other
     names.

3. **Every call runs the current build.** The isolation session is created per
   run, so there is no load step, no reload argument, and no cache to clear.

4. **Concurrency matches the existing execute tools.** Main-thread work is
   serialized by the host executor queue, as for `execute_csharp_code`. The
   Command Execution pane does not show an agent run as busy.

5. **Results stay small; output goes to the log.** The tool returns
   `Succeeded`, `Cancelled`, or `Failed`, the command `message`, the exception
   type and message, and the elapsed time. Failures use the existing
   `[RUNTIME ERROR]` / `[ROLLBACK]` tags. Trace and console output already go to
   the log pane and the host log. The result does not carry the log file path,
   because no API exposes the current rolling file name.

6. **The description states the dev-loop condition.** Search ranks the tool
   like any other tool. The description tells the agent when to call it: you
   built this DLL or wrote this script, and you know its path and entry name.
   It uses dev terms (IExternalCommand, CommandMethod, DLL, build, script,
   entry point) and no CAD/BIM domain nouns. Draft:

   > Dev loop: run an entry point you just built. `path`: the built .dll, or
   > the working folder that holds your scripts (pass the folder, not a single
   > script file, so sibling modules resolve). `entry`: for a .dll, the Revit
   > IExternalCommand full class name or the AutoCAD CommandMethod as
   > TypeFullName.MethodName; for a folder, the entry script path relative to
   > it (*script.py, *_ipy_script.py, *script.fsx, *script.csx). Exact match
   > only; nothing runs when it does not match. The DLL is loaded fresh in an
   > isolated context for each run. A command that opens a dialog or a pick
   > prompt blocks until a person answers it.

7. **Modal UI is a known limit, documented and not fixed.** A command that
   opens a `TaskDialog`, a window, or a `PickObject` prompt blocks the host
   main thread until someone answers it, and the MCP call waits with it. The
   tool description and `docs/product/mcp.md` say so. No dialog suppression,
   journal playback, or UI automation is added.

## Alternatives Considered

1. **Resolve names against the Command Execution tree.** Rejected. The agent
   already has the path and the name. The tree is owned by the UI thread, so
   using it would need a snapshot index, a shared run gate, and care around
   "last executed" state, all to answer a question the agent does not ask.
2. **Fuzzy or partial matching, or listing entries.** Rejected. A partial
   match can run a command the agent never named. In the dev loop the agent
   reads names from its own source.
3. **A search intent gate for this tool.** Rejected. The input is exact and
   required, so a wrong pick fails without running anything. The description
   is enough.
4. **Separate load and run tools, or session roots in the pane.** Rejected.
   The per-run isolation session already loads the current build.
5. **Suppress or auto-answer dialogs.** Rejected. No host API handles this
   generally, and a partial fix would hide real blocking.

## Consequences

Positive:

- Build then run is one call, through the same isolation and execution path as
  a click.
- One implementation serves Revit, AutoCAD, and scripts.
- Neither the pane nor the orchestrator changes.

Tradeoffs:

- The agent must know the exact path and entry name. The tool cannot be used
  for discovery.
- Commands take no arguments. Input comes from the current document, the
  selection (Revit `ElementSet`), or the command itself.
- A modal command makes the MCP call wait until someone dismisses it.
- The pane does not show agent runs as busy. A user who clicks Execute during
  an agent run is queued behind it.

## Follow-Up

- Implement the tool and add its `McpSpecKeys.Tool` name.
- Move the script mode rule into one shared helper.
- Focused tests: a DLL with a valid entry, a DLL with an unknown entry, a
  missing `entry` for a DLL, a folder with a relative entry script, a single
  script file (rejected), an entry outside the folder or not an entry script
  (rejected), an unsupported path.
- On landing: update `docs/product/mcp.md` (tool and modal limit), then move
  this ADR to Accepted.

## References

- `source/DevTools.Execution/External/Mcp/BuiltIn/CSharpCodeTool.cs`
- `source/DevTools.Execution/Providers/Dotnet/AssemblyExecutionStrategy.cs`
- `source/DevTools.Execution/Providers/ScriptExecutionProvider.cs`
- `source/RevitDevTool/Adapters/RevitCommandRunner.cs`
- `source/AcadDevTool/Adapters/AcadCommandDiscovery.cs`
