/**
 * Settings page — placeholder for dashboard configuration.
 */

import { Empty } from "@douyinfe/semi-ui"
import { Settings } from "lucide-react"

export function SettingsPage() {
  return (
    <Empty
      style={{ padding: 48 }}
      image={<Settings size={48} />}
      title="Settings"
      description="Dashboard configuration will be available here."
    />
  )
}
