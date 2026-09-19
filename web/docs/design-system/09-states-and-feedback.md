# 09 · States & Feedback

Every screen has five designs, not one. A screen that only looks right with a
full, successful dataset is unfinished (Principle 8).

Rendered in-app at `/demo/states` and `/demo/notifications`.

---

## The five states

| State | Rule |
| --- | --- |
| **Loading** | A skeleton that holds the real layout open. Never a spinner where content will be. |
| **Empty** | Says what happened, why it is empty, and what to do next — all three. |
| **Partial** | Data present, something failed or is still arriving. Keep what loaded; mark what did not. |
| **Error** | What failed, in the user's terms, plus a retry. Never a raw status code. |
| **Success** | The default. Also the state most likely to be the only one built. |

**No-data and no-results are different states.** "You have no deals yet" invites
creating one; "No deals match this filter" invites clearing the filter. Shipping
one message for both is the most common failure in this area.

---

## Loading

### Skeleton vs. spinner

| Use a skeleton | Use a spinner |
| --- | --- |
| The layout is known ahead of time | The wait is inside a control (a button's action) |
| Content is arriving for the first time | The region is small and its shape is unknown |
| A table, a card, a list, a detail panel | An inline save, a submit |

**Never** blank a populated view to re-load it. A refetch keeps the current
data on screen with a subtle inline indicator.

### Accessibility of skeletons

Every skeleton block is `aria-hidden`; the **region wrapping them** carries
`role="status"` + `aria-live="polite"` + `aria-busy="true"`. A screen reader
hears "Yükleniyor" once instead of counting rectangles.

```tsx
<div role="status" aria-busy="true" aria-live="polite">
  <span className="nx-skeleton" aria-hidden />
  …
</div>
```

The `.nx-skeleton` sweep is decoration — it stops under
`prefers-reduced-motion: reduce`, and the layout it holds open is unchanged.
The information is in the shape, never in the motion.

---

## Empty states

`EmptyState` takes: an `icon` (compact, inside a card or table) **or** an
`illustration` (full artwork, when the state owns the viewport), a `tone`, a
`title`, a `description`, and an `action`.

Rules:

- **Tone colours the artwork only.** Copy stays on the label ramp. A red empty
  state is not a red paragraph.
- **The action is the one thing that ends the emptiness** — "Yeni fırsat", not
  "Yardım".
- Title states the fact; description gives the reason or the next step. Do not
  put both in the title.
- Full illustrations cap at 190px wide. Beyond that the emptiness starts
  looking like a feature.

---

## Status vocabulary

The governance rule, restated because it is the one most often broken:

> **A tone is a hue, not a meaning.**

Seven tones exist. What each *means* is declared once per domain in a
`StatusRegistry<T>`:

```ts
export const STAGE_META: StatusRegistry<Stage> = {
  new:       { label: 'Yeni',                tone: 'blue'   },
  contacted: { label: 'İletişim Kuruldu',    tone: 'teal'   },
  quoted:    { label: 'Teklif Gönderildi',   tone: 'amber'  },
  meeting:   { label: 'Görüşme',             tone: 'purple' },
  ready:     { label: 'Rezervasyon Hazır',   tone: 'green'  },
  onhold:    { label: 'Beklemede',           tone: 'gray'   },
}
```

The registry is **exported**, because a stage also has to be drawn without a
pill — a Kanban column header, a legend, a chart key — and those must read the
same label and hue rather than inventing their own.

- `StatusBadge` renders label + dot (or icon). It structurally cannot render a
  fill without a label, which is how Principle 2 is enforced rather than
  merely stated.
- Every fill/text pair clears **6:1** in both themes.
- `size="sm"` for dense surfaces: Kanban cards, nested lists.
- A tone is never reused for two meanings **within one domain**. Across
  domains, reuse is expected — that is what makes it a ladder.

`--nx-pos` / `--nx-neg` are for deltas (up/down), not status.

---

## Toasts

`sonner`, bottom-right, non-modal, never focus-stealing.

| Use a toast | Don't |
| --- | --- |
| Confirming something that already happened | Asking a question — that is a dialog |
| Carrying an **Undo** action | Reporting an error the user must fix in a form — that is inline |
| A background job finishing | Anything the user must read to continue |

Rules:

- **One line, plus an optional action.** A toast that needs a paragraph needed
  a different surface.
- **Success toasts are optional; error toasts are not.** If the UI already
  shows the result, a success toast is noise.
- Duration: ~4s default, ~8s when it carries an Undo, indefinite for an error
  with an action.
- A toast never carries the *only* copy of information. It disappears.

### The undo pattern

The house pattern for reversible mutations (Principle 9, backlog item 7):

1. Apply optimistically to the React Query cache.
2. Toast: what happened + **Geri al**.
3. On undo, restore the cache and cancel the pending mutation.
4. Only after the window closes is the change final.

Build every new mutation against this, so that the durable server-side half
(soft delete, reversal endpoints) drops in without touching call sites.

---

## Notification center

Persistent, addressable feedback — the opposite end from a toast.

- Unread state lives in `useNotificationStore` (persisted).
- The bell shows a count; the count is announced, not just drawn.
- A notification is not a toast that was missed. Something worth keeping goes
  to the center; something worth interrupting for goes to a toast; a great many
  things deserve neither.

---

## Inline feedback

- **Save state** — `idle → saving → saved`, in an `aria-live="polite"` region.
  `SaveBar` is the reference.
- **Field errors** — below the control, replacing the helper text, referenced
  by `aria-describedby`, with `aria-invalid` on the control.
- **Form-level errors** — above the submit, focused on failed submit.
- **Live counts** — a running total (the instalment editor's remaining amount)
  is announced politely, not on every keystroke.

---

## Checklist

- [ ] All five states designed, not just the successful one
- [ ] Empty and no-results are distinct messages with distinct actions
- [ ] Skeleton matches the real layout; blocks `aria-hidden`, region `role="status"`
- [ ] Reduced-motion branch present for any new animation
- [ ] New status set added as a `StatusRegistry`, using existing tones
- [ ] Toast carries an action or confirms — it never asks
- [ ] Reversible mutation ships with undo, not a confirmation dialog
