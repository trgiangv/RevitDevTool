/**
 * KPI card. The number is a Semi title. The trend line is ECharts.
 */

import { Card, Typography } from "@douyinfe/semi-ui"
import { Sparkline } from "@/components/charts/sparkline"

const { Title, Text } = Typography

interface MetricCardProps {
  label: string
  value: number
  icon?: React.ReactNode
  sparklineData?: number[]
}

export function MetricCard({ label, value, icon, sparklineData }: Readonly<MetricCardProps>) {
  return (
    <Card title={<Text type="tertiary">{label}</Text>} headerExtraContent={icon}>
      <Title heading={1}>{value.toLocaleString()}</Title>
      {sparklineData && sparklineData.length >= 2 ? <Sparkline data={sparklineData} /> : null}
    </Card>
  )
}
