import type { EnvelopeKindName } from "@/lib/envelope"

/** WebView2 ↔ host envelope. Both directions use this object. */

export interface BridgeMessage {
  type: EnvelopeKindName
  command: string
  id?: string
  payload?: unknown
  error?: string | null
}

export interface PendingRequest<T = unknown> {
  resolve: (value: T) => void
  reject: (reason: Error) => void
  timeout: ReturnType<typeof setTimeout>
}

export interface WebViewMessageEvent {
  data: unknown
}

export interface ChromeWebView {
  postMessage: (message: unknown) => void
  addEventListener: (type: "message", listener: (event: WebViewMessageEvent) => void) => void
  removeEventListener: (type: "message", listener: (event: WebViewMessageEvent) => void) => void
}
