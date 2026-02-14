/**
 * Smart Schedule — Interactive Data Grid with grouping + Parameter Auditor.
 * Enhanced Revit Schedule with Excel-like filter, context menu, and color picker.
 */

import { useEffect, useMemo, useState, useCallback, useRef } from "react"
import {
  Banner,
  Button,
  Card,
  Checkbox,
  Collapse,
  ColorPicker,
  Input,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from "@douyinfe/semi-ui"
import {
  IconSearch,
  IconFilter,
  IconDelete,
  IconTickCircle,
  IconChevronDown,
  IconTick,
  IconEyeOpened,
  IconEyeClosedSolid,
  IconCrossStroked,
} from "@douyinfe/semi-icons"
import { useDashboard } from "@/providers/dashboard-provider"
import { useBridge } from "@/providers/bridge-provider"
import { useRevitActions } from "@/hooks/use-revit-actions"
import { useScheduleSync } from "@/hooks/use-schedule-sync"
import { getFilterKey } from "@/lib/filter-keys"
import type { DashboardFilterState, ElementRow } from "@/types"

const { Text } = Typography

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function formatHeader(col: string): string {
  return col
    .replace(/_/g, " ")
    .replace(/\b\w/g, (c) => c.toUpperCase())
}

function getColumnWidth(col: string): number {
  if (col === "element_id") return 110
  if (col === "name") return 260
  return 180
}

/** Check if a cell value is empty/null/undefined */
function isEmptyValue(value: unknown): boolean {
  if (value === null || value === undefined) return true
  if (typeof value === "string" && value.trim() === "") return true
  return false
}

/** Build a composite group key from multiple columns */
function buildGroupKey(row: Record<string, unknown>, columns: string[]): string {
  return columns.map((col) => String(row[col] ?? "Unknown")).join(" / ")
}

// ---------------------------------------------------------------------------
// ColumnFilterDropdown — Excel-like filter for each column
// ---------------------------------------------------------------------------

function ColumnFilterDropdown({
  col,
  allRows,
  filters,
  setFilters,
}: {
  col: string
  allRows: ElementRow[]
  filters: DashboardFilterState
  setFilters: React.Dispatch<React.SetStateAction<DashboardFilterState>>
}) {
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState("")
  const dropdownRef = useRef<HTMLDivElement>(null)

  const filterKey = getFilterKey(col)
  const selected: string[] = ((filters[filterKey] as string[] | undefined) ?? []).slice()

  // Get unique values for this column
  const uniqueValues = useMemo(() => {
    const values = new Set<string>()
    for (const row of allRows) {
      const val = String(row[col as keyof ElementRow] ?? "")
      if (val) values.add(val)
    }
    return Array.from(values).sort()
  }, [allRows, col])

  const filteredValues = useMemo(() => {
    if (!search.trim()) return uniqueValues
    const q = search.toLowerCase()
    return uniqueValues.filter((v) => v.toLowerCase().includes(q))
  }, [uniqueValues, search])

  // Close on outside click
  useEffect(() => {
    if (!open) return
    const handler = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener("mousedown", handler)
    return () => document.removeEventListener("mousedown", handler)
  }, [open])

  // Helper function to handle option toggle (shared logic)
  const handleOptionToggle = useCallback((opt: string, checked: boolean) => {
    const next = checked
      ? Array.from(new Set([...selected, opt]))
      : selected.filter((v) => v !== opt)
    setFilters((prev) => ({ ...prev, [filterKey]: next }))
  }, [selected, filterKey, setFilters])

  function handleSelectAll() {
    setFilters((prev) => ({ ...prev, [filterKey]: [...filteredValues] }))
  }

  function handleClearAll() {
    setFilters((prev) => ({ ...prev, [filterKey]: [] }))
  }

  return (
    <div style={{ position: "relative", display: "inline-flex" }} ref={dropdownRef}>
      <span
        role="button"
        tabIndex={0}
        onClick={(e) => { e.stopPropagation(); setOpen(!open) }}
        onKeyDown={(e) => { if (e.key === "Enter") { e.stopPropagation(); setOpen(!open) } }}
        style={{
          cursor: "pointer",
          marginLeft: 4,
          color: selected.length > 0 ? "var(--semi-color-primary)" : "var(--semi-color-text-2)",
          display: "inline-flex",
          alignItems: "center",
        }}
      >
        <IconChevronDown size="small" />
        {selected.length > 0 && (
          <Tag size="small" color="blue" shape="circle" style={{ marginLeft: 2, minWidth: 16, height: 16, fontSize: 10, padding: "0 4px" }}>
            {selected.length}
          </Tag>
        )}
      </span>

      {open && (
        <div
          style={{
            position: "absolute",
            top: "100%",
            left: -80,
            zIndex: 1000,
            width: 220,
            background: "var(--semi-color-bg-2)",
            border: "1px solid var(--semi-color-border)",
            borderRadius: 8,
            boxShadow: "var(--semi-shadow-elevated)",
            padding: 8,
            maxHeight: 320,
            display: "flex",
            flexDirection: "column",
          }}
          onClick={(e) => e.stopPropagation()}
        >
          {/* Search */}
          <Input
            prefix={<IconSearch />}
            value={search}
            onChange={(val) => setSearch(String(val ?? ""))}
            placeholder={`Search ${formatHeader(col)}...`}
            showClear
            size="small"
            style={{ marginBottom: 6 }}
          />

          {/* Select All / Clear All */}
          <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 6 }}>
            <Button size="small" theme="borderless" onClick={handleSelectAll}>
              Select All
            </Button>
            <Button size="small" theme="borderless" onClick={handleClearAll}>
              Clear All
            </Button>
          </div>

          {/* Checkbox list */}
          <div style={{ flex: 1, overflow: "auto", maxHeight: 200 }}>
            {filteredValues.map((val) => (
              <div key={val} style={{ padding: "2px 0" }}>
                <Checkbox
                  checked={selected.includes(val)}
                  onChange={(e) => handleOptionToggle(val, e.target.checked === true)}
                >
                  <Text size="small" ellipsis={{ showTooltip: true }} style={{ maxWidth: 160 }}>
                    {val}
                  </Text>
                </Checkbox>
              </div>
            ))}
            {filteredValues.length === 0 && (
              <Text type="tertiary" size="small" style={{ padding: 8, textAlign: "center", display: "block" }}>
                No matches
              </Text>
            )}
          </div>

          {/* Apply / done */}
          <div style={{ borderTop: "1px solid var(--semi-color-border)", paddingTop: 6, marginTop: 4 }}>
            <Button size="small" icon={<IconTick />} onClick={() => setOpen(false)} block>
              Done
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}

// ---------------------------------------------------------------------------
// FilterSection — sidebar filter panel with Select All / Deselect All
// ---------------------------------------------------------------------------

function FilterSection({
  col,
  options,
  filters,
  setFilters,
}: {
  col: string
  options: string[]
  filters: DashboardFilterState
  setFilters: React.Dispatch<React.SetStateAction<DashboardFilterState>>
}) {
  const filterKey = getFilterKey(col)
  const selected: string[] = ((filters[filterKey] as string[] | undefined) ?? []).slice()

  // Helper function to handle option toggle
  const handleOptionToggle = useCallback((opt: string, checked: boolean, selected: string[], filterKey: string) => {
    const next = checked
      ? Array.from(new Set([...selected, opt]))
      : selected.filter((v) => v !== opt)
    setFilters((prev) => ({ ...prev, [filterKey]: next }))
  }, [setFilters])

  function handleChange(opt: string, checked: boolean) {
    handleOptionToggle(opt, checked, selected, filterKey)
  }

  function handleSelectAll() {
    setFilters((prev) => ({ ...prev, [filterKey]: [...options] }))
  }

  function handleClearAll() {
    setFilters((prev) => ({ ...prev, [filterKey]: [] }))
  }

  return (
    <Collapse.Panel
      key={col}
      itemKey={col}
      header={
        <Space>
          <Text>{formatHeader(col)}</Text>
          {selected.length > 0 && <Tag size="small" color="blue">{selected.length}</Tag>}
        </Space>
      }
    >
      {/* Select All / Clear All */}
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
        <Button size="small" theme="borderless" style={{ fontSize: 11, padding: "0 4px" }} onClick={handleSelectAll}>
          Select All
        </Button>
        <Button size="small" theme="borderless" style={{ fontSize: 11, padding: "0 4px" }} onClick={handleClearAll}>
          Clear All
        </Button>
      </div>

      <div style={{ display: "grid", gap: 6, maxHeight: 200, overflow: "auto", padding: "4px 0" }}>
        {options.map((opt) => (
          <Checkbox
            key={`${col}-${opt}`}
            checked={selected.includes(opt)}
            onChange={(event) => handleChange(opt, event.target.checked === true)}
          >
            <Text size="small" ellipsis={{ showTooltip: true }} style={{ maxWidth: 180 }}>
              {opt}
            </Text>
          </Checkbox>
        ))}
      </div>
    </Collapse.Panel>
  )
}

// ---------------------------------------------------------------------------
// ContextMenu — right-click actions for selected rows (with Semi icons)
// ---------------------------------------------------------------------------

interface ContextMenuState {
  visible: boolean
  x: number
  y: number
  showColorPicker: boolean
}

function RowContextMenu({
  state,
  onClose,
  selectedCount,
  onSelect,
  onZoom,
  onIsolate,
  onResetIsolation,
  onColorOverride,
  onClearOverrides,
  onClearSelection,
  isLoading,
}: {
  state: ContextMenuState
  onClose: () => void
  selectedCount: number
  onSelect: () => void
  onZoom: () => void
  onIsolate: () => void
  onResetIsolation: () => void
  onColorOverride: (color: [number, number, number]) => void
  onClearOverrides: () => void
  onClearSelection: () => void
  isLoading: boolean
}) {
  const menuRef = useRef<HTMLDivElement>(null)

  // Close on outside click — but keep open if click lands inside a
  // Semi portal (e.g. the ColorPicker popover rendered at document.body).
  useEffect(() => {
    if (!state.visible) return
    const handler = (e: MouseEvent) => {
      const target = e.target as HTMLElement
      // Click inside the context menu itself — keep open
      if (menuRef.current?.contains(target)) return
      // Click inside a Semi popover / portal (ColorPicker) — keep open
      if (target.closest?.(".semi-popover, .semi-popover-wrapper, .semi-portal")) return
      onClose()
    }
    document.addEventListener("mousedown", handler)
    return () => document.removeEventListener("mousedown", handler)
  }, [state.visible, onClose])

  if (!state.visible || selectedCount === 0) return null

  const itemStyle: React.CSSProperties = {
    padding: "6px 12px",
    cursor: isLoading ? "wait" : "pointer",
    fontSize: 13,
    display: "flex",
    alignItems: "center",
    gap: 8,
    whiteSpace: "nowrap",
    borderRadius: 4,
  }

  const hoverBg = "var(--semi-color-fill-0)"

  return (
    <div
      ref={menuRef}
      style={{
        position: "fixed",
        top: state.y,
        left: state.x,
        zIndex: 10000,
        background: "var(--semi-color-bg-2)",
        border: "1px solid var(--semi-color-border)",
        borderRadius: 8,
        boxShadow: "var(--semi-shadow-elevated)",
        padding: "4px 0",
        minWidth: 220,
      }}
    >
      <Text type="tertiary" size="small" style={{ padding: "4px 12px 6px", display: "block" }}>
        {selectedCount} element{selectedCount > 1 ? "s" : ""} selected
      </Text>

      <div style={{ borderTop: "1px solid var(--semi-color-border)", margin: "2px 0" }} />

      <div
        style={itemStyle}
        onMouseEnter={(e) => { (e.currentTarget as HTMLElement).style.background = hoverBg }}
        onMouseLeave={(e) => { (e.currentTarget as HTMLElement).style.background = "transparent" }}
        onClick={() => { onSelect(); onClose() }}
      >
        <IconTickCircle size="small" style={{ color: "var(--semi-color-primary)" }} />
        Select in Revit
      </div>
      <div
        style={itemStyle}
        onMouseEnter={(e) => { (e.currentTarget as HTMLElement).style.background = hoverBg }}
        onMouseLeave={(e) => { (e.currentTarget as HTMLElement).style.background = "transparent" }}
        onClick={() => { onZoom(); onClose() }}
      >
        <IconSearch size="small" style={{ color: "var(--semi-color-primary)" }} />
        Zoom to Selection
      </div>
      <div
        style={itemStyle}
        onMouseEnter={(e) => { (e.currentTarget as HTMLElement).style.background = hoverBg }}
        onMouseLeave={(e) => { (e.currentTarget as HTMLElement).style.background = "transparent" }}
        onClick={() => { onIsolate(); onClose() }}
      >
        <IconEyeOpened size="small" style={{ color: "var(--semi-color-tertiary)" }} />
        Isolate
      </div>
      <div
        style={itemStyle}
        onMouseEnter={(e) => { (e.currentTarget as HTMLElement).style.background = hoverBg }}
        onMouseLeave={(e) => { (e.currentTarget as HTMLElement).style.background = "transparent" }}
        onClick={() => { onResetIsolation(); onClose() }}
      >
        <IconEyeClosedSolid size="small" style={{ color: "var(--semi-color-tertiary)" }} />
        Reset Isolation
      </div>

      <div style={{ borderTop: "1px solid var(--semi-color-border)", margin: "2px 0" }} />

      {/* Semi Design ColorPicker — usePopover positioned ABOVE the trigger
          so it doesn't get cut off at the bottom of the context menu. */}
      <div
        style={{ padding: "6px 12px" }}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <ColorPicker
          alpha={false}
          usePopover={true}
          popoverProps={{ position: "topLeft", zIndex: 10001 }}
          defaultValue={ColorPicker.colorStringToValue("#4285f4")}
          onChange={(value) => {
            const { r, g, b } = value.rgba
            onColorOverride([r, g, b])
          }}
        >
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 8,
              fontSize: 13,
              cursor: "pointer",
              whiteSpace: "nowrap",
            }}
          >
            <div
              style={{
                width: 16,
                height: 16,
                borderRadius: 3,
                backgroundColor: "#4285f4",
                border: "1px solid var(--semi-color-border)",
                flexShrink: 0,
              }}
            />
            <span>Override Color</span>
          </div>
        </ColorPicker>
      </div>

      <div style={{ borderTop: "1px solid var(--semi-color-border)", margin: "2px 0" }} />

      <div
        style={itemStyle}
        onMouseEnter={(e) => { (e.currentTarget as HTMLElement).style.background = hoverBg }}
        onMouseLeave={(e) => { (e.currentTarget as HTMLElement).style.background = "transparent" }}
        onClick={() => { onClearOverrides(); onClose() }}
      >
        <IconDelete size="small" style={{ color: "var(--semi-color-warning)" }} />
        Clear Overrides
      </div>

      <div style={{ borderTop: "1px solid var(--semi-color-border)", margin: "2px 0" }} />

      <div
        style={{ ...itemStyle, color: "var(--semi-color-danger)" }}
        onMouseEnter={(e) => { (e.currentTarget as HTMLElement).style.background = hoverBg }}
        onMouseLeave={(e) => { (e.currentTarget as HTMLElement).style.background = "transparent" }}
        onClick={() => { onClearSelection(); onClose() }}
      >
        <IconCrossStroked size="small" />
        Clear Selection
      </div>
    </div>
  )
}

