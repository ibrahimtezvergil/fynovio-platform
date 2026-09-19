# 11 · Accessibility

**Baseline: WCAG 2.2 Level AA.** Two exceptions are documented below; there are
no others, and adding one requires the same treatment — named, bounded,
mitigated, written down.

Accessibility is not a review stage. It is part of the component
(see [04 Component Standards](./04-component-standards.md)).

---

## Colour and contrast

| Surface | Requirement | Status |
| --- | --- | --- |
| Body text on ground | ≥ 4.5:1 | ✅ label ramp clears it in both themes |
| Secondary text (`--nx-label-2`) | ≥ 4.5:1 | ✅ |
| `--nx-label-3` | ≥ 4.5:1 | ⚠️ **placeholders and disabled hints only** — never body copy |
| Status pill fill/text pairs | ≥ 4.5:1 | ✅ all seven tones clear **6:1** in both themes |
| Focus ring vs. adjacent | ≥ 3:1 | ✅ `--nx-tint` at 2–3px |
| Hairlines / dividers | n/a — decorative | Never the sole carrier of structure |

### Documented exception 1 — the accent gradient

White on `--nx-accent-grad`'s top stop measures **~3.4:1**, below AA for normal
text. This is the artboard's own trade-off and it is kept.

**Known accessibility gap:** 15px text or a weight of 590 alone does not qualify
as WCAG large text. The large-text threshold is **18pt (24 CSS px) normal or
14pt (approximately 18.67 CSS px) bold**, for which the minimum contrast is 3:1;
normal-sized text requires 4.5:1. See the
[W3C contrast guidance](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html).
The documented `Button` `default` styling of `13.5px / font-590` does not meet the
large-text threshold and therefore fails AA at the reported contrast. This remains
an unresolved design-system issue; documenting the exception does not establish
WCAG conformance. Remediation must provide sufficient contrast or qualifying large
text through the shared design system.

Never use the gradient behind body copy, a paragraph, or a data value.

### Colour is never the only carrier

WCAG 1.4.1, and Principle 2. `StatusBadge` enforces it structurally: it cannot
render a fill without a label. Applies equally to charts, deltas, validation
and Kanban columns.

---

## Focus

The base layer gives everything a visible focus ring:

```css
:focus-visible {
  outline: 2px solid var(--nx-tint);
  outline-offset: 2px;
  border-radius: var(--nx-r-ctl);
}
```

Components refine it (`focus-visible:ring-3 ring-ring/40 border-ring`). Nothing
suppresses it. `outline: none` without a replacement ring is never acceptable.

- Focus order follows visual order. **No positive `tabIndex`.**
- Overlays move focus in on open and return it to the trigger on close.
- A route change moves focus to the page heading — not to the top of the DOM.
- Focus is never trapped outside a modal context.

---

## Pointer targets

`--nx-hit-min` is **44px**. Any interactive element that renders smaller widens
its hit box with the `hit-min` utility:

```css
@utility hit-min {  /* a 44px pseudo-element, without changing layout height */ }
```

The collapsed rail is 72px precisely so a 44px square clears the 14px gutter on
each side.

### Documented exception 2 — the 32px compact row

A compact row is 32px, below the 44px target, and `hit-min` **cannot** rescue
it: at that pitch adjacent rows' widened boxes would overlap, which is worse
than a small target.

**Bounded by:** compact is opt-in, persisted (a deliberate one-time choice),
and one click from reversible. Comfortable remains the default, and nothing in
the shell — topbar, sidebar, nav, KPI cards — ever goes compact.

---

## Keyboard

Everything doable with a mouse is doable with a keyboard. Today:

| Chord | Action |
| --- | --- |
| ⌘/Ctrl + K | Focus search (becoming the command palette) |
| ⌘/Ctrl + `[` (`BracketLeft`) | Toggle the sidebar |
| Escape | Close the topmost overlay |
| Tab / Shift+Tab | Move focus in visual order |

### The layout rule

**Match on `event.code`, never on the character.**

`[` is AltGr+8 on a Turkish layout, and on Windows AltGr *is* Ctrl+Alt — a
character match would fire every time someone typed a bracket. The key cap
shown in a hint comes from `navigator.keyboard.getLayoutMap()`, so it reads
⌘Ğ on a Turkish Q board and ⌘[ on a US one.

This is not a Turkish special case. It is how every shortcut in this product is
written.

### Shortcut standards

- A shortcut never shadows a browser or OS chord, and never a screen reader's.
- Every shortcut is discoverable: shown in a tooltip, a menu, or the (planned)
  `?` cheat sheet. An undocumented shortcut does not exist.
- Single-key shortcuts (J/K/N/E) only fire outside text inputs.
- Announce via `aria-keyshortcuts` on the element the chord targets — the
  topbar search already does.

---

## Semantics and ARIA

The first rule of ARIA is not to use ARIA. Use the element.

| Need | Use |
| --- | --- |
| A button | `<button>` |
| A link | `<a>` / `<Link>` |
| A table | `<table>` with `<th scope="col">` |
| A form field | `<label>` bound to the control |
| A landmark | `<header>`, `<nav>`, `<main>`, `<aside>` |

Then, where the element is not enough:

- **`aria-hidden`** on every decorative icon and every skeleton block.
- **`aria-label`** on any control whose only content is an icon.
- **`aria-live="polite"`** for save state, counts, filtered-result counts,
  calendar titles, and Kanban move announcements. Never `assertive` for
  anything that is not an error the user must handle now.
- **`aria-invalid` + `aria-describedby`** on every field with an error.
- **`aria-expanded`** on every disclosure trigger — the button styles already
  key off it.
- **`role="status"`** on the *region* wrapping skeletons, not on each block.

### Roles we deliberately do not use

`Toolbar` does **not** carry `role="toolbar"`. That role owes the user
arrow-key roving focus; a tab stop per control is the honest behaviour for a
strip holding a search box and some menus. Claiming a role you do not implement
is worse than claiming none.

---

## Motion

`prefers-reduced-motion: reduce` disables non-essential animation globally,
including transforms and opacity transitions; `.nx-skeleton` also disables its
sweep explicitly. Any new animation ships with its reduced-motion branch.

---

## Screen-reader expectations by surface

| Surface | Must announce |
| --- | --- |
| Data grid | Column headers per cell; selected count; sort state |
| Kanban | The card's new column and position after every move |
| Form | The error count on failed submit; the first invalid field on focus |
| Overlay | Its title and description on open |
| Async region | "Yükleniyor" once — not one announcement per skeleton block |
| Toast | Its message, politely, without stealing focus |

---

## Verification

Oxlint's native `jsx-a11y` rules enforce core semantic, label, ARIA, alt-text,
focus and tabindex checks in the lint gate. Until an automated browser suite is
added, every UI change is also checked manually:

1. **Tab through it.** Every control reachable, ring always visible, order
   matches the layout.
2. **Escape and Enter** behave.
3. **Zoom to 200%.** Nothing clipped, nothing horizontally scrolling at the
   page level.
4. **Both themes.** Contrast is not symmetric — the light glass surfaces are
   the harder case.
5. **Greyscale it.** Any status still readable? If not, Principle 2 is broken.
6. **VoiceOver** (⌘F5) over the changed region for anything with live state.

**Gap worth closing:** browser-level `axe-core` checks. The native lint rules
cover static JSX only.
