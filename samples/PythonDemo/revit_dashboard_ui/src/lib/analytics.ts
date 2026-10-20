/**
 * Client-side dashboard analytics.
 *
 * The host snapshot is only model info, element rows, and raw Revit warnings.
 * This module classifies warnings and builds the DashboardPayload the pages read.
 */

import { displayText } from "@/lib/text"
import type {
  ChartConfig,
  DashboardFilterState,
  DashboardPayload,
  ElementRow,
  HeavyFamily,
  HostSnapshot,
  ModelInfo,
  WarningItem,
} from "@/types"

const SCHEMA_VERSION = "1.1.0"
const UNASSIGNED = "Unassigned"

const TEXT_COLUMNS = ["category", "family", "type", "level", "phase", "workset"] as const

const BASE_COLUMNS = [
  "element_id",
  "unique_id",
  "name",
  "class_name",
  "category",
  "family",
  "type",
  "level",
  "phase",
  "workset",
  "is_view_specific",
  "is_pinned",
  "has_material_quantities",
] as const

const INCLUDE_FILTERS: Array<[keyof DashboardFilterState, (typeof TEXT_COLUMNS)[number]]> = [
  ["categories", "category"],
  ["families", "family"],
  ["types", "type"],
  ["levels", "level"],
  ["phases", "phase"],
  ["worksets", "workset"],
]

const CRITICAL_PATTERNS = [
  "slightly off axis",
  "not enclosed",
  "duplicate",
  "identical instances",
  "completely inside another",
  "overlap",
  "document corruption",
]

const MODERATE_PATTERNS = [
  "not attached",
  "duplicate mark",
  "miss the target",
  "not clean",
  "not placed",
  "multiple rooms",
  "join",
]

const WARNING_CATEGORIES: Array<[string, string[]]> = [
  ["Rooms", ["room", "area", "enclosed"]],
  ["Joins", ["join", "wall", "attach"]],
  ["Annotations", ["tag", "annotation", "text"]],
  ["Duplicates", ["duplicate", "identical", "mark"]],
  ["Geometry", ["axis", "offset", "slope", "geometry", "inside"]],
  ["Structure", ["analytical", "structural", "beam", "column"]],
  ["Parameters", ["parameter", "value", "formula"]],
]

export function buildDashboard(
  snapshot: HostSnapshot,
  filters?: DashboardFilterState,
): DashboardPayload {
  const rows = (snapshot.rows ?? []).map(normalizeRow)
  const filtered = applyFilters(rows, filters ?? {})
  const warnings = classifyWarnings(snapshot.warnings ?? [])

  return {
    schema_version: SCHEMA_VERSION,
    generated_at_utc: new Date().toISOString(),
    model_info: normalizeModelInfo(snapshot.model_info),
    kpis: computeKpis(filtered, warnings),
    filter_options: buildFilterOptions(rows),
    filterable_columns: [...TEXT_COLUMNS],
    chart_configs: defaultChartConfigs(rows),
    charts: buildChartData(filtered),
    rows: filtered,
    columns: columnsFor(filtered),
    warnings,
    heavy_families: heavyFamilies(filtered),
    active_filters: filters ?? {},
  }
}

/** Accept a host snapshot, or a payload that already has the snapshot fields. */
export function toDashboardPayload(data: unknown): DashboardPayload | null {
  if (!data || typeof data !== "object") return null
  const record = data as Partial<HostSnapshot>
  if (!record.model_info || !Array.isArray(record.rows)) return null
  return buildDashboard({
    model_info: record.model_info,
    rows: record.rows,
    warnings: Array.isArray(record.warnings) ? record.warnings : [],
  })
}

export function applyFilters(rows: ElementRow[], filters: DashboardFilterState): ElementRow[] {
  const search = (filters.search ?? "").trim().toLowerCase()
  return rows.filter(
    (row) =>
      passesIncludes(row, filters) && passesExcludes(row, filters) && matchesSearch(row, search),
  )
}

export function computeKpis(rows: ElementRow[], warnings: WarningItem[]): Record<string, number> {
  return {
    total_elements: rows.length,
    unique_categories: distinctCount(rows, "category"),
    unique_families: distinctCount(rows, "family"),
    unique_types: distinctCount(rows, "type"),
    unique_levels: distinctCount(rows, "level"),
    total_warnings: warnings.length,
    pinned_elements: rows.filter((row) => row.is_pinned).length,
    view_specific_elements: rows.filter((row) => row.is_view_specific).length,
  }
}

