# 0044 Revit Command WPF Uses the .NET 10 Fluent Theme

Date: 2026-09-23

## Status

Proposed

Does not move these screens into the WebView shell
([0037](0037-webview2-bare-window-shell.md)). Does not theme Daemon;
that process is [0043](0043-daemon-wpf-fluent.md) and already runs on
`net10.0-windows`.

## Context

There is no `source/RevitDevTool.Commands/RevitDevTool.Commands.csproj`.
Ribbon commands are `source/RevitDevTool/Commands/` (`RevitDevTool.Commands`),
compiled by `RevitDevTool.csproj` (`UseRevit` and `UseWpf`). The windows and
the Revit visual-tree work live in `source/RevitDevTool.Tools/`
(`UseWpf`). Command Browser inserts a control into Revit's document pane by
walking AvalonDock (`HwndSource`, `Autodesk.Windows`, `VisualTreeHelper`).
Element Finder and the other tool windows are WPF owned by the Revit HWND
(`ToolWindowService`). That UI has to run inside Revit's WPF dispatcher.
A page in `DevTools.Web` cannot sit in Revit's visual tree.

Host TFMs, from `docs/docs/hosts/Hosts-Revit.md`:

| Revit | TFM | Fluent in the box |
|-------|-----|-------------------|
| 2022–2024 | `net48` | No |
| 2025–2026 | `net8.0-windows` | No. Fluent and `ThemeMode` start in .NET 9 |
| 2027 | `net10.0-windows` | Yes. .NET 10 extends the .NET 9 Fluent styles |

MahApps is the theme on these windows today, and
[0037](0037-webview2-bare-window-shell.md) removes that stack. A third-party
Fluent kit would replace one library with another. The .NET 10 theme is
already the product look on Daemon ([0043](0043-daemon-wpf-fluent.md)).
Older Revit years should show that same theme.

The theme sources are
[`PresentationFramework.Fluent`](https://github.com/dotnet/wpf/tree/main/src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Fluent)
(`Controls`, `Resources`, `Styles`, `Themes`, including `Fluent.xaml` and
the Light, Dark, and HighContrast dictionaries). `dotnet/wpf` is MIT.
`main` moves to the next .NET. The .NET 10 copy of that directory is
[`release/10.0`](https://github.com/dotnet/wpf/tree/release/10.0/src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Fluent).

Applying the theme at `Application` scope inside Revit restyles Revit's own
chrome. These windows are guests in that process.

## Decision

- Command Browser, Element Finder, and the other `RevitDevTool.Tools`
  windows stay WPF. They do not become routes in `DevTools.Web`.
  [0036](0036-revit-monitor-link-element-tokens.md) still owns what those
  tools do.
- Revit 2027 (`net10.0-windows`) uses the inbox Fluent theme.
  Set `Window.ThemeMode` on our tool windows (`Light`, `Dark`, or `System`
  from the existing app theme). Suppress `WPF0001` on that TFM only.
  Do not set `Application.ThemeMode` on Revit's `Application`. Do not merge
  a copied `Fluent.xaml` on this TFM; a manual dictionary overrides the one
  `ThemeMode` loads.
- Revit 2022–2026 (`net48` and `net8.0-windows`) compile a full copy of the
  .NET 10 `PresentationFramework.Fluent` tree into `RevitDevTool.Tools`.
  The pin is `release/10.0` of that directory, recorded at the copy (commit
  id in the folder). Not a git submodule of `dotnet/wpf`, not a NuGet, not
  MahApps, not WPF-UI or ModernWpf. Keep the upstream MIT license with the
  files. The `ref` project that defines the theme assembly's public API is
  not part of the copy. `ThemeMode` does not exist on these TFMs.
- The copy is merged on our window or on the control injected into Revit's
  tree. It is not merged into `Application.Current.Resources`.
- Keys the older TFM does not ship (`SystemColors` accent brushes, added in
  .NET 9) are defined inside the copy so the dictionaries resolve. That is
  a local stand-in for a missing platform key, not a second theme and not a
  package. Visual values stay the .NET 10 dictionaries.
- `net10.0-windows` does not compile the copy. One Revit year does not load
  both the inbox theme and the vendored dictionaries.
- Daemon does not reference the copy. It keeps inbox `ThemeMode`
  ([0043](0043-daemon-wpf-fluent.md)). The WebView shell does not reference
  the copy. AutoCAD has no Revit visual-tree UI and does not take this theme.
- A refresh of the copy is one change for every pre-`net10` TFM, still pinned
  to a `release/10.0` commit. It does not float `main`.

## Alternatives Considered

1. **Move Command Browser and Element Finder into the SPA**
   ([0042](0042-wpf-ui-migration-slices.md) slice E as first written).
   The browser cannot be a child of Revit's AvalonDock grid or share that
   visual tree. The tools stay WPF.
2. **MahApps until the tools move.** MahApps is the submodule stack the
   shell migration deletes. Leaving it for these windows keeps the library.
3. **A third-party Fluent package on every TFM.** Same look, new dependency,
   and a second implementation beside the .NET 10 inbox theme.
4. **Copy `main` instead of `release/10.0`.** `main` is the next .NET.
   Older hosts would drift from the theme Revit 2027 loads from the box.
5. **Set `Application.ThemeMode` or merge Fluent at application scope.**
   That restyles Revit. The theme attaches only to our windows and to the
   injected Command Browser root.

## Consequences

Positive:

- Revit 2022 through 2027 show one Fluent generation on the command UI,
  without a theme library.
- Revit's own WPF keeps its theme. Only our windows and the injected bar
  take Fluent.
- Slice F can drop MahApps without deleting `RevitDevTool.Tools`.

Tradeoffs:

- The vendored XAML is large and frozen at a `release/10.0` commit. Inbox
  fixes that land only on later .NET 10 patches are not in the copy until
  someone updates the pin.
- `Window.ThemeMode` is experimental (`WPF0001`). On Revit 2027 a removed
  API falls back to merging the same `release/10.0` dictionaries on that
  window, still not at application scope.
- Accent brushes on `net48` and `net8` are values we define, not live
  Windows accent. .NET 9 is the first inbox `SystemColors` accent, and
  those hosts are not on .NET 9.

## Follow-Up

- Copy the theme when implementing this ADR. This file is the policy.
- [0042](0042-wpf-ui-migration-slices.md) no longer turns these tools into
  SPA routes. MahApps leaves them only after this theme is what they merge.
