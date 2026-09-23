# 0037 Product UI Is a Bare WPF Window Hosting WebView2

Date: 2026-09-23

## Status

Proposed

Updated 2026-09-23: the WPF bridge stays in `DevTools.UI`. There is no
`DevTools.WebView` project. `DevTools.Web` is the frontend only.
Product chrome for the host add-in is the web UI. Revit 2025 hosts that UI
in CefSharp because that Revit release is incompatible with the WebView2
runtime. Revit 2022, 2023, 2024, and 2026 onward use WebView2, as does the
AutoCAD family. The standalone Daemon is not this shell
([0043](0043-daemon-wpf-fluent.md)). The three WPF git submodules leave with
the custom controls. Scintilla and the WinForms host around it leave as well.

Depends on nothing. Later WebView decisions
([0038](0038-webview-react-parkui-codemirror.md),
[0039](0039-webview-json-bridge.md),
[0040](0040-webview2-host-and-virtual-host.md),
[0041](0041-webview-playwright-cdp-tests.md),
[0042](0042-wpf-ui-migration-slices.md))
assume this shell. Daemon UI is
[0043](0043-daemon-wpf-fluent.md), not a consumer of this window.

## Context

The host add-in UI is a WPF control tree. `DevTools.UI` references MahApps
and owns `ThemeManager`, theme dictionaries, custom controls, behaviors, and
converters. Views and view-models live in `DevTools.Presentation`. Host shells
are `RevitDevTool.View.MainWindow` / `MainPage` and `AcadDevTool.View.MainPage`.
`Microsoft.Web.WebView2` `1.0.4191.47` is already in `Directory.Packages.props`
and referenced by both host projects, with no product code calling it.

A CAD add-in can already host a browser this way:

- The window's only XAML child is `WebView2`. The page talks to C# by `postMessage`. Core stays headless.
- One browser surface serves many hosts. The SPA never sees a host type. Revit 2025 in this product is CefSharp; every other year and the AutoCAD family are WebView2.
- Release assets load from a virtual host. Tests split Vitest, Playwright on Chromium, standalone CDP, and an optional live host CDP session. |

The standalone Daemon is a tray app with a small status window. It does not
host this SPA. Its shell, theme, and the dropped Native AOT target are
[0043](0043-daemon-wpf-fluent.md).

## Decision

- The product UI for Revit and the AutoCAD family is the web app in
  `source/DevTools.Web`
  ([0038](0038-webview-react-parkui-codemirror.md)) inside one bare WPF
  `Window`. The window's only child is the browser surface.
- `DevTools.UI` owns the bare window, the JSON envelope, `ThemeManager`, and
  `IBrowserSurface`. It does not reference WebView2 or CefSharp. Loading
  `DevTools.UI` inside Revit 2025 must not load the WebView2 runtime. It
  does not reference Revit API, AutoCAD API, `DevTools.Execution`, or
  `DevTools.Presentation`.
- Revit 2025 cannot load the WebView2 runtime in-process. That build hosts
  CefSharp and does not reference `DevTools.UI.WebView2`. The CefSharp
  package and surface live on the Revit 2025 build of `RevitDevTool`.
- The WebView2 surface is `source/DevTools.UI.WebView2/`, referenced by
  every host add-in except the Revit 2025 build: Revit 2022, 2023, 2024,
  and 2026 onward, and the AutoCAD family. `DevTools.Daemon` does not
  reference it. This project is the control and
  its transport. `DevTools.Web` stays the frontend. Only the Revit 2025
  build loads CEF. The SPA, the envelope
  ([0039](0039-webview-json-bridge.md)), and the window chrome are the same
  on both browsers.
