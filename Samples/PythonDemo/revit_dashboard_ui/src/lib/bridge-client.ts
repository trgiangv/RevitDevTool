/**
 * WebView2 bridge client with browser mock support.
 *
 * - In Revit WebView2: Uses real bridge communication
 * - In Browser: Uses mock data for development
 */

import type {
  DashboardFilterState,
  DashboardPayload,
  PendingRequest,
  RefreshResult,
  RevitApiResponse,
} from "@/types"
import { generateMockPayload, isBrowserMode } from "./mock-data"

declare global {
  interface Window {
    __BIM_DASHBOARD_INITIAL_DATA?: DashboardPayload
    chrome?: {
      webview?: {
        postMessage: (message: unknown) => void
      }
    }
  }
}

function generateId(): string {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 9)}`
}

export class BridgeClient {
  private readonly pending = new Map<string, PendingRequest>()
  private readonly controller = new AbortController()
  private readonly isMockMode: boolean
  private mockPayload: DashboardPayload | null = null

  constructor() {
    this.isMockMode = isBrowserMode()

    if (this.isMockMode) {
      console.log("[Bridge] Running in browser mock mode")
      this.mockPayload = generateMockPayload(2500)
    } else {
      console.log("[Bridge] Running in Revit WebView2 mode")
      window.addEventListener("revit-api-result", this.onResponse as EventListener, {
        signal: this.controller.signal,
      })
    }
  }

  // -- lifecycle -----------------------------------------------------------

  dispose(): void {
    this.controller.abort()
    for (const [, req] of this.pending) clearTimeout(req.timeout)
    this.pending.clear()
  }

  // -- initial payload -----------------------------------------------------

  getInitialPayload(): DashboardPayload | null {
    if (this.isMockMode) {
      return this.mockPayload
    }
    return window.__BIM_DASHBOARD_INITIAL_DATA ?? null
  }

  // -- generic invoke ------------------------------------------------------

  invoke<T = unknown>(
    method: string,
    params: Record<string, unknown> = {},
    timeoutMs = 30_000,
  ): Promise<T> {
    // Mock mode: simulate responses
    if (this.isMockMode) {
      return this.mockInvoke<T>(method, params)
    }

    return new Promise<T>((resolve, reject) => {
      const id = generateId()

      const timeout = setTimeout(() => {
        this.pending.delete(id)
        reject(new Error(`Revit API '${method}' timed out (${timeoutMs}ms)`))
      }, timeoutMs)

      this.pending.set(id, {
        resolve: (response) => {
          if (response.ok) {
            resolve(response.data as T)
          } else {
            reject(new Error(response.error ?? "Unknown error"))
          }
        },
        reject,
        timeout,
      })

      this.postMessage({ id, type: "revit_api", method, params })
    })
  }

  // -- mock invoke for browser mode ----------------------------------------

  private async mockInvoke<T>(method: string, params: Record<string, unknown>): Promise<T> {
    // Simulate network delay
    await new Promise(resolve => setTimeout(resolve, 100 + Math.random() * 200))

    console.log(`[Mock] ${method}`, params)

    switch (method) {
      case "select":
        console.log(`[Mock] Selected ${(params.element_ids as number[])?.length ?? 0} elements`)
        return undefined as T

      case "zoom":
        console.log(`[Mock] Zoomed to ${(params.element_ids as number[])?.length ?? 0} elements`)
        return undefined as T

      case "isolate":
        console.log(`[Mock] Isolated ${(params.element_ids as number[])?.length ?? 0} elements`)
        return undefined as T

      case "isolateByLevelCategory":
        console.log(`[Mock] Isolated level=${params.level}, category=${params.category}`)
        return undefined as T

      case "resetIsolation":
        console.log("[Mock] Reset isolation")
        return undefined as T

      case "colorOverride":
        console.log(`[Mock] Color override on ${(params.element_ids as number[])?.length ?? 0} elements`)
        return undefined as T

      case "clearOverrides":
        console.log("[Mock] Cleared overrides")
        return undefined as T

      case "createWarningView":
        console.log(`[Mock] Created warning view for ${(params.element_ids as number[])?.length ?? 0} elements`)
        return undefined as T

      case "applyGroupOverrides": {
        const groups = (params.groups as Array<{ element_ids: number[]; color: number[] }>) ?? []
        const totalIds = groups.reduce((sum, g) => sum + (g.element_ids?.length ?? 0), 0)
        console.log(`[Mock] Applied group overrides: ${groups.length} groups, ${totalIds} elements`)
        return undefined as T
      }

      case "resetScheduleMode":
        console.log("[Mock] Reset schedule mode — cleared overrides and temp view")
        return undefined as T

      case "getElementParameters": {
        console.log(`[Mock] Getting parameters for element ${params.element_id}`)
        // Return realistic mock parameter groups
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
            "Constraints": [
              { name: "Base Constraint", value: "Level 1", is_readonly: false, storage_type: "ElementId" },
              { name: "Base Offset", value: "0.000 mm", is_readonly: false, storage_type: "Double" },
              { name: "Top Constraint", value: "Level 2", is_readonly: false, storage_type: "ElementId" },
              { name: "Top Offset", value: "0.000 mm", is_readonly: false, storage_type: "Double" },
              { name: "Location Line", value: "Wall Centerline", is_readonly: false, storage_type: "Integer" },
            ],
            "Dimensions": [
              { name: "Length", value: "5000.000 mm", is_readonly: true, storage_type: "Double" },
              { name: "Area", value: "15.00 m²", is_readonly: true, storage_type: "Double" },
              { name: "Volume", value: "3.00 m³", is_readonly: true, storage_type: "Double" },
              { name: "Width", value: "200.000 mm", is_readonly: true, storage_type: "Double" },
            ],
            "Phasing": [
              { name: "Phase Created", value: "New Construction", is_readonly: false, storage_type: "ElementId" },
              { name: "Phase Demolished", value: "None", is_readonly: false, storage_type: "ElementId" },
            ],
            "Other": [
              { name: "Design Option", value: "Main Model", is_readonly: true, storage_type: "String" },
              { name: "Workset", value: "Shared Levels and Grids", is_readonly: false, storage_type: "Integer" },
            ],
          },
        } as T
      }

      case "refresh":
        // Generate fresh mock data
        this.mockPayload = generateMockPayload(2500)
        return { payload: this.mockPayload } as T

      default:
        console.warn(`[Mock] Unknown method: ${method}`)
        return undefined as T
    }
  }

  // -- high-level methods --------------------------------------------------

  selectElements(ids: number[]): Promise<void> {
    return this.invoke("select", { element_ids: ids })
  }

  zoomToElements(ids: number[]): Promise<void> {
    return this.invoke("zoom", { element_ids: ids })
  }

  isolateElements(ids: number[]): Promise<void> {
    return this.invoke("isolate", { element_ids: ids })
  }

  isolateByLevelCategory(level: string, category: string): Promise<void> {
    return this.invoke("isolateByLevelCategory", { level, category })
  }

  resetIsolation(): Promise<void> {
    return this.invoke("resetIsolation")
  }

  colorOverride(ids: number[], color: [number, number, number]): Promise<void> {
    return this.invoke("colorOverride", { element_ids: ids, color })
  }

  clearOverrides(): Promise<void> {
    return this.invoke("clearOverrides")
  }

  createWarningView(elementIds: number[]): Promise<void> {
    return this.invoke("createWarningView", { element_ids: elementIds })
  }

  applyGroupOverrides(
    groups: Array<{ element_ids: number[]; color: [number, number, number] }>,
    isolateIds?: number[],
  ): Promise<void> {
    return this.invoke("applyGroupOverrides", {
      groups,
      isolate_ids: isolateIds ?? null,
    })
  }

  resetScheduleMode(): Promise<void> {
    return this.invoke("resetScheduleMode")
  }

  getElementParameters(elementId: number): Promise<{
    parameters: Record<string, Array<{
      name: string; value: string; is_readonly: boolean; storage_type: string
    }>>
  }> {
    return this.invoke("getElementParameters", { element_id: elementId })
  }

  async refreshData(): Promise<DashboardPayload> {
    const result = await this.invoke<RefreshResult>("refresh", {}, 60_000)
    return result.payload
  }

  // -- export / log --------------------------------------------------------

  requestExport(filters: DashboardFilterState): void {
    if (this.isMockMode) {
      console.log("[Mock] Export requested", filters)
      // Simulate export success with dynamic filename
      setTimeout(() => {
        const ts = new Date().toISOString().replace(/[:-]/g, "").slice(0, 15)
        window.dispatchEvent(new CustomEvent("bim-export-result", {
          detail: { ok: true, path: `C:\\Users\\Mock\\Documents\\bim_dashboard_export_${ts}.xlsx` },
        }))
      }, 500)
      return
    }
    this.postMessage({ type: "export_excel", payload: { filters } })
  }

  log(message: string): void {
    if (this.isMockMode) {
      console.log("[Mock Log]", message)
      return
    }
    this.postMessage({ type: "log", payload: { message } })
  }

  // -- internals -----------------------------------------------------------

  private postMessage(message: unknown): void {
    window.chrome?.webview?.postMessage(message)
  }

  private onResponse = (event: Event): void => {
    const detail = (event as CustomEvent<RevitApiResponse>).detail
    const req = this.pending.get(detail.id)
    if (req) {
      clearTimeout(req.timeout)
      this.pending.delete(detail.id)
      req.resolve(detail)
    }
  }
}
