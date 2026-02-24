/**
 * Revit API action wrappers with loading state and toast feedback.
 */

import { useCallback, useState } from "react"
import { toast } from "sonner"
import { useBridge } from "@/providers/bridge-provider"

export function useRevitActions() {
  const bridge = useBridge()
  const [isLoading, setIsLoading] = useState(false)

  const exec = useCallback(
    async <T>(label: string, fn: () => Promise<T>): Promise<T | undefined> => {
      setIsLoading(true)
      try {
        const result = await fn()
        return result
      } catch (err) {
        toast.error(`${label} failed`, { description: String(err) })
        return undefined
      } finally {
        setIsLoading(false)
      }
    },
    [],
  )

  const selectInRevit = useCallback(
    async (ids: number[]) => {
      if (!ids.length) return
      await exec("Selection", () => bridge.selectElements(ids))
      toast.success(`Selected ${ids.length} elements in Revit`)
    },
    [bridge, exec],
  )

  const zoomTo = useCallback(
    async (ids: number[]) => {
      if (!ids.length) return
      await exec("Zoom", () => bridge.zoomToElements(ids))
      toast.success(`Zoomed to ${ids.length} elements`)
    },
    [bridge, exec],
  )

  const isolate = useCallback(
    async (ids: number[]) => {
      if (!ids.length) return
      await exec("Isolate", () => bridge.isolateElements(ids))
      toast.success(`Isolated ${ids.length} elements`)
    },
    [bridge, exec],
  )

  const resetIsolation = useCallback(async () => {
    await exec("Reset isolation", () => bridge.resetIsolation())
    toast.success("Isolation reset")
  }, [bridge, exec])

  const colorOverride = useCallback(
    async (ids: number[], color: [number, number, number]) => {
      if (!ids.length) return
      await exec("Color override", () => bridge.colorOverride(ids, color))
      toast.success(`Applied color to ${ids.length} elements`)
    },
    [bridge, exec],
  )

  const clearOverrides = useCallback(async () => {
    await exec("Clear overrides", () => bridge.clearOverrides())
    toast.success("Cleared all overrides")
  }, [bridge, exec])

  return {
    isLoading,
    selectInRevit,
    zoomTo,
    isolate,
    resetIsolation,
    colorOverride,
    clearOverrides,
  }
}
