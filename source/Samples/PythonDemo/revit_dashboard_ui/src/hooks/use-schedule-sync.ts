/**
 * useScheduleSync — Debounced auto-sync of Schedule filter/group changes to Revit.
 *
 * When the Schedule tab is active, every change to filteredRows or groupBy
 * triggers a debounced call to `applyGroupOverrides` which:
 *   1. Resets the previous temp view mode
 *   2. Clears old graphic overrides
 *   3. Applies group-based color overrides
 *   4. Isolates the filtered set of elements
 *
 * On unmount (or when active becomes false), `resetScheduleMode` is called
 * to clean up all temporary view states and overrides.
 */

import { useEffect, useRef } from "react"
import { useBridge } from "@/providers/bridge-provider"
import { assignGroupColors } from "@/lib/color-palette"
import type { ElementRow } from "@/types"

export function useScheduleSync(
  active: boolean,
  filteredRows: ElementRow[],
  groupBy: string | undefined,
): void {
  const bridge = useBridge()
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined)

  // Cleanup: reset schedule mode when leaving the Schedule tab
  useEffect(() => {
    if (!active) return
    return () => {
      bridge.resetScheduleMode()
    }
  }, [active, bridge])

  // When filteredRows or groupBy change: recompute groups & send overrides
  useEffect(() => {
    if (!active || filteredRows.length === 0) return

    clearTimeout(debounceRef.current)
    debounceRef.current = setTimeout(() => {
      const field = groupBy ?? "category"

      // Build group map: label -> element ids
      const groupMap = new Map<string, number[]>()
      for (const row of filteredRows) {
        const key = String((row as Record<string, unknown>)[field] ?? "Unknown")
        const arr = groupMap.get(key) ?? []
        arr.push(row.element_id)
        groupMap.set(key, arr)
      }

      const groups = assignGroupColors(groupMap)

      bridge.applyGroupOverrides(
        groups.map((g) => ({ element_ids: g.ids, color: g.color })),
        filteredRows.map((r) => r.element_id),
      )
    }, 300) // 300ms debounce to avoid hammering Revit on rapid filter changes

    return () => clearTimeout(debounceRef.current)
  }, [active, filteredRows, groupBy, bridge])
}
