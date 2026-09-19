import { Component, type ErrorInfo, type ReactNode } from 'react'
import { ErrorFallback } from '@/components/common/ErrorFallback'
import { track } from '@/lib/telemetry'

interface ErrorBoundaryProps {
  children: ReactNode
  fallback?: (error: Error, reset: () => void) => ReactNode
}

interface ErrorBoundaryState {
  error: Error | null
}

/**
 * Root-level catch net (Layer 14 — Error Architecture). Without this, one
 * uncaught render error blanks the entire application with no recovery.
 * `RouteErrorBoundary` is the router-aware sibling used as `errorElement` in
 * `routes/index.tsx`; this one sits above the router in `main.tsx` and
 * catches anything thrown before or outside route rendering.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('[ErrorBoundary]', error, info.componentStack)
    track('error_boundary_caught', { message: error.message, stack: info.componentStack })
  }

  reset = () => this.setState({ error: null })

  render() {
    const { error } = this.state
    if (!error) return this.props.children

    if (this.props.fallback) return this.props.fallback(error, this.reset)

    return (
      <ErrorFallback
        detail={error.stack ?? error.message}
        onRetry={() => window.location.reload()}
        retryLabel="Sayfayı yenile"
      />
    )
  }
}
