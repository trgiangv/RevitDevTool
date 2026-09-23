# 0039 WebView Bridge Is a JSON Envelope over postMessage

Date: 2026-09-23

## Status

Proposed

Updated 2026-09-23: the envelope stays the contract after the Revit 2025
CefSharp constraint. Pure `postMessage` does not exist on CefSharp.
Reflection COM on both browsers was re-checked per use case and refused.
The Cef return path is one `deliver(json)` method.

Requires [0037](0037-webview2-bare-window-shell.md) for where the transport
sits. The contract tests in this decision do not need a window: C# round-trips
JSON, and Vitest round-trips the same fixtures.

## Context

Two bridge styles are in use on CAD hosts:

- A JSON envelope (`type`, `command`, `payload`, `id`, `error`) through
  `chrome.webview.postMessage`. The page never receives a COM object. The
  transport enqueues the message. It does not call the dispatcher itself.
- A COM host object via `AddHostObjectToScript` (or CefSharp `Register`).
  JavaScript calls `RunMethod(methodName, requestId, argsJson)`. The bridge
  reflects onto a binding. Results come back through `ExecuteScript` because
  large JSON in a script string breaks escaping. That shape freezes the
  method list at whatever is public.

RevitDevTool already uses explicit JSON contracts on pipes (`BridgeMessage`,
MCP). [0031](0031-daemon-json-source-gen.md) standardizes System.Text.Json on
the Daemon. Host add-ins are not AOT, but a second serializer for the UI
splits the platform.

CAD API calls already marshal through `IHostContextExecutor` in
`DevTools.Execution.Abstractions`. `DevTools.UI` must not take a
reference on Execution to use that type.

Revit 2025 cannot host WebView2 ([0037](0037-webview2-bare-window-shell.md)).
CefSharp has `CefSharp.PostMessage` for page→host and no
`PostWebMessageAsJson` for host→page. A pure postMessage stack therefore
does not exist on that year. The alternative that hides the difference is
reflection `RunMethod` on both browsers. These use cases are why
that alternative loses:

| Use case | Reflection COM on both | JSON envelope |
|----------|------------------------|---------------|
| Request/response (`host.echo`, settings, run) | Works. JS `await`s a host method. Cef and WebView2 can share `RunMethod`. The method list is whatever is `public`. A rename ships as a runtime miss. | Works. One `id`, one `response`. A bad `command` is `unknown_command`. WebView2 is postMessage both ways. Cef page→host is `CefSharp.PostMessage`. |
| Log stream and linkify ([0038](0038-webview-react-parkui-codemirror.md)) | Host→page is `ExecuteScript` per line or per batch. Escaped JSON and dispatcher starvation show up on this path. Clicks are a natural method, and they are the small direction. | WebView2 pushes with `PostWebMessageAsJson`. Cef pushes with one `deliver(json)` COM string, not a script literal. Clicks are `request` messages. Batches are required on Cef either way; the envelope makes the batch a `payload`, not a new method. |
| Progress and cancel | Progress is another `ExecuteScript`. Cancel is a method, easy to call and easy to forget in the reflection list. A 30–60s JS timeout fires while the host is still in one CAD turn. | `progress` repeats on the same `id`. `cancel` is a message. The CAD turn still finishes before the token is observed. That limit is the executor, not the pipe. |
| Events (`host.ready`, `host.theme`) | Each event is a script call. Theme updates while the dispatcher is busy need a raised dispatcher priority. | Same message type as everything else. `ThemeManager` does not know which browser is attached. |
| Large payload | A returned string and an `ExecuteScript` string both hit practical size limits. Escaping makes the script path fail first. | WebView2 JSON post and Cef `deliver(string)` both hit size limits. Chunk with `progress` or a paged command. Do not interpolate the chunk into script source. |
| Calls in flight | `requestId` works if every method remembers to take one. A direct `Task`-returning COM method also works and blocks the UI thread until it reaches the first await. | `id` is required. The handler returns before the CAD hop finishes, so the UI thread is not held for the whole call. |
| UI-thread deadlock | A COM call that waits on the CAD thread while that thread waits on the dispatcher deadlocks inside the call. | The response is a later message. Deadlock moves to the executor hop. Nested hops stay forbidden. |
| Tests without a CAD process | The fake must expose host objects and every method. Cef and WebView2 fakes differ. Fixtures are not the contract. | Vitest and the C# test read one JSON fixture. Playwright installs a fake pipe. Lane 3 is WebView2. Lane 4 on Revit 2025 is Cef CDP against the same `invoke`. |
| One SPA on AutoCAD, Revit except 2025, and Revit 2025 | Every host pays reflection and `ExecuteScript` so Revit 2025 can share a method list. | AutoCAD and Revit 2022, 2023, 2024, and 2026 onward use real postMessage. Only Revit 2025 pays `deliver`. Features do not branch. Daemon is not this SPA ([0043](0043-daemon-wpf-fluent.md)). |
| Navigation and HMR | Host objects must be registered again after navigation. A missed rebind looks like a missing feature. | The page re-subscribes on load. The host re-posts `host.ready`. Cef re-binds the single `devtoolsBridge` object in that same ready step. |
| Accidental API | New `public` methods become callable from the page. | The page can call only names registered in the dictionary. |

