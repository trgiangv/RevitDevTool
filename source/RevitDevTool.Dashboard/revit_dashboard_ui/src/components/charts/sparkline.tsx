/**
 * Tiny sparkline chart for KPI cards.
 * Renders as a simple SVG path showing trend over recent sessions.
 */

interface SparklineProps {
  data: number[]
  width?: number
  height?: number
  color?: string
}

export function Sparkline({ data, width = 80, height = 24, color = "var(--semi-color-primary)" }: SparklineProps) {
  if (data.length < 2) return null

  const min = Math.min(...data)
  const max = Math.max(...data)
  const range = max - min || 1

  const points = data.map((value, index) => {
    const x = (index / (data.length - 1)) * width
    const y = height - ((value - min) / range) * (height - 4) - 2
    return `${x},${y}`
  })

  const pathD = `M ${points.join(" L ")}`

  // Area fill
  const areaD = `${pathD} L ${width},${height} L 0,${height} Z`

  return (
    <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} style={{ display: "block" }}>
      <defs>
        <linearGradient id={`spark-grad-${color.replace(/[^a-z0-9]/gi, "")}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity={0.3} />
          <stop offset="100%" stopColor={color} stopOpacity={0.05} />
        </linearGradient>
      </defs>
      <path
        d={areaD}
        fill={`url(#spark-grad-${color.replace(/[^a-z0-9]/gi, "")})`}
      />
      <path
        d={pathD}
        fill="none"
        stroke={color}
        strokeWidth={1.5}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      {/* Current value dot */}
      <circle
        cx={width}
        cy={height - ((data[data.length - 1] - min) / range) * (height - 4) - 2}
        r={2}
        fill={color}
      />
    </svg>
  )
}
