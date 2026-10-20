/**
 * Session trend. ECharts draws the line, area, and axis tooltip.
 */

import { useMemo } from "react"
import type { EChartsCoreOption } from "echarts/core"
import { EChart, tooltipBase, useChartColors } from "./echart"

interface SparklineProps {
  data: number[]
  height?: number
}

export function Sparkline({ data, height = 36 }: Readonly<SparklineProps>) {
  const colors = useChartColors()
  const option = useMemo<EChartsCoreOption>(
    () => ({
      animation: false,
      grid: { left: 0, right: 4, top: 4, bottom: 4 },
      tooltip: {
        ...tooltipBase(colors),
        trigger: "axis",
        formatter: (raw: unknown) => {
          const items = Array.isArray(raw) ? raw : [raw]
          const value = (items[0] as { value?: number } | undefined)?.value
          return value == null ? "" : value.toLocaleString()
        },
      },
      xAxis: {
        type: "category",
        show: false,
        boundaryGap: false,
        data: data.map((_, index) => String(index + 1)),
      },
      yAxis: { type: "value", show: false, scale: true },
      series: [
        {
          type: "line",
          data,
          smooth: true,
          showSymbol: false,
          lineStyle: { width: 1.5, color: colors.primary },
          areaStyle: {
            opacity: 0.25,
            color: {
              type: "linear",
              x: 0,
              y: 0,
              x2: 0,
              y2: 1,
              colorStops: [
                { offset: 0, color: colors.primary },
                { offset: 1, color: "transparent" },
              ],
            },
          },
        },
      ],
    }),
    [colors, data],
  )

  if (data.length < 2) return null
  return <EChart option={option} height={height} />
}
