/**
 * Pure filter functions — no side effects, no component state.
 */

import type { DashboardFilterState, ElementRow } from "@/types"

export function applyFilters(rows: ElementRow[], filters: DashboardFilterState): ElementRow[] {
  const search = (filters.search ?? "").trim().toLowerCase()
  return rows.filter(
    (row) =>
      passesIncludes(row, filters) &&
      passesExcludes(row, filters) &&
      matchesSearch(row, search),
  )
}

function passesIncludes(row: ElementRow, f: DashboardFilterState): boolean {
  if (f.categories?.length && !f.categories.includes(row.category)) return false
  if (f.families?.length && !f.families.includes(row.family)) return false
  if (f.types?.length && !f.types.includes(row.type)) return false
  if (f.levels?.length && !f.levels.includes(row.level)) return false
  if (f.phases?.length && !f.phases.includes(row.phase)) return false
  if (f.worksets?.length && !f.worksets.includes(row.workset)) return false
  return true
}

function passesExcludes(row: ElementRow, f: DashboardFilterState): boolean {
  if (f.hide_categories?.includes(row.category)) return false
  if (f.hide_levels?.includes(row.level)) return false
  return true
}

function matchesSearch(row: ElementRow, search: string): boolean {
  if (!search) return true
  const hay = `${row.name} ${row.category} ${row.family} ${row.type}`.toLowerCase()
  return hay.includes(search)
}

/** Format column key to display label: "element_id" → "Element Id" */
export function formatColumnHeader(key: string): string {
  return key
    .replace(/_/g, " ")
    .replace(/\b\w/g, (c) => c.toUpperCase())
}
