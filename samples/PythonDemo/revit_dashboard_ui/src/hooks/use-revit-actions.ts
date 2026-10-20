/**
 * View commands post and return. The model changing is the confirmation.
 * A failed reply is the only toast.
 */

import { useCallback } from "react"
import { toast } from "@/lib/toast"
import { useBridge } from "@/providers/bridge-provider"

export function useRevitActions() {
  const bridge = useBridge()

  const exec = useCallback((label: string, fn: () => Promise<unknown>) => {
    void fn().catch((err: unknown) => {
      toast.error(`${label} failed`, { description: String(err) })
    })
  }, [])

  const selectInRevit = useCallback(
    (ids: string[]) => {
      if (!ids.length) return
      exec("Selection", () => bridge.selectElements(ids))
    },
    [bridge, exec],
  )

  const zoomTo = useCallback(
    (ids: string[]) => {
      if (!ids.length) return
      exec("Zoom", () => bridge.zoomToElements(ids))
    },
    [bridge, exec],
  )

  const isolate = useCallback(
    (ids: string[]) => {
      if (!ids.length) return
      exec("Isolate", () => bridge.isolateElements(ids))
    },
    [bridge, exec],
  )

  const resetIsolation = useCallback(() => {
    exec("Reset isolation", () => bridge.resetIsolation())
  }, [bridge, exec])

  const colorOverride = useCallback(
    (ids: string[], color: [number, number, number]) => {
      if (!ids.length) return
      exec("Color override", () => bridge.colorOverride(ids, color))
    },
    [bridge, exec],
  )

  const clearOverrides = useCallback(() => {
    exec("Clear overrides", () => bridge.clearOverrides())
  }, [bridge, exec])

  return {
    selectInRevit,
    zoomTo,
    isolate,
    resetIsolation,
    colorOverride,
    clearOverrides,
  }
}
