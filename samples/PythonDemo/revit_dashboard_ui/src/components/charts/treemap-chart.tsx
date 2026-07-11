/**
 * Spatial Treemap — hierarchical visualization: Level > Category.
 * Each rectangle represents elements grouped by Level and Category.
 * Click to isolate in Revit.
 */

import { useMemo, useCallback } from "react"
import { Treemap, ResponsiveContainer, Tooltip } from "recharts"
import { Card, Typography } from "@douyinfe/semi-ui"
import type { ElementRow } from "@/types"

const { Text } = Typography

const LEVEL_COLORS = [
  "#1890ff", "#13c2c2", "#52c41a", "#faad14", "#f5222d",
  "#722ed1", "#eb2f96", "#fa8c16", "#2f54eb", "#a0d911",
]

interface TreemapChartProps {
  rows: ElementRow[]
  onNodeClick?: (level: string, category: string) => void
}

interface TreemapNode {
  name: string
  size?: number
  children?: TreemapNode[]
  level?: string
  category?: string
  color?: string
}

interface TooltipPayload {
  name: string
  size: number
  level?: string
  category?: string
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
      {d.level && (
        <Text size="small" type="tertiary">{d.level}</Text>
      )}
      <div>
        <Text strong size="small">{d.category ?? d.name}</Text>
      </div>
      <Text size="small">{d.size?.toLocaleString()} elements</Text>
    </div>
  )
}

// Dynamic font size based on cell dimensions
function getFontSize(width: number, height: number): number {
  const minDim = Math.min(width, height)
  if (minDim > 100) return 12
  if (minDim > 60) return 10
  return 8
}

// Smart text truncation based on available width and font size
function truncateText(text: string, width: number, fontSize: number): string {
  const charWidth = fontSize * 0.55 // approximate character width
  const maxChars = Math.floor((width - 8) / charWidth) // 8px padding
  if (maxChars <= 2) return ""
  if (text.length <= maxChars) return text
  return text.slice(0, maxChars - 1) + "…"
}

// Custom rectangle content renderer
//
// UX Design notes:
// - Text uses SVG paint-order: stroke is rendered BEHIND fill so text
//   stays sharp while a thin outline provides contrast against any bg.
// - No heavy dark pill — just a soft stroke halo + gentle drop shadow.
// - font-weight 500 (normal cells) / 600 (level cells) for clean look
//   in both light and dark themes.
function CustomContent(props: {
  x: number
  y: number
  width: number
  height: number
  name: string
  size?: number
  color?: string
  depth: number
}) {
  const { x, y, width, height, name, size, color, depth } = props
  if (width < 4 || height < 4) return null

  const fontSize = getFontSize(width, height)
  const showText = width > 30 && height > 18
  const showCount = width > 50 && height > 32
  const displayName = showText ? truncateText(name, width, fontSize) : ""

  const textY = showCount ? y + height / 2 - 6 : y + height / 2

  // Shared text style: paint-order trick renders stroke behind the fill
  // creating a clean halo effect that works on any background color.
  const labelStyle: React.CSSProperties = {
    pointerEvents: "none",
    paintOrder: "stroke",
    // Subtle drop shadow only — no heavy textShadow
    filter: "drop-shadow(0 1px 1px rgba(0,0,0,0.25))",
  }

  return (
    <g>
      <rect
        x={x}
        y={y}
        width={width}
        height={height}
        fill={color ?? "#8884d8"}
        opacity={depth === 1 ? 0.85 : 0.75}
        stroke="var(--semi-color-bg-0)"
        strokeWidth={depth === 1 ? 2 : 1}
        rx={3}
        style={{ cursor: "pointer" }}
      />
      {showText && displayName && (
        <text
          x={x + width / 2}
          y={textY}
          textAnchor="middle"
          dominantBaseline="central"
          fill="white"
          stroke="rgba(0,0,0,0.45)"
          strokeWidth={2.5}
          fontSize={fontSize}
          fontWeight={depth === 1 ? 600 : 500}
          style={labelStyle}
        >
          {displayName}
        </text>
      )}
      {showCount && size !== undefined && (
        <text
          x={x + width / 2}
          y={y + height / 2 + fontSize - 2}
          textAnchor="middle"
          dominantBaseline="central"
          fill="rgba(255,255,255,0.85)"
          stroke="rgba(0,0,0,0.3)"
          strokeWidth={2}
          fontSize={fontSize - 1}
          fontWeight={400}
          style={labelStyle}
        >
          {size.toLocaleString()}
        </text>
      )}
    </g>
  )
}

export function TreemapChart({ rows, onNodeClick }: TreemapChartProps) {
  const treeData = useMemo(() => {
    // Group by Level > Category
    const levelMap = new Map<string, Map<string, number>>()

    for (const row of rows) {
      if (!levelMap.has(row.level)) {
        levelMap.set(row.level, new Map())
      }
      const catMap = levelMap.get(row.level)!
      catMap.set(row.category, (catMap.get(row.category) ?? 0) + 1)
    }

    const sortedLevels = Array.from(levelMap.keys()).sort()

    const children: TreemapNode[] = sortedLevels.map((level, levelIdx) => {
      const catMap = levelMap.get(level)!
      const color = LEVEL_COLORS[levelIdx % LEVEL_COLORS.length]

      return {
        name: level,
        color,
        children: Array.from(catMap.entries())
          .map(([category, count]) => ({
            name: category,
            size: count,
            level,
            category,
            color,
          }))
          .sort((a, b) => b.size - a.size),
      }
    })

    return children
  }, [rows])

  const handleClick = useCallback(
    (node: TreemapNode) => {
      if (node.level && node.category && onNodeClick) {
        onNodeClick(node.level, node.category)
      }
    },
    [onNodeClick],
  )

  return (
    <Card
      title={<Text strong>Spatial Treemap</Text>}
      headerStyle={{ padding: "12px 16px" }}
      bodyStyle={{ padding: "8px 16px 16px" }}
    >
      <Text type="tertiary" size="small" style={{ marginBottom: 8, display: "block" }}>
        Level / Category hierarchy. Click to isolate in Revit.
      </Text>
      <ResponsiveContainer width="100%" height={320}>
        <Treemap
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          data={treeData as any}
          dataKey="size"
          nameKey="name"
          stroke="var(--semi-color-bg-0)"
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          onClick={handleClick as any}
          content={<CustomContent x={0} y={0} width={0} height={0} name="" depth={0} />}
        >
          <Tooltip content={<CustomTooltip />} />
        </Treemap>
      </ResponsiveContainer>

      {/* Level legend */}
      <div style={{ display: "flex", flexWrap: "wrap", gap: "4px 12px", marginTop: 8 }}>
        {treeData.map((level, i) => (
          <div key={level.name} style={{ display: "flex", alignItems: "center", gap: 4 }}>
            <div
              style={{
                width: 10,
                height: 10,
                borderRadius: 2,
                backgroundColor: LEVEL_COLORS[i % LEVEL_COLORS.length],
              }}
            />
            <Text size="small">{level.name}</Text>
          </div>
        ))}
      </div>
    </Card>
  )
}
