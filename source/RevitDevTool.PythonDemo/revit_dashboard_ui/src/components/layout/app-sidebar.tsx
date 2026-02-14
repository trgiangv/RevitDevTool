/**
 * Navigation sidebar with collapsible icon menu.
 * 50px collapsed / 180px expanded.
 */

import { useState } from "react"
import { Tooltip, Typography } from "@douyinfe/semi-ui"
import {
  IconHome,
  IconGridRectangle,
  IconShield,
  IconList,
  IconSetting,
  IconChevronLeft,
  IconChevronRight,
} from "@douyinfe/semi-icons"

const { Text } = Typography

export type PageId = "home" | "inventory" | "health" | "schedule" | "settings"

interface AppSidebarProps {
  activePage: PageId
  onPageChange: (page: PageId) => void
}

const NAV_ITEMS: Array<{ id: PageId; label: string; icon: React.ReactNode }> = [
  { id: "home", label: "Project Pulse", icon: <IconHome size="large" /> },
  { id: "inventory", label: "Inventory", icon: <IconGridRectangle size="large" /> },
  { id: "health", label: "Model Health", icon: <IconShield size="large" /> },
  { id: "schedule", label: "Schedule", icon: <IconList size="large" /> },
  { id: "settings", label: "Settings", icon: <IconSetting size="large" /> },
]

export function AppSidebar({ activePage, onPageChange }: AppSidebarProps) {
  const [collapsed, setCollapsed] = useState(true)

  return (
    <div
      style={{
        width: collapsed ? 50 : 180,
        height: "100%",
        backgroundColor: "var(--semi-color-bg-1)",
        borderRight: "1px solid var(--semi-color-border)",
        display: "flex",
        flexDirection: "column",
        transition: "width 0.2s ease",
        flexShrink: 0,
        overflow: "hidden",
      }}
    >
      {/* Nav items */}
      <div style={{ flex: 1, padding: "8px 0", display: "flex", flexDirection: "column", gap: 2 }}>
        {NAV_ITEMS.map((item) => {
          const isActive = activePage === item.id

          const btn = (
            <div
              key={item.id}
              role="button"
              tabIndex={0}
              onClick={() => onPageChange(item.id)}
              onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") onPageChange(item.id) }}
              style={{
                width: collapsed ? 36 : "calc(100% - 16px)",
                height: 36,
                margin: collapsed ? "2px 7px" : "2px 8px",
                padding: 0,
                display: "flex",
                alignItems: "center",
                justifyContent: collapsed ? "center" : "flex-start",
                gap: 8,
                paddingLeft: collapsed ? 0 : 10,
                borderRadius: 8,
                cursor: "pointer",
                background: isActive ? "var(--semi-color-primary-light-default)" : "transparent",
                color: isActive ? "var(--semi-color-primary)" : "var(--semi-color-text-2)",
                transition: "background 0.15s, color 0.15s",
              }}
            >
              <span style={{ display: "flex", alignItems: "center", justifyContent: "center", width: 20, height: 20, flexShrink: 0 }}>
                {item.icon}
              </span>
              {!collapsed && (
                <Text
                  size="small"
                  style={{
                    color: isActive ? "var(--semi-color-primary)" : "var(--semi-color-text-1)",
                    whiteSpace: "nowrap",
                    fontWeight: isActive ? 600 : 400,
                  }}
                >
                  {item.label}
                </Text>
              )}
            </div>
          )

          return collapsed ? (
            <Tooltip key={item.id} content={item.label} position="right">
              {btn}
            </Tooltip>
          ) : (
            btn
          )
        })}
      </div>

      {/* Collapse toggle */}
      <div style={{ padding: 8, borderTop: "1px solid var(--semi-color-border)" }}>
        <div
          role="button"
          tabIndex={0}
          onClick={() => setCollapsed(!collapsed)}
          onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") setCollapsed(!collapsed) }}
          style={{
            width: "100%",
            height: 28,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            cursor: "pointer",
            borderRadius: 6,
            color: "var(--semi-color-text-2)",
          }}
        >
          {collapsed ? <IconChevronRight size="small" /> : <IconChevronLeft size="small" />}
        </div>
      </div>
    </div>
  )
}