COM is the better call shape when the surface is a handful of coarse methods,
host→page traffic is rare, and both browsers must look like one typed object.
This product's hot path is the opposite: log lines and progress flow
host→page, on the add-in hosts, and only Revit 2025 lacks `PostWebMessageAsJson`.
Daemon is not one of those hosts ([0043](0043-daemon-wpf-fluent.md)).

## Decision

- The envelope is the contract. WebView2 carries it on
  `window.chrome.webview.postMessage`, `CoreWebView2.WebMessageReceived`, and
  `PostWebMessageAsJson`. Revit 2025 carries the same JSON on CefSharp:
  the page calls `CefSharp.PostMessage`, and the host calls one bound method
  `deliver(json)` on a single object `devtoolsBridge`. That object has no
  business methods. `RunMethod` reflection and per-feature
  `AddHostObjectToScript` bindings are not the contract on either runtime.
  `src/bridge/runtime.ts` is the only module that branches on which pipe
  exists. Features call `invoke`.
- The envelope is one JSON object, camelCase, serialized with
  System.Text.Json. Fields:

  | Field | Role |
  |-------|------|
  | `type` | `request`, `response`, `cancel`, `event`, `progress`, `ping`, `pong` |
  | `id` | Correlation id. Required on `request`, `response`, `cancel`, `progress`. Omitted on broadcast `event`. |
  | `command` | Dotted name (`execution.run`). Required on `request`, `event`, `progress`. |
  | `payload` | JSON value. Absent when there is nothing to send. |
  | `error` | Present on a failed `response`: `{ "code": string, "message": string }`. |

  Adding a field is compatible. Renaming or removing a field is a new ADR.
- The page sends `request` and `cancel` and `ping`. The host sends
  `response`, `event`, `progress`, and `pong`. A `request` produces exactly
  one `response` for that `id`. `progress` may repeat for the same `id`
  before the response.
- Commands are a dictionary of named handlers registered by the host
  composition root. `DevTools.UI` looks up the name and invokes the
  handler. It does not reflect over public methods. An unknown command
  returns `error.code = "unknown_command"`.
- Handler shape, defined in `DevTools.UI`:

  ```csharp
  interface IUiCommand
  {
      string Name { get; }
      ValueTask<UiPayload> ExecuteAsync(UiCall call, CancellationToken cancellationToken);
  }
  ```

  `UiCall` carries `id`, `command`, and the raw payload element. Host
  projects implement commands and, inside those implementations, call
  `IHostContextExecutor` when they touch the CAD model. One executor hop per
  request. Nested hops are a defect.
- After navigation the host posts `event` / `host.ready` with payload
  `{ "host": string, "version": string, "capabilities": string[] }`. The SPA
  hides commands whose capability is missing. Capabilities are additive.
- `ThemeManager` keeps raising `ActualApplicationThemeChanged`. The window
  transport turns that into `event` / `host.theme`, including once after
  navigation. `ThemeManager` does not reference WebView2.
  Payload: `{ "preference": "light" | "dark" | "auto", "theme": "light" | "dark" }`.
  `theme` is the resolved mode the page applies. `preference` is the saved
  `AppTheme`. The page does not call back into WPF to restyle controls.
