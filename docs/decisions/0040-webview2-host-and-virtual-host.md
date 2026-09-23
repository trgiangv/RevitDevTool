# 0040 WebView2 Host, Virtual Host, and CDP Port

Date: 2026-09-23

## Status

Proposed

Updated 2026-09-24: every host year (including Revit 2025) uses WebView2.
CefSharp scheme handlers, user-data folders, and CDP port 19227 are
withdrawn. Every `CoreWebView2Environment` is created with
`AdditionalBrowserArguments` including
`--edge-webview-unique-window-class` so WebView2 does not register the same
Win32 class names as Revit 2025's in-process CEF
(`Chrome_WidgetWin_0`). Proven approach:
[pyRevit#3592](https://github.com/pyrevitlabs/pyRevit/pull/3592),
[AnalyseTool#139](https://github.com/Nikola1Davydov/AnalyzeTool/pull/139).
Autodesk documents the crash when Manage Links opens after another add-in
has started WebView2 without that flag.

Updated 2026-09-23: `WebViewWindow`, session, and transport are types in
`DevTools.UI`. The standalone exe references that project. The release URL
stays `https://app.local`.

Requires [0037](0037-webview2-bare-window-shell.md) and the app in
[0038](0038-webview-react-parkui-codemirror.md). Uses the envelope from
[0039](0039-webview-json-bridge.md). The standalone exe is enough to prove
this decision; the Revit and AutoCAD add-ins keep their current WPF entry
until [0042](0042-wpf-ui-migration-slices.md) switches it.

## Context

A CAD host that ships UI files navigates WebView2 to a Vite URL in Debug
and to a virtual HTTPS host over a local `dist` folder in Release.
`file://` breaks ES module loading. A host that does not ship UI files
navigates to a configurable HTTPS URL instead.

Create the browser when the window is first visible
(`IsVisibleChanged` → `Dispatcher.BeginInvoke` at application idle) so the
control exists before `EnsureCoreWebView2Async`. Delaying creation also
avoids startup clashes with other add-ins. Set an explicit user-data
folder. A shared folder such as `C:\temp`, or one folder for every window
under local app data, fails the second environment when two processes
open the control. WebView2 also refuses a second environment on the same
folder when `AdditionalBrowserArguments` (or other options) differ.

The NuGet API is already centralized: `Microsoft.Web.WebView2` `1.0.4191.47`
in `Directory.Packages.props`. Host csprojs reference it and do not call it.

## Decision

- `DevTools.UI` contains the bare `WebViewWindow` and the command dictionary.
  `DevTools.UI.WebView2` contains the WebView2 child, `WebViewSession` for
  that runtime, and `WebViewTransport`. Every Revit year and the AutoCAD
  family reference that project. No CefSharp surface. No MahApps type on
  this window, no ribbon, no overlay controls. A solid window background is
  set in code so the HWND does not flash white.
- Environment creation is one helper (for example
  `WebView2Runtime.CreateEnvironmentAsync`) used by every window that starts
  WebView2. It always sets
  `CoreWebView2EnvironmentOptions.AdditionalBrowserArguments` to include
  `--edge-webview-unique-window-class` (and Debug CDP args below). The flag
  is applied on every host year, including years with no CEF conflict, so
  options stay identical wherever the same user-data folder layout is used.
- The control is created when the window is visible, on the dispatcher, then
  `EnsureCoreWebView2Async` runs. Bindings register before navigation.
  Navigation runs after `host.ready` can be posted (navigation-completed, or
  immediately after navigate if the page is ready).
- Start URL:
  - Debug: `http://127.0.0.1:5173/` plus the hash route.
  - Release: `https://app.local/index.html` plus the hash route. `<dist>` is
    `{add-in dir}/ui/web`, copied from `source/DevTools.Web/dist`.
  - WebView2 maps that host with
    `SetVirtualHostNameToFolderMapping("app.local", <dist>, Allow)`.
- User-data folder, one per process:

  `%LocalAppData%\RevitDevTool\WebView2\{hostApp}\{hostVersion}\{pid}`

  Standalone uses `hostApp = standalone` and omits a CAD version. Product
  settings that must survive a new PID stay in `DevTools.Settings`. The SPA
  does not treat `localStorage` as the settings database.
- CDP:
  - Debug WebView2 (add-in and the test exe): `--remote-debugging-port=19226`
    (combined with the unique-window-class flag in the same
    `AdditionalBrowserArguments` string).
  - Standalone tests: an ephemeral WebView2 port passed with
    `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` (or the environment helper),
    not 19226. That path must still include
    `--edge-webview-unique-window-class` when the same options contract is
    shared.
  - Release: remote debugging off.
- Missing WebView2 Evergreen runtime: the window shows a short text block
  and does not throw out of the host command. The check runs before
  `EnsureCoreWebView2Async`.
- Debug enables browser DevTools (F12 and a startup open). Release disables
  the default context menu and zoom-by-gesture.
- `Microsoft.Web.WebView2` stays on the existing CPM pin. The package
  reference moves from `RevitDevTool.csproj` and `ACadDevTool.csproj` onto
  `DevTools.UI.WebView2.csproj`. Every host year references that project.
  There is no CefSharp package reference.
- An exe `tests/DevTools.UI.Standalone` boots `WebViewWindow` with the
  `host.echo` command from [0039](0039-webview-json-bridge.md) and no CAD
  API. That exe is the proof of this ADR.

## Alternatives Considered

1. **Production UI loaded from a public URL.** Useful for a hosted page.
   Debug already has a local path (`127.0.0.1:5173`). Release stays on the
   virtual host so an air-gapped machine still opens the tool.
2. **One shared user-data folder** under `%LocalAppData%\RevitDevTool`.
   Breaks when two Revit sessions, or Revit and AutoCAD, open the window.
3. **PID-less folder plus a mutex** that refuses the second window. The
   second session is a supported layout (two host PIDs). Isolating by PID is
   the compatible choice; durable settings live in `DevTools.Settings`.
4. **Create WebView2 in `OnStartup` of the add-in.** Pays browser startup
   cost before the user opens the tool and races other add-ins. Creation
   stays on the window that the user opened.
5. **MahApps `MetroWindow` as the shell.** The bare window has nothing for
   Metro chrome to host, and a Metro dialog over the HWND is the airspace
   bug this shell avoids.
6. **CefSharp on Revit 2025 (withdrawn).** Avoided the class-name clash by
   never loading WebView2 in that process. The unique-window-class flag
   removes the clash without a second browser stack
   ([0037](0037-webview2-bare-window-shell.md)).
7. **Apply unique-window-class only on Revit 2025.** Different options on
   the same user-data folder fail the second environment. One helper, one
   options string, every year.

## Consequences

Positive:

- Debug HMR is a running Vite process. Release is files beside the add-in.
- The standalone exe is the same `WebViewWindow` the hosts will show later.
- CDP is on in Debug without a code change when someone attaches Playwright.
- Revit 2025 Manage Links (and similar CEF dialogs) stay usable after the
  product UI has started WebView2.

Tradeoffs:

- `localStorage` and cookies do not follow the user to the next host PID.
  Anything that must persist is a `DevTools.Settings` command.
- Virtual host name `app.local` is reserved by this app on that WebView2
  environment. Extension pages, if added later, need their own host name and
  a new ADR.
- Hash routes are part of the URL ([0038](0038-webview-react-parkui-codemirror.md)).
  A release build that forgets the `ui/web` copy navigates to a blank
  virtual host; the build must fail if `dist/index.html` is missing when
  the add-in is packed. Until a host command shows `WebViewWindow`, only the
  standalone csproj enforces that copy.
- All windows that share a profile folder must go through the same
  environment helper. A second code path that omits the unique-window-class
  flag is a defect.

## Follow-Up

- Wire the add-in command to `WebViewWindow` only in the entry-switch slice
  of [0042](0042-wpf-ui-migration-slices.md).
- Record the Evergreen runtime minimum in `docs/agents/` when the check text
  is real, not in this ADR.
