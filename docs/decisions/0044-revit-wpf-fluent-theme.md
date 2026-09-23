# 0044 Revit Command WPF Uses the .NET 10 Fluent Theme

Date: 2026-09-23  
Accepted: 2026-09-24

## Status

Accepted

Does not move these screens into the WebView shell
([0037](0037-webview2-bare-window-shell.md)). Daemon theming is
[0043](0043-daemon-wpf-fluent.md).

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

MahApps is still the theme on other Presentation windows, and
[0037](0037-webview2-bare-window-shell.md) removes that stack later. A
third-party Fluent kit would replace one library with another. The .NET 10
theme is already the product look on Daemon ([0043](0043-daemon-wpf-fluent.md)).
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

- Command Browser, Element Finder, and Stub Builder stay WPF. They do not
  become routes in `DevTools.Web`.
  [0036](0036-revit-monitor-link-element-tokens.md) still owns what those
  tools do. Other `DevTools.Presentation` views keep MahApps `Theme.xaml`
  until a later migration.
- Fluent surfaces merge
  `/DevTools.UI;component/Theme/FluentTheme.xaml` on the window or injected
  control (`FluentThemeResources`, same singleton pattern as MahApps
  `ThemeResources`). Theme changes reapply through
  `ThemeManager` → `FluentThemeResources.Current`. Never merge into
  `Application.Current.Resources`. Never set `Application.ThemeMode` on
  Revit's `Application`.
- One pack URI names the dictionaries on every TFM:
  `pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.{Light|Dark}.xaml`.
  On `net48` / `net8.0-windows` that assembly is the vendored
  `source/PresentationFramework.Fluent/` project (targets those TFMs only).
  On `net10.0-windows` the same URI resolves to the inbox WPF assembly;
  `DevTools.UI` does not ProjectReference the vendored project on net10.
- The vendored pin is `dotnet/wpf` `release/10.0` commit
  `87e4d30e28c1aaadf1866fa0bfdab110bbef1d6f`. Not a submodule, not a NuGet,
  not MahApps / WPF-UI / ModernWpf. Keep upstream MIT (`LICENSE.TXT`,
  `NOTICE.md`). Only `Themes/*.xaml` and `Controls/**/*.cs` compile;
  `Styles/` and `Resources/` stay on disk as source. The assembly is
  excluded from ILRepack of `RevitDevTool` and `AcadDevTool`.
- Tool windows set `Background` / `Foreground` to Fluent brushes explicitly.
  An implicit `Window` style inside `Window.Resources` does not style that
  window, and Fluent's default window background is `Transparent`.
- Keys older TFMs do not ship (`SystemColors` accent brushes from .NET 9)
  are stand-ins inside the copy so dictionaries resolve. Visual values stay
  the .NET 10 dictionaries.
- Design-time preview uses `FluentDesignTimeResources.xaml` via
  `IntellisenseResources`, parallel to MahApps `DesignTimeResources.xaml`.
- Daemon does not reference the copy ([0043](0043-daemon-wpf-fluent.md)).
  The WebView shell does not reference the copy.

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
6. **`Window.ThemeMode` on net10 tool windows instead of dictionary merge.**
   Daemon uses `Application.ThemeMode`. Tool windows use the shared
   `FluentTheme.xaml` dictionary path on every TFM so one attach story
   covers Revit 2022–2027.

## Consequences

Positive:

- Revit 2022 through 2027 show one Fluent generation on Element Finder,
  Command Browser, and Stub Builder, without a theme NuGet.
- Revit's own WPF keeps its theme. Only our windows and the injected bar
  take Fluent.
- Slice F can drop MahApps from remaining Presentation views without
  deleting `RevitDevTool.Tools`.

Tradeoffs:

- The vendored XAML is large and frozen at a `release/10.0` commit. Inbox
  fixes that land only on later .NET 10 patches are not in the copy until
  someone updates the pin.
- Accent brushes on `net48` and `net8` are values we define, not live
  Windows accent. .NET 9 is the first inbox `SystemColors` accent, and
  those hosts are not on .NET 9.
- Remaining Presentation windows still merge MahApps until they move.

## Follow-Up

- Spike that landed this ADR:
  [completed/2026-09-23-fluent-theme-spike](../plans/completed/2026-09-23-fluent-theme-spike.md).
- [0042](0042-wpf-ui-migration-slices.md) no longer turns these tools into
  SPA routes. MahApps leaves the other Presentation windows only after they
  merge `FluentTheme.xaml` the same way.
