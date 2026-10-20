/**
 * WebView2 bridge client with browser mock support.
 *
 * Host traffic uses one JSON envelope posted through chrome.webview.
 * The constructor does not subscribe; call start() from a provider effect.
 */

import type {
  BridgeMessage,
  ChromeWebView,
  DashboardPayload,
  HostSnapshot,
  PendingRequest,
} from "@/types"
import { buildDashboard, toDashboardPayload } from "./analytics"
import { DASHBOARD_EVENT, EnvelopeField, EnvelopeKind } from "./envelope"
import { displayText } from "./text"
import { generateMockPayload, isBrowserMode } from "./mock-data"

type SaveFilePicker = (options: {
  suggestedName?: string
  types?: Array<{ description?: string; accept: Record<string, string[]> }>
}) => Promise<FileSystemFileHandle>

declare global {
  interface Window {
    __BIM_DASHBOARD_INITIAL_DATA?: unknown
    chrome?: {
      webview?: ChromeWebView
    }
    showSaveFilePicker?: SaveFilePicker
  }
}

type SelectionListener = (elementIds: string[]) => void

async function mockSaveWorkbook(
  params: Record<string, unknown>,
): Promise<{ path?: string; cancelled?: boolean }> {
  const picker = window.showSaveFilePicker
  if (!picker) throw new Error("This browser has no Save dialog")
  const filename =
    typeof params.filename === "string" ? params.filename : "bim_dashboard_export.xlsx"
  const content = typeof params.content_base64 === "string" ? params.content_base64 : ""
  try {
    const handle = await picker({
      suggestedName: filename,
      types: [
        {
          description: "Excel workbook",
          accept: {
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": [".xlsx"],
          },
        },
      ],
    })
    const binary = Uint8Array.from(atob(content), (char) => {
      const code = char.codePointAt(0)
      return code ?? 0
    })
    const writable = await handle.createWritable()
    await writable.write(binary)
    await writable.close()
    return { path: handle.name, cancelled: false }
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") return { cancelled: true }
    throw error
  }
}

function generateId(): string {
  return crypto.randomUUID()
}

function parseBridgeMessage(data: unknown): BridgeMessage | null {
  let value = data
  if (typeof value === "string") {
    try {
      value = JSON.parse(value) as unknown
    } catch {
      return null
    }
  }
  if (!value || typeof value !== "object") return null
  const record = value as Partial<
    Record<(typeof EnvelopeField)[keyof typeof EnvelopeField], unknown>
  >
  const kind = record[EnvelopeField.TYPE]
  const command = record[EnvelopeField.COMMAND]
  if (
    kind !== EnvelopeKind.REQUEST &&
    kind !== EnvelopeKind.RESPONSE &&
    kind !== EnvelopeKind.EVENT
  )
    return null
  if (typeof command !== "string") return null
  const requestId = record[EnvelopeField.ID]
  const error = record[EnvelopeField.ERROR]
  return {
    type: kind,
    command,
    id: typeof requestId === "string" ? requestId : undefined,
    payload: record[EnvelopeField.PAYLOAD],
    error: typeof error === "string" || error === null ? error : undefined,
  }
}

export class BridgeClient {
  private readonly pending = new Map<string, PendingRequest>()
  private readonly selectionListeners = new Set<SelectionListener>()
  private readonly isMockMode: boolean
  private mockPayload: DashboardPayload | null = null
  private messageListener: ((event: { data: unknown }) => void) | null = null
  private hostListener: ((event: Event) => void) | null = null
  private readonly inflight = new Map<string, Promise<unknown>>()
  private readonly snapshotRows = new Map<string, Map<number, unknown[]>>()
  private readonly chunkedReply = new Map<string, Record<string, unknown>>()

  constructor() {
    this.isMockMode = isBrowserMode()
    if (this.isMockMode) {
      console.log("[Bridge] Running in browser mock mode")
      this.mockPayload = generateMockPayload(2500)
    } else {
      console.log("[Bridge] Running in Revit WebView2 mode")
    }
  }

  /**
   * Subscribe to host messages. A second call keeps the same listener.
   * StrictMode must not remove it: the host reply would arrive with no pending id.
   */
  start(): () => void {
    if (this.messageListener) return () => this.stop()
    const webview = window.chrome?.webview
    if (!webview) return () => {}

    this.messageListener = (event) => {
      this.onMessage(event.data)
    }
    webview.addEventListener("message", this.messageListener)
    this.hostListener = (event: Event) => {
      this.onMessage((event as CustomEvent<unknown>).detail)
    }
    window.addEventListener(DASHBOARD_EVENT, this.hostListener)
    return () => this.stop()
  }

