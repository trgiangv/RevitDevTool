/**
 * Project Pulse — Home page.
 * KPI "Big Numbers" cards + Category/Level Donut charts + Quick overview cards.
 */

import { useMemo, useCallback } from "react"
import { Banner, Card, Progress, Space, Tag, Typography } from "@douyinfe/semi-ui"
import {
  IconGridRectangle,
  IconAlertTriangle,
  IconInherit,
  IconLayers,
  IconFile,
  IconTemplate,
} from "@douyinfe/semi-icons"
import { useDashboard } from "@/providers/dashboard-provider"
import { MetricCard } from "@/features/home/metric-card"
import { DonutChart } from "@/components/charts/donut-chart"
import { getSparklineData } from "@/lib/session-history"
import type { PageId } from "@/components/layout/app-sidebar"

const { Text } = Typography

interface HomePageProps {
  onNavigate?: (page: PageId) => void
}

export function HomePage({ onNavigate }: HomePageProps) {
  const { payload, filteredRows, chartFilter, setChartFilter } = useDashboard()

  const stats = useMemo(
    () => ({
      elements: filteredRows.length,
      categories: new Set(filteredRows.map((r) => r.category)).size,
      families: new Set(filteredRows.map((r) => r.family)).size,
      levels: new Set(filteredRows.map((r) => r.level)).size,
      warnings: payload?.warnings?.length ?? 0,
      views: payload?.model_info?.total_views ?? 0,
      sheets: payload?.model_info?.total_sheets ?? 0,
    }),
    [filteredRows, payload],
  )

  const categoryData = useMemo(
    () =>
      ((payload?.charts?.category_counts as Array<{ category: string; count: number }>) ?? [])
        .slice(0, 10)
        .map((x) => ({ label: x.category, value: x.count })),
    [payload],
  )

  const levelData = useMemo(
    () =>
      ((payload?.charts?.level_counts as Array<{ level: string; count: number }>) ?? [])
        .slice(0, 10)
        .map((x) => ({ label: x.level, value: x.count })),
    [payload],
  )

  // Warning summary by severity
  const warningSummary = useMemo(() => {
    const warnings = payload?.warnings ?? []
    return {
      critical: warnings.filter((w) => w.severity === "critical").length,
      moderate: warnings.filter((w) => w.severity === "moderate").length,
      info: warnings.filter((w) => w.severity === "info").length,
    }
  }, [payload])

  // Top 5 families by instance count
  const topFamilies = useMemo(() => {
    const counts = new Map<string, { count: number; category: string }>()
    for (const row of filteredRows) {
      const existing = counts.get(row.family)
      if (existing) {
        existing.count++
      } else {
        counts.set(row.family, { count: 1, category: row.category })
      }
    }
    return Array.from(counts.entries())
      .map(([name, data]) => ({ name, ...data }))
      .sort((a, b) => b.count - a.count)
      .slice(0, 5)
  }, [filteredRows])

  // Data quality score
  const dataQuality = useMemo(() => {
    if (filteredRows.length === 0) return { score: 100, filled: 0, total: 0 }
    const checkFields = ["category", "family", "type", "level"]
    let filled = 0
    let total = 0
    for (const row of filteredRows) {
      for (const field of checkFields) {
        total++
        const val = row[field as keyof typeof row]
        if (val && String(val).trim() !== "" && !String(val).startsWith("Unassigned")) {
          filled++
        }
      }
    }
    return { score: total > 0 ? Math.round((filled / total) * 100) : 100, filled, total }
  }, [filteredRows])

  const topFamilyMax = Math.max(1, ...topFamilies.map((f) => f.count))

  const handleNavigateHealth = useCallback(() => onNavigate?.("health"), [onNavigate])

  return (
    <div style={{ padding: 20 }}>
      {/* Chart filter banner */}
      {chartFilter && (
        <Banner
          type="warning"
          fullMode={false}
          closeIcon={null}
          style={{ marginBottom: 16, borderRadius: 8 }}
          description={
            <Space>
              <Text>
                Chart filter: <Text strong>{chartFilter.field} = {chartFilter.key}</Text>
              </Text>
              <button
                onClick={() => setChartFilter(null)}
                style={{
                  background: "none",
                  border: "1px solid var(--semi-color-border)",
                  borderRadius: 4,
                  padding: "2px 8px",
                  cursor: "pointer",
                  color: "var(--semi-color-text-1)",
                  fontSize: 12,
                }}
              >
                Clear
              </button>
            </Space>
          }
        />
      )}

      {/* KPI Cards */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
          gap: 12,
          marginBottom: 20,
        }}
      >
        <MetricCard
          label="Total Elements"
          value={stats.elements}
          color="blue"
          icon={<IconGridRectangle />}
          sparklineData={getSparklineData("total_elements")}
        />
        <MetricCard
          label="Warnings"
          value={stats.warnings}
          color="red"
          icon={<IconAlertTriangle />}
          sparklineData={getSparklineData("total_warnings")}
        />
        <MetricCard
          label="Families"
          value={stats.families}
          color="violet"
          icon={<IconInherit />}
          sparklineData={getSparklineData("unique_families")}
        />
        <MetricCard
          label="Views / Sheets"
          value={stats.views + stats.sheets}
          color="cyan"
          icon={<IconFile />}
          sparklineData={getSparklineData("total_views")}
        />
        <MetricCard
          label="Categories"
          value={stats.categories}
          color="green"
          icon={<IconTemplate />}
          sparklineData={getSparklineData("unique_categories")}
        />
        <MetricCard
          label="Levels"
          value={stats.levels}
          color="orange"
          icon={<IconLayers />}
          sparklineData={getSparklineData("unique_levels")}
        />
      </div>

      {/* Distribution Charts */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 16 }}>
        <DonutChart
          title="Category Distribution"
          data={categoryData}
          activeKey={chartFilter?.field === "category" ? chartFilter.key : null}
          onSelect={(key) => {
            if (chartFilter?.field === "category" && chartFilter.key === key) {
              setChartFilter(null)
            } else {
              setChartFilter({ field: "category", key })
            }
          }}
        />
        <DonutChart
          title="Level Distribution"
          data={levelData}
          activeKey={chartFilter?.field === "level" ? chartFilter.key : null}
          onSelect={(key) => {
            if (chartFilter?.field === "level" && chartFilter.key === key) {
              setChartFilter(null)
            } else {
              setChartFilter({ field: "level", key })
            }
          }}
        />
      </div>

      {/* Quick Overview Cards */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
        {/* Warnings Summary */}
        <Card
          title={
            <Space>
              <IconAlertTriangle style={{ color: "var(--semi-color-warning)" }} />
              <Text strong>Warnings</Text>
            </Space>
          }
          headerStyle={{ padding: "10px 16px" }}
          bodyStyle={{ padding: "12px 16px" }}
        >
          <div style={{ display: "flex", gap: 12, marginBottom: 10 }}>
            <div style={{ flex: 1, textAlign: "center", padding: "8px 4px", background: "rgba(255,77,79,0.08)", borderRadius: 6 }}>
              <Text strong style={{ fontSize: 20, color: "var(--semi-color-danger)" }}>{warningSummary.critical}</Text>
              <div><Tag size="small" color="red">Critical</Tag></div>
            </div>
            <div style={{ flex: 1, textAlign: "center", padding: "8px 4px", background: "rgba(250,173,20,0.08)", borderRadius: 6 }}>
              <Text strong style={{ fontSize: 20, color: "var(--semi-color-warning)" }}>{warningSummary.moderate}</Text>
              <div><Tag size="small" color="orange">Moderate</Tag></div>
            </div>
            <div style={{ flex: 1, textAlign: "center", padding: "8px 4px", background: "rgba(82,196,26,0.08)", borderRadius: 6 }}>
              <Text strong style={{ fontSize: 20, color: "var(--semi-color-success)" }}>{warningSummary.info}</Text>
              <div><Tag size="small" color="green">Info</Tag></div>
            </div>
          </div>
          {onNavigate && (
            <button
              onClick={handleNavigateHealth}
              style={{
                width: "100%",
                background: "none",
                border: "1px solid var(--semi-color-border)",
                borderRadius: 6,
                padding: "6px 12px",
                cursor: "pointer",
                color: "var(--semi-color-primary)",
                fontSize: 12,
              }}
            >
              View Details
            </button>
          )}
        </Card>

        {/* Top Families */}
        <Card
          title={<Text strong>Top Families</Text>}
          headerStyle={{ padding: "10px 16px" }}
          bodyStyle={{ padding: "8px 16px 12px" }}
        >
          <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            {topFamilies.map((f, i) => (
              <div
                key={f.name}
                style={{
                  display: "grid",
                  gridTemplateColumns: "16px 1fr 40px",
                  alignItems: "center",
                  gap: 6,
                }}
              >
                <Text type="tertiary" size="small" style={{ textAlign: "right" }}>
                  {i + 1}.
                </Text>
                <div>
                  <Text size="small" ellipsis={{ showTooltip: true }} style={{ maxWidth: 140, display: "block" }}>
                    {f.name}
                  </Text>
                  <Progress
                    percent={Math.round((f.count / topFamilyMax) * 100)}
                    showInfo={false}
                    size="small"
                    style={{ marginTop: 2 }}
                  />
                </div>
                <Text strong size="small" style={{ textAlign: "right" }}>
                  {f.count}
                </Text>
              </div>
            ))}
          </div>
        </Card>

        {/* Data Quality */}
        <Card
          title={<Text strong>Data Quality</Text>}
          headerStyle={{ padding: "10px 16px" }}
          bodyStyle={{ padding: "12px 16px" }}
        >
          <div style={{ textAlign: "center", marginBottom: 12 }}>
            <div style={{ position: "relative", display: "inline-block" }}>
              <Progress
                percent={dataQuality.score}
                type="circle"
                width={90}
                strokeWidth={6}
                stroke={(() => {
                  if (dataQuality.score >= 90) return "var(--semi-color-success)"
                  if (dataQuality.score >= 70) return "var(--semi-color-warning)"
                  return "var(--semi-color-danger)"
                })()}
                format={() => (
                  <Text strong style={{ fontSize: 20 }}>{dataQuality.score}%</Text>
                )}
              />
            </div>
          </div>
          <Text type="tertiary" size="small" style={{ display: "block", textAlign: "center" }}>
            {dataQuality.filled.toLocaleString()} / {dataQuality.total.toLocaleString()} fields filled
          </Text>
          <Text type="tertiary" size="small" style={{ display: "block", textAlign: "center", marginTop: 4 }}>
            Checks: Category, Family, Type, Level
          </Text>
        </Card>
      </div>
    </div>
  )
}
