/**
 * Settings page — placeholder for dashboard configuration.
 */

import { Card, Typography } from "@douyinfe/semi-ui"
import { IconSetting } from "@douyinfe/semi-icons"

const { Title, Text } = Typography

export function SettingsPage() {
  return (
    <div style={{ padding: 20 }}>
      <Card>
        <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 16, padding: 32 }}>
          <IconSetting size="extra-large" style={{ color: "var(--semi-color-text-2)" }} />
          <Title heading={4}>Settings</Title>
          <Text type="tertiary">Dashboard configuration will be available here.</Text>
        </div>
      </Card>
    </div>
  )
}
