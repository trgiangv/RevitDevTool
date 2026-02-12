/**
 * Main Dashboard Application — orchestrates layout, navigation, and pages.
 *
 * Layout:
 * ┌──────────────────────────────────────────────────┐
 * │ Sidebar │  Top Bar (Breadcrumb + Actions)        │
 * │  (Nav)  │────────────────────────────────────────│
 * │         │  Main Content (Active Page)             │
 * │         │                          ┌─────────────│
 * │         │                          │ Properties  │
 * │         │                          │ Panel       │
 * └──────────────────────────────────────────────────┘
 */

import { useEffect, useState, useCallback } from "react"
import { toast } from "sonner"
import { Spin } from "@douyinfe/semi-ui"
import { useDashboard } from "@/providers/dashboard-provider"
import { useBridge } from "@/providers/bridge-provider"
import { useKeyboardShortcuts } from "@/hooks/use-keyboard-shortcuts"
import { AppSidebar, type PageId } from "@/components/layout/app-sidebar"
import { TopBar } from "@/components/layout/top-bar"
import { PropertiesPanel } from "@/components/layout/properties-panel"
import { HomePage } from "@/pages/home-page"
import { InventoryPage } from "@/pages/inventory-page"
import { HealthPage } from "@/pages/health-page"
import { SchedulePage } from "@/pages/schedule-page"
import { SettingsPage } from "@/pages/settings-page"

function DashboardContent() {
  const {
    payload,
    refreshData,
    filteredRows,
    allRows,
    filters,
    isRefreshing,
    propertiesElement,
    setPropertiesElement,
  } = useDashboard()
  const bridge = useBridge()

  const [activePage, setActivePage] = useState<PageId>("home")
  const [propertiesVisible, setPropertiesVisible] = useState(false)

  // Listen for export results
  useEffect(() => {
    const onExport = (event: Event) => {
      const d = (event as CustomEvent<{ ok: boolean; path?: string; error?: string }>).detail
      if (d?.ok) toast.success("Exported", { description: d.path })
      else toast.error("Export failed", { description: d?.error ?? "Unknown error" })
    }
    window.addEventListener("bim-export-result", onExport)
    return () => window.removeEventListener("bim-export-result", onExport)
  }, [])

  // Open properties panel when element is selected (Ghost Mode)
  useEffect(() => {
    if (propertiesElement) {
      setPropertiesVisible(true)
    } else {
      setPropertiesVisible(false)
    }
  }, [propertiesElement])

  useKeyboardShortcuts(refreshData)

  const handleExport = useCallback(() => {
    bridge.requestExport(filters)
  }, [bridge, filters])

  const handleCloseProperties = useCallback(() => {
    setPropertiesVisible(false)
    setPropertiesElement(null)
  }, [setPropertiesElement])

  // Loading state
  if (!payload) {
    return (
      <div style={{ height: "100vh", display: "grid", placeItems: "center" }}>
        <Spin size="large" tip="Loading BIM data..." />
      </div>
    )
  }

  const renderPage = () => {
    switch (activePage) {
      case "home":
        return <HomePage onNavigate={setActivePage} />
      case "inventory":
        return <InventoryPage active={activePage === "inventory"} />
      case "health":
        return <HealthPage active={activePage === "health"} />
      case "schedule":
        return <SchedulePage active={activePage === "schedule"} />
      case "settings":
        return <SettingsPage />
      default:
        return <HomePage />
    }
  }

  return (
    <div style={{ height: "100vh", display: "flex", flexDirection: "column" }}>
      {/* Top Bar */}
      <TopBar
        activePage={activePage}
        modelInfo={payload.model_info}
        elementCount={filteredRows.length}
        totalCount={allRows.length}
        isRefreshing={isRefreshing}
        onRefresh={refreshData}
        onExport={handleExport}
      />

      {/* Body: Sidebar + Content */}
      <div style={{ flex: 1, display: "flex", overflow: "hidden" }}>
        {/* Navigation Sidebar */}
        <AppSidebar activePage={activePage} onPageChange={setActivePage} />

        {/* Main Content */}
        <div
          style={{
            flex: 1,
            overflow: "auto",
            backgroundColor: "var(--semi-color-bg-0)",
          }}
        >
          {renderPage()}
        </div>
      </div>

      {/* Properties Panel (Ghost Mode) */}
      <PropertiesPanel
        element={propertiesElement}
        visible={propertiesVisible}
        onClose={handleCloseProperties}
      />
    </div>
  )
}

export default DashboardContent
