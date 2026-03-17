/**
 * Session history — stores KPI snapshots in localStorage
 * for sparkline trend display across sessions.
 */

import type { SessionSnapshot } from "@/types"

const STORAGE_KEY = "bim-dashboard-session-history"
const MAX_SNAPSHOTS = 10

export function getSessionHistory(): SessionSnapshot[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return []
    const parsed = JSON.parse(raw)
    return Array.isArray(parsed) ? parsed : []
  } catch {
    return []
  }
}

export function pushSessionSnapshot(kpis: Record<string, number>): void {
  const history = getSessionHistory()
  history.push({
    timestamp: new Date().toISOString(),
    kpis,
  })

  // Keep only the most recent snapshots
  const trimmed = history.slice(-MAX_SNAPSHOTS)

  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(trimmed))
  } catch {
    // localStorage might be full or unavailable
  }
}

/**
 * Get sparkline data for a specific KPI key.
 * Returns an array of values from oldest to newest.
 */
export function getSparklineData(kpiKey: string): number[] {
  const history = getSessionHistory()
  return history.map((s) => s.kpis[kpiKey] ?? 0)
}
