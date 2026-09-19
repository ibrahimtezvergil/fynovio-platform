# 01 · Principles

Ten rules. Each one exists because it settled a real argument in this codebase.
When a review stalls, the principle wins over taste.

---

## 1. One accent, one meaning

`--nx-accent` (`#6355C7` light / `#A99EF0` dark) is the only brand colour. It
carries text, icons and tints alike — there is no "accent for text" and
"accent for fill" split. `--nx-accent-grad` fills exactly three things: the
primary button, the brand mark, and meters.

**In practice:** one filled action per screen. If a screen has two primary
buttons, one of them is wrong.

**Violation looks like:** a second brand colour introduced "for variety", or a
secondary action promoted to the gradient because it felt important.

---

## 2. Colour is never the only carrier

Status, stage, delta, severity — none of them may be encoded by hue alone. A
dot, an icon or a label always rides along. `StatusBadge` enforces this
structurally: the component cannot render a fill without a label.

**Why:** greyscale, colour-vision deficiency, printed pages, and a screen
reader all need the same information (WCAG 1.4.1).

**In practice:** `<StageBadge stage="quoted" />` — never a coloured cell.

---

## 3. A tone is a hue, not a meaning

The `.nx-pill` ladder gives seven tones. What "amber" *means* is decided once
per domain, in that domain's registry (`STAGE_META`, the invoice registry in
`demo-badges/data/registries.ts`), so amber never says "waiting" on one screen
and "shipped" on the next.

**In practice:** new status set → new `StatusRegistry<T>`, not new colours.

---

## 4. Pick from the ladder; never invent a value

Radii, elevation steps, tones, control heights, gaps — each is a short fixed
set. A raw `px` in a component is a claim that the ladder is wrong; make that
claim in `tokens.css`, in the open, or don't make it.

**In practice:** `rounded-md`, not `rounded-[14px]`. `h-control`, not `h-[38px]`.

---

## 5. Tokens flow one way

`tokens.css` defines → `index.css` maps → components consume. Components read
shadcn semantic variables (`--primary`, `--muted`, `--border`) or `.nx-*`
classes. **A component never reads a raw `--nx-*` token** except where
`index.css` has no mapping for it yet — and that is a gap to close, not a
pattern to copy.

**Why:** retinting the entire product must stay a single-file edit.

---

## 6. State that outlives the interaction lives in the URL or a store — not in a component

Sorting, paging, the active filter, the current view: the URL owns them
(`useTableSearchParams`). Theme, density, sidebar width: a persisted zustand
slice owns them. Form fields: react-hook-form owns them, and never a store.

**Test:** if a refresh or a shared link should reproduce what the user sees,
the URL owns it. If it should survive a new session, the store owns it. If
neither, local state is correct.

---

## 7. Density is a token flip, never a prop

Compact mode repaints custom properties on `<html data-density>`; `.nx-dense`
rebinds control and row heights from them. Anything that resolves its size
through those tokens follows automatically.

**Violation looks like:** a `density` prop threaded through a table into a
cell renderer. That is a rewrite of the whole tree on every flip, and it will
be inconsistent within a month.

---

## 8. Every state is a designed state

Empty, loading, error, partial, and success are five designs, not one design
plus four accidents. A screen that only looks right with data is unfinished.

**In practice:** skeletons that hold the real layout open, an `EmptyState` that
says what happened *and* what to do next, an error that offers a retry.

---

## 9. Reversibility beats confirmation

Prefer an action the user can undo over a dialog that asks whether they meant
it. A confirmation dialog is for the genuinely irreversible; everything else
gets an optimistic result and an undo affordance.

**Why:** confirmations are click-through noise within a week. Undo is not.

---

## 10. Trade-offs are documented, not hidden

Two known compromises live in this product on purpose:

- White on the accent gradient measures **~3.4:1** — below AA. Mitigated by
  keeping filled-accent labels at 15px+ or bold. See [11 Accessibility](./11-accessibility.md).
- A **32px compact row** is below the 44px pointer target, and `hit-min`
  cannot rescue it because adjacent rows' widened boxes would overlap. That is
  compact's explicit trade, which is why it is opt-in, persisted, and one
  click from reversible.

**The rule:** a compromise you can name and bound is engineering. A compromise
nobody wrote down is a defect that has not been found yet.

---

## The umbrella

> **Don't make the user navigate, remember, repeat or fear.**

Lifted from the UX backlog and adopted as the product's north star. Every
pattern in this folder is a way of moving one of those four costs off the user
and onto the system.
