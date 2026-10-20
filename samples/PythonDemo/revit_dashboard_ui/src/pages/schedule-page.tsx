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
  Dropdown,
  Input,
  ResizeGroup,
  ResizeHandler,
  ResizeItem,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from "@douyinfe/semi-ui"
import { IconSearch } from "@douyinfe/semi-icons"
import { Check, ChevronDown, CircleCheck, Eye, EyeOff, Filter, Search, X } from "lucide-react"
import { useDashboard } from "@/providers/dashboard-provider"
import { useBridge } from "@/providers/bridge-provider"
import { useRevitActions } from "@/hooks/use-revit-actions"
import { useScheduleSync } from "@/hooks/use-schedule-sync"
import { formatColumnHeader } from "@/features/filters/filter-utils"
import {
  getFilterKey,
  GROUP_COLUMNS,
  SCHEDULE_COLUMNS,
  type ScheduleFilterField,
} from "@/lib/filter-keys"
import type { DashboardFilterState, ElementRow } from "@/types"
import { displayText } from "@/lib/text"

const { Text } = Typography

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Check if a cell value is empty/null/undefined */
function isEmptyValue(value: unknown): boolean {
  if (value === null || value === undefined) return true
  if (typeof value === "string" && value.trim() === "") return true
  return false
}

/** Build a composite group key from multiple columns */
function buildGroupKey(row: Record<string, unknown>, columns: string[]): string {
  return columns.map((col) => displayText(row[col], "Unknown")).join(" / ")
}

const FLAT_PAGINATION = {
  pageSize: 50,
  showSizeChanger: true,
  pageSizeOpts: [25, 50, 100, 200],
}

const SELECTION_COLUMN_WIDTH = 48
const HEADER_ROW_HEIGHT = 40
const PAGER_HEIGHT = 56

function fitWidths(weights: number[], available: number): number[] {
  if (weights.length === 0 || available <= 0) return weights
  const total = weights.reduce((sum, weight) => sum + weight, 0)
  const widths = weights.map((weight) => Math.floor((weight / total) * available))
  const used = widths.reduce((sum, width) => sum + width, 0)
  widths[widths.length - 1] += available - used
  return widths
}

function useScrollbarWidth(): number {
  const [width] = useState(() => {
    const probe = document.createElement("div")
    probe.style.cssText = "position:absolute;top:-9999px;width:100px;height:100px;overflow:scroll"
    document.body.appendChild(probe)
    const value = probe.offsetWidth - probe.clientWidth
    probe.remove()
    return value
  })
  return width
}

function useFillSize() {
  const ref = useRef<HTMLDivElement>(null)
  const [size, setSize] = useState({ width: 0, height: 0 })

  useEffect(() => {
    const node = ref.current
    if (!node) return
    const measure = () => {
      const width = node.clientWidth
      const height = node.clientHeight
      setSize((prev) => (prev.width === width && prev.height === height ? prev : { width, height }))
    }
    measure()
    const observer = new ResizeObserver(measure)
    observer.observe(node)
    return () => observer.disconnect()
  }, [])

  return { ref, width: size.width, height: size.height }
}

// ---------------------------------------------------------------------------
// ColumnFilterDropdown — Excel-like filter for each column
// ---------------------------------------------------------------------------

