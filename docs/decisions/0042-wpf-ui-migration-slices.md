# 0042 WPF Product UI Leaves in Independent Slices

Date: 2026-09-23

## Status

Proposed

Updated 2026-09-23: `DevTools.UI` stays as the bare-WPF bridge. Slice F
removes the MahApps, ControlzEx, and XamlBehaviors submodules together with
the custom WPF controls. It does not delete `DevTools.UI`.

Requires the shell rules in [0037](0037-webview2-bare-window-shell.md) and a
working standalone window ([0040](0040-webview2-host-and-virtual-host.md),
[0041](0041-webview-playwright-cdp-tests.md)). Each slice below can merge on
its own once those exist. Slices do not share a branch-blocking order except
where a later slice deletes a project the earlier one still compiles.

## Context

Today's add-in UI is spread across:

| Owner | WPF role |
|-------|----------|
| `DevTools.Presentation` | Execution, Command, Package, Memory, MCP registry, settings, stub builder. View-models plus XAML. |
| `DevTools.UI` | MahApps project reference, theme dictionaries, custom controls (`AutoCompleteComboBox`, `ExtendedTreeView`, `MouseDoubleClick`), behaviors, converters, `HighlightRange`. Keepers: `ThemeManager`, `HostUiHelper`, `Win32Utils`, and the future bare window. |
| `libs/MahApps.Metro`, `libs/ControlzEx`, `libs/XamlBehaviorsWpf` | The three WPF git submodules. `libs/pythonnet-stub-generator` is not in this list. |
| `RevitDevTool` / `AcadDevTool` | `MainWindow` / `MainPage` shells, host settings views. |
| `DevTools.Execution` | `UseWpf` for `XmlnsDefinition` so XAML can bind models; `ProjectReference` to `DevTools.UI`; `ZLogger.Scintilla` (`ICustomSerializer` on `PythonJsonSerializer`). The package goes away with Scintilla; the serializer stays. |
| `RevitDevTool.Tools` | Element Finder and Command Browser. They stay WPF: Command Browser is injected into Revit's visual tree. Theme is the .NET 10 Fluent theme ([0044](0044-revit-wpf-fluent-theme.md)), not a SPA route. |
| `RevitDevTool.Core` | `UseWpf` on the csproj. No `System.Windows` usages in its C# sources. |
| `DevTools.Daemon` | Out of this migration. Plain WPF on the .NET 10 Fluent theme ([0043](0043-daemon-wpf-fluent.md)). Shipped UI stays MewUI until that ADR is Accepted. |

One shared shell. Civil 3D and Plant 3D are composition modules on the
AutoCAD host, not a second UI. The core stays headless. UI I/O stays in the
host project. A WPF surface is deleted only when the route answers the same
task, and that route is proven on the standalone exe first.

## Decision

Rules for every slice:

- A slice adds the SPA route, the `IUiCommand` handlers, and the bridge
  fixtures, and deletes the XAML for that feature in the same change.
- The standalone exe hosts the new commands with in-memory or existing
  headless services. Lane 2 and lane 3 from
  [0041](0041-webview-playwright-cdp-tests.md) cover the route before the
  add-in entry points at `WebViewWindow`.
- No second theme and no "legacy WPF" toggle remains after the slice merges.
- Handlers call existing services. View-model state that only existed to feed
  XAML is deleted rather than reimplemented in the SPA.
- `DevTools.Execution` stays the execution engine and keeps its
  `DevTools.UI` reference for `HostUiHelper.RunOnMainThread`. `UseWpf` on
  Execution drops in the slice that deletes the last XAML `xmlns` into
  Execution models. `ZLogger.Scintilla` is removed with the log slice, not
  kept for `PythonJsonSerializer`. That class stays and stops implementing
  `ICustomSerializer`. `TreeNodeBase.HighlightRange` is the WPF color
  type in `DevTools.UI.Behaviors`; slice A removes it. A start/end that the
  engine still needs becomes a plain record in Execution.
- `RevitDevTool.Core` drops `UseWpf` in any slice that touches that csproj
  once a build confirms no XAML resource needs it.
- New host-add-in shell UI does not get added as XAML while this decision
  is accepted. Revit tool windows that must join Revit's WPF are
  [0044](0044-revit-wpf-fluent-theme.md). Daemon views are
  [0043](0043-daemon-wpf-fluent.md).

Slices, each shippable alone:

