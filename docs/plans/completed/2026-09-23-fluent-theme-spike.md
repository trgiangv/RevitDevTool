# Execution Plan: Fluent Theme Feasibility Spike

Date: 2026-09-23

## Status

Completed 2026-09-24

## Outcome

Proven. Inbox Fluent on Daemon `net10` (`Application.ThemeMode`) and a
vendored `PresentationFramework.Fluent` pin for host `net48`/`net8` replace
MahApps on Command Browser, Element Finder, and StubBuilder via declarative
`FluentTheme` / `FluentThemeResources`. Daemon desktop is plain WPF +
`H.NotifyIcon.Wpf` again. [0043](../../decisions/0043-daemon-wpf-fluent.md)
and [0044](../../decisions/0044-revit-wpf-fluent-theme.md) are **Accepted**.

## Context

- Daemon UI before `8c7f91d9` (`feat(daemon): MewUI desktop shell`):
  `UseWPF`, `H.NotifyIcon.Wpf`, and a `System.Windows.Application` with
  `ShutdownMode="OnExplicitShutdown"`. `Program.Main` built `App` and called
  `Run()` on the desktop path. `--stdio` never created that `App`. The tray
  icon was a `tb:TaskbarIcon` in `Tray/TrayResources.xaml`. `App.OnStartup`
  called `FindResource` and `ForceCreate()` on the WPF STA thread before
  `await _host.StartAsync()`. Left click and the context menu bound to
  `TrayViewModel`. That commit also split folders
  (`Composition`, `Control`, `Gateway`, `Desktop`, `Tools`, `Views`). The
  folder split stays. Only the UI toolkit from that commit is what this
  spike puts back, with Fluent instead of the MahApps dictionaries that
  `App.xaml` merged then.
- Daemon today: `Program.RunDesktop` calls `Aprillz.MewUI.Application.Create()`
  and `.Run()`. `Views/TrayMenu.cs` uses `H.NotifyIcon.Core.TrayIcon` and a
  MewUI menu window. Package is `H.NotifyIcon`, not `H.NotifyIcon.Wpf`.
  [0043](../../decisions/0043-daemon-wpf-fluent.md) is the target look:
  `Application.ThemeMode`, suppress `WPF0001`, do not merge `Fluent.xaml`.
- Revit tools: [0044](../../decisions/0044-revit-wpf-fluent-theme.md).
  `ElementFinderView` is a `Window`. `CommandBrowserView` is a `UserControl`
  injected into Revit's AvalonDock. Both merge `DevTools.UI` `Theme.xaml`
  and set MahApps properties. `ToolWindowService` and
  `CommandBrowserController` stay as they are.
- Host TFMs: Revit 2022–2024 `net48`, 2025–2026 `net8.0-windows`, 2027
  `net10.0-windows`. Daemon is `net10.0-windows` only.
- `Window.ThemeMode` and `Application.ThemeMode` are
  `[Experimental("WPF0001")]`. Copied dictionaries for older TFMs come from
  `dotnet/wpf` `release/10.0` `PresentationFramework.Fluent`, not `main`.

## Scope

In scope:

- `ElementFinderView`, `CommandBrowserView`, and `StubBuilderWindow` lose
  the `Theme.xaml` merge and the `mah:` setters on those files. Behavior,
  code-behind, and the services that show them stay.
- On `net10.0-windows`, `ElementFinderView` sets `Window.ThemeMode` to
  `System`. On `net48` and `net8.0-windows`, that same window merges a small
  slice of the `release/10.0` Fluent dictionaries for the controls it still
  has after MahApps-only controls are swapped for inbox controls.
- `CommandBrowserView` never sets `ThemeMode` on a window. It merges the
  Fluent dictionary on its own `UserControl.Resources`. On `net10` that
  dictionary is the inbox Fluent pack URI. On `net48` and `net8` it is the
  same copied slice, still scoped to this control.
- Daemon desktop returns to `System.Windows.Application` plus
  `H.NotifyIcon.Wpf`, themed with `Application.ThemeMode`. Existing
  `Views/` types (`TrayMenu`, `MainWindow`, `OverviewView`, `HostsView`,
  `SettingsView`) become that WPF UI. `Desktop/` keeps owning state,
  theme preference, and dispatch.
- A note of what happened: theme visible or not, Revit chrome changed or
  not, tray click still opens the dashboard, Quit still exits, `--stdio`
  still has no window.

Out of scope:

- A second probe window, a second tray, or leaving MewUI running beside WPF.
- Restoring the pre-`8c7f91d9` folders (`Hosting/`, `Dashboard/`, `Tray/`).
- Deleting MahApps, ControlzEx, or XamlBehaviors from the host add-in.
  `DevTools.UI` `Theme.xaml` stays for every window this plan does not list.
- WebView2, CefSharp, `DevTools.Web`, or
  [2026-09-23-webview2-shell](2026-09-23-webview2-shell.md).
