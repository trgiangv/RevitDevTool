/** Types for the WebView2 ↔ Python bridge protocol. */

export interface BridgeMessage {
  id?: string
  type: string
  method?: string
  params?: Record<string, unknown>
  payload?: Record<string, unknown>
}

export interface RevitApiResponse {
  id: string
  ok: boolean
  data?: unknown
  error?: string
}

export interface RefreshResult {
  payload: import("./payload").DashboardPayload
}

export interface PendingRequest {
  resolve: (value: RevitApiResponse) => void
  reject: (reason: Error) => void
  timeout: ReturnType<typeof setTimeout>
}
