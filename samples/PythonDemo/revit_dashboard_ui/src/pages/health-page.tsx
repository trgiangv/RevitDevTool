/**
 * Model Health & Hygiene — Warnings matrix + Heavy elements tracker.
 * Supports isolation, selection, and color override per warning/family.
 */

import { useCallback, useEffect } from "react"
import { Space } from "@douyinfe/semi-ui"
import { toast } from "@/lib/toast"
import { useDashboard } from "@/providers/dashboard-provider"
import { useBridge } from "@/providers/bridge-provider"
import { WarningSeverityMatrix } from "@/features/health/warning-matrix"
import { HeavyTracker } from "@/features/health/heavy-tracker"

interface HealthPageProps {
  active?: boolean
}

export function HealthPage({ active = true }: Readonly<HealthPageProps>) {
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
    (elementIds: string[]) => {
      void bridge.createWarningView(elementIds).catch((err: unknown) => {
        toast.error("Failed to create warning view", { description: String(err) })
      })
    },
    [bridge],
  )

  const handleSelectElements = useCallback(
    (elementIds: string[]) => {
      void bridge.selectElements(elementIds).catch((err: unknown) => {
        toast.error("Failed to select elements", { description: String(err) })
      })
    },
    [bridge],
  )

  const handleColorOverride = useCallback(
    (elementIds: string[], color: [number, number, number]) => {
      void bridge.colorOverride(elementIds, color).catch((err: unknown) => {
        toast.error("Failed to apply color override", { description: String(err) })
      })
    },
    [bridge],
  )

  const handleIsolateFamily = useCallback(
    (familyName: string) => {
      const allRows = payload?.rows ?? []
      const ids = allRows.filter((r) => r.family === familyName).map((r) => r.element_id)
      if (ids.length === 0) {
        toast.info(`No elements found for family "${familyName}"`)
        return
      }
      void bridge.isolateElements(ids).catch((err: unknown) => {
        toast.error("Failed to isolate family", { description: String(err) })
      })
    },
    [bridge, payload?.rows],
  )

  return (
    <Space
      vertical
      spacing="medium"
      style={{ padding: 20, width: "100%", boxSizing: "border-box", alignItems: "stretch" }}
    >
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
    </Space>
  )
}
