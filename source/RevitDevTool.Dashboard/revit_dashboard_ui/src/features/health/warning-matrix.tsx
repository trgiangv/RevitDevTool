/**
 * Warning Severity Matrix — classifies Revit warnings by severity.
 * Critical (red), Moderate (yellow), Info (green).
 * Features: per-row click-to-isolate, Select All, Color Override per severity.
 */

import { useMemo, useState } from "react"
import { Button, Card, Collapse, Progress, Space, Tag, Tooltip, Typography } from "@douyinfe/semi-ui"
import {
  IconAlertTriangle,
  IconEyeOpened,
  IconTickCircle,
} from "@douyinfe/semi-icons"
import type { WarningItem } from "@/types"

const { Text } = Typography

interface WarningSeverityMatrixProps {
  warnings: WarningItem[]
  onIsolateWarningElements: (elementIds: number[]) => void
  onSelectElements?: (elementIds: number[]) => void
  onColorOverride?: (elementIds: number[], color: [number, number, number]) => void
}

const SEVERITY_CONFIG = {
  critical: {
    color: "red" as const,
    label: "Critical",
    bg: "rgba(255,77,79,0.08)",
    overrideColor: [255, 77, 79] as [number, number, number],
  },
  moderate: {
    color: "orange" as const,
    label: "Moderate",
    bg: "rgba(250,173,20,0.08)",
    overrideColor: [250, 173, 20] as [number, number, number],
  },
  info: {
    color: "green" as const,
    label: "Info",
    bg: "rgba(82,196,26,0.08)",
    overrideColor: [82, 196, 26] as [number, number, number],
  },
}

export function WarningSeverityMatrix({
  warnings,
  onIsolateWarningElements,
  onSelectElements,
  onColorOverride,
}: WarningSeverityMatrixProps) {
  const [expandedSeverity, setExpandedSeverity] = useState<string[]>(["critical"])

  const grouped = useMemo(() => {
    const groups: Record<WarningItem["severity"], WarningItem[]> = {
      critical: [],
      moderate: [],
      info: [],
    }
    for (const w of warnings) {
      groups[w.severity].push(w)
    }
    return groups
  }, [warnings])

  const total = warnings.length

  // Get all element IDs for a severity group
  const getElementIds = (severity: WarningItem["severity"]) => {
    const ids = new Set<number>()
    for (const w of grouped[severity]) {
      for (const id of w.element_ids) ids.add(id)
    }
    return Array.from(ids)
  }

  return (
    <Card
      title={
        <Space>
          <IconAlertTriangle style={{ color: "var(--semi-color-warning)" }} />
          <Text strong>Warning Severity Matrix</Text>
          <Tag size="small">{total} total</Tag>
        </Space>
      }
      headerStyle={{ padding: "12px 16px" }}
      bodyStyle={{ padding: "8px 16px 16px" }}
    >
      {/* Summary bars */}
      <div style={{ display: "flex", gap: 12, marginBottom: 12 }}>
        {(["critical", "moderate", "info"] as const).map((sev) => {
          const config = SEVERITY_CONFIG[sev]
          const count = grouped[sev].length
          const pct = total > 0 ? Math.round((count / total) * 100) : 0
          return (
            <div
              key={sev}
              style={{
                flex: 1,
                padding: "10px 12px",
                borderRadius: 8,
                background: config.bg,
                textAlign: "center",
              }}
            >
              <Text strong style={{ fontSize: 20 }}>{count}</Text>
              <div>
                <Tag size="small" color={config.color}>{config.label}</Tag>
              </div>
              <Progress
                percent={pct}
                showInfo={false}
                size="small"
                stroke={(() => {
                  if (sev === "critical") return "var(--semi-color-danger)"
                  if (sev === "moderate") return "var(--semi-color-warning)"
                  return "var(--semi-color-success)"
                })()}
                style={{ marginTop: 6 }}
              />
            </div>
          )
        })}
      </div>

      {/* Detail list */}
      <Collapse activeKey={expandedSeverity} onChange={(keys) => setExpandedSeverity(keys as string[])}>
        {(["critical", "moderate", "info"] as const).map((sev) => {
          const config = SEVERITY_CONFIG[sev]
          const items = grouped[sev]
          if (items.length === 0) return null

          const sevElementIds = getElementIds(sev)

          return (
            <Collapse.Panel
              key={sev}
              itemKey={sev}
              header={
                <Space>
                  <Tag color={config.color} size="small">{config.label}</Tag>
                  <Text size="small">{items.length} warnings</Text>
                </Space>
              }
              extra={
                <Space>
                  {/* Select all elements for this severity */}
                  {onSelectElements && (
                    <Tooltip content={`Select all ${sevElementIds.length} elements`}>
                      <Button
                        size="small"
                        theme="borderless"
                        icon={<IconTickCircle size="small" />}
                        onClick={(e) => {
                          e.stopPropagation()
                          onSelectElements(sevElementIds)
                        }}
                      />
                    </Tooltip>
                  )}
                  {/* Color override for all elements of this severity */}
                  {onColorOverride && (
                    <Tooltip content={`Color override (${config.label})`}>
                      <Button
                        size="small"
                        theme="borderless"
                        onClick={(e) => {
                          e.stopPropagation()
                          onColorOverride(sevElementIds, config.overrideColor)
                        }}
                        style={{ padding: "0 4px" }}
                      >
                        <div
                          style={{
                            width: 14,
                            height: 14,
                            borderRadius: 3,
                            backgroundColor: `rgb(${config.overrideColor.join(",")})`,
                            border: "1px solid var(--semi-color-border)",
                          }}
                        />
                      </Button>
                    </Tooltip>
                  )}
                  {/* Isolate all elements for this severity */}
                  <Tooltip content="Isolate in Revit">
                    <Button
                      size="small"
                      theme="borderless"
                      icon={<IconEyeOpened size="small" />}
                      onClick={(e) => {
                        e.stopPropagation()
                        onIsolateWarningElements(sevElementIds)
                      }}
                    />
                  </Tooltip>
                </Space>
              }
            >
              <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                {items.slice(0, 20).map((w) => (
                  <div
                    key={w.id}
                    role="button"
                    tabIndex={0}
                    onClick={() => onIsolateWarningElements(w.element_ids)}
                    onKeyDown={(e) => { if (e.key === "Enter") onIsolateWarningElements(w.element_ids) }}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 8,
                      padding: "4px 8px",
                      borderRadius: 4,
                      background: "var(--semi-color-fill-0)",
                      cursor: "pointer",
                      transition: "background 0.15s",
                    }}
                    onMouseEnter={(e) => {
                      (e.currentTarget as HTMLElement).style.background = "var(--semi-color-fill-1)"
                    }}
                    onMouseLeave={(e) => {
                      (e.currentTarget as HTMLElement).style.background = "var(--semi-color-fill-0)"
                    }}
                    title={`Click to isolate ${w.element_ids.length} elements in Revit`}
                  >
                    <IconEyeOpened size="extra-small" style={{ color: "var(--semi-color-text-3)", flexShrink: 0 }} />
                    <Tag size="small" color="grey">{w.category}</Tag>
                    <Text size="small" style={{ flex: 1 }}>{w.description}</Text>
                    <Text type="tertiary" size="small">{w.element_ids.length} elem</Text>
                  </div>
                ))}
                {items.length > 20 && (
                  <Text type="tertiary" size="small" style={{ textAlign: "center", padding: 4 }}>
                    ... and {items.length - 20} more
                  </Text>
                )}
              </div>
            </Collapse.Panel>
          )
        })}
      </Collapse>
    </Card>
  )
}
