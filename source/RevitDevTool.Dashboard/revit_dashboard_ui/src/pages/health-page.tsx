/**
 * Model Health & Hygiene — Warnings matrix + Heavy elements tracker.
 * Supports isolation, selection, and color override per warning/family.
 */

import { useCallback, useEffect } from "react"
import { toast } from "sonner"
import { useDashboard } from "@/providers/dashboard-provider"
import { useBridge } from "@/providers/bridge-provider"
import { WarningSeverityMatrix } from "@/features/health/warning-matrix"
import { HeavyTracker } from "@/features/health/heavy-tracker"

interface HealthPageProps {
  active?: boolean
}

export function HealthPage({ active = true }: HealthPageProps) {
  const { payload } = useDashboard()
  const bridge = useBridge()

  // Cleanup: reset isolation when leaving the Health tab
  useEffect(() => {
    if (!active) {
      bridge.resetIsolation().catch(() => {})
    }
    return () => {
      bridge.resetIsolation().catch(() => {})
    }
  }, [active, bridge])

  const handleIsolateWarningElements = useCallback(
    async (elementIds: number[]) => {
      try {
        await bridge.createWarningView(elementIds)
        toast.success(`Isolated ${elementIds.length} warning elements`)
      } catch (err) {
        toast.error("Failed to create warning view", { description: String(err) })
      }
    },
    [bridge],
  )

  const handleSelectElements = useCallback(
    async (elementIds: number[]) => {
      try {
        await bridge.selectElements(elementIds)
      } catch (err) {
        toast.error("Failed to select elements", { description: String(err) })
      }
    },
    [bridge],
  )

  const handleColorOverride = useCallback(
    async (elementIds: number[], color: [number, number, number]) => {
      try {
        await bridge.colorOverride(elementIds, color)
        toast.success(`Applied color override to ${elementIds.length} elements`)
      } catch (err) {
        toast.error("Failed to apply color override", { description: String(err) })
      }
    },
    [bridge],
  )

  const handleIsolateFamily = useCallback(
    async (familyName: string) => {
      // Find all element IDs matching this family from allRows
      const allRows = payload?.rows ?? []
      const ids = allRows
        .filter((r) => r.family === familyName)
        .map((r) => r.element_id)
      if (ids.length === 0) {
        toast.info(`No elements found for family "${familyName}"`)
        return
      }
      try {
        await bridge.isolateElements(ids)
        toast.success(`Isolated ${ids.length} elements from "${familyName}"`)
      } catch (err) {
        toast.error("Failed to isolate family", { description: String(err) })
      }
    },
    [bridge, payload?.rows],
  )

  return (
    <div style={{ padding: 20, display: "flex", flexDirection: "column", gap: 16 }}>
      <WarningSeverityMatrix
        warnings={payload?.warnings ?? []}
        onIsolateWarningElements={handleIsolateWarningElements}
        onSelectElements={handleSelectElements}
        onColorOverride={handleColorOverride}
      />
      <HeavyTracker
        families={payload?.heavy_families ?? []}
        onIsolateFamily={handleIsolateFamily}
      />
    </div>
  )
}