| Slice | Removes | Leaves in place |
|-------|---------|-----------------|
| A. Execution | `ExecutionView` XAML, `HighlightRange` | Other Presentation views, add-in still opens `MainPage` |
| B. Settings (general, log, MCP) | Settings XAML in Presentation and host settings views | Execution route from A if it has merged |
| C. Log | `ScintillaLogViewerWpf`, `MonitorLogTarget` WPF viewer, `ZLogger.Scintilla`, `Scintilla5.NET`. Log route is CodeMirror linkify + render ([0038](0038-webview-react-parkui-codemirror.md)). `RevitLinkifier` click behavior moves to a bridge command | MCP registry, packages, memory, commands, stub builder can share this slice or follow it |
| D. Entry switch | `MainPage` / host shell XAML, Acad `ElementHost` / `PaletteSet` UI host, `UseWindowsForms` on both host csprojs. The add-in command opens `WebViewWindow`. Revit 2025 puts CefSharp in that window. Revit 2022, 2023, 2024, 2026 onward, and AutoCAD put WebView2 | Tools windows ([0044](0044-revit-wpf-fluent-theme.md)). Daemon is [0043](0043-daemon-wpf-fluent.md) |
| F. Strip WPF libraries | Submodules `libs/MahApps.Metro`, `libs/ControlzEx`, `libs/XamlBehaviorsWpf`. `tests/DevTools.MahApps.Metro.Tests`. `SharedSidecars` entries and host DLL copy lists for the three assemblies. Theme dictionaries, custom controls, behaviors, converters, `ThemeManager` dependency properties. Empty `DevTools.Presentation` project. | `DevTools.UI` (`WebViewWindow`, transport, plain `ThemeManager`, `HostUiHelper`, `Win32Utils`). `libs/pythonnet-stub-generator`. `RevitDevTool.Tools` on the Fluent theme ([0044](0044-revit-wpf-fluent-theme.md)) |

Slice D merges only after A–C have routes, because one window replaces
`MainPage` and there is no second chrome for a missing tool. A–C can merge
in any order before D. F is after D. Command Browser and Element Finder are
not a slice: they stay WPF ([0044](0044-revit-wpf-fluent-theme.md)). F does
not delete them. F removes MahApps only after those windows merge Fluent
instead.

## Alternatives Considered

1. **Big-bang replacement of `MainPage` in the first PR.** One review would
   carry execution, settings, the log, MCP, packages, and memory. A slice
   that fails review would block the shell. A–C land against the standalone
   exe first.
2. **Move Element Finder and Command Browser into the SPA.** They join
   Revit's visual tree and HWND. A browser page cannot. They stay WPF on
   the .NET 10 Fluent theme ([0044](0044-revit-wpf-fluent-theme.md)).
3. **Per-tool WebView2 windows** (one browser per today's view). Multiplies
   processes and user-data folders. One window, hash routes.
4. **Keep `ZLogger.Scintilla` for `PythonJsonSerializer`.** That keeps the
   package this migration exists to delete. The serializer stays; its base
   type leaves the package in slice C.
5. **A CodeMirror code editor.** Script editing is not the goal. CodeMirror
   renders and linkifies the log.

## Consequences

Positive:

- Each slice has a reviewable surface: one route, one command set, one XAML
  delete, lane 2 and lane 3 evidence.
- The add-in the user launches stays on the current shell until D, so a
  half-migrated tool is not the daily entry.
- After F, the browser shell's WPF is only inside `DevTools.UI`:
  `WebViewWindow`, the browser child, and the Win32 owner/caption helpers.
  The add-in shell shows the page. `ThemeManager` talks to that page through
  `host.theme`. Scintilla is gone. `RevitDevTool.Tools` stays WPF on the
  Fluent theme ([0044](0044-revit-wpf-fluent-theme.md)). Daemon stays a
  separate WPF window ([0043](0043-daemon-wpf-fluent.md)).

Tradeoffs:

- Until D, two UIs exist in the repo: `MainPage` for the add-in, and the SPA
  in the standalone exe. That overlap is temporary and ends at D. It is not
  a supported dual-UI mode.
- Command Browser and Element Finder stay inside Revit. Their behavior
  proof is the existing Tools tests, not a Playwright lane. The theme is
  [0044](0044-revit-wpf-fluent-theme.md).
- Slice F deletes the three submodules only after D, and only after
  `RevitDevTool.Tools` has stopped merging MahApps. `Win32Utils` does
  not move. Doing F earlier breaks the shell the add-in is still showing.

## Follow-Up

- Track slice status in
  `docs/plans/active/2026-09-23-webview2-shell.md`.
- When D merges, update `docs/agents/host-boundaries.md` and the architecture
  index. Do not describe `DevTools.Presentation` as the UI before that.
- When F merges, mark [0037](0037-webview2-bare-window-shell.md) Accepted and
  point this ADR at the resulting module doc.
