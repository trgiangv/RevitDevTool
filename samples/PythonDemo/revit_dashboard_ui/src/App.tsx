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
import { toast } from "@/lib/toast"
import { Banner, Layout, Spin } from "@douyinfe/semi-ui"
import { useBridge } from "@/providers/bridge-provider"
import { useDashboard } from "@/providers/dashboard-provider"
import { useKeyboardShortcuts } from "@/hooks/use-keyboard-shortcuts"
import { exportDashboardWorkbook } from "@/lib/export-workbook"
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
    loadError,
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
    void exportDashboardWorkbook(
      {
        rows: filteredRows,
        filters,
        warnings: payload?.warnings,
      },
      (filename, contentBase64) => bridge.saveWorkbook(filename, contentBase64),
    )
      .then((filename) => {
        if (filename) toast.success("Exported", { description: filename })
      })
      .catch((err: unknown) => {
        toast.error("Export failed", {
          description: err instanceof Error ? err.message : "Unknown error",
        })
      })
  }, [bridge, filteredRows, filters, payload])

  const handleCloseProperties = useCallback(() => {
    setPropertiesVisible(false)
    setPropertiesElement(null)
  }, [setPropertiesElement])

  const { Header, Sider, Content } = Layout

  if (!payload) {
    return (
      <Layout style={{ height: "100vh" }}>
        <Content>
          {loadError ? (
            <div
              style={{
                height: "100%",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
              }}
            >
              <Banner type="danger" fullMode={false} closeIcon={null} description={loadError} />
            </div>
          ) : (
            <Spin size="large" tip="Loading BIM data..." style={{ width: "100%", height: "100%" }}>
              <div style={{ height: "100vh" }} />
            </Spin>
          )}
        </Content>
      </Layout>
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
    <Layout style={{ height: "100vh" }}>
      <Sider>
        <AppSidebar activePage={activePage} onPageChange={setActivePage} />
      </Sider>
      <Layout>
        <Header>
          <TopBar
            activePage={activePage}
            modelInfo={payload.model_info}
            elementCount={filteredRows.length}
            totalCount={allRows.length}
            isRefreshing={isRefreshing}
            onRefresh={refreshData}
            onExport={handleExport}
          />
        </Header>
        <Content style={{ overflow: "auto" }}>{renderPage()}</Content>
      </Layout>
      <PropertiesPanel
        element={propertiesElement}
        visible={propertiesVisible}
        onClose={handleCloseProperties}
      />
    </Layout>
  )
}

export default DashboardContent
