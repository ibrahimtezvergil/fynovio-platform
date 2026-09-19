# 08 · Overlays

Six layers that all "open on top". What tells them apart is **what they take
away from the user**: the page behind, the escape routes, or nothing at all.

Rendered in-app at `/demo/overlays` and `/demo/drawers`.

---

## Choosing

| Layer | Use for | Avoid when | Modal |
| --- | --- | --- | --- |
| **Dialog** | A short form, a preview, a confirmation — when the flow needs an answer to continue. | The content is page-sized. Open a route instead. | Yes |
| **Alert Dialog** | One irreversible decision: delete, reset, send. | There is a form field inside. If the question does not fit in one sentence, use a Dialog. | Yes — outside click does **not** dismiss |
| **Sheet** | Filters, detail, side editing — when the link to the list behind must survive. | You need a decision. A side panel does not look unavoidable. | Yes |
| **Drawer** | Touch: drag-to-open, with detents for a half-open state. | Wide, mouse-only screens — Sheet is right there. | Yes (`modal={false}` for no) |
| **Popover** | A small edit bound to its trigger, an explanation, a mini form. | A critical decision — an outside click closes it silently. | No |
| **Tooltip** | The name of an icon button, an abbreviation's expansion. One line, not clickable. | Any information that must stand alone — it never opens on touch. | No |

Two heuristics that resolve most cases:

1. **Does the user need what is behind it?** Yes → Sheet or Popover. No → Dialog.
2. **Can the decision be undone?** Yes → do it and offer undo, no overlay at
   all. No → Alert Dialog.

---

## Material and stacking

- Overlays use `.nx-overlay`: deeper blur, brighter edge, `--nx-elev-over`.
- Menus and sheets sit on `--nx-glass-over` (96% opaque) — they must stay
  readable over arbitrary page content, which plain glass does not guarantee.
- **Every portalled layer is `z-50`.** A portal escapes the stacking context,
  so they do not fight. Wanting `z-60` means the element should have been
  portalled.
- The scrim is the primitive's default. Do not restyle it per-instance.

---

## Focus and dismissal

Handled by the shadcn/Base UI primitives — **preserve their behaviour**. What
that behaviour is, so you notice when it breaks:

| Concern | Expected |
| --- | --- |
| Focus on open | Moves into the overlay, to the first meaningful control — not the close button |
| Focus trap | Modal layers trap; Popover and Tooltip do not |
| Focus on close | Returns to the trigger |
| Escape | Closes everything except Alert Dialog's outside click |
| Outside click | Closes Dialog, Sheet, Popover. Does **not** close Alert Dialog |
| Scroll | Locked behind modal layers, free behind Popover |
| Labelling | `aria-labelledby` on the title, `aria-describedby` on the description — every modal has both |

If a form inside an overlay is dirty, Escape and outside-click must warn before
discarding.

---

## Stacked overlays

Allowed, but capped at **two**. A third layer means the information
architecture is wrong — the second layer's content wanted a route.

When stacking, the layer beneath stays visible and inert; it does not re-render
or lose scroll position. `StackedSection` in `demo-drawers` is the reference.

---

## The rail flyout

A collapsed-sidebar `NavParent` is a `Popover`, `openOnHover`, 220ms in /
140ms out. Two constraints drive that:

- The rail's scroll container **clips**. Portalled content is the only kind
  that escapes it — this is why it cannot be an inline expansion.
- The asymmetric timing is deliberate: slow enough in to survive a passing
  cursor, fast enough out that the rail does not feel sticky.

---

## Toasts are not overlays

`sonner`, bottom-right, non-modal, never focus-stealing. They belong to
[09 States & Feedback](./09-states-and-feedback.md) — a toast that blocks the
page is a dialog wearing the wrong clothes.

---

## Checklist

- [ ] The layer matches the table above, not habit
- [ ] Modal layers have both a title and a description, wired to ARIA
- [ ] Focus enters on open and returns to the trigger on close
- [ ] Escape and outside click behave per the table (Alert Dialog excepted)
- [ ] A dirty form warns before discard
- [ ] Stack depth ≤ 2
- [ ] `z-50`, portalled, not a local z-index
- [ ] Verified in both themes — glass over a light ground is the harder one
