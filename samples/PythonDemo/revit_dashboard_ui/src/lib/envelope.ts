/** Wire names from `pyrevit_webview.bridge.contract`. Values are the JSON text. */

export const EnvelopeKind = {
  REQUEST: "request",
  RESPONSE: "response",
  EVENT: "event",
} as const

export type EnvelopeKindName = (typeof EnvelopeKind)[keyof typeof EnvelopeKind]

export const EnvelopeField = {
  TYPE: "type",
  COMMAND: "command",
  ID: "id",
  PAYLOAD: "payload",
  ERROR: "error",
  OK: "ok",
} as const

/** CustomEvent the dashboard host dispatches. Invoke responses use `bridge-response`. */
export const DASHBOARD_EVENT = "bim-bridge"
