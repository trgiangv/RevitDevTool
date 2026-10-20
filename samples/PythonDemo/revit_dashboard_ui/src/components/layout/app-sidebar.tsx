/**
 * Vertical navigation. Collapse and the selected item are Semi Nav.
 */

import { Nav } from "@douyinfe/semi-ui"
import { Boxes, Gauge, HeartPulse, Settings, Table2 } from "lucide-react"

export type PageId = "home" | "inventory" | "health" | "schedule" | "settings"

interface AppSidebarProps {
  activePage: PageId
  onPageChange: (page: PageId) => void
}

const NAV_ITEMS: Array<{ itemKey: PageId; text: string; icon: React.ReactNode }> = [
  { itemKey: "home", text: "Project Pulse", icon: <Gauge size={20} /> },
  { itemKey: "inventory", text: "Inventory", icon: <Boxes size={20} /> },
  { itemKey: "health", text: "Model Health", icon: <HeartPulse size={20} /> },
  { itemKey: "schedule", text: "Schedule", icon: <Table2 size={20} /> },
  { itemKey: "settings", text: "Settings", icon: <Settings size={20} /> },
]

const PAGE_IDS = new Set<string>(NAV_ITEMS.map((item) => item.itemKey))

export function AppSidebar({ activePage, onPageChange }: Readonly<AppSidebarProps>) {
  return (
    <Nav
      style={{ height: "100%", maxWidth: 220 }}
      defaultIsCollapsed
      selectedKeys={[activePage]}
      items={NAV_ITEMS}
      footer={{ collapseButton: true }}
      onSelect={(data) => {
        const key = String(data.itemKey)
        if (PAGE_IDS.has(key)) onPageChange(key as PageId)
      }}
    />
  )
}