function passesIncludes(row: ElementRow, filters: DashboardFilterState): boolean {
  for (const [key, column] of INCLUDE_FILTERS) {
    const selected = filters[key]
    if (Array.isArray(selected) && selected.length > 0 && !selected.includes(String(row[column]))) {
      return false
    }
  }
  return true
}

function passesExcludes(row: ElementRow, filters: DashboardFilterState): boolean {
  if (filters.hide_categories?.length && filters.hide_categories.includes(row.category))
    return false
  if (filters.hide_levels?.length && filters.hide_levels.includes(row.level)) return false
  return true
}

function matchesSearch(row: ElementRow, search: string): boolean {
  if (!search) return true
  for (const value of Object.values(row)) {
    if (typeof value === "string" && value.toLowerCase().includes(search)) return true
  }
  return false
}

function classifyWarnings(warnings: HostSnapshot["warnings"]): WarningItem[] {
  return warnings.map((warning) => {
    const description = String(warning.description ?? "")
    return {
      id: Number(warning.id ?? 0),
      description,
      severity: classifySeverity(description, String(warning.severity ?? "")),
      element_ids: Array.isArray(warning.element_ids)
        ? warning.element_ids.map((id) => String(id))
        : [],
      category: categoriseWarning(description),
    }
  })
}

function classifySeverity(description: string, revitSeverity: string): WarningItem["severity"] {
  const severityName = revitSeverity.trim().toLowerCase()
  if (severityName === "error" || severityName === "documentcorruption") return "critical"

  const text = description.toLowerCase()
  if (CRITICAL_PATTERNS.some((pattern) => text.includes(pattern))) return "critical"
  if (MODERATE_PATTERNS.some((pattern) => text.includes(pattern))) return "moderate"
  return "info"
}

function categoriseWarning(description: string): string {
  const text = description.toLowerCase()
  for (const [label, keywords] of WARNING_CATEGORIES) {
    if (keywords.some((keyword) => text.includes(keyword))) return label
  }
  return "General"
}

function buildFilterOptions(rows: ElementRow[]): Record<string, string[]> {
  const options: Record<string, string[]> = {}
  for (const column of TEXT_COLUMNS) {
    options[column] = [...new Set(rows.map((row) => String(row[column])))].sort()
  }
  return options
}

function buildChartData(rows: ElementRow[]): Record<string, Record<string, unknown>[]> {
  return {
    category_counts: groupCount(rows, "category"),
    level_counts: groupCount(rows, "level"),
    family_counts: familyCounts(rows, 50),
    workset_counts: groupCount(rows, "workset"),
    phase_counts: groupCount(rows, "phase"),
    quality: TEXT_COLUMNS.map((column) => ({
      metric: `missing_${column}`,
      count: rows.filter((row) => row[column] === UNASSIGNED).length,
    })),
    outlier_families: outlierFamilies(rows),
  }
}

function defaultChartConfigs(rows: ElementRow[]): ChartConfig[] {
  const configs: ChartConfig[] = [
    {
      type: "bar",
      title: "Elements by Category",
      data_key: "category_counts",
      label_field: "category",
      value_field: "count",
      max_items: 15,
      click_filter_field: "category",
    },
    {
      type: "bar",
      title: "Elements by Level",
      data_key: "level_counts",
      label_field: "level",
      value_field: "count",
      max_items: 15,
      click_filter_field: "level",
    },
  ]

  if (distinctCount(rows, "workset") > 1) {
    configs.push({
      type: "bar",
      title: "Elements by Workset",
      data_key: "workset_counts",
      label_field: "workset",
      value_field: "count",
      max_items: 15,
      click_filter_field: "workset",
    })
  }

  if (distinctCount(rows, "phase") > 1) {
    configs.push({
      type: "bar",
      title: "Elements by Phase",
      data_key: "phase_counts",
      label_field: "phase",
      value_field: "count",
      max_items: 15,
      click_filter_field: "phase",
    })
  }

  return configs
}

