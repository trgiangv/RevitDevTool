/**
 * Level / category treemap. ECharts lays out the rectangles.
 * A leaf click isolates that level and category in Revit.
 */

import { useMemo } from "react"
import type { EChartsCoreOption } from "echarts/core"
import { Card, Typography } from "@douyinfe/semi-ui"
import type { ElementRow } from "@/types"
import { EChart, tooltipBase, useChartColors } from "./echart"

const { Text } = Typography

interface TreemapChartProps {
  rows: ElementRow[]
  onNodeClick?: (level: string, category: string) => void
}

interface TreemapLeaf {
  name: string
  value: number
  level: string
  category: string
}

function isLeaf(data: unknown): data is TreemapLeaf {
  if (!data || typeof data !== "object") return false
  const record = data as { level?: unknown; category?: unknown }
  return typeof record.level === "string" && typeof record.category === "string"
}

export function TreemapChart({ rows, onNodeClick }: Readonly<TreemapChartProps>) {
  const colors = useChartColors()
  const data = useMemo(() => {
    const levelMap = new Map<string, Map<string, number>>()
    for (const row of rows) {
      const categories = levelMap.get(row.level) ?? new Map<string, number>()
      categories.set(row.category, (categories.get(row.category) ?? 0) + 1)
      levelMap.set(row.level, categories)
    }
    return [...levelMap.keys()]
      .sort((a, b) => a.localeCompare(b))
      .map((level) => {
        const categories = levelMap.get(level) ?? new Map<string, number>()
        return {
          name: level,
          children: [...categories.entries()]
            .sort((left, right) => right[1] - left[1])
            .map(([category, size]) => ({
              name: category,
              value: size,
              level,
              category,
            })),
        }
      })
  }, [rows])

  const option = useMemo<EChartsCoreOption>(
    () => ({
      tooltip: {
        ...tooltipBase(colors),
        trigger: "item",
        formatter: (raw: unknown) => {
          const item = raw as { name?: string; value?: number; data?: unknown }
          if (isLeaf(item.data)) {
            return `${item.data.level} / ${item.data.category}: ${(item.value ?? 0).toLocaleString()} elements`
          }
          return `${item.name ?? ""}: ${(item.value ?? 0).toLocaleString()} elements`
        },
      },
      series: [
        {
          type: "treemap",
          width: "100%",
          height: "100%",
          left: 0,
          right: 0,
          top: 8,
          bottom: 0,
          roam: false,
          nodeClick: false,
          breadcrumb: { show: false },
          colorMappingBy: "parent",
          label: { show: true, color: "#fff", overflow: "truncate" },
          upperLabel: { show: true, height: 22, color: "#fff" },
          itemStyle: { borderColor: colors.bg, gapWidth: 2 },
          levels: [
            {
              upperLabel: { show: true, height: 22 },
              itemStyle: { borderWidth: 3, gapWidth: 3, borderColor: colors.bg },
            },
            {
              itemStyle: { borderWidth: 1, gapWidth: 1, borderColor: colors.bg },
            },
          ],
          data,
        },
      ],
    }),
    [colors, data],
  )

  return (
    <Card
      title={<Text strong>Spatial Treemap</Text>}
      style={{ width: "100%" }}
      headerStyle={{ padding: "12px 16px" }}
      bodyStyle={{ padding: "8px 16px 16px" }}
    >
      <Text type="tertiary" size="small">
        Level, then category. Click a category to isolate it in Revit.
      </Text>
      <EChart
        option={option}
        height={480}
        onClick={(params) => {
          if (!isLeaf(params.data)) return
          onNodeClick?.(params.data.level, params.data.category)
        }}
      />
    </Card>
  )
}
