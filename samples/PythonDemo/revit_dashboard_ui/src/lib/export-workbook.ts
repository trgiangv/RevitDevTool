/**
 * Excel export. The dashboard window shows its Save dialog and writes the file.
 */

import ExcelJS from "exceljs"
import type { DashboardFilterState, ElementRow, WarningItem } from "@/types"
import { computeKpis } from "./analytics"

const PREFERRED_COLUMNS = [
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
]

export async function exportDashboardWorkbook(
  input: {
    rows: ElementRow[]
    filters: DashboardFilterState
    warnings?: WarningItem[]
  },
  save: (filename: string, contentBase64: string) => Promise<string | null>,
): Promise<string | null> {
  const filename = `bim_dashboard_export_${exportTimestamp()}.xlsx`
  const workbook = new ExcelJS.Workbook()

  const summary = workbook.addWorksheet("Summary")
  summary.addRow(["Metric", "Value"])
  for (const [metric, value] of Object.entries(computeKpis(input.rows, input.warnings ?? []))) {
    summary.addRow([metric, value])
  }

  const filters = workbook.addWorksheet("Filters")
  filters.addRow(["Filter", "Values"])
  for (const [key, value] of Object.entries(input.filters)) {
    filters.addRow([key, Array.isArray(value) ? value.join(", ") : String(value ?? "")])
  }

  const elements = workbook.addWorksheet("Elements")
  const columns = columnsOf(input.rows)
  elements.addRow(columns)
  for (const row of input.rows) {
    elements.addRow(columns.map((column) => cellValue(row[column])))
  }

  const buffer = await workbook.xlsx.writeBuffer()
  return save(filename, bytesToBase64(new Uint8Array(buffer)))
}

function bytesToBase64(bytes: Uint8Array): string {
  const chunk = 0x8000
  let binary = ""
  for (let index = 0; index < bytes.length; index += chunk) {
    binary += String.fromCodePoint(...bytes.subarray(index, index + chunk))
  }
  return btoa(binary)
}

function columnsOf(rows: ElementRow[]): string[] {
  const present = new Set<string>()
  for (const row of rows) {
    for (const key of Object.keys(row)) present.add(key)
  }
  if (present.size === 0) return [...PREFERRED_COLUMNS]
  const ordered = PREFERRED_COLUMNS.filter((column) => present.has(column))
  for (const key of present) {
    if (!PREFERRED_COLUMNS.includes(key)) ordered.push(key)
  }
  return ordered
}

function cellValue(value: unknown): string | number | boolean {
  if (value === null || value === undefined) return ""
  if (typeof value === "string" || typeof value === "number" || typeof value === "boolean")
    return value
  return JSON.stringify(value)
}

function exportTimestamp(date = new Date()): string {
  const pad = (value: number) => String(value).padStart(2, "0")
  return [
    date.getFullYear(),
    pad(date.getMonth() + 1),
    pad(date.getDate()),
    "_",
    pad(date.getHours()),
    pad(date.getMinutes()),
    pad(date.getSeconds()),
  ].join("")
}
