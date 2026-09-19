# Fynovio — Dashboard

Private dashboard panel for the Fynovio B2B SaaS CRM. React + TypeScript, organised
along Feature-Sliced Design lines. **No backend yet** — the data layer is mocked
behind React Query so that swapping in real endpoints touches one file per feature.

## Stack

| Concern | Choice |
|---|---|
| Build | Vite (`react-ts`) |
| Styling | Tailwind CSS v4 + shadcn/ui |
| Icons | lucide-react (stroke-width 1.5) |
| Routing | react-router-dom (`createBrowserRouter`) |
| Server state | @tanstack/react-query |
| Client state | zustand (+ `persist`) |
| HTTP | axios |
| Forms | react-hook-form + zod |

## Getting started

```bash
npm install
npm run dev      # http://localhost:5173
npm run build    # tsc -b && vite build
```

Login is mocked: any valid e-mail and an 8+ character password signs you in.
The form is pre-filled.

## Folder layout

```
src/
  api/              axios instance, interceptors, QueryClient, endpoint map
  components/
    ui/             shadcn primitives (generated — avoid hand-editing)
    common/         cross-feature building blocks (SegmentedControl, StageBadge…)
    common/inputs/  the form control kit (money, discount, phone, date, …)
    data-table/     shared TanStack Table wrapper
  layouts/          DashboardLayout, AuthLayout + Sidebar/Topbar/UserMenu
  features/
    auth/           login, session
    dashboard/      overview KPIs and widgets
    pipeline/       deal board, stage tracking
    settings/       account/profile/notification forms
    demo-tables/    data-table showcase
    demo-forms/     form control showcase (/demo/forms)
    placeholder/    stub pages for unbuilt sections
  routes/           router, paths, ProtectedRoute, PublicOnlyRoute
  store/            global app state (theme, sidebar)
  styles/           tokens.css — the design-token layer
  types/            shared TypeScript types
```

### The rule that keeps this scalable

A feature owns its pages, components, store and queries. Features do not import
from each other — anything two features both need moves up into `components/common`,
`store/` or `types/`. Layouts and routes may import from features; never the reverse.
This is enforced by `.oxlintrc.json` (`no-restricted-imports` overrides per feature).

## Form controls

`src/components/common/inputs/` holds every input the CRM and ERP screens are built from:

- **Money** — amount with a currency picker, discount as fixed-or-percent, percentage,
  quantity with a unit, number ranges, a foreign amount with the rate it was booked at,
  and a share allocation that has to close on its total.
- **Identity** — phone with dial codes and as-you-type formatting (E.164 out), IBAN,
  VKN / TCKN, card number, expiry and CVC, each checking its own check digits; a barcode
  field that can tell a wedge scanner from a person typing.
- **Selection** — native select, rich select, searchable and creatable comboboxes, a
  server-backed one, chip multi-select, free tags, dependent (cascading) selects, and a
  tree for hierarchies like a chart of accounts.
- **Time** — date and date-range pickers on a Turkish month grid, time ranges, durations
  stored as minutes, quarters.
- **Content** — rich text (tiptap), signature pad, image upload with an in-place cropper,
  file drop, colour, rating, OTP.

They share one contract: controlled, `value` + `onValueChange`, and they accept the props
`Field`'s render prop hands a control, so `<Field>{(props) => <MoneyInput {...props} …/>}</Field>`
always works. The barrel re-exports everything and tree-shakes cleanly — a page importing
only `MoneyInput` does not pay for the editor or the phone metadata.

`/demo/forms` renders all of them with their live emitted values, plus the composites they
exist for: a quote's line table, an instalment plan, and a `react-hook-form` + zod form.

## Design system

> **The written standard lives in [`docs/design-system/`](./docs/design-system/)** —
> principles, foundations, theming & density, component standards, the pattern docs
> (navigation, grids, forms, overlays, states, charts), accessibility, content rules
> and the UI definition of done. Read it before building a screen; the summary below
> is only the orientation.

`src/styles/tokens.css` is the design-token layer and the single source of truth for
color, elevation, radii, blur and motion — the `--nx-*` custom properties. It defines a
blue-slate dark ground and a white-glass-over-cool-grey light ground, a single purple
accent (`--nx-accent`, aliased as `--nx-tint`) that carries text, icons and fills alike,
and a real soft drop shadow (`--nx-elev`) in both themes, not just the light one.

`src/index.css` maps shadcn's semantic variables (`--primary`, `--muted`, …) onto those
tokens and defines the `.nx-*` material/component classes (`.nx-card`, `.nx-material`,
`.nx-pill`, `.nx-nav-item`, `.nx-grid`, …). **Components never read `--nx-*` directly** —
they read shadcn's semantic layer or the `.nx-*` classes, so retinting the whole product
is a single-file edit.

Two rules worth keeping:

- One accent, not a role split: `--nx-tint` is an alias of `--nx-accent`
  (`#a99ef0` dark / `#6355c7` light) and carries text, icons and tints alike;
  `--nx-accent-grad` fills the primary button, brand mark and meters only.
- Stage is never encoded by colour alone — `StageBadge` pairs the status-pill tone
  with a label.

Dark theme keys off the `.dark` class on `<html>`, written by `applyTheme()` in
`src/store/useAppStore.ts` and persisted to localStorage.

## Connecting a backend

1. Set `VITE_API_URL` (see `.env.example`).
2. Replace the mock `queryFn` in `src/features/*/api.ts` with an `apiClient` call —
   `endpoints` in `src/api/endpoints.ts` already holds the paths.
3. Swap the mock in `useAuthStore.login` for `POST /auth/login` and feed the real
   token to `setAuthToken()`.

Nothing else in the tree needs to change.
