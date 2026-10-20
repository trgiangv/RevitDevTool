/**
 * Shared ECharts host. Charts register only the series they render.
 */

import { useEffect, useMemo, useRef } from "react"
import { init, use, type ECharts, type EChartsCoreOption } from "echarts/core"
import { LineChart, PieChart, TreemapChart } from "echarts/charts"
import { GridComponent, LegendComponent, TooltipComponent } from "echarts/components"
import { CanvasRenderer } from "echarts/renderers"
import { useTheme } from "@/components/theme-provider"

use([
  LineChart,
  PieChart,
  TreemapChart,
  GridComponent,
  LegendComponent,
  TooltipComponent,
  CanvasRenderer,
])

export interface ChartClick {
  name: string
  data: unknown
}

export interface ChartColors {
  text: string
  textSecondary: string
  border: string
  bg: string
  primary: string
}

function readToken(name: string, fallback: string): string {
  const value = getComputedStyle(document.body).getPropertyValue(name).trim()
  return value || fallback
}

export function useChartColors(): ChartColors {
  const { resolvedTheme } = useTheme()
  return useMemo(
    () => ({
      text: readToken("--semi-color-text-0", "#1c1f23"),
      textSecondary: readToken("--semi-color-text-2", "#6b7075"),
      border: readToken("--semi-color-border", "#e6e8ea"),
      bg: readToken("--semi-color-bg-1", "#ffffff"),
      primary: readToken("--semi-color-primary", "#1677ff"),
    }),
    [resolvedTheme],
  )
}

export function tooltipBase(colors: ChartColors) {
  return {
    confine: true,
    backgroundColor: colors.bg,
    borderColor: colors.border,
    textStyle: { color: colors.text },
  }
}

interface EChartProps {
  option: EChartsCoreOption
  height?: number
  onClick?: (params: ChartClick) => void
}

export function EChart({ option, height = 280, onClick }: Readonly<EChartProps>) {
  const nodeRef = useRef<HTMLDivElement>(null)
  const chartRef = useRef<ECharts | null>(null)
  const onClickRef = useRef(onClick)

  useEffect(() => {
    onClickRef.current = onClick
  }, [onClick])

  useEffect(() => {
    const node = nodeRef.current
    if (!node) return
    const chart = init(node)
    chartRef.current = chart
    const handleClick = (params: unknown) => {
      const click = params as { name?: unknown; data?: unknown }
      if (typeof click.name !== "string") return
      onClickRef.current?.({ name: click.name, data: click.data })
    }
    chart.on("click", handleClick)
    const observer = new ResizeObserver(() => chart.resize())
    observer.observe(node)
    return () => {
      observer.disconnect()
      chart.off("click", handleClick)
      chart.dispose()
      chartRef.current = null
    }
  }, [])

  useEffect(() => {
    const chart = chartRef.current
    if (!chart) return
    chart.setOption(option, { notMerge: true })
    chart.resize()
  }, [option])

  return <div ref={nodeRef} style={{ width: "100%", height, minWidth: 0 }} />
}