  dispose(): void {
    this.stop()
    for (const req of this.pending.values()) clearTimeout(req.timeout)
    this.pending.clear()
  }

  subscribeSelection(listener: SelectionListener): () => void {
    this.selectionListeners.add(listener)
    return () => {
      this.selectionListeners.delete(listener)
    }
  }

  getInitialPayload(): DashboardPayload | null {
    if (this.isMockMode) return this.mockPayload
    return toDashboardPayload(window.__BIM_DASHBOARD_INITIAL_DATA)
  }

  invoke<T = unknown>(command: string, payload: unknown = {}, timeoutMs = 30_000): Promise<T> {
    if (command === "GetSnapshot") {
      const existing = this.inflight.get(command)
      if (existing) return existing as Promise<T>
    }
    return this.post<T>(command, payload, timeoutMs)
  }

  private post<T>(command: string, payload: unknown, timeoutMs: number): Promise<T> {
    if (this.isMockMode) return this.mockInvoke<T>(command, payload)

    const webview = window.chrome?.webview
    if (!webview) {
      return Promise.reject(new Error("WebView2 bridge is not available"))
    }
    this.start()

    const releaseSnapshot = () => {
      if (command === "GetSnapshot" && this.inflight.get(command) === promise) {
        this.inflight.delete(command)
      }
    }

    const promise = new Promise<T>((resolve, reject) => {
      const id = generateId()
      const timeout = setTimeout(() => {
        this.pending.delete(id)
        releaseSnapshot()
        reject(new Error(`Revit API '${command}' timed out (${timeoutMs}ms)`))
      }, timeoutMs)

      this.pending.set(id, {
        resolve: (value) => {
          releaseSnapshot()
          resolve(value as T)
        },
        reject: (error) => {
          releaseSnapshot()
          reject(error)
        },
        timeout,
      })
      webview.postMessage({
        [EnvelopeField.TYPE]: EnvelopeKind.REQUEST,
        [EnvelopeField.COMMAND]: command,
        [EnvelopeField.ID]: id,
        [EnvelopeField.PAYLOAD]: payload ?? {},
      } satisfies BridgeMessage)
    })

    if (command === "GetSnapshot") this.inflight.set(command, promise)
    return promise
  }

  selectElements(ids: string[]): Promise<void> {
    return this.invoke("Select", { element_ids: ids })
  }

  zoomToElements(ids: string[]): Promise<void> {
    return this.invoke("Zoom", { element_ids: ids })
  }

  isolateElements(ids: string[]): Promise<void> {
    return this.invoke("Isolate", { element_ids: ids })
  }

  isolateByLevelCategory(level: string, category: string): Promise<void> {
    return this.invoke("IsolateByLevelCategory", { level, category })
  }

  resetIsolation(): Promise<void> {
    return this.invoke("ResetIsolation")
  }

  colorOverride(ids: string[], color: [number, number, number]): Promise<void> {
    return this.invoke("ColorOverride", { element_ids: ids, color })
  }

  clearOverrides(): Promise<void> {
    return this.invoke("ClearOverrides")
  }

  createWarningView(elementIds: string[]): Promise<void> {
    return this.invoke("CreateWarningView", { element_ids: elementIds })
  }

  applyGroupOverrides(
    groups: Array<{ element_ids: string[]; color: [number, number, number] }>,
    isolateIds?: string[],
  ): Promise<void> {
    return this.invoke("ApplyGroupOverrides", {
      groups,
      isolate_ids: isolateIds ?? null,
    })
  }

  resetScheduleMode(): Promise<void> {
    return this.invoke("ResetScheduleMode")
  }

  getElementParameters(elementId: string): Promise<{
    parameters: Record<
      string,
      Array<{
        name: string
        value: string
        is_readonly: boolean
        storage_type: string
      }>
    >
  }> {
    return this.invoke("GetElementParameters", { element_id: elementId })
  }

  saveWorkbook(filename: string, contentBase64: string): Promise<string | null> {
    return this.invoke<{ path?: string; cancelled?: boolean }>(
      "SaveWorkbook",
      { filename, content_base64: contentBase64 },
      600_000,
    ).then((result) => (result.cancelled || !result.path ? null : result.path))
  }

