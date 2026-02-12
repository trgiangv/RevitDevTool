/**
 * Family & Type Explorer — Grid view of loaded families.
 * Shows family name + instance count, sorted by usage (most → least).
 */

import { useMemo, useState } from "react"
import { Card, Input, Space, Tag, Typography } from "@douyinfe/semi-ui"
import { IconSearch } from "@douyinfe/semi-icons"
import type { ElementRow } from "@/types"

const { Text } = Typography

interface FamilyExplorerProps {
  rows: ElementRow[]
  onSelectFamily?: (familyName: string) => void
}

interface FamilyInfo {
  name: string
  category: string
  instanceCount: number
  typeCount: number
}

export function FamilyExplorer({ rows, onSelectFamily }: FamilyExplorerProps) {
  const [search, setSearch] = useState("")

  const families = useMemo(() => {
    const map = new Map<string, { category: string; count: number; types: Set<string> }>()

    for (const row of rows) {
      const existing = map.get(row.family)
      if (existing) {
        existing.count++
        existing.types.add(row.type)
      } else {
        map.set(row.family, { category: row.category, count: 1, types: new Set([row.type]) })
      }
    }

    const result: FamilyInfo[] = Array.from(map.entries())
      .map(([name, data]) => ({
        name,
        category: data.category,
        instanceCount: data.count,
        typeCount: data.types.size,
      }))
      .sort((a, b) => b.instanceCount - a.instanceCount)

    if (search.trim()) {
      const q = search.toLowerCase()
      return result.filter(
        (f) => f.name.toLowerCase().includes(q) || f.category.toLowerCase().includes(q),
      )
    }

    return result
  }, [rows, search])

  const maxCount = Math.max(1, ...families.map((f) => f.instanceCount))

  return (
    <Card
      title={
        <Space>
          <Text strong>Family & Type Explorer</Text>
          <Tag size="small">{families.length} families</Tag>
        </Space>
      }
      headerStyle={{ padding: "12px 16px" }}
      bodyStyle={{ padding: "8px 16px 16px" }}
    >
      <Input
        prefix={<IconSearch />}
        placeholder="Search families..."
        value={search}
        onChange={(val) => setSearch(String(val ?? ""))}
        showClear
        size="small"
        style={{ marginBottom: 8 }}
      />

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))",
          gap: 8,
          maxHeight: 400,
          overflow: "auto",
        }}
      >
        {families.map((f) => {
          const barWidth = Math.max(4, (f.instanceCount / maxCount) * 100)

          return (
            <div
              key={f.name}
              role="button"
              tabIndex={0}
              onClick={() => onSelectFamily?.(f.name)}
              onKeyDown={(e) => { if (e.key === "Enter") onSelectFamily?.(f.name) }}
              style={{
                padding: "8px 10px",
                borderRadius: 6,
                border: "1px solid var(--semi-color-border)",
                cursor: onSelectFamily ? "pointer" : "default",
                background: "var(--semi-color-bg-2)",
                transition: "background 0.15s",
                position: "relative",
                overflow: "hidden",
              }}
            >
              {/* Background bar */}
              <div
                style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  bottom: 0,
                  width: `${barWidth}%`,
                  background: "var(--semi-color-primary-light-default)",
                  opacity: 0.1,
                  transition: "width 0.3s ease",
                }}
              />

              <div style={{ position: "relative" }}>
                <Text size="small" strong ellipsis={{ showTooltip: true }} style={{ maxWidth: 180, display: "block" }}>
                  {f.name}
                </Text>
                <div style={{ display: "flex", alignItems: "center", gap: 6, marginTop: 4 }}>
                  <Tag size="small" color="blue">{f.instanceCount}</Tag>
                  <Text type="tertiary" size="small">{f.category}</Text>
                  <Text type="tertiary" size="small">· {f.typeCount} types</Text>
                </div>
              </div>
            </div>
          )
        })}
      </div>
    </Card>
  )
}