- `ping` / `pong` exist so a pane can show that the host dispatcher is
  blocked. The host does not emit a timer. The page may ping while a request
  is in flight. No 100 ms idle poll.
- The TypeScript client lives at `source/DevTools.Web/src/bridge/`.
  `runtime.ts` is the only file that touches `chrome.webview` or `CefSharp`.
  Features call `invoke(command, payload, { signal, onProgress })`.
- Shared JSON fixtures live in `source/DevTools.Web/bridge-fixtures/`. The C#
  contract test project and Vitest both read those files. A fixture change
  fails both sides.
- Incoming JSON is parsed with `zod` (`4.6.5`,
  [0038](0038-webview-react-parkui-codemirror.md)) inside `src/bridge/`
  before a feature sees it. The envelope schema checks `type`, and `id`,
  `command`, and `error` where that `type` requires them. A message that
  fails parse is not delivered. The bridge client reports it as a local
  error. It does not throw across the page.
- A command that has a fixture also has a payload schema next to that
  fixture. Vitest runs `schema.parse` on the file. The C# test still reads
  the same JSON. Zod is not generated from C#, and C# is not generated from
  Zod.
- Log and progress messages parse once per batch. Zod does not run per
  line. After a batch parses, the bridge appends with `EditorView.dispatch`
  on the view from [0038](0038-webview-react-parkui-codemirror.md). That
  append does not call `setState`.

## Alternatives Considered

1. **`RunMethod` reflection on both browsers.** One reflection ABI would
   hide the CefSharp difference.
   The method list would be whatever reflection sees, and the result would
   still come back through `ExecuteScript`. Revit 2025 uses Cef only as the
   pipe for the same envelope: `CefSharp.PostMessage` in, `deliver(json)` out.
2. **One COM object per feature** (`executionBinding`, `settingsBinding`, …).
   Same transport problem, more host objects for the page to probe.
3. **Reuse the pytest `BridgeMessage` pipe inside the page.** The page is
   in-process with the add-in. A named pipe from the WebView to the same
   process adds a second connection for a call that already has a message
   callback.
4. **Newtonsoft.Json for the envelope.** Host UI JSON stays on
   System.Text.Json, consistent with [0031](0031-daemon-json-source-gen.md).
5. **A single string `error` field.** A code plus message lets the SPA
   branch without parsing English text.
6. **TypeScript types only, no runtime parse.** Types are gone at runtime.
   A bad `type` or a missing `id` would fail inside a feature. `zod` fails
   in `src/bridge/` instead.
7. **Generate one side from the other** (C# records from Zod, or Zod from
   C#). Both sides already read the same fixture file. A generator is a
   third copy of the contract.

## Consequences

Positive:

- The contract is testable with no WebView2, no Revit, and no AutoCAD.
- Playwright can install a fake pipe that speaks the same envelope
  ([0041](0041-webview-playwright-cdp-tests.md)). The fake is
  `chrome.webview` or `CefSharp.PostMessage`; `invoke` does not change.
- Host types never cross the boundary. The SPA sees command names and JSON.
- Revit 2022, 2023, 2024, 2026 onward, and AutoCAD do not take a
  COM business API in order to match Revit 2025.

Tradeoffs:

- Cancellation reaches the handler token only. A Revit or AutoCAD API call
  already inside one executor turn runs until that turn returns; the
  response is then `error.code = "canceled"` if the handler observed the
  token. The CAD turn is the limit.
- `ping` measures the WPF dispatcher of the bare window, not the CAD main
  thread. A stalled CAD thread still answers `pong` if the dispatcher is
  free. Stall UI for the CAD thread is `progress` that stops, not ping.
- Command names are strings. The fixture set is what keeps TypeScript and C#
  aligned; there is no generated OpenAPI step in this decision.
- Revit 2025 host→page is `deliver(json)`, a COM string. It does not escape
  through script source, and it is still a Cef-only push. Log and progress
  on that year go in batches. A batch that is too large is a Cef limit, and
  switching the commands to `RunMethod` does not remove it.

## Follow-Up

- First commands may be `host.ready` (event) and `host.echo` (request) so
  [0040](0040-webview2-host-and-virtual-host.md) has a round-trip before any
  product feature moves.
- Product command names are added by the slice that implements them
  ([0042](0042-wpf-ui-migration-slices.md)), each with a fixture.