  async refreshData(): Promise<DashboardPayload> {
    if (this.isMockMode) {
      this.mockPayload = generateMockPayload(2500)
      return this.mockPayload
    }
    const snapshot = await this.invoke<HostSnapshot>("GetSnapshot", {}, 300_000)
    return buildDashboard(snapshot)
  }

  private stop(): void {
    if (this.messageListener) {
      window.chrome?.webview?.removeEventListener("message", this.messageListener)
      this.messageListener = null
    }
    if (this.hostListener) {
      window.removeEventListener(DASHBOARD_EVENT, this.hostListener)
      this.hostListener = null
    }
  }

  private onMessage(data: unknown): void {
    const message = parseBridgeMessage(data)
    if (!message) return

    if (message.type === EnvelopeKind.EVENT && message.command === "selection-changed") {
      const payload = message.payload as { element_ids?: unknown } | undefined
      const elementIds = Array.isArray(payload?.element_ids)
        ? payload.element_ids.map((id) => String(id))
        : []
      for (const listener of this.selectionListeners) listener(elementIds)
      return
    }

    if (message.type === EnvelopeKind.EVENT && message.command === "snapshot-rows") {
      this.storeSnapshotRows(message.payload)
      return
    }

    if (message.type !== EnvelopeKind.RESPONSE || !message.id) return
    const pending = this.pending.get(message.id)
    if (!pending) return
    if (message.error) {
      this.finish(message.id, pending, undefined, message.error)
      return
    }
    const payload = (message.payload ?? {}) as Record<string, unknown>
    if (payload.rows_chunked === true) {
      const count = Number(payload.row_count ?? 0)
      const rows = this.assembleSnapshotRows(message.id, count)
      if (!rows) {
        this.chunkedReply.set(message.id, payload)
        return
      }
      payload.rows = rows
    }
    this.finish(message.id, pending, payload)
  }

  private storeSnapshotRows(payload: unknown): void {
    if (!payload || typeof payload !== "object") return
    const record = payload as { start?: unknown; rows?: unknown } & Partial<
      Record<typeof EnvelopeField.ID, unknown>
    >
    const requestId = record[EnvelopeField.ID]
    if (typeof requestId !== "string" || !Array.isArray(record.rows)) return
    const start = Number(record.start ?? 0)
    let parts = this.snapshotRows.get(requestId)
    if (!parts) {
      parts = new Map()
      this.snapshotRows.set(requestId, parts)
    }
    parts.set(start, record.rows)

    const waiting = this.chunkedReply.get(requestId)
    const pending = this.pending.get(requestId)
    if (!waiting || !pending) return
    const rows = this.assembleSnapshotRows(requestId, Number(waiting.row_count ?? 0))
    if (!rows) return
    waiting.rows = rows
    this.chunkedReply.delete(requestId)
    this.finish(requestId, pending, waiting)
  }

  private assembleSnapshotRows(id: string, count: number): unknown[] | null {
    const parts = this.snapshotRows.get(id)
    if (!parts) return null
    const ordered = [...parts.entries()].sort((left, right) => left[0] - right[0])
    let got = 0
    for (const [, rows] of ordered) got += rows.length
    if (got < count) return null
    this.snapshotRows.delete(id)
    return ordered.flatMap(([, rows]) => rows)
  }

  private finish(id: string, pending: PendingRequest, payload: unknown, error?: string): void {
    clearTimeout(pending.timeout)
    this.pending.delete(id)
    this.chunkedReply.delete(id)
    this.snapshotRows.delete(id)
    if (error) pending.reject(new Error(error))
    else pending.resolve(payload)
  }

