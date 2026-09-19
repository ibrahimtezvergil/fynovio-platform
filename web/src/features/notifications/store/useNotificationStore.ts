import { create } from 'zustand'
import { SEED_NOTIFICATIONS } from '@/features/notifications/data/notifications'
import type { AppNotification } from '@/features/notifications/types'

interface NotificationState {
  items: AppNotification[]
  markRead: (id: string) => void
  markUnread: (id: string) => void
  markAllRead: () => void
  dismiss: (id: string) => void
  clearAll: () => void
  /** Puts the seed back — the demo page needs a way out of an empty center. */
  reset: () => void
}

/**
 * Notifications outlive the popover that shows them: the badge on the topbar
 * reads the same list whether the center is open or not, and a row marked read
 * in the drawer is read in the popover too. That is what earns them a store
 * rather than component state.
 */
export const useNotificationStore = create<NotificationState>()((set) => ({
  items: SEED_NOTIFICATIONS,

  markRead: (id) =>
    set((state) => ({
      items: state.items.map((item) => (item.id === id ? { ...item, read: true } : item)),
    })),

  markUnread: (id) =>
    set((state) => ({
      items: state.items.map((item) => (item.id === id ? { ...item, read: false } : item)),
    })),

  markAllRead: () =>
    set((state) => ({ items: state.items.map((item) => ({ ...item, read: true })) })),

  dismiss: (id) => set((state) => ({ items: state.items.filter((item) => item.id !== id) })),

  clearAll: () => set({ items: [] }),

  reset: () => set({ items: SEED_NOTIFICATIONS }),
}))

/**
 * Derived in a selector rather than stored: an unread count kept as its own
 * field is a second source of truth, and the two drift the first time a row is
 * dismissed while unread.
 */
export function useUnreadCount(): number {
  return useNotificationStore((state) => state.items.reduce((n, i) => n + (i.read ? 0 : 1), 0))
}
