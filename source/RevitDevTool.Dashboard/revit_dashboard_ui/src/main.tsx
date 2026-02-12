import { StrictMode } from "react"
import { createRoot } from "react-dom/client"
// eslint-disable-next-line sonarjs/no-internal-api-use
import "../node_modules/@douyinfe/semi-ui/dist/css/semi.min.css"
import en_US from "@douyinfe/semi-ui/lib/es/locale/source/en_US"
import { LocaleProvider } from "@douyinfe/semi-ui"
import { Toaster } from "@/components/ui/sonner"
import { ThemeProvider } from "@/components/theme-provider"
import { ErrorBoundary } from "@/components/error-boundary"
import { BridgeProvider } from "@/providers/bridge-provider"
import { DashboardProvider } from "@/providers/dashboard-provider"
import DashboardContent from "./App"
import "./index.css"

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <ErrorBoundary>
      <LocaleProvider locale={en_US}>
        <ThemeProvider defaultTheme="dark">
          <BridgeProvider>
            <DashboardProvider>
              <DashboardContent />
              <Toaster richColors position="bottom-right" />
            </DashboardProvider>
          </BridgeProvider>
        </ThemeProvider>
      </LocaleProvider>
    </ErrorBoundary>
  </StrictMode>,
)
