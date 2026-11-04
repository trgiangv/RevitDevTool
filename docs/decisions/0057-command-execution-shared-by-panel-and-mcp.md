# 0057 Command Execution has one owner, shared by the panel and `run_command`

Date: 2026-11-04

## Status

Accepted. Supersedes decisions 1, 2 (no listing) and 3, and alternatives 1, 2
(listing) and 4 of [0056](0056-mcp-run-command.md). The exact-input rules,
results, description style, and the modal limit of 0056 still hold.

## Context

`run_command` ran a built entry without the Command Execution tree. The panel
did not show what the agent loaded or ran, the agent could not see or change
what the user loaded, and "last executed" did not follow agent runs.

Ownership was also split inside the product: `ExecutionOrchestrator` owned the
tree and file watching, while `CommandViewModel` saved and removed paths in
`ExecutionConfig`, partly through `RootRemoved` events. Any second caller of
the orchestrator would have had to copy that settings logic.

## Decision

1. **`ExecutionOrchestrator` owns all Command Execution state**: roots, saved
   paths, file watching, and the last executed node. `ExecuteAsync` marks the
   node it was given as last executed before it runs, whatever the result, and
   clears the mark everywhere else in the tree. Loading a root saves its
   path. Removing a root (directly, by cascade, by delete or rename on disk, by
   a failed reload, or when a parent folder absorbs it) drops or moves its saved
   path. `RootRemoved` is gone. Saved paths load once
   (`LoadSavedPathsAsync` is idempotent), whichever of the panel or MCP asks
   first.
2. **The orchestrator is UI-thread affine.** The panel calls it on the UI
   thread. `run_command` runs each call on the host UI thread through
   `HostUiHelper.RunOnMainThreadAsync`, so tree changes reach the panel through
   `TreeChanged` like a click.
3. **`CommandViewModel` is a view.** It calls the orchestrator and redraws on
   `TreeChanged`. It does not touch settings.
4. **One MCP tool, `run_command(action?, path?, entry?)`**, no new public tool:
   - `run` (default): `path` and `entry` follow 0056. The tool loads `path` as
     a root when the tree does not hold it, finds the entry in the tree, and on
     a miss reloads that root once (a fresh build may add the command). It then
     runs the node with `ExecuteAsync`, the same call as a click: fresh DLL
     isolation per run, telemetry, progress, last executed.
   - `add`: load `path` as a root. Reports `Added`, `AlreadyLoaded`, or
     `CoveredByRoot` with the root's entries.
   - `remove`: drop the root whose path is exactly `path`.
   - `list`: every root, or the root that holds `path`, with entries in the
     form `run` accepts (class name or `TypeFullName.MethodName` for a DLL,
     script path relative to the root for a folder).
5. **Covering rule.** A script root covers folders and scripts under it, never
   a `.dll`. A `.dll` inside a loaded script folder is its own root, and loading
   a folder absorbs only script roots under it.

## Alternatives Considered

1. **Separate `add_command_root` / `remove_command_root` / `list_command_roots`
   tools.** Rejected: one tool keeps the catalog small and the dev loop in one
   name.
2. **A loader service next to the orchestrator that adds roots and saves
   paths.** Rejected: it would be a second owner of the same state.
3. **Keep running outside the tree (0056).** Rejected: the agent and the user
   would keep seeing different state.

## Consequences

- The panel shows what the agent loaded and ran; the agent sees what the user
  loaded.
- An agent run blocks the UI thread only while the host executor runs the
  command, as a click does. The pane still does not show agent runs as busy.
- `list` makes discovery possible; `run` still matches exactly and never picks
  an entry the agent did not name.

## References

- `source/DevTools.Execution/Services/ExecutionOrchestrator.cs`
- `source/DevTools.Execution/External/Mcp/BuiltIn/RunCommandTool.cs`
- `source/DevTools.Presentation/ViewModels/CommandViewModel.cs`
- `tests/DevTools.Execution.Mcp.Tests/RunCommandToolTests.cs`
