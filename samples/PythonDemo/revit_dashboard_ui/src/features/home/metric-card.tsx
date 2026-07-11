/**
 * KPI Metric Card — "Big Numbers" style card.
 * Large font, dark background, optional sparkline for session history.
 */

import { Card, Typography } from "@douyinfe/semi-ui"
import { Sparkline } from "@/components/charts/sparkline"

const { Title, Text } = Typography

const COLOR_MAP: Record<string, { accent: string; bg: string }> = {
  blue: { accent: "#3b82f6", bg: "linear-gradient(135deg, #1e3a5f 0%, #1a2744 100%)" },
  cyan: { accent: "#06b6d4", bg: "linear-gradient(135deg, #164e63 0%, #1a2744 100%)" },
  violet: { accent: "#8b5cf6", bg: "linear-gradient(135deg, #3b1f6e 0%, #1a2744 100%)" },
  green: { accent: "#22c55e", bg: "linear-gradient(135deg, #14532d 0%, #1a2744 100%)" },
  orange: { accent: "#f97316", bg: "linear-gradient(135deg, #7c2d12 0%, #1a2744 100%)" },
  red: { accent: "#ef4444", bg: "linear-gradient(135deg, #7f1d1d 0%, #1a2744 100%)" },
}

interface MetricCardProps {
  label: string
  value: number
  color: string
  icon?: React.ReactNode
  sparklineData?: number[]
}

export function MetricCard({ label, value, color, icon, sparklineData }: MetricCardProps) {
  const palette = COLOR_MAP[color] ?? COLOR_MAP.blue

  return (
    <Card
      bodyStyle={{
        padding: "16px 20px",
        background: palette.bg,
        borderRadius: 8,
        position: "relative",
        overflow: "hidden",
      }}
      style={{ border: "none" }}
    >
      {/* Icon watermark */}
      {icon && (
        <div
          style={{
            position: "absolute",
            right: 12,
            top: 8,
            opacity: 0.15,
            fontSize: 36,
            color: palette.accent,
          }}
        >
          {icon}
        </div>
      )}

      <Text
        style={{
          color: "rgba(255,255,255,0.6)",
          textTransform: "uppercase",
          letterSpacing: "0.08em",
          fontSize: 11,
          fontWeight: 500,
        }}
      >
        {label}
      </Text>

      <Title
        heading={2}
        style={{
          margin: "4px 0 0",
          color: "#ffffff",
          fontSize: 36,
          fontWeight: 700,
          lineHeight: 1.1,
        }}
      >
        {value.toLocaleString()}
      </Title>

      {/* Sparkline */}
      {sparklineData && sparklineData.length >= 2 && (
        <div style={{ marginTop: 8 }}>
          <Sparkline data={sparklineData} width={100} height={20} color={palette.accent} />
        </div>
      )}
    </Card>
  )
}