- `ThemeManager` keeps its mechanism: `Setup(resolveHostTheme, subscribe)`,
  `AppTheme` (`Light`, `Dark`, `Auto`), `ApplySettingsTheme`, and
  `ActualApplicationTheme` plus the host-change callback already used by
  `HostBackgroundController`. In the end state it is a plain class, not a
  `DependencyObject`. It stops being a WPF resource applicator. When
  the actual theme changes it tells the page (bridge event in
  [0039](0039-webview-json-bridge.md)) and sets the bare window caption with
  `Win32Utils.SetTitleBarTheme`. `HostUiHelper` and `Win32Utils` stay; they
  are the window's HWND helpers, not a control library.
- Custom WPF leaves `DevTools.UI`: theme dictionaries (`Theme.xaml`,
  `ThemeResources`, `ControlsResources`, design-time intellisense
  dictionaries), `AutoCompleteComboBox`, `ExtendedTreeView`,
  `MouseDoubleClick`, highlight behaviors, converters, and `HighlightRange`.
  These controls are not rebuilt in WPF and not wrapped for the page. The
  page uses Park UI and the log surface in
  [0038](0038-webview-react-parkui-codemirror.md).
- Three git submodules leave together. MahApps references ControlzEx, and
  ControlzEx references `Microsoft.Xaml.Behaviors`, so dropping one leaves
  the others unloaded:

  | Submodule | Path |
  |-----------|------|
  | MahApps.Metro | `libs/MahApps.Metro` |
  | ControlzEx | `libs/ControlzEx` |
  | XamlBehaviorsWpf (`DevTools.Microsoft.Xaml.Behaviors`) | `libs/XamlBehaviorsWpf` |

  The same change removes `tests/DevTools.MahApps.Metro.Tests`, the three
  names in `DevTools.AssemblyIsolation/SharedSidecars.cs`, and the host
  copy lists for `DevTools.MahApps.Metro.dll`, `DevTools.ControlzEx.dll`,
  and `DevTools.Microsoft.Xaml.Behaviors.dll`. `libs/pythonnet-stub-generator`
  stays. Slice timing is [0042](0042-wpf-ui-migration-slices.md). While
  Presentation XAML still merges `Theme.xaml`, the submodules stay. They
  are deleted in the change that removes their last consumer.
- Product interaction for the host add-in shell is the web UI. Lists,
  settings, and the log (linkify + render) render in `DevTools.Web`. A host
  feature does not get a new WPF control, WinForms host, behavior, or
  MahApps style because the bare window already exists. WPF that remains
  for that shell is the window, its browser child, `HostUiHelper`,
  `Win32Utils`, and `ThemeManager`. Command Browser and the Revit tool
  windows stay inside Revit's WPF and use the .NET 10 Fluent theme
  ([0044](0044-revit-wpf-fluent-theme.md)). The Daemon window is
  [0043](0043-daemon-wpf-fluent.md).
- Scintilla leaves the product entirely: `ScintillaLogViewerWpf`,
  `Scintilla5.NET`, and the `ZLogger.Scintilla` package, including
  `ILinkifier` / `ICustomSerializer` as reasons to keep that package.
  Linkify and log rendering move to CodeMirror
  ([0038](0038-webview-react-parkui-codemirror.md)). Token clicks still call
  the host through the bridge; [0036](0036-revit-monitor-link-element-tokens.md)
  stays the token rules. `PythonJsonSerializer` stays in Execution and stops
  implementing a type from `ZLogger.Scintilla`.
- WinForms that exists to host WPF or Scintilla leaves with those controls.
  `AcadDevTool` `ElementHost` inside `PaletteSet` (`PanelController`) goes
  when the Acad shell is the bare window. `UseWindowsForms` drops from
  `RevitDevTool.csproj` and `ACadDevTool.csproj` in that change.
- Each host add-in owns when the window opens and which commands it registers.
  Host chrome is a plain `Window` with `Win32Utils.SetHostAppOwner`. Revit
  dockable panes and AutoCAD `PaletteSet` are not the v1 shell.
