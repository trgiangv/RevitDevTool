/**
 * Project Pulse — Home page.
 * KPI "Big Numbers" cards + Category/Level Donut charts + Quick overview cards.
 */

import { useMemo, useCallback } from "react"
import { displayText } from "@/lib/text"
import { Banner, Button, Card, Col, Progress, Row, Space, Tag, Typography } from "@douyinfe/semi-ui"
import { AppWindow, Component, Cuboid, Layers, Shapes, TriangleAlert } from "lucide-react"
import { useDashboard } from "@/providers/dashboard-provider"
import { MetricCard } from "@/features/home/metric-card"
import { DonutChart } from "@/components/charts/donut-chart"
import { getSparklineData } from "@/lib/session-history"
import type { PageId } from "@/components/layout/app-sidebar"

const { Text, Title } = Typography

interface HomePageProps {
  onNavigate?: (page: PageId) => void
}

export function HomePage({ onNavigate }: Readonly<HomePageProps>) {
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
        const val = displayText(row[field as keyof typeof row])
        if (val.trim() !== "" && !val.startsWith("Unassigned")) {
          filled++
        }
      }
    }
    return { score: total > 0 ? Math.round((filled / total) * 100) : 100, filled, total }
  }, [filteredRows])

  const topFamilyMax = Math.max(1, ...topFamilies.map((f) => f.count))

  const handleNavigateHealth = useCallback(() => onNavigate?.("health"), [onNavigate])

  return (
    <Space
      vertical
      spacing="medium"
      style={{ padding: 20, width: "100%", boxSizing: "border-box", alignItems: "stretch" }}
    >
      {chartFilter && (
        <Banner
          type="warning"
          fullMode={false}
          closeIcon={null}
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

      <Row gutter={[12, 12]} style={{ width: "100%" }}>
        <Col xs={24} sm={12} xl={4}>
          <MetricCard
            label="Total Elements"
            value={stats.elements}
            icon={<Cuboid size={16} />}
            sparklineData={getSparklineData("total_elements")}
          />
        </Col>
        <Col xs={24} sm={12} xl={4}>
          <MetricCard
            label="Warnings"
            value={stats.warnings}
            icon={<TriangleAlert size={16} />}
            sparklineData={getSparklineData("total_warnings")}
          />
        </Col>
        <Col xs={24} sm={12} xl={4}>
          <MetricCard
            label="Families"
            value={stats.families}
            icon={<Component size={16} />}
            sparklineData={getSparklineData("unique_families")}
          />
        </Col>
        <Col xs={24} sm={12} xl={4}>
          <MetricCard
            label="Views / Sheets"
            value={stats.views + stats.sheets}
            icon={<AppWindow size={16} />}
            sparklineData={getSparklineData("total_views")}
          />
        </Col>
        <Col xs={24} sm={12} xl={4}>
          <MetricCard
            label="Categories"
            value={stats.categories}
            icon={<Shapes size={16} />}
            sparklineData={getSparklineData("unique_categories")}
          />
        </Col>
        <Col xs={24} sm={12} xl={4}>
          <MetricCard
            label="Levels"
            value={stats.levels}
            icon={<Layers size={16} />}
            sparklineData={getSparklineData("unique_levels")}
          />
        </Col>
      </Row>

      <Row gutter={[12, 12]} style={{ width: "100%" }}>
        <Col span={12}>
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
        </Col>
        <Col span={12}>
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
        </Col>
      </Row>

      <Row gutter={[12, 12]} style={{ width: "100%" }}>
        <Col span={8}>
          <Card
            title={
              <Space>
                <TriangleAlert size={16} style={{ color: "var(--semi-color-warning)" }} />
                <Text strong>Warnings</Text>
              </Space>
            }
          >
            <Space style={{ width: "100%", justifyContent: "space-around" }}>
              <Space vertical align="center">
                <Title heading={3} type="danger">
                  {warningSummary.critical}
                </Title>
                <Tag size="small" color="red">
                  Critical
                </Tag>
              </Space>
              <Space vertical align="center">
                <Title heading={3} type="warning">
                  {warningSummary.moderate}
                </Title>
                <Tag size="small" color="orange">
                  Moderate
                </Tag>
              </Space>
              <Space vertical align="center">
                <Title heading={3} type="success">
                  {warningSummary.info}
                </Title>
                <Tag size="small" color="green">
                  Info
                </Tag>
              </Space>
            </Space>
            {onNavigate ? (
              <Button block theme="light" onClick={handleNavigateHealth} style={{ marginTop: 12 }}>
                View Details
              </Button>
            ) : null}
          </Card>
        </Col>
        <Col span={8}>
          <Card title={<Text strong>Top Families</Text>}>
            <Space vertical spacing="tight" style={{ width: "100%" }}>
              {topFamilies.map((f, i) => (
                <Space key={f.name} style={{ width: "100%" }}>
                  <Text type="tertiary" size="small">
                    {i + 1}.
                  </Text>
                  <Space vertical spacing={2} style={{ flex: 1 }}>
                    <Text size="small" ellipsis={{ showTooltip: true }}>
                      {f.name}
                    </Text>
                    <Progress
                      percent={Math.round((f.count / topFamilyMax) * 100)}
                      showInfo={false}
                      size="small"
                    />
                  </Space>
                  <Text strong size="small">
                    {f.count}
                  </Text>
                </Space>
              ))}
            </Space>
          </Card>
        </Col>
        <Col span={8}>
          <Card title={<Text strong>Data Quality</Text>}>
            <Space vertical align="center" style={{ width: "100%" }}>
              <Progress
                percent={dataQuality.score}
                type="circle"
                width={90}
                stroke={(() => {
                  if (dataQuality.score >= 90) return "var(--semi-color-success)"
                  if (dataQuality.score >= 70) return "var(--semi-color-warning)"
                  return "var(--semi-color-danger)"
                })()}
                format={() => <Text strong>{dataQuality.score}%</Text>}
              />
              <Text type="tertiary" size="small">
                {dataQuality.filled.toLocaleString()} / {dataQuality.total.toLocaleString()} fields
                filled
              </Text>
              <Text type="tertiary" size="small">
                Checks: Category, Family, Type, Level
              </Text>
            </Space>
          </Card>
        </Col>
      </Row>
    </Space>
  )
}
