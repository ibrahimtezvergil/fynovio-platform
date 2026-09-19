import type { ReactNode } from 'react'
import { create } from 'zustand'

export type OverlayKind = 'dialog' | 'drawer'
export type DrawerSwipeDirection = 'up' | 'right' | 'down' | 'left'

export interface OverlayConfig<Result = void> {
  content: ReactNode
  className?: string
  /** Lets a global trigger find and toggle its own existing overlay. */
  overlayKey?: string
  /** Drawers declare the edge they belong to instead of inheriting the primitive's bottom default. */
  drawerSwipeDirection?: DrawerSwipeDirection
  result?: Result
}

interface OverlayEntry extends OverlayConfig<unknown> {
  id: string
  kind: OverlayKind
  trigger: HTMLElement | null
  resolve: (result: unknown) => void
}

interface OverlayState {
  entries: OverlayEntry[]
  open: (kind: OverlayKind, config: OverlayConfig<unknown>) => Promise<unknown>
  close: (id: string) => void
}

const MAX_STACK_DEPTH = 2

export const useOverlayStore = create<OverlayState>((set, get) => ({
  entries: [],
  open: (kind, config) => new Promise((resolve) => {
    if (get().entries.length >= MAX_STACK_DEPTH) {
      resolve(undefined)
      return
    }
    const id = crypto.randomUUID()
    const trigger = document.activeElement instanceof HTMLElement ? document.activeElement : null
    set((state) => ({ entries: [...state.entries, { ...config, id, kind, trigger, resolve }] }))
  }),
  close: (id) => {
    const entry = get().entries.find((candidate) => candidate.id === id)
    if (!entry) return
    set((state) => ({ entries: state.entries.filter((candidate) => candidate.id !== id) }))
    entry.resolve(entry.result)
    requestAnimationFrame(() => entry.trigger?.focus())
  },
}))

export function openDialog<Result = void>(config: OverlayConfig<Result>): Promise<Result | undefined> {
  return useOverlayStore.getState().open('dialog', config) as Promise<Result | undefined>
}

export function openDrawer<Result = void>(config: OverlayConfig<Result>): Promise<Result | undefined> {
  return useOverlayStore.getState().open('drawer', config) as Promise<Result | undefined>
}
