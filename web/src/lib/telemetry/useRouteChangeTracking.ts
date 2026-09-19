import { useEffect, useRef } from 'react'
import { useLocation } from 'react-router-dom'
import { track } from '@/lib/telemetry'

export function useRouteChangeTracking() {
  const location = useLocation()
  const previousChangeAt = useRef<number | null>(null)

  useEffect(() => {
    const changedAt = performance.now()
    track('route_change', {
      path: `${location.pathname}${location.search}${location.hash}`,
      durationMs: Math.round(changedAt - (previousChangeAt.current ?? changedAt)),
    })
    previousChangeAt.current = changedAt
  }, [location.hash, location.pathname, location.search])
}
