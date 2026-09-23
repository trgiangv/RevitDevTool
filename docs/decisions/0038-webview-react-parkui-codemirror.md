# 0038 Web UI Is React, Vite, Park UI, and CodeMirror 6

Date: 2026-09-23

## Status

Proposed

Updated 2026-09-23: `source/DevTools.Web` is the frontend package only. The
WPF bridge is `DevTools.UI` ([0037](0037-webview2-bare-window-shell.md)).
Package manager is bun, the same major the Docusaurus site already pins.
CodeMirror 6 renders the log and linkify. It is not a code editor. The log
document stays outside React render. There is no application store. `zod`
parses bridge JSON only.

Requires [0037](0037-webview2-bare-window-shell.md). Implementable with no
C# host: the app builds, typechecks, and serves from Vite alone.

## Context

[0037](0037-webview2-bare-window-shell.md) moves product UI into a browser
control. A CAD browser shell can be Vue, Nuxt, or React. This product is
React, and the test ladder is Vitest plus Playwright and CDP.

Scintilla is the log control, not an editor. `ScintillaLogViewerWpf` sits
behind `MonitorLogTarget`. `RevitLinkifier` implements `ILinkifier` from
`ZLogger.Scintilla` and turns element tokens into click actions.
`Scintilla5.NET` copies the native DLL on `net48`. `PythonJsonSerializer`
implements `ICustomSerializer` from the same package. None of that is a
code editor. The goal is to delete Scintilla and the WPF/WinForms around
it. CodeMirror 6 is the log component for the new web stack: render the
text and linkify tokens. Script editing is not a goal of this component.

Package versions below are `npm view <pkg> version` on 2026-09-23. The
lockfile committed with the scaffold is the pin. Do not float majors past
that lockfile without updating this ADR.

## Decision

- The frontend lives at `source/DevTools.Web/` as one package (name
  `@revitdevtool/web`). That folder is the SPA only: no WPF window, no
  WebView2 transport, no `ThemeManager`. bun `1.4.2` is the package
  manager, matching `packageManager` in `docs/package.json`. One lockfile
  (`bun.lock`). A workspace waits until a second frontend package exists.
  Do not introduce pnpm or npm for this app.
- Runtime and build:

  | Package | Version |
  |---------|---------|
  | `react`, `react-dom` | 19.3.0 |
  | `vite` | 8.3.0 |
  | `@vitejs/plugin-react` | 6.1.1 |
  | `typescript` | 7.0.2 |
  | `vitest` | 5.0.1 |
  | `@playwright/test` | 1.63.0 |
  | `react-router` | 8.4.0 |
  | `zod` | 4.6.5 |

- UI kit is Park UI: components are source added with `@park-ui/cli` `1.0.1`
  and committed under `src/components/ui`. The versioned libraries are
  `@ark-ui/react` `5.39.2`, `@pandacss/dev` `1.12.1`, and `lucide-react`
  `1.47.0`. Do not wrap Park in a second component library.
- Routing is hash history so the release virtual host
  ([0040](0040-webview2-host-and-virtual-host.md)) does not need SPA
  fallbacks. `react-router` `8.4.0` is the pin (`npm view` on 2026-09-23).
- CodeMirror 6 is the log surface, at `source/DevTools.Web/src/log/`.
  It renders monitor text for the host add-in and linkifies tokens in that
  text. A click on a token calls the bridge; it
  does not run a `ZLogger.Scintilla` `ILinkifier`. The component is not a
  code editor: no script-language modes as a product surface, no
  `@codemirror/lang-javascript` or `@codemirror/lang-python` unless a later
  ADR adds them for a different reason.
  `ScintillaLogViewerWpf`, `Scintilla5.NET`, and `ZLogger.Scintilla` leave
  in the same slice that this log route replaces the monitor
  ([0042](0042-wpf-ui-migration-slices.md)). `PythonJsonSerializer` remains
  and drops the `ICustomSerializer` base type from that package.
