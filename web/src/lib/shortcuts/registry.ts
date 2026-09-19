export interface Shortcut {
  id: string
  priority?: number
  matches: (event: KeyboardEvent) => boolean
  run: () => void
}

const shortcuts = new Map<string, Shortcut>()
let listening = false

function isTextInput(target: EventTarget | null) {
  const element = target as HTMLElement | null
  return Boolean(element?.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(element?.tagName ?? ''))
}

function onKeyDown(event: KeyboardEvent) {
  if (isTextInput(event.target)) return
  const shortcut = [...shortcuts.values()]
    .sort((a, b) => (b.priority ?? 0) - (a.priority ?? 0))
    .find((candidate) => candidate.matches(event))
  if (!shortcut) return
  event.preventDefault()
  shortcut.run()
}

/** One global listener, ordered registrations, and input-safe default scope. */
export function registerShortcut(shortcut: Shortcut): () => void {
  shortcuts.set(shortcut.id, shortcut)
  if (!listening) {
    window.addEventListener('keydown', onKeyDown)
    listening = true
  }
  return () => {
    shortcuts.delete(shortcut.id)
    if (shortcuts.size === 0 && listening) {
      window.removeEventListener('keydown', onKeyDown)
      listening = false
    }
  }
}