// ---------------------------------------------------------------------------
// SchedulePage
// ---------------------------------------------------------------------------

export function SchedulePage({ active = true }: { active?: boolean } = {}) {
  const {
    payload,
    filteredRows,
    allRows,
    filters,
    setFilters,
    resetFilters,
    selectedIds,
    setSelectedIds,
    chartFilter,
    setChartFilter,
  } = useDashboard()

  const bridge = useBridge()
  const { isLoading, selectInRevit, zoomTo, isolate, resetIsolation, colorOverride, clearOverrides } =
    useRevitActions()

  const [groupByColumns, setGroupByColumns] = useState<string[]>([])
  const [highlightEmpty, setHighlightEmpty] = useState(false)
  const [tableSearch, setTableSearch] = useState("")

  // Context menu state
  const [contextMenu, setContextMenu] = useState<ContextMenuState>({
    visible: false,
    x: 0,
    y: 0,
    showColorPicker: false,
  })

  // Auto-sync Schedule filter/group state to Revit (color overrides + isolation)
  // Pass first groupBy column for the sync hook
  useScheduleSync(active, filteredRows, groupByColumns[0])

  const selectedIdArray = useMemo(() => Array.from(selectedIds), [selectedIds])

  const activeFilterCount = useMemo(
    () =>
      Object.values(filters).filter((v) => v && (Array.isArray(v) ? v.length > 0 : String(v).trim() !== ""))
        .length,
    [filters],
  )

  // Additional local search filter applied on top of global filteredRows
  const searchFilteredRows = useMemo(() => {
    if (!tableSearch.trim()) return filteredRows
    const needle = tableSearch.toLowerCase()
    return filteredRows.filter((row) => {
      // Search across all string-valued columns
      for (const val of Object.values(row)) {
        if (typeof val === "string" && val.toLowerCase().includes(needle)) return true
        if (typeof val === "number" && String(val).includes(needle)) return true
      }
      return false
    })
  }, [filteredRows, tableSearch])

  // Build table columns with Excel-like filter dropdowns and empty-cell highlighting
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const tableColumns: any[] = useMemo(() => {
    const columns = payload?.columns ?? []
    const visible = ["element_id", "name", "category", "family", "type", "level", "phase", "workset"].filter((c) =>
      columns.includes(c),
    )
    return visible.map((col) => ({
      title: (
        <Space style={{ whiteSpace: "nowrap" }}>
          <span>{formatHeader(col)}</span>
          {col !== "element_id" && col !== "name" && (
            <ColumnFilterDropdown
              col={col}
              allRows={allRows}
              filters={filters}
              setFilters={setFilters}
            />
          )}
        </Space>
      ),
      dataIndex: col,
      key: col,
      width: getColumnWidth(col),
      sorter: (a?: ElementRow, b?: ElementRow) => {
        const aVal = String((a as Record<string, unknown>)?.[col] ?? "")
        const bVal = String((b as Record<string, unknown>)?.[col] ?? "")
        return aVal.localeCompare(bVal)
      },
      render: (text: unknown) => {
        const empty = isEmptyValue(text)
        const showDash = highlightEmpty && empty
        let displayText: string
        if (empty) {
          displayText = showDash ? "\u2014" : ""
        } else {
          displayText = String(text)
        }
        return (
          <span
            style={{
              background: showDash ? "rgba(255,77,79,0.12)" : undefined,
              padding: showDash ? "2px 4px" : undefined,
              borderRadius: 2,
              color: showDash ? "var(--semi-color-danger)" : undefined,
            }}
          >
            {displayText}
          </span>
        )
      },
    }))
  }, [payload?.columns, highlightEmpty, allRows, filters, setFilters])

  // Group data if groupByColumns is set
  const tableData = useMemo(() => {
    const rows = searchFilteredRows.map((row) => ({ ...row, key: row.element_id }))

    if (groupByColumns.length === 0) return rows

    // Sort by the groupBy columns so groups are contiguous
    return [...rows].sort((a, b) => {
      for (const col of groupByColumns) {
        const aVal = String((a as Record<string, unknown>)[col] ?? "")
        const bVal = String((b as Record<string, unknown>)[col] ?? "")
        const cmp = aVal.localeCompare(bVal)
        if (cmp !== 0) return cmp
      }
      return 0
    })
  }, [searchFilteredRows, groupByColumns])

  // Group-aware pagination: compute groups and paginate by group
  const [groupPage, setGroupPage] = useState(1)
  const groupsPerPage = 10

  const allGroups = useMemo(() => {
    if (groupByColumns.length === 0) return null
    const groups = new Map<string, { count: number; ids: number[] }>()
    for (const row of tableData) {
      const key = buildGroupKey(row as Record<string, unknown>, groupByColumns)
      const existing = groups.get(key)
      if (existing) {
        existing.count++
        existing.ids.push(row.element_id)
      } else {
        groups.set(key, { count: 1, ids: [row.element_id] })
      }
    }
    return groups
  }, [groupByColumns, tableData])


  const paginatedTableData = useMemo(() => {
    if (!allGroups || groupByColumns.length === 0) return tableData

    // Get the group keys for the current page
    const groupKeys = Array.from(allGroups.keys())
    const startIdx = (groupPage - 1) * groupsPerPage
    const pageGroupKeys = new Set(groupKeys.slice(startIdx, startIdx + groupsPerPage))

    return tableData.filter((row) => {
      const key = buildGroupKey(row as Record<string, unknown>, groupByColumns)
      return pageGroupKeys.has(key)
    })
  }, [tableData, allGroups, groupByColumns, groupPage, groupsPerPage])

  const totalGroupPages = allGroups ? Math.ceil(allGroups.size / groupsPerPage) : 0

  // Handle row click for properties panel
  const handleRowClick = useCallback(
    (record: ElementRow) => {
      window.dispatchEvent(new CustomEvent("dashboard-element-selected", { detail: record }))
    },
    [],
  )

  // Context menu handler
  const handleContextMenu = useCallback(
    (e: React.MouseEvent) => {
      if (selectedIds.size === 0) return
      e.preventDefault()
      setContextMenu({
        visible: true,
        x: e.clientX,
        y: e.clientY,
        showColorPicker: false,
      })
    },
    [selectedIds],
  )

  const closeContextMenu = useCallback(() => {
    setContextMenu((prev) => ({ ...prev, visible: false }))
  }, [])

  // Keyboard shortcuts for this page
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      const isInput = document.activeElement?.tagName === "INPUT"
      if (e.ctrlKey && e.key === "a" && !isInput) {
        e.preventDefault()
        setSelectedIds(new Set(searchFilteredRows.slice(0, 500).map((r) => r.element_id)))
      }
    }
    window.addEventListener("keydown", handler)
    return () => window.removeEventListener("keydown", handler)
  }, [searchFilteredRows, setSelectedIds])

  // Handle Group By change — explicitly reset groupPage
  const handleGroupByChange = useCallback((val: string[] | string | undefined) => {
    let newColumns: string[]
    if (Array.isArray(val)) {
      newColumns = val
    } else if (val) {
      newColumns = [val]
    } else {
      newColumns = []
    }
    setGroupByColumns(newColumns)
    setGroupPage(1)
  }, [])

  return (
    <div style={{ display: "flex", height: "100%" }}>
      {/* Sidebar Filters */}
      <div
        style={{
          width: 260,
          backgroundColor: "var(--semi-color-bg-1)",
          borderRight: "1px solid var(--semi-color-border)",
          overflow: "auto",
          flexShrink: 0,
        }}
      >
        <div style={{ padding: "12px 16px" }}>
          <Space style={{ width: "100%", justifyContent: "space-between", marginBottom: 12 }}>
            <Space>
              <IconFilter size="small" />
              <Text strong>Filters</Text>
              {activeFilterCount > 0 && (
                <Tag color="blue" size="small" shape="circle">{activeFilterCount}</Tag>
              )}
            </Space>
            <Button
              theme="borderless"
              size="small"
              icon={<IconDelete />}
              onClick={resetFilters}
            >
              Clear
            </Button>
          </Space>

          <Input
            prefix={<IconSearch />}
            value={filters.search ?? ""}
            onChange={(value) => setFilters((prev) => ({ ...prev, search: String(value ?? "") }))}
            placeholder="Search elements..."
            showClear
            style={{ marginBottom: 8 }}
          />

          <Collapse>
            {(payload?.filterable_columns ?? Object.keys(payload?.filter_options ?? {})).map((col) => (
              <FilterSection
                key={col}
                col={col}
                options={payload?.filter_options[col] ?? []}
                filters={filters}
                setFilters={setFilters}
              />
            ))}
          </Collapse>

          {/* Audit section */}
          <div style={{ marginTop: 12, padding: "8px 0", borderTop: "1px solid var(--semi-color-border)" }}>
            <Text type="tertiary" size="small" style={{ display: "block", marginBottom: 6 }}>
              Audit
            </Text>
            <Checkbox
              checked={highlightEmpty}
              onChange={(e) => setHighlightEmpty(e.target.checked === true)}
            >
              <Text size="small">Audit Missing Data</Text>
            </Checkbox>
          </div>
        </div>
      </div>

      {/* Main Table Area */}
      <div style={{ flex: 1, overflow: "auto", padding: 16 }}>
        {/* Chart filter banner */}
        {chartFilter && (
          <Banner
            type="warning"
            fullMode={false}
            closeIcon={null}
            style={{ marginBottom: 12, borderRadius: 8 }}
            icon={<IconTickCircle />}
            description={
              <Space>
                <Text>
                  Chart filter: <Text strong>{chartFilter.field} = {chartFilter.key}</Text>
                </Text>
                <Button size="small" theme="borderless" onClick={() => setChartFilter(null)}>
                  Clear
                </Button>
              </Space>
            }
          />
        )}

        {/* Table toolbar */}
        <Card
          title={
            <Space>
              <Text strong>Element Data</Text>
              <Tag size="small">{searchFilteredRows.length.toLocaleString()} / {allRows.length.toLocaleString()} rows</Tag>
              {selectedIds.size > 0 && (
                <Tag color="blue" size="small">
                  {selectedIds.size} selected
                </Tag>
              )}
            </Space>
          }
          headerExtraContent={
            <Space>
              {/* Global search bar */}
              <Input
                prefix={<IconSearch />}
                value={tableSearch}
                onChange={(val) => setTableSearch(String(val ?? ""))}
                placeholder="Search all columns..."
                showClear
                size="small"
                style={{ width: 200 }}
              />
              <Select
                placeholder="Group by..."
                value={groupByColumns}
                onChange={(val) => handleGroupByChange(val as string[] | undefined)}
                multiple
                showClear
                size="small"
                style={{ minWidth: 160 }}
                maxTagCount={2}
                optionList={["category", "family", "level", "phase", "workset"].map((c) => ({
                  label: formatHeader(c),
                  value: c,
                }))}
              />
            </Space>
          }
          headerStyle={{ padding: "8px 16px" }}
          bodyStyle={{ padding: "0 12px 12px 12px" }}
        >
          {/* Group headers info */}
          {groupByColumns.length > 0 && allGroups && (
            <div style={{ padding: "6px 4px", background: "var(--semi-color-fill-0)", display: "flex", gap: 6, flexWrap: "wrap", borderRadius: 4, marginTop: 4 }}>
              <Text type="tertiary" size="small">
                Grouped by {groupByColumns.map(formatHeader).join(" + ")}:
              </Text>
              <Tag size="small" color="violet">{allGroups.size} groups</Tag>
              {totalGroupPages > 1 && (
                <Space style={{ marginLeft: "auto" }}>
                  <Button
                    size="small"
                    theme="borderless"
                    disabled={groupPage <= 1}
                    onClick={() => setGroupPage((p) => Math.max(1, p - 1))}
                  >
                    Prev
                  </Button>
                  <Text size="small">{groupPage} / {totalGroupPages}</Text>
                  <Button
                    size="small"
                    theme="borderless"
                    disabled={groupPage >= totalGroupPages}
                    onClick={() => setGroupPage((p) => Math.min(totalGroupPages, p + 1))}
                  >
                    Next
                  </Button>
                </Space>
              )}
            </div>
          )}

          <div onContextMenu={handleContextMenu}>
            {/* key forces full remount when switching between grouped/flat modes,
                preventing Semi Table from caching stale internal group state. */}
            <Table
              key={groupByColumns.length > 0 ? `grouped-${groupByColumns.join("-")}` : "flat"}
              size="small"
              columns={tableColumns}
              dataSource={
                groupByColumns.length > 0 && paginatedTableData.length > 0
                  ? paginatedTableData
                  : tableData
              }
              pagination={groupByColumns.length > 0
                ? false
                : { pageSize: 50, showSizeChanger: true, pageSizeOpts: [25, 50, 100, 200] }
              }
              scroll={{ x: "max-content" }}
              rowSelection={{
                selectedRowKeys: selectedIdArray,
                onChange: (keys) => setSelectedIds(new Set((keys as number[]).map(Number))),
              }}
              onRow={(record) => ({
                onClick: () => handleRowClick(record as ElementRow),
                style: { cursor: "pointer" },
              })}
              groupBy={groupByColumns.length > 0
                ? ((record) => buildGroupKey(record as Record<string, unknown>, groupByColumns))
                : undefined
              }
              renderGroupSection={groupByColumns.length > 0
                ? (groupKey) => {
                    if (groupKey == null) return null
                    const groupIds = searchFilteredRows
                      .filter((r) => buildGroupKey(r as Record<string, unknown>, groupByColumns) === String(groupKey))
                      .map((r) => r.element_id)
                    return (
                      <div
                        role="button"
                        tabIndex={0}
                        onClick={() => bridge.isolateElements(groupIds)}
                        onKeyDown={(e) => { if (e.key === "Enter") bridge.isolateElements(groupIds) }}
                        style={{ cursor: "pointer", padding: "4px 8px", display: "inline-flex", alignItems: "center", gap: 6 }}
                        title={`Click to isolate ${groupIds.length} elements in Revit`}
                      >
                        {groupByColumns.map((col) => (
                          <Tag key={col} color="blue" size="small">{formatHeader(col)}</Tag>
                        ))}
                        <Text strong size="small">{String(groupKey)}</Text>
                        <Text type="tertiary" size="small">({groupIds.length})</Text>
                      </div>
                    )
                  }
                : undefined
              }
            />
          </div>
        </Card>
      </div>

      {/* Context menu for right-click actions */}
      <RowContextMenu
        state={contextMenu}
        onClose={closeContextMenu}
        selectedCount={selectedIds.size}
        onSelect={() => selectInRevit(selectedIdArray)}
        onZoom={() => zoomTo(selectedIdArray)}
        onIsolate={() => isolate(selectedIdArray)}
        onResetIsolation={resetIsolation}
        onColorOverride={(color) => colorOverride(selectedIdArray, color)}
        onClearOverrides={clearOverrides}
        onClearSelection={() => setSelectedIds(new Set())}
        isLoading={isLoading}
      />
    </div>
  )
}
