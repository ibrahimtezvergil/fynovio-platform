# Fynovio Design System

The standard for how this product looks, behaves and feels. It exists so that a
decision is made **once** — not re-argued in every pull request, and not
re-invented by every new screen.

This is a working standard, not a style guide to admire. Every rule in here is
either already enforced by the code or is a gap we have written down on purpose.

---

## Who this is for

| Reader | Start at |
| --- | --- |
| Building a new screen | [01 Principles](./01-principles.md) → the relevant pattern doc (05–10) → [13 Definition of Done](./13-ux-definition-of-done.md) |
| Building a new component | [04 Component Standards](./04-component-standards.md) |
| Changing colour, spacing, motion | [02 Foundations](./02-foundations.md) → [03 Theming & Density](./03-theming-and-density.md) |
| Reviewing a UI pull request | [13 Definition of Done](./13-ux-definition-of-done.md) |
| Writing UI copy | [12 Content & Formatting](./12-content-and-formatting.md) |
| An AI agent working in this repo | root `CLAUDE.md` / `AGENTS.md` first, then this folder |

---

## The map

### Foundations — the values everything is built from

- **[01 · Principles](./01-principles.md)** — the ten rules that settle arguments.
- **[02 · Foundations](./02-foundations.md)** — colour, typography, space, radii, elevation, motion, icons, z-index, breakpoints.
- **[03 · Theming & Density](./03-theming-and-density.md)** — light / dark / system, and the second axis: comfortable / compact.

### Standards — how we build

- **[04 · Component Standards](./04-component-standards.md)** — the three layers, the controlled-component contract, naming, documentation, deprecation.

### Patterns — how we solve recurring problems

- **[05 · Navigation & App Shell](./05-navigation-and-app-shell.md)**
- **[06 · Data Grids](./06-data-grids.md)**
- **[07 · Forms](./07-forms.md)**
- **[08 · Overlays](./08-overlays.md)**
- **[09 · States & Feedback](./09-states-and-feedback.md)**
- **[10 · Data Visualization](./10-data-visualization.md)**

### Quality — what "done" means

- **[11 · Accessibility](./11-accessibility.md)** — WCAG 2.2 AA baseline, and our two documented exceptions.
- **[12 · Content & Formatting](./12-content-and-formatting.md)** — voice, microcopy, numbers, dates, currency, i18n readiness.
- **[13 · UX Definition of Done](./13-ux-definition-of-done.md)** — the checklist a UI change ships against.

---

## Source-of-truth hierarchy

When two things disagree, the one higher in this list wins:

1. **`src/styles/tokens.css`** — the canonical *values*. Colour, elevation, radii,
   blur, motion, metrics, density. Never audit it against an external design file;
   the file itself is the design file.
2. **`src/index.css`** — the canonical *mapping*. shadcn's semantic variables and
   the `.nx-*` material classes. The only place allowed to read raw `--nx-*` tokens.
3. **These documents** — the canonical *rules*. What the values mean, when to use
   which, and what is forbidden.
4. **Source comments** — the canonical *reasoning* for one specific decision.
   Several rules in here exist because a component comment explains why; those
   comments stay authoritative for their own component.

If you change a rule here, change the code in the same pull request. A standard
that has drifted from the code is worse than no standard.

---

## Governance

### Adding a design token

1. Does an existing token already mean this? Reuse it. The ladder is deliberately
   short — a new value is a claim that the ladder is wrong.
2. If it is genuinely new, add it to `src/styles/tokens.css` in **both** the
   `:root` and `.dark` block, even if the value is identical. A token defined in
   only one theme is a bug waiting for a theme switch.
3. Map it in `src/index.css` — either onto a shadcn semantic variable or into a
   `.nx-*` class. Components never read `--nx-*` directly.
4. Document it in [02 Foundations](./02-foundations.md).

### Adding a component

See [04 Component Standards](./04-component-standards.md) for the full contract.
The short version: `ui/` is shadcn-generated, `common/` is cross-feature,
`features/<name>/components/` is feature-local, and code only ever moves **up**.

### Deprecating something

Mark it in the source with a `@deprecated` JSDoc tag naming the replacement,
update the doc that describes it, and remove it once nothing imports it. Do not
delete a shared component in the same change that introduces its replacement —
that turns one reviewable decision into two unreviewable ones.

### Changing a rule in this folder

A rule change is a design decision, not an edit. State in the pull request: what
the rule was, what it becomes, what breaks, and why the old reasoning no longer
holds. The token comments in `tokens.css` are the model for this — several of
them record a reversal and say so ("This deliberately reverses the earlier
'edge light only' rule").

---

## Glossary

| Term | Meaning |
| --- | --- |
| **Token** | A `--nx-*` custom property in `tokens.css`. The raw value. |
| **Semantic variable** | shadcn's `--primary`, `--muted`, `--border`, … Mapped from tokens in `index.css`. What components actually read. |
| **Material** | A surface treatment: glass + hairline + specular inset. `.nx-material` (no cast) and `.nx-card` / `.nx-overlay` (cast). |
| **Ground** | The page background — `--nx-canvas`. Not a surface. |
| **Hairline** | The 1px edge that separates materials. Never a "border colour" in the general sense. |
| **Tone** | A hue in the status ladder (gray · blue · teal · green · amber · red · purple). A tone is a colour, **not** a meaning. |
| **Vocabulary** | A domain's mapping from status values to tone + label, e.g. `STAGE_META`. Where meaning is assigned. |
| **Density** | The comfortable / compact axis. One boolean, expressed as CSS custom properties. |
| **Density region** | A subtree carrying `.nx-dense`, usually via `DensityScope` or `Toolbar`. |
| **Ladder** | A short, fixed set of allowed values (radii, tones, elevation steps). Picking from a ladder, not inventing a value, is the whole point. |
