/**
 * The exact material `Input` wears, pulled out so composite controls — money
 * plus currency, phone plus dial code, discount plus mode — can wear it as one
 * shell instead of stacking two bordered boxes next to each other.
 */
export const CONTROL_SHELL = [
  'flex h-control w-full min-w-0 items-stretch overflow-hidden rounded-md',
  'border border-[var(--nx-hairline)] bg-[var(--nx-fill)]',
  'transition-[background,border-color,box-shadow] duration-[250ms] ease-fluid',
  'hover:border-[var(--nx-hairline-strong)]',
  'focus-within:border-ring focus-within:bg-[var(--nx-surface)] focus-within:shadow-[0_0_0_4px_var(--nx-tint-fill)]',
  'has-[:disabled]:pointer-events-none has-[:disabled]:opacity-45',
  // Invalid on the shell itself (a trigger button) or on the control inside it
  // (an input) — both have to paint the same edge.
  'aria-invalid:border-destructive aria-invalid:focus-within:shadow-[0_0_0_4px_var(--nx-st-red-bg)]',
  'has-[[aria-invalid=true]]:border-destructive',
  'has-[[aria-invalid=true]]:focus-within:shadow-[0_0_0_4px_var(--nx-st-red-bg)]',
].join(' ')

/** A bare input that fills a `CONTROL_SHELL` slot: the shell paints the chrome. */
export const CONTROL_INPUT = [
  'h-full w-full min-w-0 border-0 bg-transparent px-[13px] text-[13.5px] text-foreground outline-none',
  'placeholder:text-[var(--nx-label-3)] disabled:cursor-not-allowed',
].join(' ')

/** Non-interactive text welded to the shell's edge — a unit, a symbol, a code. */
export const CONTROL_ADORNMENT = [
  'flex shrink-0 items-center justify-center gap-1 px-[11px]',
  'text-[12.5px] font-[550] text-muted-foreground select-none',
].join(' ')

/** Divider between two halves of one shell. */
export const CONTROL_DIVIDER = 'border-l border-[var(--nx-hairline)]'

/** Floating list surface for select · combobox · date popups. */
export const POPUP = [
  'nx-overlay z-50 rounded-lg p-1.5 text-foreground',
  'origin-[var(--transform-origin)] transition-[opacity,transform] duration-[150ms] ease-fluid',
  'data-starting-style:scale-[0.97] data-starting-style:opacity-0',
  'data-ending-style:scale-[0.97] data-ending-style:opacity-0',
].join(' ')

/** One row inside a `POPUP` list. Highlight is the tint fill, never a hue swap. */
export const POPUP_ITEM = [
  'grid cursor-default grid-cols-[16px_minmax(0,1fr)] items-center gap-2.5 rounded-sm px-2.5 py-2',
  'text-[13.5px] leading-[1.3] outline-none select-none',
  'transition-colors duration-[120ms] ease-fluid',
  'data-highlighted:bg-[var(--nx-fill-hover)] data-selected:text-accent-foreground',
  'data-disabled:pointer-events-none data-disabled:opacity-45',
].join(' ')

export const POPUP_GROUP_LABEL =
  'px-2.5 pt-2 pb-1 text-[11px] font-[600] tracking-[0.05em] text-[var(--nx-label-3)] uppercase'

export const POPUP_EMPTY = 'px-2.5 py-3 text-[12.5px] text-muted-foreground'
