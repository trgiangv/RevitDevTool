/**
 * Column name -> DashboardFilterState key mapping.
 *
 * The naive `${col}s` suffix breaks for irregular plurals:
 *   category -> "categorys" (WRONG, should be "categories")
 *   family   -> "familys"   (WRONG, should be "families")
 */

import type { DashboardFilterState } from "@/types"

const COLUMN_TO_FILTER: Record<string, keyof DashboardFilterState> = {
  category: "categories",
  family: "families",
  type: "types",
  level: "levels",
  phase: "phases",
  workset: "worksets",
}

/** Return the correct `DashboardFilterState` key for a column name. */
export function getFilterKey(col: string): keyof DashboardFilterState {
  return COLUMN_TO_FILTER[col] ?? (`${col}s` as keyof DashboardFilterState)
}
