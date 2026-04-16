/**
 * Top bar with breadcrumb, global filters, and action buttons.
 */

import { Button, Space, Tag, Typography } from "@douyinfe/semi-ui"
import { IconRefresh, IconDownload, IconSun, IconMoon } from "@douyinfe/semi-icons"
import { useTheme } from "@/components/theme-provider"
import type { ModelInfo } from "@/types"
import type { PageId } from "./app-sidebar"

const { Title, Text } = Typography

const PAGE_LABELS: Record<PageId, string> = {
  home: "Project Pulse",
  inventory: "Visual Inventory",
  health: "Model Health",
  schedule: "Smart Schedule",
  settings: "Settings",
}

interface TopBarProps {
  activePage: PageId
  modelInfo: ModelInfo | null
  elementCount: number
  totalCount: number
  isRefreshing: boolean
  onRefresh: () => void
  onExport: () => void
}

export function TopBar({
  activePage,
  modelInfo,
  elementCount,
  totalCount,
  isRefreshing,
  onRefresh,
  onExport,
}: TopBarProps) {
  const { resolvedTheme, setTheme } = useTheme()

  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        padding: "0 20px",
        height: 44,
        backgroundColor: "var(--semi-color-bg-1)",
        borderBottom: "1px solid var(--semi-color-border)",
        flexShrink: 0,
      }}
    >
      {/* Breadcrumb */}
      <Space>
        <Title heading={6} style={{ margin: 0 }}>
          {modelInfo?.file_name ?? "BIM Dashboard"}
        </Title>
        <Text type="tertiary">/</Text>
        <Text type="secondary">{PAGE_LABELS[activePage]}</Text>
        {modelInfo?.current_view && (
          <>
            <Text type="tertiary">/</Text>
            <Text type="tertiary" size="small">{modelInfo.current_view}</Text>
          </>
        )}
        <Tag color="blue" size="small">
          {elementCount.toLocaleString()} / {totalCount.toLocaleString()}
        </Tag>
      </Space>

      {/* Actions */}
      <Space>
        <Button
          theme="borderless"
          icon={resolvedTheme === "dark" ? <IconSun /> : <IconMoon />}
          onClick={() => setTheme(resolvedTheme === "dark" ? "light" : "dark")}
        />
        <Button
          size="small"
          icon={<IconRefresh spin={isRefreshing} />}
          loading={isRefreshing}
          onClick={onRefresh}
        >
          Refresh
        </Button>
        <Button size="small" icon={<IconDownload />} onClick={onExport}>
          Export
        </Button>
      </Space>
    </div>
  )
}
