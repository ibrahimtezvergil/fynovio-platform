import { useEffect } from 'react'
import { isRouteErrorResponse, useNavigate, useRouteError } from 'react-router-dom'
import { ErrorFallback } from '@/components/common/ErrorFallback'
import { track } from '@/lib/telemetry'
import { paths } from '@/routes/paths'

/**
 * `errorElement` for the data router (`routes/index.tsx`). Catches render,
 * loader and action errors anywhere in the route tree that don't have a more
 * specific `errorElement` of their own — the route-tree half of Layer 14,
 * paired with the plain `ErrorBoundary` mounted above the router in `main.tsx`.
 */
export function RouteErrorBoundary() {
  const error = useRouteError()
  const navigate = useNavigate()

  useEffect(() => {
    const message =
      error instanceof Error
        ? error.message
        : isRouteErrorResponse(error)
          ? error.statusText
          : String(error)
    track('route_error_boundary_caught', { message })
  }, [error])

  if (isRouteErrorResponse(error)) {
    return (
      <ErrorFallback
        title={error.status === 404 ? 'Sayfa bulunamadı' : `Hata ${error.status}`}
        description={
          error.status === 404
            ? 'Aradığınız adres taşınmış veya hiç var olmamış olabilir.'
            : (error.statusText ?? 'Bu sayfa yüklenemedi.')
        }
        onRetry={() => navigate(paths.dashboard, { replace: true })}
        retryLabel="Panele dön"
      />
    )
  }

  const detail = error instanceof Error ? (error.stack ?? error.message) : String(error)

  return (
    <ErrorFallback
      detail={detail}
      onRetry={() => navigate(0)}
      retryLabel="Yeniden dene"
    />
  )
}
