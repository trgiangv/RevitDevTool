# 0043 Daemon Desktop Is Plain WPF on the .NET 10 Fluent Theme

Date: 2026-09-23  
Accepted: 2026-09-24

## Status

Accepted

Replaces the Daemon shell and the Native AOT target in
[0032](0032-daemon-mewui-and-aot.md). Do not add MewUI views. Do not set
`PublishAot`. Do not put WebView2 in `DevTools.Daemon`.

Host add-in chrome is a separate proposal
([0037](0037-webview2-bare-window-shell.md) through
[0042](0042-wpf-ui-migration-slices.md)). This ADR does not change that
shell, and those ADRs do not choose the Daemon window.

JIT publish in [0032](0032-daemon-mewui-and-aot.md) section 2 stays.

## Context

`DevTools.Daemon` is the tray process: gateway, control pipe, and a small
desktop (overview, hosts, settings). `--stdio` is a second process with no
window. The desktop today is MewUI (`Aprillz.MewUI.Windows`, Direct2D) because
[0032](0032-daemon-mewui-and-aot.md) wanted a path to Native AOT.
`PublishAot` is not production. A 2026-09-03 spike linked a binary and was
rolled back.

The host add-in is moving to a browser SPA. Putting that SPA in the Daemon
window would start the WebView2 runtime for a tray app whose screens are a
status list and a settings form. The process should open a window, not a
browser environment. Host add-ins unify on WebView2
([0037](0037-webview2-bare-window-shell.md)); Daemon still stays out of that
stack.

Daemon already targets `net10.0-windows`. WPF on that runtime has an inbox
Fluent theme:

- .NET 9 added the theme, `Application.ThemeMode` / `Window.ThemeMode`
  (`None`, `Light`, `Dark`, `System`), window backdrop, and
  `SystemColors` accent keys.
- .NET 10 extends Fluent styles to controls this window actually uses
  (`TextBox`, `Label`, `GroupBox`, `GridView`, `GridSplitter`, `Hyperlink`,
  `Expander`, and others) and fixes HighContrast crashes plus RTL on
  `MenuItem`, `Expander`, and `TreeViewItem`. Microsoft still marks Fluent
  support as in progress
  ([What's new in WPF for .NET 10](https://learn.microsoft.com/dotnet/desktop/wpf/whats-new/net100#fluent-style-changes)).
- `ThemeMode` is `[Experimental("WPF0001")]` on the .NET 10 desktop API.
  Setting it from code requires suppressing that diagnostic
  ([.NET 9 ThemeMode](https://learn.microsoft.com/dotnet/desktop/wpf/whats-new/net90#thememode)).

MahApps is the theme stack the host migration is deleting. MewUI is a second
toolkit whose reason was AOT. Neither belongs on this window.

## Decision

- Daemon desktop UI is plain WPF on `net10.0-windows`. The window and its
  tray menu use inbox controls. No MahApps, no MewUI, no WebView2, no
  `DevTools.UI` window, no `DevTools.Web` page.
- `DevTools.Daemon` does not reference `Microsoft.Web.WebView2` or
  `DevTools.UI.WebView2`. Startup of the tray process does not create a
  browser environment. `--stdio` still has no UI.
- Theme is `Application.ThemeMode`. Existing Light / Dark / Auto settings map
  to `ThemeMode.Light`, `ThemeMode.Dark`, and `ThemeMode.System`. The Daemon
  project suppresses `WPF0001` so that assignment compiles. Do not also merge
  `PresentationFramework.Fluent` `Fluent.xaml`; a manual dictionary overrides
  the dictionaries `ThemeMode` loads and drifts from the window backdrop.
- Daemon does not reference the vendored Fluent copy used by older Revit
  hosts ([0044](0044-revit-wpf-fluent-theme.md)). Those years cannot load the
  inbox theme. Daemon is `net10.0-windows` and keeps `ThemeMode`.
- Fluent gaps stay inbox. Microsoft documents the theme as unfinished. A
  control without a Fluent style keeps the platform fallback under the same
  `ThemeMode`. Do not fill the gap with MahApps, MewUI, or a custom theme
  dictionary.
- Native AOT is not a Daemon target. `PublishAot` stays unset. There is no
  follow-up spike, no gateway-only AOT fork, and no UI choice made to keep
  an AOT graph open. `PresentationFramework` on this process is accepted.
- Publish stays the JIT policy in [0032](0032-daemon-mewui-and-aot.md)
  section 2: framework-dependent, single-file, not self-contained, not
  ReadyToRun. The machine still needs the .NET 10 runtime.
- [0031](0031-daemon-json-source-gen.md) source-gen stays the JSON style for
  Daemon wires. It is no longer an AOT prerequisite.
- Tray icon stays `H.NotifyIcon` (the package already on the csproj). The
  right-click menu is a WPF `ContextMenu`. Close hides the window; Quit on
  the tray exits. Mutex `DevToolsDaemon_v1`, auth, gateway, and the control
  pipe are unchanged.
- Screens stay overview, hosts, and settings. They are WPF views in
  `DevTools.Daemon`, not routes in `DevTools.Web`.

## Alternatives Considered

1. **Keep MewUI so Native AOT stays open**
   ([0032](0032-daemon-mewui-and-aot.md) section 1 and 3). AOT is not shipping,
   and the UI exists to reach a publish mode this decision drops. A second
   toolkit remains for no product screen.
2. **Host the add-in SPA in the Daemon window.** One visual language, and a
   WebView2 startup on every tray launch for three simple screens. The host
   shell keeps the SPA. Daemon does not.
3. **WPF plus MahApps**, matching the old Daemon dashboard. MahApps is the
   submodule stack [0037](0037-webview2-bare-window-shell.md) removes from
   the add-in. .NET 10 already ships Fluent.
4. **Hand-merge `Fluent.xaml` and ignore `ThemeMode`.** Same theme files,
   without the backdrop and system-mode behavior `ThemeMode` applies, and
   with two sources of dictionaries when both are set.
5. **WinUI or another Fluent toolkit.** The process would take a second UI
   stack. Inbox WPF Fluent is the .NET 10 surface.

## Consequences

Positive:

- The tray process starts a WPF window. It does not start WebView2.
- Light, dark, and system theme are a platform property, including the
  window backdrop. Daemon does not share `ThemeManager` with the add-in page.
- Native AOT leaves the Daemon roadmap. Publish stays the JIT exe that ships
  today.

Tradeoffs:

- Daemon and the add-in do not share a visual tree. The add-in is Park UI in
  a browser. Daemon is Fluent WPF. Matching pixel style is not a goal.
- `ThemeMode` is experimental (`WPF0001`) and Fluent styles are still in
  progress. A later .NET can change or remove the API. The fallback if it
  is removed is the inbox `PresentationFramework.Fluent` dictionary, still
  not MahApps and still not a browser.
- `PresentationFramework` stays on the Daemon publish. That is the reason
  AOT is dropped.

## Follow-Up

- When this ADR is Accepted, mark [0032](0032-daemon-mewui-and-aot.md)
  section 1 and section 3 superseded and point them here. Leave section 2.
- Implementation removes `Aprillz.MewUI.Windows`, `MewUIBackend`, and the
  MewUI views, and adds `UseWPF` plus `ThemeMode`. That work is not a slice
  of [0042](0042-wpf-ui-migration-slices.md).
- Update `docs/architecture/MCP/daemon.md` to describe the WPF window when
  the shell lands. Until then that page describes the shipped MewUI app.
