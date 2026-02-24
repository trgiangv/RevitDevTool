/**
 * Global keyboard shortcuts for the dashboard.
 */

import { useEffect } from "react"
import { useDashboard } from "@/providers/dashboard-provider"
import { useBridge } from "@/providers/bridge-provider"

export function useKeyboardShortcuts(
  onRefresh: () => void,
) {
  const { filters, selectedIds, setSelectedIds, chartFilter, setChartFilter, filteredRows } =
    useDashboard()
  const bridge = useBridge()

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      const isInput = document.activeElement?.tagName === "INPUT"

      if (e.ctrlKey && e.key === "a" && !isInput) {
        e.preventDefault()
        setSelectedIds(new Set(filteredRows.slice(0, 500).map((r) => r.element_id)))
        return
      }
      if (e.ctrlKey && e.key === "r") {
        e.preventDefault()
        onRefresh()
        return
      }
      if (e.ctrlKey && e.key === "e") {
        e.preventDefault()
        bridge.requestExport(filters)
        return
      }
      if (e.key === "Escape") {
        if (selectedIds.size > 0) {
          setSelectedIds(new Set())
        } else if (chartFilter) {
          setChartFilter(null)
        }
      }
    }
    window.addEventListener("keydown", handler)
    return () => window.removeEventListener("keydown", handler)
  }, [onRefresh, filters, selectedIds, setSelectedIds, chartFilter, setChartFilter, filteredRows, bridge])
}
