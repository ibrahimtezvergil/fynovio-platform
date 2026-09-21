import { createContext, useContext } from 'react'

/** Closes the overlay entry the calling content is rendered in; `OverlayHost` provides it per entry. */
export const OverlayCloseContext = createContext<(() => void) | null>(null)

/**
 * Lets content opened through `openDialog()`/`openDrawer()` dismiss its own overlay (e.g. a form after a successful
 * save) through the same path as Escape and the backdrop, so focus is restored and the opener's promise resolves.
 */
export function useOverlayClose(): () => void {
  const close = useContext(OverlayCloseContext)
  if (!close) throw new Error('useOverlayClose must be used inside an overlay opened with openDialog() or openDrawer().')
  return close
}