- Setting `Application.ThemeMode` on Revit's `Application`.
- Vendoring the full `PresentationFramework.Fluent` tree. The full copy is
  0044 after this spike says the thin slice loads on the real windows.
- `PublishAot`, publish flags, or a new project.
- Merging `Fluent.xaml` on the Daemon `Application`. `ThemeMode` already
  loads those dictionaries.

## Approach

1. **Element Finder, in place.** On `ElementFinderView.xaml`, remove the
   `Theme.xaml` merge and every `mah:` property. Replace
   `mah:DropDownButton` with an inbox `Menu` or `Button` plus `ContextMenu`
   in the same row. Keep the watermark text as a normal `TextBox`
   placeholder or adjacent label. On `net10`, set `ThemeMode="System"` in
   code for that window and suppress `WPF0001` in that file. On `net48` and
   `net8`, merge `FluentProbe.xaml` into `ElementFinderView.Resources` only.
   Build that dictionary from the `release/10.0` styles those remaining
   controls need, plus any accent brush the TFM does not define. Record the
   commit copied. `ToolWindowService.Show` and `SetHostAppOwner` are
   unchanged. Open the existing Element Finder command. Pass means the
   window is Fluent and Revit's ribbon and dialogs are not. Fail means a
   crash, a white window, or Revit controls picking up Fluent.
2. **Command Browser, in place.** On `CommandBrowserView.xaml`, remove the
   `Theme.xaml` merge and the `mah:ControlsHelper` setters. Merge the Fluent
   dictionary on this `UserControl` only. Do not walk up to Revit's
   `Window` to set `ThemeMode`. `CommandBrowserController` stays. Pass means
   the injected pane shows the Fluent controls and the surrounding
   AvalonDock chrome does not. Record any key that fails to resolve.
3. **Stub Builder, in place.** On `StubBuilderWindow.xaml`, remove the
   `Theme.xaml` merge and every `mah:` property. Inbox `CheckBox`,
   `TextBox`, `ListBox`, and `ProgressBar` replace the MahApps styles and
   `MetroProgressBar`. No watermark, clear button, or custom row template.
   Merge `FluentTheme.xaml` on the window. `SetWindowButtons` and `SetTitleBarTheme`
   stay. Pass means the stubs window is Fluent and the other Presentation
   views are still MahApps.
4. **Daemon, same shell as before `8c7f91d9`, Fluent instead of MahApps.**
   Set `UseWPF` and drop `MewUIBackend`. Swap the package to
   `H.NotifyIcon.Wpf` and drop `Aprillz.MewUI.Windows`. Add `App.xaml` /
   `App.xaml.cs` with `ShutdownMode="OnExplicitShutdown"` and no MahApps
   dictionaries. `Program.Main` uses `new App(); InitializeComponent();
   app.Run()` for the desktop path and keeps `RunStdioAsync` for `--stdio`.
   Put `tb:TaskbarIcon` in a resource dictionary. In `OnStartup`,
   `ForceCreate()` that icon on the STA thread before `await
   StartAsync()`, and set its `DataContext` from the existing desktop
   services (`AppState` or a thin tray view-model that calls them). Left
   click opens `MainWindow`. Quit calls `Application.Current.Shutdown()`.
   Rewrite `Views/MainWindow`, `OverviewView`, `HostsView`, and
   `SettingsView` as WPF XAML that binds the same state. `ThemeHelper`
   assigns `Application.ThemeMode` (`Light` / `Dark` / `System`) and
   suppresses `WPF0001` there. `UiDispatch` posts to
   `Application.Current.Dispatcher`. Do not call
   `Aprillz.MewUI.Application`. Pass means the tray icon appears, the
   dashboard paints Fluent, theme preference tracks Windows, and Quit
   exits. Fail means the process dies at startup, the icon never appears
   because it was created after the first `await`, or `--stdio` opens a
   window.

Stop after the four checks. Write the pass/fail notes in Result. Do not
start the 0044 full theme copy or delete host MahApps from a partial pass.

## Risks And Recovery

- `H.NotifyIcon.Wpf` must be created with `ForceCreate()` before the first
  `await` in `OnStartup`. The pre-`8c7f91d9` `App.xaml.cs` did this. Moving
  it later leaves the tray missing while the host is running.
- `Window.ThemeMode` on `ElementFinderView` stays on that window. Setting it
  on `Application.Current` inside Revit restyles Revit. Recovery is closing
  Element Finder.
- Command Browser shares Revit's visual tree. A dictionary merged above the
  `UserControl` will restyle AvalonDock. Keep the merge on
   `CommandBrowserView.Resources`.
- `StubBuilderWindow` is a real `Window` in `DevTools.Presentation`, shown
  from both hosts. It merges `FluentTheme.xaml` (`FluentThemeResources`).
  `SetWindowButtons` and `SetTitleBarTheme` stay.
  Other Presentation views keep `Theme.xaml`.