- React does not own the log document. `EditorView` is created once on a
  `ref` when the log route mounts and destroyed when that route unmounts.
  A batch from the bridge calls `EditorView.dispatch` on that view.
  Log text is not a React prop, not React children, and not state that
  would re-render the page. A `host.theme` change reconfigures a
  compartment on the same view. It does not construct a new `EditorView`.
  A token is a CodeMirror decoration. The click handler calls the bridge.
  It is not a React node per token. CodeMirror draws the visible lines
  itself. Do not add `@uiw/react-codemirror` or any wrapper whose `value`
  prop writes the document back through React. Chrome around the log
  (tree, toolbar, settings) stays normal React.

  | Package | Version |
  |---------|---------|
  | `codemirror` | 6.0.2 |
  | `@codemirror/view` | 6.43.13 |
  | `@codemirror/state` | 6.7.6 |
  | `@codemirror/language` | 6.12.4 |
  | `@codemirror/commands` | 6.11.1 |
- Feature folders own their state (`src/features/<feature>/`). There is no
  application-wide store and no store package: not `zustand`, Redux, Jotai,
  or `@tanstack/react-query`. Host data is the bridge in
  [0039](0039-webview-json-bridge.md). The bridge client keeps in-flight
  `id`s. A feature keeps drafts in component state. The open screen is the
  hash route. Log text lives in the CodeMirror document, not in React state.
- `zod` `4.6.5` parses bridge JSON only, at `src/bridge/`. It is not used
  for forms and it is not a cache. The parse rules are
  [0039](0039-webview-json-bridge.md).
- Color mode comes from the `host.theme` event. Park UI applies light or
  dark. The page does not read WPF resource dictionaries.
- Vite dev server is `http://127.0.0.1:5173`. Playwright's mocked-browser
  lane uses port `5174` so it does not take the port a live host is watching.
- MSBuild does not run bun until [0040](0040-webview2-host-and-virtual-host.md)
  copies `dist`. This ADR's proof is `bun install`, `bun run typecheck`, and
  `bun test` from `source/DevTools.Web`.

## Alternatives Considered

1. **Vue.** The bridge and the window do not care which framework paints
   the DOM. The stack for this product is React, with a CDP test ladder.
2. **A second React component kit beside Park UI.** Park UI is the kit.
   Another design system would split the page.
3. **Keep Scintilla for the log** and render linkify in WPF. That keeps the
   native viewer, `ILinkifier`, and the WinForms/WPF host this decision
   removes. Linkify is a CodeMirror decoration plus a bridge click.
4. **Treat CodeMirror as a code editor** for C#, Python, or F# scripts.
   There is no editor goal. The component exists to render and linkify logs.
5. **pnpm or npm for `DevTools.Web`.** `docs/` already standardizes on bun
   `1.4.2`. A second package manager splits install and CI.
6. **Browser history routing.** Virtual-host navigations to client paths 404
   without a rewrite step. Hash URLs work for both the dev server and
   `https://app.local`.
7. **`zustand` (or Redux, or Jotai) as the app store.** Host state would be
   copied into the store and could disagree with the last event. A log line
   would notify React. Feature state, the route, and the bridge client
   cover these screens.
8. **TanStack Query as the cache for `invoke`.** The bridge is not HTTP.
   There is no cache key, retry policy, or stale time. In-flight work is
   the envelope `id`.
9. **A React wrapper that binds CodeMirror `value`**
   (`@uiw/react-codemirror` or the same shape). Each batch becomes React
   state and the wrapper writes it back into the editor. The log route
   mounts `EditorView` once and dispatches into it. React keeps the chrome
   around that view.

## Consequences

Positive:

- UI work runs in a browser with Vite HMR, without Revit or AutoCAD.
- Park UI components are source in-repo, so a host add-in build does not
  depend on a component CDN.
- CodeMirror 6 is the log renderer, including linkify, for every web
  surface. Scintilla is not beside it. Log appends do not pass through
  React render.

Tradeoffs:

- TypeScript 7 and Vite 8 are current as of this date; contributors need a
  Node version that Vite 8 accepts. The scaffold engines line starts at
  Node `^20.19.0 || >=22.12.0`, and rises if Vite 8's own engines field
  is stricter.
- Vendored Park components drift unless upgrades go through `@park-ui/cli`
  and a `bun.lock` bump together.
- `docs/` and `DevTools.Web` share bun `1.4.2` and still have separate
  lockfiles. Bumping bun means both `packageManager` fields.

## Follow-Up

- Scaffold commit adds `package.json` (`packageManager`: `bun@1.4.2`),
  `bun.lock`, `panda.config.ts`, `vite.config.ts`, and a route that mounts
  the log surface with one linkified sample line.
- Bridge client types arrive with [0039](0039-webview-json-bridge.md), not
  in the scaffold beyond a typed stub.
