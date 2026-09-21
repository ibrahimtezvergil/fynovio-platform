import { Dialog, DialogContent } from '@/components/ui/dialog'
import { Drawer, DrawerContent } from '@/components/ui/drawer'
import { OverlayCloseContext, useOverlayStore } from '@/lib/overlay'

/** The only renderer for imperative overlays; the primitives retain focus and escape behaviour. */
export function OverlayHost() {
  const entries = useOverlayStore((state) => state.entries)
  const close = useOverlayStore((state) => state.close)
  const topId = entries.at(-1)?.id

  return entries.map((entry) => {
    const dismiss = (open: boolean) => {
      if (!open && entry.id === topId) close(entry.id)
    }
    const content = <OverlayCloseContext.Provider value={() => close(entry.id)}>{entry.content}</OverlayCloseContext.Provider>
    if (entry.kind === 'drawer') {
      return <Drawer key={entry.id} open swipeDirection={entry.drawerSwipeDirection ?? 'right'} onOpenChange={dismiss}><DrawerContent className={entry.className}>{content}</DrawerContent></Drawer>
    }
    return <Dialog key={entry.id} open onOpenChange={dismiss}><DialogContent className={entry.className}>{content}</DialogContent></Dialog>
  })
}
