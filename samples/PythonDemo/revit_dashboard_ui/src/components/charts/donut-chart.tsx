/**
 * Category or level donut. Hover and slice layout belong to ECharts.
 * Click reports the slice name so the dashboard can filter.
 */

import { useMemo } from "react"
import type { EChartsCoreOption } from "echarts/core"
import { Card, Typography } from "@douyinfe/semi-ui"
import { EChart, tooltipBase, useChartColors } from "./echart"

const { Text } = Typography

interface DonutChartProps {
  title: string
  data: Array<{ label: string; value: number }>
  activeKey: string | null | undefined
  onSelect: (key: string) => void
}

export function DonutChart({ title, data, activeKey, onSelect }: Readonly<DonutChartProps>) {
  const colors = useChartColors()
  const option = useMemo<EChartsCoreOption>(
    () => ({
      color: [
        colors.primary,
        "#36cfc9",
        "#597ef7",
        "#ff7a45",
        "#ffc53d",
        "#73d13d",
        "#ff4d4f",
        "#b37feb",
        "#ff85c0",
        "#85a5ff",
      ],
      tooltip: {
        ...tooltipBase(colors),
        trigger: "item",
        formatter: (raw: unknown) => {
          const item = raw as { name?: string; value?: number; percent?: number }
          const value = item.value ?? 0
          const percent = item.percent ?? 0
          return `${item.name ?? ""}: ${value.toLocaleString()} (${percent.toFixed(1)}%)`
        },
      },
      legend: {
        type: "scroll",
        bottom: 0,
        selectedMode: false,
        textStyle: { color: colors.textSecondary },
      },
      series: [
        {
          type: "pie",
          radius: ["46%", "70%"],
          center: ["50%", "44%"],
          avoidLabelOverlap: true,
          label: { show: false },
          itemStyle: { borderColor: colors.bg, borderWidth: 2, borderRadius: 4 },
          emphasis: { scale: true, scaleSize: 6 },
          data: data.map((item) => ({
            name: item.label,
            value: item.value,
            itemStyle: activeKey && activeKey !== item.label ? { opacity: 0.35 } : { opacity: 1 },
          })),
        },
      ],
    }),
    [activeKey, colors, data],
  )

  return (
    <Card
      title={<Text strong>{title}</Text>}
      style={{ width: "100%" }}
      headerStyle={{ padding: "12px 16px" }}
      bodyStyle={{ padding: "8px 16px 16px" }}
    >
      <EChart option={option} height={280} onClick={(params) => onSelect(params.name)} />
    </Card>
  )
}
