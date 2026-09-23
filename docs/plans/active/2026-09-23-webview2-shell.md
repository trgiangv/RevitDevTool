# Execution Plan: WebView2 Product Shell

Date: 2026-09-23

## Status

Active

## Outcome

The host add-in UI is one React app in one bare WPF window. Revit 2025
hosts it in CefSharp. Revit 2022, 2023, 2024, 2026 onward, and the AutoCAD
family host it in WebView2. MahApps, ControlzEx, Xaml Behaviors, Scintilla,
and WinForms hosting are gone. Daemon UI is a separate decision
([0043](../../decisions/0043-daemon-wpf-fluent.md)).

Choices live in the decisions below. This plan only tracks slice order and
proof. Do not restate the policy here.

## Context

- Shell: [0037](../../decisions/0037-webview2-bare-window-shell.md)
- Frontend: [0038](../../decisions/0038-webview-react-parkui-codemirror.md)
- Bridge: [0039](../../decisions/0039-webview-json-bridge.md)
- Host and virtual host: [0040](../../decisions/0040-webview2-host-and-virtual-host.md)
- Tests: [0041](../../decisions/0041-webview-playwright-cdp-tests.md)
- Slices: [0042](../../decisions/0042-wpf-ui-migration-slices.md)
- Daemon is not this plan: [0043](../../decisions/0043-daemon-wpf-fluent.md)

## Scope

In scope:

- `source/DevTools.Web` (frontend) and the bare window inside `source/DevTools.UI`
- Standalone exe `tests/DevTools.UI.Standalone` and the four test lanes
- Replacing Presentation and host shell XAML, then Revit Tools WPF
- Removing MahApps, ControlzEx, and XamlBehaviors submodules, custom WPF
  controls, Scintilla (`ZLogger.Scintilla`, `Scintilla5.NET`), and WinForms
  hosting (`ElementHost`, `UseWindowsForms`)
- Log surface in `DevTools.Web` (CodeMirror 6 linkify + render)
- CefSharp only in the Revit 2025 build of `RevitDevTool`

Out of scope:

- Daemon UI, including a WebView2 or CefSharp window inside `DevTools.Daemon`
  ([0043](../../decisions/0043-daemon-wpf-fluent.md))
- Native AOT of Daemon (dropped by 0043, not deferred here)
- CefSharp on any host add-in except the Revit 2025 build (Revit 2022, 2023,
  2024, 2026 onward, and the AutoCAD family stay on WebView2)
- A hosted production URL for the SPA
- A CodeMirror code editor for scripts

## Approach

Land the scaffold and the standalone window before any add-in entry changes.
Feature slices A–C talk to the standalone exe. Slice D switches the add-in
command once those routes exist. F follows D. Command Browser and Element
Finder stay WPF under [0044](../../decisions/0044-revit-wpf-fluent-theme.md);
they are not a slice of this plan.

## Risks And Recovery

- WebView2 Evergreen missing on a machine: the window shows the runtime
  message from 0040; lane 3 skips. Recovery is installing the runtime, not a
  CefSharp fallback.
- Slice D before A–C: the daily tool loses `MainPage` with no route. Do not
  merge D until A–C are in.
- Revit 2025 WebView2 runtime: do not "fix" that year onto WebView2. Recovery
  is the CefSharp surface, not a newer Evergreen install.
- Rollback of an unmerged slice is dropping that branch. After D, rollback is
  reverting the entry-switch commit together with the deleted XAML, not a
  runtime flag.

## Progress

- [ ] 0038 — `source/DevTools.Web` scaffold with bun, typecheck, Vitest, Playwright on port 5174
- [ ] 0039 — envelope fixtures, C# contract test, TS client, `host.echo`
- [ ] 0040 — bare `WebViewWindow` in `DevTools.UI` + standalone exe loads the SPA
- [ ] 0041 lane 3 — MSTest CDP against `tests/DevTools.UI.Standalone`
- [ ] 0042 A — execution route replaces `ExecutionView`
- [ ] 0042 B — settings routes
- [ ] 0042 C — CodeMirror log + linkify; delete Scintilla packages
- [ ] 0042 D — add-in command opens `WebViewWindow`; drop `ElementHost` and `UseWindowsForms`
- [x] 0044 — Fluent on `RevitDevTool.Tools`: declarative `FluentTheme` (inbox on net10, vendored `release/10.0` on net48/net8); spike [completed](../completed/2026-09-23-fluent-theme-spike.md)
- [ ] 0042 F — remove MahApps, ControlzEx, and XamlBehaviors submodules; strip custom WPF; delete empty Presentation
- [ ] Mark 0037 Accepted; update `docs/agents/host-boundaries.md`

## Decisions

- 2026-09-23: Lasting choices are 0037–0042 (Proposed). Nothing in this plan
  overrides them.
- 2026-09-23: Bridge stays in `DevTools.UI`. Frontend stays `DevTools.Web`.
  No `DevTools.WebView` project. `ThemeManager` forwards theme to the page.
- 2026-09-23: Remove submodules `libs/MahApps.Metro`, `libs/ControlzEx`, and
  `libs/XamlBehaviorsWpf` together. CodeMirror 6 renders and linkifies logs.
  It is not a code editor.
- 2026-09-23: Revit 2025 hosts the SPA in CefSharp. `DevTools.UI` references
  neither WebView2 nor CefSharp, so that process does not load the WebView2
  runtime. WebView2 lives in `DevTools.UI.WebView2` for Revit 2022, 2023,
  2024, 2026 onward, and the AutoCAD family. Daemon does not reference it.
- 2026-09-23: Frontend package manager is bun `1.4.2`, matching `docs/`.
- 2026-09-23: Daemon is [0043](../../decisions/0043-daemon-wpf-fluent.md)
  (**Accepted**): plain WPF, .NET 10 Fluent `ThemeMode`, no WebView2, no
  Native AOT. It is not a slice of this plan.
- 2026-09-23: Command Browser and Element Finder stay in `RevitDevTool.Tools`.
  [0044](../../decisions/0044-revit-wpf-fluent-theme.md) (**Accepted**):
  inbox Fluent on Revit 2027, vendored `PresentationFramework.Fluent` on
  earlier years via `FluentTheme`. Not a SPA route.

## Validation

- Focused proof: per slice, the lane named in that ADR (Vitest, Playwright
  5174, or standalone CDP).
- Integration or end-to-end proof: lane 3 smoke before D; lane 4 only after D
  and only outside an MTP host run.
- Repository-required checks: touched csproj compile from the build skill;
  `bun run typecheck` and `bun test` in `source/DevTools.Web`.

## Result

Not started.
