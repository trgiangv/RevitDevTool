/**
 * Visual Inventory — Treemap + Family Explorer.
 * Replaces the boring Project Browser tree with visual data.
 */

import { useCallback, useEffect } from "react"
import { toast } from "sonner"
import { useDashboard } from "@/providers/dashboard-provider"
import { useBridge } from "@/providers/bridge-provider"
import { TreemapChart } from "@/components/charts/treemap-chart"
import { FamilyExplorer } from "@/features/inventory/family-explorer"

export function InventoryPage({ active = true }: { active?: boolean } = {}) {
  const { filteredRows, setFilters } = useDashboard()
  const bridge = useBridge()

  // Reset temporary view mode when leaving this page
  useEffect(() => {
    if (!active) return
    return () => {
      bridge.resetIsolation()
    }
  }, [active, bridge])

  const handleTreemapClick = useCallback(
    async (level: string, category: string) => {
      try {
        await bridge.isolateByLevelCategory(level, category)
        toast.success(`Isolated ${category} at ${level}`)
      } catch (err) {
        toast.error("Isolate failed", { description: String(err) })
      }
    },
    [bridge],
  )

  const handleSelectFamily = useCallback(
    (familyName: string) => {
      setFilters((prev) => ({
        ...prev,
        families: prev.families?.includes(familyName)
          ? prev.families.filter((f) => f !== familyName)
          : [...(prev.families ?? []), familyName],
      }))
    },
    [setFilters],
  )

  return (
    <div style={{ padding: 20, display: "flex", flexDirection: "column", gap: 16 }}>
      <TreemapChart rows={filteredRows} onNodeClick={handleTreemapClick} />
      <FamilyExplorer rows={filteredRows} onSelectFamily={handleSelectFamily} />
    </div>
  )
}
