# 0045 MCP Apps Is an Optional Visual Face on the Client

Date: 2026-09-23

## Status

Proposed — experimental

Not current behavior. Does not amend
[0027](0027-mcp-product-surface.md). Do not add
`ModelContextProtocol.Extensions.Apps`, a `ui://` resource, or an Apps host
in the WebView shell until this ADR is Accepted.

The C# SDK marks the API `[Experimental("MCPEXP003")]`.

## Context

[0027](0027-mcp-product-surface.md) is the shipped MCP product: the client
talks to Daemon. `tools/list` is the envelope (`search_dynamic` /
`invoke_dynamic` plus a few infrastructure tools). The CAD process is the
execution hop behind that envelope. It is not an `McpServer` the client
initializes.

`ModelContextProtocol.Extensions.Apps` is a separate package on the C# SDK.
It lets a server attach UI metadata to a tool (`[McpAppUi]`,
`_meta.ui.resourceUri` such as `ui://…`) and serve that URI as HTML
(`text/html;profile=mcp-app`). A client that negotiates
`io.modelcontextprotocol/ui` fetches the resource and renders it in its own
UI, typically a sandboxed iframe. The host passes tool arguments and the
result into that frame. The frame calls tools back through the host. It
does not receive `UIApplication` or `Document`.

That is the opposite of the in-Revit shell in
[0037](0037-webview2-bare-window-shell.md). The shell is a page inside the
CAD process. Apps is a page inside the chat client.

```text
Cursor / ChatGPT
  agent text
  iframe  ui://…     tools/call
        │                 │
        ▼                 ▼
     Daemon envelope ──► host pipe ──► ExternalEvent ──► Revit API
```

`Visibility = [McpUiToolVisibility.App]` hides a tool from the model. A
button in the frame can call it without another model turn. Model-visible
tools stay the ones the agent should reason about.

The same HTML file could later be opened by the Revit WebView and by an
Apps iframe. That reuse is a product choice. The package does not load
itself into Revit.

An in-Revit chat that renders `ui://` pages would be an MCP Apps **host**
(App bridge in the page: fetch the resource, sandbox it, proxy
`tools/call`). `Extensions.Apps` is only the server half.

## Decision

- Record the direction: Apps is a visual face for a tool result in the MCP
  client. The server returns data plus a `ui://` resource. The client
  renders it. Clicks return as `tools/call` through that client, then the
  existing Daemon hop, then `ExternalEvent`.
- Refuse the reading that Apps draws a pane inside Revit, or that the
  server pushes HTML into the CAD window.
- The package stays off the core SDK reference. `DevTools.Mcp.Core`,
  `DevTools.Mcp.Adapter`, and the host pipe do not reference
  `ModelContextProtocol.Extensions.Apps`. If a spike is accepted later, the
  reference lives on Daemon only, in an optional composition, and
  `WithMcpApps` is not implied by today's `AddMcpServer` setup.
- [0027](0027-mcp-product-surface.md) stays the client-visible surface
  until this ADR is Accepted. An Apps experiment does not put host tool
  names on Daemon `tools/list` for the model. An App-only tool, if one is
  tried, is `McpUiToolVisibility.App` and still executes through the
  envelope hop. The iframe never holds a Revit API object.
- Clients that do not negotiate the UI extension still get the text or
  JSON result. Missing Apps support is not an error.
- [0037](0037-webview2-bare-window-shell.md) through
  [0042](0042-wpf-ui-migration-slices.md) do not grow an Apps host, an
  iframe bridge, or a chat transcript. Building that host is a separate
  decision.

## Alternatives Considered

1. **Use Apps as the dockable Revit dashboard.** The extension never
   parents an HWND in Revit. The in-product shell stays the WebView window.
2. **Reference the package from `DevTools.Mcp.Core` or the host pipe.**
   That pulls an experimental UI extension into every host process and
   suggests the CAD pipe is the client's server. Daemon is the client
   session ([0027](0027-mcp-product-surface.md)).
3. **Send every button click through the model.** App-only visibility
   exists so select, zoom, isolate, paging, and refresh do not occupy the
   model's tool list or a second completion.
4. **Make the WebView shell the Apps host in the same migration.** That is
   a chat product: LLM client, resource fetch, sandboxed frame, and proxy.
   The shell migration is the add-in chrome.

## Consequences

Positive:

- Agents can cite one place for what Apps does and what it refuses.
- The shipped envelope and the WebView shell stay free of the package
  while the extension is still experimental in the SDK.

Tradeoffs:

- A rich result in Cursor depends on that client implementing MCP Apps.
  Text results remain the fallback.
- App-only calls still cross Daemon and the host pipe. They are not a
  shortcut from the iframe to `UIDocument`.
- Serving `ui://` HTML from Daemon is a new resource on the client
  session. It is not part of the host-pipe protocol.

## Follow-Up

- When this ADR is Accepted, choose the package version next to the
  `ModelContextProtocol` pin, suppress `MCPEXP003` only on the Daemon
  composition that calls `WithMcpApps`, and name which envelope results
  get a `ui://` view.
- An in-Revit Apps host, if ever wanted, is its own ADR. It is not a
  follow-up task of the WebView slices.

## References

- C# SDK: [MCP Apps](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/apps/apps.md)
  (`ModelContextProtocol.Extensions.Apps`, `[McpAppUi]`, `McpUiToolVisibility`).
- Extension overview: [MCP Apps](https://modelcontextprotocol.io/extensions/apps/overview).
