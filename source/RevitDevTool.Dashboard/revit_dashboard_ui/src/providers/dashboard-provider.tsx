/**
 * Central dashboard state: payload, filters, selection, chart interaction, ghost mode.
 *
 * Supports both:
 * - Browser mode: Uses mock data
 * - Revit mode: Uses real data from WebView2 bridge
 *
 * In dev mode, listens for 'revit-data-ready' event when Revit injects data.
 */

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react"
import type {
  DashboardFilterState,
  DashboardPayload,
  ElementRow,
} from "@/types"
import { useBridge } from "./bridge-provider"
import { applyFilters } from "@/features/filters/filter-utils"
import { pushSessionSnapshot } from "@/lib/session-history"

interface ChartFilter {
  key: string
  field: string
}

interface DashboardState {
  payload: DashboardPayload | null
  isRefreshing: boolean
  // filters
  filters: DashboardFilterState
  setFilters: React.Dispatch<React.SetStateAction<DashboardFilterState>>
  resetFilters: () => void
  // selection
  selectedIds: Set<number>
  setSelectedIds: React.Dispatch<React.SetStateAction<Set<number>>>
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

export function DashboardProvider({ children }: { children: React.ReactNode }) {
  const bridge = useBridge()
  const [payload, setPayload] = useState<DashboardPayload | null>(bridge.getInitialPayload())
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [filters, setFilters] = useState<DashboardFilterState>({ ...EMPTY_FILTERS })
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set())
  const [chartFilter, setChartFilter] = useState<ChartFilter | null>(null)
  const [propertiesElement, setPropertiesElement] = useState<ElementRow | null>(null)

  // Save session snapshot when payload changes
  useEffect(() => {
    if (payload?.kpis) {
      pushSessionSnapshot(payload.kpis)
    }
  }, [payload])

  // Listen for Revit data injection in dev mode
  useEffect(() => {
    const onRevitDataReady = () => {
      const newPayload = window.__BIM_DASHBOARD_INITIAL_DATA
      if (newPayload) {
        console.log("[Dashboard] Received Revit data via injection")
        setPayload(newPayload)
      }
    }

    window.addEventListener("revit-data-ready", onRevitDataReady)
    return () => window.removeEventListener("revit-data-ready", onRevitDataReady)
  }, [])

  // Ghost Mode: Listen for Revit selection changes
  useEffect(() => {
    const onRevitSelection = (event: Event) => {
      const detail = (event as CustomEvent<{ element_ids: number[] }>).detail
      if (!detail?.element_ids?.length || !payload) return

      // Find the first matching element in our rows
      const firstId = detail.element_ids[0]
      const element = payload.rows.find((r) => r.element_id === firstId)
      if (element) {
        setPropertiesElement(element)
      }
    }

    // Listen for dashboard internal element selection (table row click)
    const onDashboardSelect = (event: Event) => {
      const element = (event as CustomEvent<ElementRow>).detail
      if (element) {
        setPropertiesElement(element)
      }
    }

    window.addEventListener("revit-selection-changed", onRevitSelection)
    window.addEventListener("dashboard-element-selected", onDashboardSelect)
    return () => {
      window.removeEventListener("revit-selection-changed", onRevitSelection)
      window.removeEventListener("dashboard-element-selected", onDashboardSelect)
    }
  }, [payload])

  const resetFilters = useCallback(() => {
    setFilters({ ...EMPTY_FILTERS })
    setChartFilter(null)
  }, [])

  const allRows = useMemo(() => payload?.rows ?? [], [payload])

  const filteredRows = useMemo(() => {
    let rows = applyFilters(allRows, filters)
    if (chartFilter) {
      rows = rows.filter(
        (row) => String(row[chartFilter.field as keyof ElementRow] ?? "") === chartFilter.key,
      )
    }
    return rows
  }, [allRows, filters, chartFilter])

  const refreshData = useCallback(async () => {
    setIsRefreshing(true)
    try {
      const newPayload = await bridge.refreshData()
      setPayload(newPayload)
      setSelectedIds(new Set())
      setChartFilter(null)
    } finally {
      setIsRefreshing(false)
    }
  }, [bridge])

  const value = useMemo<DashboardState>(
    () => ({
      payload,
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

// Type declaration for window
declare global {
  interface Window {
    __BIM_DASHBOARD_INITIAL_DATA?: DashboardPayload
  }
}
