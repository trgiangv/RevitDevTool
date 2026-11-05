# 0058 MCP toolsets have one owner, shared by the registry panel and `add_tools`

Date: 2026-11-05

## Status

Accepted.

## Context

An agent that writes its own MCP toolset (a `[McpTool]` .dll or a `*mcp.py`
folder) had no way to register or reload it. Only the registry panel could
load a path or press Reload, and only the settings page could remove one.

Ownership was split. `McpCatalogStore` loaded paths, while
`McpSettingViewModel` removed a path by editing `McpRegistryConfig` and then
reloading. The store saved nothing to disk, so a host killed by the build loop
lost every path added since it started.

Reload did not reload code. The store raised `CatalogChanged` only when the
catalog metadata hash changed, and only `CatalogChanged` unloads the toolset
load contexts. A rebuild that changed a method body kept the old code. Even
after the contexts were cleared, `DotnetMethodResolver` matched any assembly
with an empty `Location`. Every stream-loaded toolset has one, so it picked an
old or unrelated toolset that had the same type name.

## Decision

1. **`McpCatalogStore` owns the registered toolsets and their saved paths.**
   `AddPathAsync` registers a path, or reloads it when it is already
   registered. `RemovePathAsync` unregisters it. `ReloadAsync` reloads all of
   them. Each change to the saved paths calls `ISettingsService.SaveSettings`
   at once. `ToolsetPaths` lists the paths.
2. **An explicit add, reload, or remove always rebuilds.** It raises
   `CatalogChanged` even when the metadata is unchanged, so the host server
   clears the source caches and the load contexts, and the daemon re-lists the
   host.
3. **Loading never drops a saved path; an explicit action may.** `EnsureLoaded`
   keeps paths that load nothing, because at host startup Python may not be
   ready yet. Add and reload still drop paths that produce no tools or
   resources.
4. **A toolset method resolves only from its own assembly.** An already loaded
   assembly matches a binding only when its `Location` is the binding's
   source path. Stream-loaded toolsets always resolve through their current
   load context.
5. **The panels are views.** The registry panel and the settings page call the
   store and redraw on `CatalogChanged`. They do not touch
   `McpRegistryConfig`.
6. **One MCP tool, `add_tools(action?, path?)`**: `add` (default; registers,
   or reloads when already registered), `reload` (every toolset), `remove`
   (exact path), `list` (paths with tool names and resource URIs). It resolves
   the store when it is called, because the store's loader lists the built-in
   tools.

## Alternatives Considered

1. **Add the actions to `run_command`.** Rejected: Command Execution runs an
   entry once, while a toolset registers tools in the catalog. One description
   for both would mislead search.
2. **A file watcher that reloads toolsets on rebuild.** Deferred: a build
   writes many files, and an agent already knows when it has rebuilt.
3. **Raise `CatalogChanged` only on metadata change, and clear contexts
   separately.** Rejected: a second change path for the same rebuild.

## Consequences

- Dev loop: rebuild, `add_tools(path)`, then call the tool from the next
  `code_mode` program. In the same program, the daemon has not re-listed the
  host yet and `InvokeAsync` reports the new tool as not found.
- Every explicit reload sends `list_changed`, even when nothing changed.
- The rebuilt DLL is never locked: toolsets load from bytes.
- A .NET toolset ships its own MCP SDK
  ([0019](0019-ilrepack-and-polyfill-isolated-alc.md) decision 7, S1). When it
  does not, the load fails and `add_tools` returns that failure with the fix
  instead of a generic "nothing found": providers report per-path failures in
  `RegistryCatalog.Failures`, kept by the store as `LoadFailures`.
- Host pipe sessions copy every shared `McpServerOptions` setting, including
  `RequestHandlers`, so a tool called as a task can be polled with `tasks/get`.

## References

- `source/DevTools.Mcp.Catalog/McpCatalogStore.cs`
- `source/DevTools.Mcp.Catalog/Discovery/MethodResolutionHelper.cs`
- `source/DevTools.Execution/External/Mcp/BuiltIn/AddToolsTool.cs`
- `source/DevTools.Presentation/ViewModels/Settings/McpSettingViewModel.cs`
- `tests/DevTools.Execution.Mcp.Tests/AddToolsToolTests.cs`
- `tests/DevTools.Mcp.Catalog.Tests/DotnetMethodResolverTests.cs`