function ColumnFilterDropdown({
  col,
  filterKey,
  allRows,
  filters,
  setFilters,
}: Readonly<{
  col: string
  filterKey: ScheduleFilterField
  allRows: ElementRow[]
  filters: DashboardFilterState
  setFilters: React.Dispatch<React.SetStateAction<DashboardFilterState>>
}>) {
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState("")
  const dropdownRef = useRef<HTMLDivElement>(null)

  const selected = [...(filters[filterKey] ?? [])]

  // Get unique values for this column
  const uniqueValues = useMemo(() => {
    const values = new Set<string>()
    for (const row of allRows) {
      const val = displayText(row[col as keyof ElementRow])
      if (val) values.add(val)
    }
    return Array.from(values).sort()
  }, [allRows, col])

  const filteredValues = useMemo(() => {
    if (!search.trim()) return uniqueValues
    const q = search.toLowerCase()
    return uniqueValues.filter((v) => v.toLowerCase().includes(q))
  }, [uniqueValues, search])

  // Close on outside click. Clicks inside stay here so the column header does not sort.
  useEffect(() => {
    const node = dropdownRef.current
    if (!node) return
    const stop = (event: MouseEvent) => event.stopPropagation()
    node.addEventListener("click", stop)
    return () => node.removeEventListener("click", stop)
  }, [])

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
  const handleOptionToggle = useCallback(
    (opt: string, checked: boolean) => {
      const next = checked
        ? Array.from(new Set([...selected, opt]))
        : selected.filter((v) => v !== opt)
      setFilters((prev) => ({ ...prev, [filterKey]: next }))
    },
    [selected, filterKey, setFilters],
  )

  function handleSelectAll() {
    setFilters((prev) => ({ ...prev, [filterKey]: [...filteredValues] }))
  }

  function handleClearAll() {
    setFilters((prev) => ({ ...prev, [filterKey]: [] }))
  }

  return (
    <div style={{ position: "relative", display: "inline-flex" }} ref={dropdownRef}>
      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation()
          setOpen(!open)
        }}
        style={{
          cursor: "pointer",
          marginLeft: 4,
          padding: 0,
          border: "none",
          background: "transparent",
          color: selected.length > 0 ? "var(--semi-color-primary)" : "var(--semi-color-text-2)",
          display: "inline-flex",
          alignItems: "center",
        }}
      >
        <ChevronDown size={14} />
        {selected.length > 0 && (
          <Tag
            size="small"
            color="blue"
            shape="circle"
            style={{ marginLeft: 2, minWidth: 16, height: 16, fontSize: 10, padding: "0 4px" }}
          >
            {selected.length}
          </Tag>
        )}
      </button>

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
        >
          {/* Search */}
          <Input
            prefix={<IconSearch />}
            value={search}
            onChange={(val) => setSearch(String(val ?? ""))}
            placeholder={`Search ${formatColumnHeader(col)}...`}
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
              <Text
                type="tertiary"
                size="small"
                style={{ padding: 8, textAlign: "center", display: "block" }}
              >
                No matches
              </Text>
            )}
          </div>

          {/* Apply / done */}
          <div
            style={{ borderTop: "1px solid var(--semi-color-border)", paddingTop: 6, marginTop: 4 }}
          >
            <Button size="small" icon={<Check size={14} />} onClick={() => setOpen(false)} block>
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
}: Readonly<{
  col: string
  options: string[]
  filters: DashboardFilterState
  setFilters: React.Dispatch<React.SetStateAction<DashboardFilterState>>
}>) {
  const filterKey = getFilterKey(col)
  const selected = filterKey ? [...(filters[filterKey] ?? [])] : []

  const handleOptionToggle = useCallback(
    (opt: string, checked: boolean, current: string[], key: ScheduleFilterField) => {
      const next = checked
        ? Array.from(new Set([...current, opt]))
        : current.filter((v) => v !== opt)
      setFilters((prev) => ({ ...prev, [key]: next }))
    },
    [setFilters],
  )

  if (!filterKey) return null
  const key = filterKey

  function handleChange(opt: string, checked: boolean) {
    handleOptionToggle(opt, checked, selected, key)
  }

  function handleSelectAll() {
    setFilters((prev) => ({ ...prev, [key]: [...options] }))
  }

  function handleClearAll() {
    setFilters((prev) => ({ ...prev, [key]: [] }))
  }

  return (
    <Collapse.Panel
      key={col}
      itemKey={col}
      header={
        <Space>
          <Text>{formatColumnHeader(col)}</Text>
          {selected.length > 0 && (
            <Tag size="small" color="blue">
              {selected.length}
            </Tag>
          )}
        </Space>
      }
    >
      {/* Select All / Clear All */}
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
        <Button
          size="small"
          theme="borderless"
          style={{ fontSize: 11, padding: "0 4px" }}
          onClick={handleSelectAll}
        >
          Select All
        </Button>
        <Button
          size="small"
          theme="borderless"
          style={{ fontSize: 11, padding: "0 4px" }}
          onClick={handleClearAll}
        >
          Clear All
        </Button>
      </div>

      <div
        style={{
          display: "grid",
          gap: 6,
          maxHeight: 200,
          overflow: "auto",
          minWidth: 0,
          padding: "4px 0",
        }}
      >
        {options.map((opt) => (
          <div
            key={`${col}-${opt}`}
            style={{ display: "flex", alignItems: "center", gap: 8, minWidth: 0 }}
          >
            <Checkbox
              checked={selected.includes(opt)}
              onChange={(event) => handleChange(opt, event.target.checked === true)}
            />
            <Text
              size="small"
              ellipsis={{ showTooltip: true }}
              style={{ flex: 1, minWidth: 0, cursor: "pointer" }}
              onClick={() => handleChange(opt, !selected.includes(opt))}
            >
              {opt}
            </Text>
          </div>
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
}: Readonly<{
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
}>) {
  const [overrideColor, setOverrideColor] = useState(() =>
    ColorPicker.colorStringToValue("#4285f4"),
  )
  const { r, g, b } = overrideColor.rgba
  const swatch = `rgb(${r}, ${g}, ${b})`
  const open = state.visible && selectedCount > 0

  return (
    <Dropdown
      trigger="custom"
      visible={open}
      position="bottomLeft"
      clickToHide={false}
      zIndex={10000}
      onClickOutSide={(event) => {
        const target = event.target
        if (!(target instanceof Element)) return
        if (target.closest(".semi-popover, .semi-popover-wrapper, .semi-portal, .semi-colorPicker"))
          return
        onClose()
      }}
      render={
        <Dropdown.Menu>
          <Dropdown.Title>
            {selectedCount} element{selectedCount > 1 ? "s" : ""} selected
          </Dropdown.Title>
          <Dropdown.Item
            icon={<CircleCheck size={16} />}
            onClick={() => {
              onSelect()
              onClose()
            }}
          >
            Select in Revit
          </Dropdown.Item>
          <Dropdown.Item
            icon={<Search size={16} />}
            onClick={() => {
              onZoom()
              onClose()
            }}
          >
            Zoom to Selection
          </Dropdown.Item>
          <Dropdown.Item
            icon={<Eye size={16} />}
            onClick={() => {
              onIsolate()
              onClose()
            }}
          >
            Isolate
          </Dropdown.Item>
          <Dropdown.Item
            icon={<EyeOff size={16} />}
            onClick={() => {
              onResetIsolation()
              onClose()
            }}
          >
            Reset Isolation
          </Dropdown.Item>
          <Dropdown.Divider />
          <ColorPicker
            alpha={false}
            usePopover
            value={overrideColor}
            popoverProps={{ trigger: "click", position: "topLeft", zIndex: 10001 }}
            onChange={(value) => {
              setOverrideColor(value)
              const next = value.rgba
              onColorOverride([next.r, next.g, next.b])
            }}
          >
            <Dropdown.Item
              icon={
                <span
                  style={{
                    width: 16,
                    height: 16,
                    borderRadius: 3,
                    backgroundColor: swatch,
                    border: "1px solid var(--semi-color-border)",
                    display: "inline-block",
                  }}
                />
              }
            >
              Override Color
            </Dropdown.Item>
          </ColorPicker>
          <Dropdown.Divider />
          <Dropdown.Item
            type="warning"
            icon={<X size={16} />}
            onClick={() => {
              onClearOverrides()
              onClose()
            }}
          >
            Clear Overrides
          </Dropdown.Item>
          <Dropdown.Item
            type="danger"
            icon={<X size={16} />}
            onClick={() => {
              onClearSelection()
              onClose()
            }}
          >
            Clear Selection
          </Dropdown.Item>
        </Dropdown.Menu>
      }
    >
      <span style={{ position: "fixed", left: state.x, top: state.y, width: 0, height: 0 }} />
    </Dropdown>
  )
}

// ---------------------------------------------------------------------------
// SchedulePage
// ---------------------------------------------------------------------------

export function SchedulePage({ active = true }: Readonly<{ active?: boolean }> = {}) {
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
  const { selectInRevit, zoomTo, isolate, resetIsolation, colorOverride, clearOverrides } =
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
      Object.values(filters).filter(
        (v) => v && (Array.isArray(v) ? v.length > 0 : String(v).trim() !== ""),
      ).length,
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

  const tableColumns = useMemo(() => {
    const columns = payload?.columns ?? []
    const visible = SCHEDULE_COLUMNS.filter((column) => columns.includes(column.key))
    return visible.map((column) => ({
      title: (
        <Space style={{ width: "100%", minWidth: 0, overflow: "hidden" }}>
          <span
            style={{
              overflow: "hidden",
              textOverflow: "ellipsis",
              whiteSpace: "nowrap",
              minWidth: 0,
            }}
          >
            {formatColumnHeader(column.key)}
          </span>
          {column.filter ? (
            <ColumnFilterDropdown
              col={column.key}
              filterKey={column.filter}
              allRows={allRows}
              filters={filters}
              setFilters={setFilters}
            />
          ) : null}
        </Space>
      ),
      dataIndex: column.key,
      key: column.key,
      width: column.width,
      sorter: (a?: ElementRow, b?: ElementRow) => {
        const aVal = displayText((a as Record<string, unknown> | undefined)?.[column.key])
        const bVal = displayText((b as Record<string, unknown> | undefined)?.[column.key])
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

  const grouping = groupByColumns.length > 0
  const tableFrame = useFillSize()
  const scrollbarWidth = useScrollbarWidth()
  const fittedColumns = useMemo(() => {
    const available = Math.max(tableFrame.width - SELECTION_COLUMN_WIDTH - scrollbarWidth, 0)
    const widths = fitWidths(
      tableColumns.map((column) => column.width),
      available,
    )
    return tableColumns.map((column, index) => ({
      ...column,
      width: widths[index] ?? column.width,
      ellipsis: true,
    }))
  }, [tableColumns, tableFrame.width, scrollbarWidth])
  const scrollX = fittedColumns.reduce((sum, column) => sum + column.width, SELECTION_COLUMN_WIDTH)
  const bodyHeight = Math.max(
    tableFrame.height - HEADER_ROW_HEIGHT - (grouping ? 0 : PAGER_HEIGHT),
    0,
  )
  const virtualized = tableFrame.width > 0 && bodyHeight > 0
  // Semi rebuilds groups only when dataSource changes identity.
  const tableRows = useMemo(
    () => (grouping ? searchFilteredRows.slice() : searchFilteredRows),
    [grouping, groupByColumns, searchFilteredRows, virtualized],
  )

  const groupRows = useCallback(
    (record?: ElementRow) => {
      if (!record) return ""
      return buildGroupKey(record as Record<string, unknown>, groupByColumns)
    },
    [groupByColumns],
  )

  const renderGroupSection = useCallback(
    (groupKey?: string | number, group?: Array<string | number>) => {
      if (groupKey == null) return null
      const ids = (group ?? []).map(String)
      return (
        <button
          type="button"
          onClick={() => bridge.isolateElements(ids)}
          style={{
            cursor: "pointer",
            padding: 0,
            border: "none",
            background: "transparent",
            font: "inherit",
            color: "inherit",
          }}
          title={`Click to isolate ${ids.length} elements in Revit`}
        >
          <Text strong size="small">
            {String(groupKey)} ({ids.length})
          </Text>
        </button>
      )
    },
    [bridge],
  )

  // Handle row click for properties panel
  const handleRowClick = useCallback((record: ElementRow) => {
    window.dispatchEvent(new CustomEvent("dashboard-element-selected", { detail: record }))
  }, [])

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

  const handleGroupByChange = useCallback((val: string[] | string | undefined) => {
    if (Array.isArray(val)) setGroupByColumns(val)
    else if (val) setGroupByColumns([val])
    else setGroupByColumns([])
  }, [])

  return (
    <>
      <div style={{ width: "100%", height: "100%" }}>
        <ResizeGroup direction="horizontal">
          <ResizeItem
            defaultSize="260px"
            min="200px"
            max="480px"
            style={{
              height: "100%",
              overflow: "hidden",
              backgroundColor: "var(--semi-color-bg-1)",
            }}
          >
            <div
              style={{
                height: "100%",
                overflow: "auto",
                minWidth: 0,
                padding: "12px 16px",
                boxSizing: "border-box",
              }}
            >
              <Space style={{ width: "100%", justifyContent: "space-between", marginBottom: 12 }}>
                <Space>
                  <Filter size={14} />
                  <Text strong>Filters</Text>
                  {activeFilterCount > 0 && (
                    <Tag color="blue" size="small" shape="circle">
                      {activeFilterCount}
                    </Tag>
                  )}
                </Space>
                <Button
                  theme="borderless"
                  size="small"
                  icon={<X size={14} />}
                  onClick={resetFilters}
                >
                  Clear
                </Button>
              </Space>

              <Input
                prefix={<IconSearch />}
                value={filters.search ?? ""}
                onChange={(value) =>
                  setFilters((prev) => ({ ...prev, search: String(value ?? "") }))
                }
                placeholder="Search elements..."
                showClear
                style={{ marginBottom: 8 }}
              />

              <Collapse>
                {(payload?.filterable_columns ?? Object.keys(payload?.filter_options ?? {})).map(
                  (col) => (
                    <FilterSection
                      key={col}
                      col={col}
                      options={payload?.filter_options[col] ?? []}
                      filters={filters}
                      setFilters={setFilters}
                    />
                  ),
                )}
              </Collapse>

              {/* Audit section */}
              <div
                style={{
                  marginTop: 12,
                  padding: "8px 0",
                  borderTop: "1px solid var(--semi-color-border)",
                }}
              >
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
          </ResizeItem>
          <ResizeHandler />
          <ResizeItem style={{ height: "100%", minWidth: 0, overflow: "hidden" }}>
            <div
              style={{
                height: "100%",
                overflow: "hidden",
                minWidth: 0,
                padding: 16,
                boxSizing: "border-box",
                display: "flex",
                flexDirection: "column",
              }}
            >
              {/* Chart filter banner */}
              {chartFilter && (
                <Banner
                  type="warning"
                  fullMode={false}
                  closeIcon={null}
                  style={{ marginBottom: 12, borderRadius: 8 }}
                  icon={<CircleCheck size={16} />}
                  description={
                    <Space>
                      <Text>
                        Chart filter:{" "}
                        <Text strong>
                          {chartFilter.field} = {chartFilter.key}
                        </Text>
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
                style={{ flex: 1, minHeight: 0, display: "flex", flexDirection: "column" }}
                title={
                  <Space>
                    <Text strong>Element Data</Text>
                    <Tag size="small">
                      {searchFilteredRows.length.toLocaleString()} /{" "}
                      {allRows.length.toLocaleString()} rows
                    </Tag>
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
                      optionList={GROUP_COLUMNS.map((key) => ({
                        label: formatColumnHeader(key),
                        value: key,
                      }))}
                    />
                  </Space>
                }
                headerStyle={{ padding: "8px 16px" }}
                bodyStyle={{
                  flex: 1,
                  minHeight: 0,
                  display: "flex",
                  flexDirection: "column",
                  padding: "0 12px 12px 12px",
                }}
              >
                <div
                  ref={tableFrame.ref}
                  onContextMenu={handleContextMenu}
                  style={{ flex: 1, minHeight: 0, minWidth: 0, overflow: "hidden" }}
                >
                  <Table
                    rowKey="element_id"
                    size="small"
                    columns={fittedColumns}
                    dataSource={tableRows}
                    pagination={grouping ? false : FLAT_PAGINATION}
                    virtualized={virtualized}
                    style={tableFrame.width > 0 ? { width: tableFrame.width } : undefined}
                    scroll={virtualized ? { x: scrollX, y: bodyHeight } : undefined}
                    rowSelection={{
                      width: SELECTION_COLUMN_WIDTH,
                      selectedRowKeys: selectedIdArray,
                      onChange: (keys) =>
                        setSelectedIds(new Set((keys ?? []).map((key) => displayText(key)))),
                    }}
                    onRow={(record) => ({
                      onClick: () => handleRowClick(record as ElementRow),
                      style: { cursor: "pointer" },
                    })}
                    groupBy={grouping ? groupRows : undefined}
                    renderGroupSection={grouping ? renderGroupSection : undefined}
                  />
                </div>
              </Card>
            </div>
          </ResizeItem>
        </ResizeGroup>
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
      />
    </>
  )
}
