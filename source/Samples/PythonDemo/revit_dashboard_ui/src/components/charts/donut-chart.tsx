/**
 * Donut chart for Category Distribution using Recharts.
 * Click a slice to filter the entire dashboard by that category.
 */

import { useCallback } from "react"
import { PieChart, Pie, Cell, Tooltip, ResponsiveContainer } from "recharts"
import { Card, Typography } from "@douyinfe/semi-ui"

const { Text } = Typography

const COLORS = [
  "var(--semi-color-primary)",
  "#36cfc9",
  "#597ef7",
  "#ff7a45",
  "#ffc53d",
  "#73d13d",
  "#ff4d4f",
  "#b37feb",
  "#ff85c0",
  "#85a5ff",
  "#5cdbd3",
  "#ffd666",
]

interface DonutChartProps {
  title: string
  data: Array<{ label: string; value: number }>
  activeKey: string | null | undefined
  onSelect: (key: string) => void
}

interface TooltipPayload {
  label: string
  value: number
  percent: number
}

function CustomTooltip({ active, payload }: { active?: boolean; payload?: Array<{ payload: TooltipPayload }> }) {
  if (!active || !payload?.[0]) return null
  const d = payload[0].payload
  return (
    <div
      style={{
        background: "var(--semi-color-bg-2)",
        border: "1px solid var(--semi-color-border)",
        borderRadius: 6,
        padding: "8px 12px",
        boxShadow: "var(--semi-shadow-elevated)",
      }}
    >
      <Text strong size="small">{d.label}</Text>
      <div style={{ marginTop: 4 }}>
        <Text size="small">{d.value.toLocaleString()} elements</Text>
        <Text type="tertiary" size="small" style={{ marginLeft: 8 }}>
          ({d.percent.toFixed(1)}%)
        </Text>
      </div>
    </div>
  )
}

export function DonutChart({ title, data, activeKey, onSelect }: DonutChartProps) {
  const total = data.reduce((sum, d) => sum + d.value, 0)
  const chartData = data.map((d) => ({
    ...d,
    percent: total > 0 ? (d.value / total) * 100 : 0,
  }))

  const handleClick = useCallback(
    (_: unknown, index: number) => {
      const item = data[index]
      if (item) onSelect(item.label)
    },
    [data, onSelect],
  )

  return (
    <Card
      title={<Text strong>{title}</Text>}
      headerStyle={{ padding: "12px 16px" }}
      bodyStyle={{ padding: "8px 16px 16px" }}
    >
      <ResponsiveContainer width="100%" height={220}>
        <PieChart>
          <Pie
            data={chartData}
            cx="50%"
            cy="50%"
            innerRadius={55}
            outerRadius={85}
            paddingAngle={2}
            dataKey="value"
            nameKey="label"
            onClick={handleClick}
            style={{ cursor: "pointer", outline: "none" }}
          >
            {chartData.map((entry, index) => (
              <Cell
                key={entry.label}
                fill={COLORS[index % COLORS.length]}
                opacity={activeKey && activeKey !== entry.label ? 0.3 : 1}
                stroke={activeKey === entry.label ? "var(--semi-color-text-0)" : "transparent"}
                strokeWidth={activeKey === entry.label ? 2 : 0}
              />
            ))}
          </Pie>
          <Tooltip
            content={<CustomTooltip />}
            wrapperStyle={{ pointerEvents: "none" }}
            allowEscapeViewBox={{ x: true, y: true }}
            offset={20}
            isAnimationActive={false}
          />
        </PieChart>
      </ResponsiveContainer>

      {/* Legend */}
      <div style={{ display: "flex", flexWrap: "wrap", gap: "4px 12px", marginTop: 4 }}>
        {chartData.slice(0, 6).map((d, i) => (
          <div
            key={d.label}
            role="button"
            tabIndex={0}
            onClick={() => onSelect(d.label)}
            onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") onSelect(d.label) }}
            style={{
              display: "flex",
              alignItems: "center",
              gap: 4,
              cursor: "pointer",
              opacity: activeKey && activeKey !== d.label ? 0.4 : 1,
            }}
          >
            <div
              style={{
                width: 8,
                height: 8,
                borderRadius: "50%",
                backgroundColor: COLORS[i % COLORS.length],
                flexShrink: 0,
              }}
            />
            <Text size="small" ellipsis={{ showTooltip: true }} style={{ maxWidth: 80 }}>
              {d.label}
            </Text>
          </div>
        ))}
      </div>
    </Card>
  )
}
