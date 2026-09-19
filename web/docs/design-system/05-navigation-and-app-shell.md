# 05 · Navigation & App Shell

The frame every screen lives inside, and the rules that keep a user oriented.

---

## Anatomy

```
┌──────────────┬──────────────────────────────────────────────┐
│              │  Topbar (64px, .nx-material, sticky, z-[5])  │
│   Sidebar    │  breadcrumb · search ⌘K · theme · notifs     │
│   266 / 72px ├──────────────────────────────────────────────┤
│ .nx-material │  PageHeader   eyebrow / title / description  │
│              │               actions →                      │
│              ├──────────────────────────────────────────────┤
│              │  Toolbar      search · filters · view ctrls  │
│              ├──────────────────────────────────────────────┤
│              │  Content                                     │
└──────────────┴──────────────────────────────────────────────┘
```

The shell is `.nx-material`: glass + hairline + specular, and **no cast**.
Only floating surfaces (`.nx-card`, `.nx-overlay`) cast a shadow. The shell is
part of the window, not floating above the page.

---

## Information architecture

### Nesting is one level deep, and no deeper

`NavParent` holds `NavLeaf[]`. A `NavParent` is **never a route itself** —
that is precisely what lets the collapsed rail treat it as a flyout trigger
rather than a link (`isNavParent()` in `src/types` is the narrowing).

If a section needs three levels, it needs in-page navigation (tabs,
`SectionNav`) — not a third nav tier.

### One path→nav resolver

`findNavTrail()` in `src/layouts/navigation.ts` is **the** resolver. The
breadcrumb and the active nav state both read it, which is what stops them from
drifting apart. Never re-derive "which nav item is active" from `pathname` in a
component.

### Route strings live in one place

`src/routes/paths.ts`. Links and guards read from it so they cannot disagree.
Adding a navigable page means: add the path, register the route (preserving the
lazy boundary), and add the nav entry.

---

## The sidebar

Two widths, one bit: `sidebarCollapsed` in `useAppStore`, persisted, read
through `useSidebar()`.

| | Expanded | Collapsed (rail) |
| --- | --- | --- |
| Width | 266px | 72px |
| Leaf | Icon + label | Icon + right-side `Tooltip` |
| Parent | Inline disclosure | `Popover` flyout, `openOnHover`, 220ms in / 140ms out |
| Active state | Tint + label weight | Tint **plus a bar** — there is no label to carry it |

### Two decisions worth not re-litigating

**Labels are dropped, not faded.** Only `width` transitions. Fading a label
means animating layout, which stutters and lands the text mid-fade on a slow
frame.

**The rail's flyouts are portalled.** The rail's scroll container clips, and
portalled content is the only kind that escapes it. This is why a rail parent
is a `Popover` and not an inline expansion.

### The keyboard chord

Toggle is ⌘/Ctrl + the key to the right of P, matched on
**`event.code === 'BracketLeft'`, never on the character**.

