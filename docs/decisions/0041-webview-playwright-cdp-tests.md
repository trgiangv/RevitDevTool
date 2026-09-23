# 0041 Web UI Tests Are Vitest, Playwright, and CDP

Date: 2026-09-23

## Status

Proposed

Updated 2026-09-23: lane 3 launches `tests/DevTools.UI.Standalone`.
That exe is the WebView test host, not `DevTools.Daemon`. Daemon UI is
[0043](0043-daemon-wpf-fluent.md). Lane 3 stays on WebView2. Revit 2025
lane 4 attaches to CefSharp CDP port 19227.

Lanes 1 and 2 need only [0038](0038-webview-react-parkui-codemirror.md) and
the fixtures from [0039](0039-webview-json-bridge.md). Lane 3 needs the
standalone exe from [0040](0040-webview2-host-and-virtual-host.md). Lane 4
waits until a host add-in actually shows the window
([0042](0042-wpf-ui-migration-slices.md)).

## Context

A React page inside WebView2 is tested at more than one level: Vitest in
jsdom, Playwright against Vite with a mocked host, a standalone WPF exe
driven with `chromium.connectOverCDP`, and an optional live host session.
In-host test runs and WebView2 Playwright do not share a CAD process.

This repo's in-host tests are Microsoft Testing Platform (NUnit/TUnit), and
in-repo unit tests are MSTest.Sdk ([0034](0034-execution-mstest-sdk-scoped-tests.md),
[0035](0035-mstest-sdk-repo-tests.md)). The window under test is the add-in
SPA. WPF UI automation is the wrong tool for that page. Daemon is not this
UI ([0043](0043-daemon-wpf-fluent.md)). A page with no browser test lane
leaves the WebView host unproven.

## Decision

Four lanes. Each lane has one question. A higher lane does not replace a
lower one.

| Lane | Where | Proves | Needs CAD? |
|------|--------|--------|------------|
| 1. Vitest 5.0.1 | `source/DevTools.Web` | Bridge client, fixtures, editor setup | No |
| 2. Playwright Chromium | Vite on port **5174**, `page.addInitScript` fake `chrome.webview` | Visible UI flows against the envelope | No |
| 3. Standalone CDP | `tests/DevTools.UI.Standalone` + MSTest.Sdk | Real WebView2, real `WebViewTransport`, ephemeral CDP port | No |
| 4. Live host CDP | Opt-in, dedicated run | The add-in window and host executor together. Revit 2025 uses Cef CDP | Yes |

- Lane 2 uses `@playwright/test` `1.63.0`. Selectors hit visible controls.
  Calling `invoke()` from the test is allowed only in a bridge-contract spec,
  not as the way a feature spec presses the UI.
- Lane 3 is an MSTest.Sdk project, same SDK and coverage collector as
  [0035](0035-mstest-sdk-repo-tests.md). It starts the standalone exe, reads
  the CDP endpoint from the process output, and connects with Playwright.
  One standalone process per test class. The suite does not share a WebView2
  user-data folder across processes ([0040](0040-webview2-host-and-virtual-host.md)).
- Lane 4 sets `DEVTOOLS_CDP_ENDPOINT`. WebView2 debug default is port
  `19226`. Revit 2025 CefSharp default is port `19227`
  ([0040](0040-webview2-host-and-virtual-host.md)). The lane is skipped when
  the variable is absent. It is not part of `dotnet test` for the repo and
  not part of an MTP host run.
- In-host MTP and lane 4 never share a CAD process. Lane 3 never starts
  Revit or AutoCAD.
- FlaUI, WPF-MCP, and MewUI automation are not how the host SPA is tested.
  Daemon UI is out of these lanes ([0043](0043-daemon-wpf-fluent.md)).

A change to the envelope fixtures must pass lane 1 and the C# fixture test
from [0039](0039-webview-json-bridge.md) before lane 3 is expected to pass.

## Alternatives Considered

1. **Playwright only, against the live host.** Every UI edit would need a
   licensed CAD session. Lanes 1–3 exist so that session stays optional.
2. **CDP only, skipping the mocked Chromium lane.** Failures inside the real
   WebView2 mix DOM bugs with transport bugs. Lane 2 isolates the DOM.
3. **xUnit or NUnit for the standalone integration project.** New in-repo
   tests use MSTest.Sdk.
4. **Drive the WebView with the WPF automation tools** already used elsewhere
   on the machine. The product surface is the DOM inside the browser, which
   those tools do not see.

## Consequences

Positive:

- Lanes 1 and 2 run on any agent machine with Node. Lane 3 needs the WebView2
  Evergreen runtime and a Windows desktop session, not a CAD license.
- The same Playwright API covers the fake browser and the real WebView2, so
  a flow can move up a lane without a new harness.

Tradeoffs:

- Lane 3 is slower and serial per WebView2 process. Keep it to transport and
  one smoke flow per feature; leave exhaustive clicks on lane 2.
- Lane 4 evidence is local and opt-in. A green CI run does not claim the
  Revit HWND path was exercised.
- Two Playwright configs (port 5174 vs CDP attach) have to stay in the Web
  package so the port split in [0038](0038-webview-react-parkui-codemirror.md)
  does not get "fixed" back to 5173.

## Follow-Up

- Add a row to `docs/agents/test-matrix.md` when lane 3 exists, including the
  Skip when Evergreen or a desktop session is missing.
- Lane 4 gets a short `docs/agents/` note only after the add-in shows
  `WebViewWindow`.
