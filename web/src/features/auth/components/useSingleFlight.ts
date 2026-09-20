import { useCallback, useRef } from 'react'

/**
 * Runs `task` unless a previous run is still going. A ref, not state: two
 * clicks in the same tick both see the un-rendered `isPending`, but only one
 * sees the ref flip.
 */
export function useSingleFlight() {
  const running = useRef(false)
  return useCallback(async (task: () => Promise<void>) => {
    if (running.current) return
    running.current = true
    try {
      await task()
    } finally {
      running.current = false
    }
  }, [])
}
