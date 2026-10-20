/**
 * Header: breadcrumb, element count, and page actions.
 */

import { Breadcrumb, Button, Nav, Space, Tag } from "@douyinfe/semi-ui"
import { FileDown, Moon, Sun } from "lucide-react"
import { useTheme } from "@/components/theme-provider"
import type { ModelInfo } from "@/types"
import type { PageId } from "./app-sidebar"

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
}: Readonly<TopBarProps>) {
  const { resolvedTheme, setTheme } = useTheme()

  return (
    <Nav
      mode="horizontal"
      header={
        <Space>
          <Breadcrumb>
            <Breadcrumb.Item>{modelInfo?.file_name ?? "BIM Dashboard"}</Breadcrumb.Item>
            <Breadcrumb.Item>{PAGE_LABELS[activePage]}</Breadcrumb.Item>
            {modelInfo?.current_view ? (
              <Breadcrumb.Item>{modelInfo.current_view}</Breadcrumb.Item>
            ) : null}
          </Breadcrumb>
          <Tag color="blue" size="small">
            {elementCount.toLocaleString()} / {totalCount.toLocaleString()}
          </Tag>
        </Space>
      }
      footer={
        <Space>
          <Button
            theme="borderless"
            icon={resolvedTheme === "dark" ? <Sun size={16} /> : <Moon size={16} />}
            onClick={() => setTheme(resolvedTheme === "dark" ? "light" : "dark")}
          />
          <Button size="small" loading={isRefreshing} onClick={onRefresh}>
            Refresh
          </Button>
          <Button size="small" icon={<FileDown size={16} />} onClick={onExport}>
            Export
          </Button>
        </Space>
      }
    />
  )
}
