import { Component } from "react"
import { Button, Card, Typography } from "@douyinfe/semi-ui"
import { IconAlertTriangle } from "@douyinfe/semi-icons"

const { Title, Text } = Typography

interface Props {
  children: React.ReactNode
}

interface State {
  hasError: boolean
  error: Error | null
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props)
    this.state = { hasError: false, error: null }
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error }
  }

  render() {
    if (this.state.hasError) {
      return (
        <div style={{ display: "grid", placeItems: "center", minHeight: "100vh", padding: 32 }}>
          <Card style={{ maxWidth: 480 }}>
            <div style={{ display: "flex", flexDirection: "column", gap: 16, alignItems: "center" }}>
              <IconAlertTriangle size="extra-large" style={{ color: "var(--semi-color-danger)" }} />
              <Title heading={4}>Something went wrong</Title>
              <Text type="tertiary">{this.state.error?.message}</Text>
              <Button onClick={() => this.setState({ hasError: false, error: null })}>
                Try Again
              </Button>
            </div>
          </Card>
        </div>
      )
    }
    return this.props.children
  }
}