`[` is AltGr+8 on a Turkish layout, and on Windows AltGr *is* Ctrl+Alt — a
character match would fire every time someone typed a bracket. The cap shown in
the hint comes from `navigator.keyboard.getLayoutMap()`: ⌘Ğ on a Turkish Q
board, ⌘[ on a US one.

**This generalises.** Every shortcut in this product matches on `code` and
labels itself from the layout map. See [11 Accessibility](./11-accessibility.md).

---

## The topbar

Fixed responsibilities, in order: breadcrumb (left) → spacer → search →
general destinations → theme toggle → notification center → user menu. General
destinations are the small set of workspace-wide pages that must remain one
click away when the sidebar is collapsed; their feature-owned nav contribution
uses `surface: 'topbar'`, so they stay in the breadcrumb and command-palette
registries while being omitted from the sidebar.

- The breadcrumb renders from `findNavTrail()`. Root is always "Fynovio".
- Search is currently a ⌘K-focused input. It is slated to become a full
  command palette — see `docs/ux-backlog/UX_BACKLOG_ASSESSMENT.md` item 1.
- The topbar is **never** a density region. It stays comfortable in both modes.

---

## Page-level structure

Every content page opens the same way:

```tsx
<PageHeader
  eyebrow="Satış"
  title="Fırsat Hattı"
  description="Açık fırsatlar, aşamalarına göre."
  actions={<Button>Yeni fırsat</Button>}
/>
<Toolbar>…</Toolbar>
<Card>…</Card>
```

Rules:

- **One `h1` per page**, and it is `PageHeader`'s title. Everything below is
  `h2`/`h3`.
- **`PageHeader` is not a density region.** The header stays comfortable while
  the toolbar below it shrinks — that contrast is the point.
- **Primary action goes in `actions`, right-aligned.** One filled button
  maximum (Principle 1).
- The eyebrow is the section, not a repeat of the breadcrumb's last crumb.

---

## URL as state

The URL owns anything that should survive a refresh or travel in a shared link:

| Owned by the URL | Owned by a persisted store | Owned by local state |
| --- | --- | --- |
| Sorting, paging, global filter (`useTableSearchParams`) | Theme, density, sidebar width | Draft input before it settles |
| The active view / mode | Auth session | Open/closed of a transient popover |
| Selected record when it has its own route | Notification read state | Hover, focus |

`useTableSearchParams` supports a `prefix` so two grids can live in one URL, and
defaults to `replaceHistory: true` — paging must not fill the back button.

---

## Choosing a surface for "show me more about this row"

Four answers, and the wrong one is fixed by information architecture later, not
by swapping a component. (Rendered in-app at `/demo/drawers`.)

| Surface | When | Avoid when |
| --- | --- | --- |
| **Slide-over / peek drawer** | See a whole row, or edit briefly. The list stays visible and the scroll position survives. | Content needs more than ~768px or more than two tabs. |
| **Dialog** | One decision ends the task: a confirmation, a single question, a short form. | Content scrolls, has tabs, or must be read next to something else. |
| **Full page** | The record *is* a workspace: multi-tab detail, long form, editor. Gets a URL, is shareable, back works. | The user will bounce straight back to the list — every round trip rebuilds it. |
| **Inline row expansion** | A few extra fields, and comparing rows matters. Two can be open at once. | The detail carries actions or runs past ~5 fields. A table row must not become a form. |

---

## Responsive behaviour

Desktop-first B2B: the design target is 1280–1920px. Respond to the available
application window, not the physical device: a split desktop window and a
tablet can need the same shell.

- At `lg:` (1024px) and wider the persisted sidebar preference selects the
  266px expanded sidebar or the 72px rail.
- From `sm:` (640px) up to `lg:` the sidebar is always the 72px rail; the
  Topbar hamburger opens the navigation drawer for application switching,
  tools, and labelled navigation. From `lg:` to `xl:` the persistent sidebar
  already carries the navigation, so the drawer holds only Applications and
  Tools.
- Below `sm:` there is no persistent sidebar. The hamburger opens a left sheet
  (85vw, maximum 320px at every width); it closes when the route changes — by
  driving the sheet's own `open` state, never by remounting it — so the Sheet
  primitive still supplies Escape, overlay dismissal, focus trapping, the exit
  animation, and focus restoration to the trigger. The Topbar keeps only menu,
  page title, search, conversations, notifications, and user menu.
- Below `xl:` every Topbar button is 44px (`--nx-hit-min`). Conversations is
  not a Tool, so it stays in the Topbar at every width instead of moving into
  the drawer.
- The complete desktop Topbar directory (breadcrumb, full search input,
  Applications, Tools, and workspace destinations) starts at `xl:` (1280px).
- Below `sm:` the goal is **usable, not equivalent**. Toolbars wrap; grids
  scroll horizontally inside their own `overflow-x-auto` container rather than
  reflowing into cards.
- The page body never scrolls horizontally. Wide content scrolls inside itself.
