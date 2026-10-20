import { Component } from "react"
import { Button, Empty } from "@douyinfe/semi-ui"
import { TriangleAlert } from "lucide-react"

interface Props {
  children: React.ReactNode
}

interface State {
  hasError: boolean
  error: Error | null
}

export class ErrorBoundary extends Component<Readonly<Props>, State> {
  constructor(props: Readonly<Props>) {
    super(props)
    this.state = { hasError: false, error: null }
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error }
  }

  render() {
    if (this.state.hasError) {
      return (
        <Empty
          style={{ padding: 48 }}
          image={<TriangleAlert size={48} />}
          title="Something went wrong"
          description={this.state.error?.message}
        >
          <Button onClick={() => this.setState({ hasError: false, error: null })}>Try Again</Button>
        </Empty>
      )
    }
    return this.props.children
  }
}
