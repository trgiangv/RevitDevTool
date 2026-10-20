/**
 * Schedule columns. Width, filter field, and group-by live on the column,
 * so callers do not special-case key strings.
 */

import type { DashboardFilterState } from "@/types"

export type ScheduleFilterField = keyof Pick<
  DashboardFilterState,
  "categories" | "families" | "types" | "levels" | "phases" | "worksets"
>

export interface ScheduleColumn {
  key: string
  width: number
  filter?: ScheduleFilterField
  group?: boolean
}

export const SCHEDULE_COLUMNS: readonly ScheduleColumn[] = [
  { key: "element_id", width: 110 },
  { key: "name", width: 260 },
  { key: "category", width: 180, filter: "categories", group: true },
  { key: "family", width: 180, filter: "families", group: true },
  { key: "type", width: 180, filter: "types" },
  { key: "level", width: 180, filter: "levels", group: true },
  { key: "phase", width: 180, filter: "phases", group: true },
  { key: "workset", width: 180, filter: "worksets", group: true },
]

const COLUMN_BY_KEY = new Map(SCHEDULE_COLUMNS.map((column) => [column.key, column]))

export const GROUP_COLUMNS = SCHEDULE_COLUMNS.filter((column) => column.group).map(
  (column) => column.key,
)

/** Filter field for a column that appears in the schedule catalog. */
export function getFilterKey(col: string): ScheduleFilterField | undefined {
  return COLUMN_BY_KEY.get(col)?.filter
}