- The end state deletes `DevTools.Presentation` views and host `MainPage`
  shells. `DevTools.UI` remains, reduced to the bare
  window, the bridge, and `ThemeManager`. Until the entry switch, today's
  shells stay so unmigrated tools remain usable. The new window is proven
  first by the standalone exe in
  [0040](0040-webview2-host-and-virtual-host.md).

## Alternatives Considered

1. **Keep WPF and embed WebView2 only for the log.** Leaves two UI toolkits
   and the MahApps shell. The log is a page in the same SPA.
2. **A remote HTTPS SPA** as the production UI. The add-in would depend on a
   network deploy and a frozen bridge ABI. DevTools is a local tool and must
   run offline from files next to the add-in.
3. **CefSharp on every host.** CefSharp stays on the Revit 2025 build only.
   Revit 2022, 2023, 2024, and 2026 onward, plus the AutoCAD family, stay on
   WebView2. Daemon is not a browser host
   ([0043](0043-daemon-wpf-fluent.md)).
4. **Drop Revit 2025** until WebView2 works there. 2025 is a supported host
   year. The UI for that year is CefSharp, not a missing shell.
5. **Put this SPA in the Daemon window** so the tray app matches Park UI.
   That starts WebView2 for overview, hosts, and settings. Daemon stays
   plain WPF ([0043](0043-daemon-wpf-fluent.md)).
6. **Avalonia or WinUI instead of a bare WPF window.** The host process
   already has a WPF dispatcher. A second UI framework does not remove the
   HWND the browser needs.
7. **A new `DevTools.WebView` project for the bridge.** `DevTools.Web` is
   already the frontend. `DevTools.UI` is already the shared UI assembly
   (`ThemeManager`, `HostUiHelper`, `Win32Utils`). A third project would
   split the window from the theme mechanism that has to talk to the page.
8. **Keep one of the three submodules.** ControlzEx and XamlBehaviors exist
   in this repo as MahApps dependencies. A bare window does not reference
   them. Vendoring a newer MahApps is the same shell this decision removes.

## Consequences

Positive:

- Product UI for the host add-in is DOM. Palette and dark/light live in
  Park UI; `ThemeManager` only resolves `AppTheme` and forwards the result.
  Log text, including linkify, is CodeMirror in that page.
- `DevTools.UI` stays the shared UI assembly for Revit and every
  AutoCAD-family add-in. Host-specific docking is not required to ship the
  shell. Daemon does not reference it
  ([0043](0043-daemon-wpf-fluent.md)).
- The test exe can load the same SPA the add-in loads, which is what
  [0041](0041-webview-playwright-cdp-tests.md) drives over CDP.

Tradeoffs:

- WebView2 is an HWND. CefSharp on Revit 2025 is also an HWND. Native
  dialogs cannot draw over either. File pickers go through bridge commands
  that hide or complete before the dialog. Product content does not use MahApps
  dialogs. The MahApps reference stays on `DevTools.UI` only until
  Presentation stops merging `Theme.xaml`
  ([0042](0042-wpf-ui-migration-slices.md)).
- Revit 2025 ships CefSharp binaries with that add-in only. Revit 2022,
  2023, 2024, 2026 onward, and AutoCAD layouts must not copy those binaries
  in. The Cef package version is chosen when that project reference is
  added; it has to load inside the Revit 2025 process.
- Two host processes must not share one WebView2 user-data folder. Profile
  layout is [0040](0040-webview2-host-and-virtual-host.md).
- `RevitDevTool.Tools` (Element Finder, Command Browser) is still WPF after
  this decision. [0042](0042-wpf-ui-migration-slices.md) schedules it; new
  product chrome does not land there meanwhile.

## Follow-Up

- Add the bare window to `DevTools.UI` when implementing
  [0040](0040-webview2-host-and-virtual-host.md). This ADR is policy.
- When the add-in entry switches, update `docs/agents/host-boundaries.md`:
  `DevTools.UI` is the WebView2 bridge, and `DevTools.Presentation` is gone.