function heavyFamilies(rows: ElementRow[]): HeavyFamily[] {
  const grouped = new Map<
    string,
    { category: string; instance_count: number; types: Set<string> }
  >()
  for (const row of rows) {
    if (!row.family) continue
    const existing = grouped.get(row.family)
    if (existing) {
      existing.instance_count += 1
      existing.types.add(row.type)
    } else {
      grouped.set(row.family, {
        category: row.category,
        instance_count: 1,
        types: new Set([row.type]),
      })
    }
  }

  return [...grouped.entries()]
    .map(([family_name, data]) => {
      const type_count = data.types.size
      return {
        family_name,
        category: data.category,
        instance_count: data.instance_count,
        type_count,
        estimated_complexity: data.instance_count * Math.max(type_count, 1) * 10,
      }
    })
    .sort((a, b) => b.estimated_complexity - a.estimated_complexity)
    .slice(0, 10)
}

function groupCount(
  rows: ElementRow[],
  key: (typeof TEXT_COLUMNS)[number],
): Record<string, unknown>[] {
  const counts = new Map<string, number>()
  for (const row of rows) {
    const label = String(row[key])
    counts.set(label, (counts.get(label) ?? 0) + 1)
  }
  return [...counts.entries()]
    .map(([label, count]) => ({ [key]: label, count }))
    .sort((a, b) => Number(b.count) - Number(a.count))
}

function familyCounts(rows: ElementRow[], limit: number): Record<string, unknown>[] {
  const counts = new Map<string, { category: string; family: string; count: number }>()
  for (const row of rows) {
    const key = `${row.category}\0${row.family}`
    const existing = counts.get(key)
    if (existing) existing.count += 1
    else counts.set(key, { category: row.category, family: row.family, count: 1 })
  }
  return [...counts.values()].sort((a, b) => b.count - a.count).slice(0, limit)
}

function outlierFamilies(rows: ElementRow[]): Record<string, unknown>[] {
  const groups = familyCounts(rows, Number.POSITIVE_INFINITY) as Array<{
    category: string
    family: string
    count: number
  }>
  if (groups.length === 0) return []
  const ascending = groups.map((group) => group.count).sort((a, b) => a - b)
  const threshold = quantileNearest(ascending, 0.95)
  return groups.filter((group) => group.count >= threshold).slice(0, 30)
}

/** Polars default quantile interpolation: nearest index of q * (n - 1). */
function quantileNearest(sortedAscending: number[], quantile: number): number {
  if (sortedAscending.length === 0) return 0
  const index = Math.round((sortedAscending.length - 1) * quantile)
  const clamped = Math.min(sortedAscending.length - 1, Math.max(0, index))
  return sortedAscending[clamped] ?? 0
}

function distinctCount(rows: ElementRow[], key: (typeof TEXT_COLUMNS)[number]): number {
  return new Set(rows.map((row) => String(row[key]))).size
}

function columnsFor(rows: ElementRow[]): string[] {
  const extras: string[] = []
  const seen = new Set<string>(BASE_COLUMNS)
  for (const row of rows) {
    for (const key of Object.keys(row)) {
      if (!seen.has(key)) {
        seen.add(key)
        extras.push(key)
      }
    }
  }
  return [...BASE_COLUMNS, ...extras]
}

function normalizeModelInfo(raw: Partial<ModelInfo> | undefined): ModelInfo {
  return {
    file_name: raw?.file_name || "Unknown",
    file_path: raw?.file_path ?? "",
    current_view: raw?.current_view ?? "",
    total_views: Number(raw?.total_views ?? 0),
    total_sheets: Number(raw?.total_sheets ?? 0),
  }
}

function normalizeRow(raw: Partial<ElementRow> & Record<string, unknown>): ElementRow {
  const row: ElementRow = {
    element_id: raw.element_id == null ? "" : String(raw.element_id),
    unique_id: textOrEmpty(raw.unique_id),
    name: textOrEmpty(raw.name),
    class_name: textOrEmpty(raw.class_name),
    category: textOrUnassigned(raw.category),
    family: textOrUnassigned(raw.family),
    type: textOrUnassigned(raw.type),
    level: textOrUnassigned(raw.level),
    phase: textOrUnassigned(raw.phase),
    workset: textOrUnassigned(raw.workset),
    is_view_specific: asBoolean(raw.is_view_specific),
    is_pinned: asBoolean(raw.is_pinned),
    has_material_quantities: asBoolean(raw.has_material_quantities),
  }
  for (const [key, value] of Object.entries(raw)) {
    if (!(key in row)) row[key] = value
  }
  return row
}

function textOrUnassigned(value: unknown): string {
  const text = displayText(value)
  return text === "" ? UNASSIGNED : text
}

function textOrEmpty(value: unknown): string {
  return displayText(value)
}

function asBoolean(value: unknown): boolean {
  return value === true || value === 1 || value === "true"
}
