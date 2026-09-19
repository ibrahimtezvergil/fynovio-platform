import { create } from 'zustand'

interface CommandPaletteState {
  open: boolean
  setOpen: (open: boolean) => void
}

/**
 * Whether the shared command palette is open. One underlying store, multiple
 * entry points (the Topbar's search button, Home's `CommandBar`, the global
 * ⌘K shortcut) — so there is exactly one `CommandPalette` mounted, not one
 * per entry point with its own local `open` state.
 */
export const useCommandPaletteStore = create<CommandPaletteState>((set) => ({
  open: false,
  setOpen: (open) => set({ open }),
}))