  private async mockInvoke<T>(command: string, payload: unknown): Promise<T> {
    const values = new Uint32Array(1)
    crypto.getRandomValues(values)
    const delay = 100 + (values[0] / 2 ** 32) * 200
    await new Promise((resolve) => setTimeout(resolve, delay))
    const params = (payload ?? {}) as Record<string, unknown>
    console.log(`[Mock] ${command}`, params)

    switch (command) {
      case "Select":
        console.log(`[Mock] Selected ${(params.element_ids as number[])?.length ?? 0} elements`)
        return undefined as T
      case "Zoom":
        console.log(`[Mock] Zoomed to ${(params.element_ids as number[])?.length ?? 0} elements`)
        return undefined as T
      case "Isolate":
        console.log(`[Mock] Isolated ${(params.element_ids as number[])?.length ?? 0} elements`)
        return undefined as T
      case "IsolateByLevelCategory":
        console.log(
          `[Mock] Isolated level=${displayText(params.level)}, category=${displayText(params.category)}`,
        )
        return undefined as T
      case "ResetIsolation":
        console.log("[Mock] Reset isolation")
        return undefined as T
      case "ColorOverride":
        console.log(
          `[Mock] Color override on ${(params.element_ids as number[])?.length ?? 0} elements`,
        )
        return undefined as T
      case "ClearOverrides":
        console.log("[Mock] Cleared overrides")
        return undefined as T
      case "CreateWarningView":
        console.log(
          `[Mock] Created warning view for ${(params.element_ids as number[])?.length ?? 0} elements`,
        )
        return undefined as T
      case "ApplyGroupOverrides": {
        const groups = (params.groups as Array<{ element_ids: number[]; color: number[] }>) ?? []
        const totalIds = groups.reduce((sum, group) => sum + (group.element_ids?.length ?? 0), 0)
        console.log(`[Mock] Applied group overrides: ${groups.length} groups, ${totalIds} elements`)
        return undefined as T
      }
      case "ResetScheduleMode":
        console.log("[Mock] Reset schedule mode — cleared overrides and temp view")
        return undefined as T
      case "GetElementParameters":
        console.log(`[Mock] Getting parameters for element ${displayText(params.element_id)}`)
        return {
          parameters: {
            "Identity Data": [
              { name: "Category", value: "Walls", is_readonly: true, storage_type: "String" },
              { name: "Family", value: "Basic Wall", is_readonly: true, storage_type: "String" },
              { name: "Type", value: "Generic - 200mm", is_readonly: true, storage_type: "String" },
              { name: "Type Id", value: "12345", is_readonly: true, storage_type: "ElementId" },
              { name: "Comments", value: "", is_readonly: false, storage_type: "String" },
              { name: "Mark", value: "W-01", is_readonly: false, storage_type: "String" },
            ],
            Constraints: [
              {
                name: "Base Constraint",
                value: "Level 1",
                is_readonly: false,
                storage_type: "ElementId",
              },
              {
                name: "Base Offset",
                value: "0.000 mm",
                is_readonly: false,
                storage_type: "Double",
              },
              {
                name: "Top Constraint",
                value: "Level 2",
                is_readonly: false,
                storage_type: "ElementId",
              },
              { name: "Top Offset", value: "0.000 mm", is_readonly: false, storage_type: "Double" },
              {
                name: "Location Line",
                value: "Wall Centerline",
                is_readonly: false,
                storage_type: "Integer",
              },
            ],
            Dimensions: [
              { name: "Length", value: "5000.000 mm", is_readonly: true, storage_type: "Double" },
              { name: "Area", value: "15.00 m²", is_readonly: true, storage_type: "Double" },
              { name: "Volume", value: "3.00 m³", is_readonly: true, storage_type: "Double" },
              { name: "Width", value: "200.000 mm", is_readonly: true, storage_type: "Double" },
            ],
            Phasing: [
              {
                name: "Phase Created",
                value: "New Construction",
                is_readonly: false,
                storage_type: "ElementId",
              },
              {
                name: "Phase Demolished",
                value: "None",
                is_readonly: false,
                storage_type: "ElementId",
              },
            ],
            Other: [
              {
                name: "Design Option",
                value: "Main Model",
                is_readonly: true,
                storage_type: "String",
              },
              {
                name: "Workset",
                value: "Shared Levels and Grids",
                is_readonly: false,
                storage_type: "Integer",
              },
            ],
          },
        } as T
      case "SaveWorkbook":
        return (await mockSaveWorkbook(params)) as T
      case "GetSnapshot": {
        this.mockPayload = generateMockPayload(2500)
        return {
          model_info: this.mockPayload.model_info,
          rows: this.mockPayload.rows,
          warnings: this.mockPayload.warnings.map((warning) => ({
            id: warning.id,
            description: warning.description,
            severity: warning.severity === "critical" ? "Error" : "Warning",
            element_ids: warning.element_ids,
          })),
        } as T
      }
      default:
        console.warn(`[Mock] Unknown command: ${command}`)
        return undefined as T
    }
  }
}
