# 13 · UX Definition of Done

A UI change is done when it passes this. Not when it renders.

---

## Verification commands

| Change | Command |
| --- | --- |
| Small / local | `npm run lint -- src/path/to/file.tsx` |
| Behaviour or types | `npm run build` (`tsc -b && vite build`) |
| Cross-cutting | `npm run build` + full `npm run lint` |
| Anything visual | The manual matrix below, via `npm run dev` |

**There is no test suite and no CI.** `npm run build` is the closest local
equivalent to a release check, and a green build is **not** behavioural
coverage. Say in the pull request which smoke checks you actually ran.

Note that oxlint's architecture restrictions include warnings — a zero exit code
can still carry findings. Read them; do not suppress them.

---

## The manual matrix

Anything visual is checked across all four, not one:

| Axis | States |
| --- | --- |
| **Theme** | Light · Dark *(and `system` if theme resolution changed)* |
| **Density** | Comfortable · Compact |
| **Sidebar** | Expanded · Collapsed rail |
| **Width** | ~1440px · ~1024px · ~640px |

Contrast is not symmetric between themes — light glass over a pale ground is
the harder case, and it is the one most often skipped.

---

## The checklist

### Tokens and styling

- [ ] No raw `px` for a radius, control height or row height — the ladder covers it
- [ ] No hard-coded colour; semantic variables or `.nx-*` classes only
- [ ] No component reads a raw `--nx-*` token (or, if it must, the mapping was added to `index.css`)
- [ ] Any new token defined in **both** `:root` and `.dark`
- [ ] Elevation is correct: shell = `.nx-material` (no cast), floating = `.nx-card` / `.nx-overlay`
- [ ] One filled accent action on the screen, maximum

### Structure

- [ ] The component sits in the right layer (`ui` / `common` / feature)
- [ ] No feature imports another feature
- [ ] Route strings come from `paths.ts`; nav from `navigation.ts`
- [ ] One `h1` per page, and it is `PageHeader`'s title
- [ ] Zustand read through a selector (`useShallow` for several slices)
- [ ] Density handled by `DensityScope` / `.nx-dense`, never a prop
- [ ] Theme read through `useResolvedTheme()`, never a local media query

### States

- [ ] Loading state designed — skeleton at the real layout, not a spinner
- [ ] Empty state designed, with the action that ends the emptiness
- [ ] **No-results** is a distinct state from **no-data**
- [ ] Error state has a retry and no status code
- [ ] Refetch does not blank populated content
- [ ] Every interactive element has hover, focus-visible, active, disabled

### Accessibility

- [ ] Tab reaches everything; ring always visible; order matches layout
- [ ] Icon-only controls have `aria-label`; decorative icons `aria-hidden`
- [ ] Overlays: focus in on open, back to trigger on close, title + description
- [ ] Async regions announce once (`role="status"` on the wrapper)
- [ ] No information carried by colour alone — greyscale check passes
- [ ] Interactive elements ≥44px, or carry `hit-min` (compact rows excepted)
- [ ] New animation has a `prefers-reduced-motion` branch
- [ ] Contrast checked in both themes

### Content

- [ ] Turkish UI copy, English code
- [ ] Sentence case; buttons carry the verb
- [ ] Errors say what would be right
- [ ] Numbers/dates/currency through `Intl`; numbers tabular; null renders `—`
- [ ] No sentence assembled from fragments

### Data surfaces (when relevant)

- [ ] Grid: numbers right-aligned via `meta.align`, columns data-only
- [ ] Grid: sorting/paging/search in the URL with a `prefix`
- [ ] Form: zod schema colocated, no form state in a store, `Field` used
- [ ] Overlay: correct layer per the decision table, stack depth ≤2
- [ ] Chart: ≤3 series from `SERIES`, `STATIC_MARK`, one y-scale, zero-based bars
- [ ] Status: rendered through a `StatusRegistry`, never a bare colour

---

## Review guide

For the reviewer, in priority order. Stop at the first that fails — the rest
will need re-checking anyway.

1. **Does it break an invariant?** Feature-to-feature import, a density prop, a
   raw token, a second accent, colour-only status. These are not opinions.
2. **Are all five states there?** The most common gap by a wide margin.
3. **Keyboard and focus.** Tab through it in the browser; do not read for it.
4. **Both themes, both densities.** Screenshots in the pull request, or it
   was not checked.
5. **Copy.** Read the strings aloud. Vague labels survive review because
   reviewers read code, not text.
6. **Then** style and structure.

### What to attach to a UI pull request

- A screenshot per theme, at minimum, for anything visual.
- A one-line note of what was smoke-checked and what was not.
- If a documented rule was bent, say which and why — see Principle 10. A
  compromise you can name is engineering; an undocumented one is a defect that
  has not been found yet.
