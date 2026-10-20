/**
 * Central dashboard state: payload, filters, selection, chart interaction, ghost mode.
 *
 * Supports both:
 * - Browser mode: Uses mock data
 * - Revit mode: Uses real data from WebView2 bridge
 *
 * Host mode loads a GetSnapshot envelope and builds the payload in the page.
 */

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react"
import type { DashboardFilterState, DashboardPayload, ElementRow } from "@/types"
import { useBridge } from "./bridge-provider"
import { applyFilters } from "@/lib/analytics"
import { pushSessionSnapshot } from "@/lib/session-history"
import { displayText } from "@/lib/text"

interface ChartFilter {
  key: string
  field: string
}

interface DashboardState {
  payload: DashboardPayload | null
  loadError: string | null
  isRefreshing: boolean
  // filters
  filters: DashboardFilterState
  setFilters: React.Dispatch<React.SetStateAction<DashboardFilterState>>
  resetFilters: () => void
  // selection
  selectedIds: Set<string>
  setSelectedIds: React.Dispatch<React.SetStateAction<Set<string>>>
  // chart interaction
  chartFilter: ChartFilter | null
  setChartFilter: React.Dispatch<React.SetStateAction<ChartFilter | null>>
  // ghost mode — element selected from Revit or table click
  propertiesElement: ElementRow | null
  setPropertiesElement: React.Dispatch<React.SetStateAction<ElementRow | null>>
  // derived
  allRows: ElementRow[]
  filteredRows: ElementRow[]
  // actions
  refreshData: () => Promise<void>
}

const DashboardContext = createContext<DashboardState | null>(null)

const EMPTY_FILTERS: DashboardFilterState = {
  categories: [],
  families: [],
  types: [],
  levels: [],
  phases: [],
  worksets: [],
  search: "",
  hide_categories: [],
  hide_levels: [],
}

export function DashboardProvider({ children }: Readonly<{ children: React.ReactNode }>) {
  const bridge = useBridge()
  const [payload, setPayload] = useState<DashboardPayload | null>(bridge.getInitialPayload())
  const [loadError, setLoadError] = useState<string | null>(null)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [filters, setFilters] = useState<DashboardFilterState>({ ...EMPTY_FILTERS })
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [chartFilter, setChartFilter] = useState<ChartFilter | null>(null)
  const [propertiesElement, setPropertiesElement] = useState<ElementRow | null>(null)

  const refreshData = useCallback(async () => {
    setIsRefreshing(true)
    setLoadError(null)
    try {
      const newPayload = await bridge.refreshData()
      setPayload(newPayload)
      setSelectedIds(new Set())
      setChartFilter(null)
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : "Snapshot failed")
    } finally {
      setIsRefreshing(false)
    }
  }, [bridge])

  // Save session snapshot when payload changes
  useEffect(() => {
    if (payload?.kpis) {
      pushSessionSnapshot(payload.kpis)
    }
  }, [payload])

  useEffect(() => {
    if (bridge.getInitialPayload()) return
    void refreshData()
  }, [bridge, refreshData])

  // Ghost Mode: host selection-changed is translated by the bridge client.
  useEffect(() => {
    const unsubscribe = bridge.subscribeSelection((elementIds) => {
      if (!elementIds.length || !payload) return
      const element = payload.rows.find((row) => row.element_id === elementIds[0])
      if (element) setPropertiesElement(element)
    })

    const onDashboardSelect = (event: Event) => {
      const element = (event as CustomEvent<ElementRow>).detail
      if (element) setPropertiesElement(element)
    }

    window.addEventListener("dashboard-element-selected", onDashboardSelect)
    return () => {
      unsubscribe()
      window.removeEventListener("dashboard-element-selected", onDashboardSelect)
    }
  }, [bridge, payload])

  const resetFilters = useCallback(() => {
    setFilters({ ...EMPTY_FILTERS })
    setChartFilter(null)
  }, [])

  const allRows = useMemo(() => payload?.rows ?? [], [payload])

  const filteredRows = useMemo(() => {
    let rows = applyFilters(allRows, filters)
    if (chartFilter) {
      rows = rows.filter(
        (row) => displayText(row[chartFilter.field as keyof ElementRow]) === chartFilter.key,
      )
    }
    return rows
  }, [allRows, filters, chartFilter])

  const value = useMemo<DashboardState>(
    () => ({
      payload,
      loadError,
      isRefreshing,
      filters,
      setFilters,
      resetFilters,
      selectedIds,
      setSelectedIds,
      chartFilter,
      setChartFilter,
      propertiesElement,
      setPropertiesElement,
      allRows,
      filteredRows,
      refreshData,
    }),
    [
      payload,
      loadError,
      isRefreshing,
      filters,
      resetFilters,
      selectedIds,
      chartFilter,
      propertiesElement,
      allRows,
      filteredRows,
      refreshData,
    ],
  )

  return <DashboardContext value={value}>{children}</DashboardContext>
}

export function useDashboard(): DashboardState {
  const ctx = useContext(DashboardContext)
  if (!ctx) throw new Error("useDashboard must be used within <DashboardProvider>")
  return ctx
}
