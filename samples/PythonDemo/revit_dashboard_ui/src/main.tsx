import "@douyinfe/semi-ui/react19-adapter"
import { StrictMode } from "react"
import { createRoot } from "react-dom/client"
import "@douyinfe/semi-ui/dist/css/semi.min.css"
import en_US from "@douyinfe/semi-ui/lib/es/locale/source/en_US"
import { LocaleProvider, Toast } from "@douyinfe/semi-ui"
import { ThemeProvider } from "@/components/theme-provider"
import { ErrorBoundary } from "@/components/error-boundary"
import { BridgeProvider } from "@/providers/bridge-provider"
import { DashboardProvider } from "@/providers/dashboard-provider"
import DashboardContent from "./App"
import "./index.css"

Toast.config({ bottom: 24, right: 24, theme: "light" })

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <ErrorBoundary>
      <LocaleProvider locale={en_US}>
        <ThemeProvider defaultTheme="dark">
          <BridgeProvider>
            <DashboardProvider>
              <DashboardContent />
            </DashboardProvider>
          </BridgeProvider>
        </ThemeProvider>
      </LocaleProvider>
    </ErrorBoundary>
  </StrictMode>,
)
