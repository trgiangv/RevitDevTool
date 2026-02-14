/**
 * Heavy Elements Tracker — Leaderboard of families with highest complexity.
 * Click a family row to isolate its elements in Revit.
 */

import { Card, Progress, Space, Tag, Tooltip, Typography } from "@douyinfe/semi-ui"
import { IconEyeOpened } from "@douyinfe/semi-icons"
import type { HeavyFamily } from "@/types"

const { Text } = Typography

interface HeavyTrackerProps {
  families: HeavyFamily[]
  onIsolateFamily?: (familyName: string) => void
}

function getMedalEmoji(index: number): string {
  if (index === 0) return "1st"
  if (index === 1) return "2nd"
  if (index === 2) return "3rd"
  return `#${index + 1}`
}

function getSeverityColor(complexity: number): string {
  if (complexity >= 3500) return "var(--semi-color-danger)"
  if (complexity >= 2000) return "var(--semi-color-warning)"
  return "var(--semi-color-success)"
}

export function HeavyTracker({ families, onIsolateFamily }: HeavyTrackerProps) {
  const maxComplexity = Math.max(1, ...families.map((f) => f.estimated_complexity))

  return (
    <Card
      title={
        <Space>
          <Text strong>Heavy Elements Tracker</Text>
          <Tag size="small" color="orange">Top {families.length}</Tag>
        </Space>
      }
      headerStyle={{ padding: "12px 16px" }}
      bodyStyle={{ padding: "8px 16px 16px" }}
    >
      <Text type="tertiary" size="small" style={{ marginBottom: 8, display: "block" }}>
        Families with highest estimated geometry complexity. Click a row to isolate in Revit.
      </Text>

      <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
        {families.map((f, index) => {
          const pct = Math.round((f.estimated_complexity / maxComplexity) * 100)
          const color = getSeverityColor(f.estimated_complexity)

          return (
            <div
              key={f.family_name}
              role="button"
              tabIndex={0}
              onClick={() => onIsolateFamily?.(f.family_name)}
              onKeyDown={(e) => { if (e.key === "Enter") onIsolateFamily?.(f.family_name) }}
              style={{
                display: "grid",
                gridTemplateColumns: "32px 1fr 28px 60px",
                alignItems: "center",
                gap: 8,
                padding: "6px 8px",
                borderRadius: 6,
                background: index < 3 ? "var(--semi-color-fill-0)" : "transparent",
                cursor: onIsolateFamily ? "pointer" : "default",
                transition: "background 0.15s",
              }}
              onMouseEnter={(e) => {
                if (onIsolateFamily) {
                  (e.currentTarget as HTMLElement).style.background = "var(--semi-color-fill-1)"
                }
              }}
              onMouseLeave={(e) => {
                (e.currentTarget as HTMLElement).style.background =
                  index < 3 ? "var(--semi-color-fill-0)" : "transparent"
              }}
              title={`Click to isolate all "${f.family_name}" elements`}
            >
              {/* Rank */}
              <Tag
                size="small"
                color={index < 3 ? "orange" : "grey"}
                style={{ textAlign: "center", width: 32, justifyContent: "center" }}
              >
                {getMedalEmoji(index)}
              </Tag>

              {/* Family info + bar */}
              <div>
                <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 2 }}>
                  <Text size="small" strong ellipsis={{ showTooltip: true }} style={{ maxWidth: 160 }}>
                    {f.family_name}
                  </Text>
                  <Text type="tertiary" size="small">{f.category}</Text>
                </div>
                <Progress
                  percent={pct}
                  showInfo={false}
                  size="small"
                  stroke={color}
                  style={{ width: "100%" }}
                />
                <div style={{ display: "flex", gap: 8, marginTop: 2 }}>
                  <Text type="tertiary" size="small">{f.instance_count} instances</Text>
                  <Text type="tertiary" size="small">{f.type_count} types</Text>
                </div>
              </div>

              {/* Isolate icon */}
              <Tooltip content="Isolate in Revit">
                <IconEyeOpened
                  size="small"
                  style={{ color: "var(--semi-color-text-3)", opacity: 0.6 }}
                />
              </Tooltip>

              {/* Complexity score */}
              <Text strong size="small" style={{ textAlign: "right", color }}>
                {f.estimated_complexity.toLocaleString()}
              </Text>
            </div>
          )
        })}
      </div>
    </Card>
  )
}
