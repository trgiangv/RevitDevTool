/**
 * Family and type explorer.
 * Search is a Semi Input. Each family is a grid List item.
 */

import { useMemo, useState } from "react"
import { Card, Descriptions, Empty, Input, List, Space, Tag, Typography } from "@douyinfe/semi-ui"
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

export function FamilyExplorer({ rows, onSelectFamily }: Readonly<FamilyExplorerProps>) {
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

    const query = search.trim().toLowerCase()
    if (!query) return result
    return result.filter(
      (family) =>
        family.name.toLowerCase().includes(query) || family.category.toLowerCase().includes(query),
    )
  }, [rows, search])

  return (
    <Card
      title="Family & Type Explorer"
      headerExtraContent={<Tag>{families.length} families</Tag>}
      style={{ width: "100%" }}
    >
      <Space vertical spacing="medium" style={{ width: "100%", alignItems: "stretch" }}>
        <Input
          prefix={<IconSearch />}
          placeholder="Search families"
          value={search}
          onChange={(value) => setSearch(value)}
          showClear
        />
        <List
          split={false}
          grid={{ gutter: [12, 12], xs: 24, sm: 12, md: 12, lg: 8, xl: 6, xxl: 6 }}
          dataSource={families}
          emptyContent={<Empty description="No families match this search" />}
          renderItem={(family) => (
            <List.Item
              onClick={() => onSelectFamily?.(family.name)}
              style={{
                border: "1px solid var(--semi-color-border)",
                backgroundColor: "var(--semi-color-bg-2)",
                borderRadius: 3,
                padding: "12px 16px",
                cursor: onSelectFamily ? "pointer" : undefined,
                height: "100%",
              }}
            >
              <Space vertical align="start" spacing="tight" style={{ width: "100%" }}>
                <Text strong ellipsis={{ showTooltip: true }}>
                  {family.name}
                </Text>
                <Descriptions
                  size="small"
                  data={[
                    { key: "Category", value: family.category },
                    { key: "Instances", value: family.instanceCount },
                    { key: "Types", value: family.typeCount },
                  ]}
                />
              </Space>
            </List.Item>
          )}
        />
      </Space>
    </Card>
  )
}
