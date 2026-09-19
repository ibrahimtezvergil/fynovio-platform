import { useEffect, useState } from 'react'

/**
 * Trails `value` by `delay` ms. Used to keep a search box responsive while the
 * URL — and, in server mode, the network — only sees settled input.
 */
export function useDebouncedValue<T>(value: T, delay = 250): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    if (Object.is(value, debounced)) return
    const timer = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(timer)
  }, [value, delay, debounced])

  return debounced
}
