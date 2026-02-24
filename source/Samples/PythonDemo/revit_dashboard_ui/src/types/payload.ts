/** Types mirroring the Python DashboardPayload contract. */

export interface ElementRow {
  element_id: number
  unique_id: string
  name: string
  class_name: string
  category: string
  family: string
  type: string
  level: string
  phase: string
  workset: string
  is_view_specific: boolean
  is_pinned: boolean
  has_material_quantities: boolean
  [key: string]: unknown // allow dynamic columns
}

export interface ChartConfig {
  type: string
  title: string
  data_key: string
  label_field: string
  value_field: string
  max_items?: number
  click_filter_field?: string
}

export interface ModelInfo {
  file_name: string
  file_path: string
  current_view: string
  total_views: number
  total_sheets: number
}

export interface WarningItem {
  id: number
  description: string
  severity: "critical" | "moderate" | "info"
  element_ids: number[]
  category: string
}

export interface HeavyFamily {
  family_name: string
  category: string
  instance_count: number
  type_count: number
  estimated_complexity: number
}

export interface DashboardPayload {
  schema_version: string
  generated_at_utc: string
  model_info: ModelInfo
  kpis: Record<string, number>
  filter_options: Record<string, string[]>
  filterable_columns: string[]
  chart_configs: ChartConfig[]
  charts: Record<string, Record<string, unknown>[]>
  rows: ElementRow[]
  columns: string[]
  warnings: WarningItem[]
  heavy_families: HeavyFamily[]
  active_filters?: DashboardFilterState
}

export interface DashboardFilterState {
  categories?: string[]
  families?: string[]
  types?: string[]
  levels?: string[]
  phases?: string[]
  worksets?: string[]
  search?: string
  hide_categories?: string[]
  hide_levels?: string[]
}

/** Session KPI snapshot for sparkline history */
export interface SessionSnapshot {
  timestamp: string
  kpis: Record<string, number>
}