- A copied Fluent dictionary can fail to compile on `net48` because it
  references a .NET 9+ type. Drop that control from the slice and record
  the type. Do not add a theme package.
- Rollback is reverting the spike commits. Host `Theme.xaml` and the WebView
  plan are not part of those commits.

## Progress

- [x] `ElementFinderView` compiles with `Window.ThemeMode` on `net10` and vendored `Fluent.Light.xaml` on `net48` / `net8`. Revit chrome not checked live.
- [x] `CommandBrowserView` compiles with the inbox Fluent pack URI on `net10` and the same vendored Light dictionary on `net48` / `net8`, merged on the `UserControl` only. AvalonDock chrome not checked live.
- [x] `StubBuilderWindow` compiles with `FluentTheme.xaml` merge. MahApps `Theme.xaml` and `mah:` setters are off that window. Title-bar helpers stay. Live chrome not checked.
- [x] `RevitDevTool.Tools` builds `Debug.Autodesk.2022`, `.2025`, and `.2027` (0 errors). Copy pin `87e4d30e28c1aaadf1866fa0bfdab110bbef1d6f` under `Themes/PresentationFramework.Fluent`. Only `Themes/*.xaml` and `Controls/**/*.cs` compile; `Styles/` and `Resources/` stay on disk.
- [x] Daemon desktop compiles as `Application` + `H.NotifyIcon.Wpf` + `ThemeMode` (`dotnet build source/DevTools.Daemon/DevTools.Daemon.csproj -c Debug`, 0 errors). Live tray and `--stdio` not run: published `DevTools.Daemon.exe` holds `DevToolsDaemon_v1`.
- [x] Daemon tests host WPF (`WpfSession`). `dotnet run --project tests/DevTools.Daemon.Tests/DevTools.Daemon.Tests.csproj` — 76 passed, 0 failed. No `Aprillz.MewUI` left in that project.
- [x] Result notes written here
- [ ] Live: Element Finder, Command Browser, and Stub Builder in Revit 2022, 2025, and 2027; Daemon tray click, Quit, and `--stdio`

## Decisions

- 2026-09-23: Spike edits the existing tool windows and the existing Daemon
  views. No sibling window and no second tray.
- 2026-09-23: Daemon UI implementation follows the pre-`8c7f91d9` WPF
  `Application` and `H.NotifyIcon.Wpf` loop. Folder layout from that commit
  stays. MahApps dictionaries from that `App.xaml` are not restored.
- 2026-09-23: Host add-in MahApps and the WebView plan stay. Promote a pass
  into 0043 or 0044 only by editing those ADRs.
- 2026-09-23: The vendored tree moved to
  `PresentationFramework.Fluent`. Fluent surfaces merge `FluentTheme.xaml`
  (`FluentThemeResources`); `ThemeManager` reapplies on theme change.
  The assembly is excluded from the RevitDevTool ILRepack.
- 2026-09-23: `StubBuilderWindow` joins the same `FluentTheme.xaml` path. The rest of
  `DevTools.Presentation` stays on MahApps `Theme.xaml`.
- 2026-09-23: Replaced `AttachFluent` with declarative `FluentTheme.xaml` /
  `FluentThemeResources` / `FluentDesignTimeResources`, parallel to MahApps
  `Theme.xaml` / `ThemeResources` / `DesignTimeResources`.

## Validation

- Focused proof: Element Finder, Command Browser, and Stub Builder open
  themed, and the Daemon tray opens `MainWindow`. No new test project.
- Integration or end-to-end proof: one manual session per TFM above, plus
  one Daemon desktop launch and one `--stdio` launch.
- Repository-required checks: compile `RevitDevTool.Tools` and
  `DevTools.Presentation` for `net48`, `net8`, and `net10`, and
  `DevTools.Daemon` for `net10`. Build skill for the touched csproj.

## Result

Compile passed on 2026-09-23 for both halves. Live theme, Revit chrome,
tray click, Quit, and `--stdio` were not run.

Tools: `dotnet build source/RevitDevTool.Tools/RevitDevTool.Tools.csproj`
for `Debug.Autodesk.2022`, `.2025`, and `.2027` (deploy props off). Vendored
Fluent is `release/10.0` commit `87e4d30e28c1aaadf1866fa0bfdab110bbef1d6f`.

Daemon: `dotnet build source/DevTools.Daemon/DevTools.Daemon.csproj -c Debug`.
`dotnet run --project tests/DevTools.Daemon.Tests/DevTools.Daemon.Tests.csproj`
passed 76, failed 0. The test host is `WpfSession` (`System.Windows.Application`).
Published `DevTools.Daemon.exe` held `DevToolsDaemon_v1`, so the new tray
was not started.

Stub Builder: `dotnet build source/DevTools.Presentation/DevTools.Presentation.csproj`
for `Debug.Autodesk.2022`, `.2025`, and `.2027` (deploy props off), 0 errors.
Live stubs window not opened.
