# CRM / ERP Master UX Backlog — Feasibility Assessment

Source: `CRM_ERP_Master_UX_Backlog.pdf` (31 items, 4 layers)
Assessed against: `fynovio-react` @ `ios-27` — React 19 + TS, TanStack Table v9, React Query,
zustand, react-hook-form + zod, recharts, FullCalendar. **No backend. All data is mocked
per-feature behind a React Query `queryFn`.**

Companion document: [`../frontend-platform/ENTERPRISE_LAYERS_ASSESSMENT.md`](../frontend-platform/ENTERPRISE_LAYERS_ASSESSMENT.md) — the 60 platform layers that hold
these 31 user-facing systems up. Items referenced there as `UX #n` point back here.

## Verdict legend

| Code | Meaning |
| --- | --- |
| 🟢 **NOW** | Buildable today, end to end, with mock data. The mocked version *is* the real behaviour — swapping in an endpoint later changes one `api.ts`. |
| 🟡 **SHELL NOW** | The UI and the interaction can be built now and will be genuinely useful as a demo, but the feature is only *true* once a backend supplies the data or guarantees it claims. |
| 🔴 **BE-GATED** | Not honestly buildable without a backend. A frontend-only version would be a lie (fake audit trails, fake presence, fake lineage). |

"Must" column: **★** = we should commit to it, **☆** = valuable but can wait,
**—** = park it until the backend exists and the product has real users.

---

## The 31 items

### Layer 1 — Foundation / Power User

| # | System | Verdict | Must | Why, against this codebase |
| --- | --- | --- | --- | --- |
| 1 | Universal Command Palette (⌘K) | 🟢 NOW | ★ | `Topbar.tsx:24-35` already grabs ⌘K — but only to focus the search input. Replace with a real palette: a command registry (`{id, label, group, icon, run()}`), navigation commands generated from `src/layouts/navigation.ts`, record commands from the mocked feature APIs. Actions that mutate stay mocked; navigation and search are 100% real. |
| 2 | Saved Table Views & Column Workspace | 🟢 NOW (personal) / 🟡 (team) | ★ | Two thirds already exist: `columnVisibilityFeature` + `columnSizingFeature` + `columnResizingFeature` in `saasGridTable.tsx`, URL-owned sorting/paging/filter in `useTableSearchParams.ts`, and a saved-view model in `demo-filters/data/views.ts`. Missing: `columnOrderFeature`, and one persisted zustand slice that snapshots the whole view. **Team-shared views need BE** (ownership, permissions). |
| 3 | Contextual Bulk Action Bar | 🟢 NOW | ★ | `rowSelectionFeature` is registered on both the pipeline grid and the SaaS demo grid; `DataTableSelection.tsx` exists. What's missing is the floating bar and a per-entity action map. Mutations resolve against mock APIs — the *interaction* is fully real. |
| 4 | Peek Drawer / Contextual Slide-over | 🟢 NOW | ★ | Effectively already built in `src/features/demo-drawers/` (`DetailDrawer`, `EditDrawer`, `RecordExplorer`, stacked-drawer section). The work is promoting it out of the demo into `components/common` and wiring row-click on Pipeline. Cheapest high-impact item on the list. |
| 5 | Multi-View Switcher | 🟢 NOW | ★ | Every renderer exists in isolation — `DataTable`, `demo-kanban/KanbanBoard`, `demo-timeline/TimelineFeed`, `calendar/CalendarBoard`. What's missing is one shared dataset behind them and a `SegmentedControl` (already have it) writing the mode to the URL. This is the single most impressive demo per hour spent. |
| 6 | Keyboard-First Navigation (J/K, N, E, /, ?) | 🟢 NOW | ★ | Today there are three ad-hoc `keydown` listeners (`Topbar`, `Sidebar`, `UserMenu`). Needs one shortcut registry with scope/priority, a roving-focus row cursor in `DataTable`, and a `?` cheat-sheet sheet. Note the existing lesson in `Sidebar.tsx`: match on `event.code`, not the character — Turkish layouts break character matching. |
| 7 | Universal Undo & Reversible Actions | 🟡 SHELL NOW | ★ | The FE half is real: optimistic mutation + `sonner` toast with an Undo action + a 5–8s revert window over React Query cache. The **durable** half is BE — soft deletes, reversal endpoints, and undo that survives a page reload. Build the pattern now so every future mutation is written against it. |
| 8 | Focus / Zen Mode | 🟢 NOW | ☆ | Pure layout state. `sidebarCollapsed` in `useAppStore` is the precedent; add a `focusMode` boolean that also hides the topbar and widens the content column. Half a day. |

### Layer 2 — Speed Layer

| # | System | Verdict | Must | Why, against this codebase |
| --- | --- | --- | --- | --- |
| 9 | Predictive Next Action | 🟡 SHELL NOW | ☆ | A deterministic rule table (`stage × role → suggested action`) over `STAGES` in `src/types` is honest and buildable now. Anything learned from *actual* workflow history is BE. Start rule-based; the UI slot doesn't change when the ranking gets smarter. |
| 10 | One-Key Workflow Completion (⌘Enter) | 🟡 SHELL NOW | ☆ | ⌘Enter → validate → submit is trivial with react-hook-form. The "advance workflow + create task + fire notification" chain is a **BE transaction** — doing it as three FE calls would be wrong. Ship the keybinding, defer the chain. |
| 11 | Intent-Based Data Entry (free text → form) | 🔴 BE-GATED | — | Needs an LLM call server-side. A hand-written Turkish grammar parser ("ABC'ye 10 X100, %10 indirim, cuma teslim") is *technically* FE-doable and would demo well, but it is brittle and will be thrown away when the real parser lands. Skip until BE. |
| 12 | Smart Paste / Paste Anything | 🟢 NOW | ☆ | Pure clipboard work: intercept paste on `LineItemsEditor`, sniff TSV/CSV/plain text, map columns, show a confirm-mapping step. Zero backend. Genuinely useful, and the demo lands hard. |
| 13 | Transaction Preview / Dry Run | 🔴 BE-GATED | ★ (post-BE) | A preview that says "these 240 records will change" is only trustworthy if the server computed it. FE can render the before/after table — the numbers must come from a `POST /…/dry-run`. Design the response shape now; build the screen when the endpoint exists. |
| 14 | Before / After Diff | 🟢 NOW | ★ | A pure presentation component: two objects in, field-level diff out. `demo-timeline/data/audit.ts` already carries change records to render it against. Reused by #13, #24 and #25 later — build it early, it's leverage. |
| 15 | Ghost Save / Smart Autosave | 🟡 SHELL NOW | ☆ | Debounced `watch()` + a save-state chip (`idle → saving → saved`) is FE. Real drafts that survive a device change are BE. localStorage drafts are a reasonable interim, but be explicit that they're per-device. |

### Layer 3 — Intelligence Layer

| # | System | Verdict | Must | Why, against this codebase |
| --- | --- | --- | --- | --- |
| 16 | Focus Queue / Work Inbox | 🟡 SHELL NOW | ★ | The page is buildable now over merged mock sources (deals + activities + notifications). It becomes *real* when the BE can answer "what is assigned to me, ranked by urgency" in one query. Strategically the most important item in the PDF — it reframes the product — so build the shell early and let it shape the API. |
| 17 | Next Record Auto-Advance | 🟢 NOW | ★ | Entirely FE: keep the row cursor in list state, on complete advance to `rows[i+1]`. Pairs with #4 and #6, costs almost nothing once those exist. |
| 18 | Batch Workflow Studio | 🔴 BE-GATED | — | The condition-builder UI is FE, but execution needs a server-side job runner with retries and an audit trail. Building the builder with no engine behind it produces a toy. |
| 19 | Semantic / Natural-Language Filters | 🔴 BE-GATED | — | Same reasoning as #11 — needs an LLM server-side. `demo-filters/lib/query.ts` is already the right *target* representation, which is the useful FE prep work: keep the filter model serializable so a parser can emit it. |
| 20 | Exception-First UX | 🟡 SHELL NOW | ★ | Threshold rules (overdue, stock mismatch, price anomaly) can run FE over mock data and the cards are real UI. Anomaly detection worth the name — baselines, seasonality — is BE. Ship the surface, keep the rules replaceable. |
| 21 | Explain This Number | 🔴 BE-GATED | ☆ | Attributing a KPI delta needs the underlying facts and a decomposition query. FE can only render the waterfall it's handed. Design the panel; wait for data. |
| 22 | Instant Drill-Through | 🟢 NOW | ★ | **The best value/effort ratio in the whole intelligence layer.** recharts exposes `onClick` on every mark; the filter model and URL state already exist. Chart segment click → navigate to the grid with the filter pre-applied. No backend needed, and it delivers the "dashboard → why → action" chain the PDF is asking for. |
| 23 | Data Provenance / Number Lineage | 🔴 BE-GATED | — | Metadata pipeline problem, not a UI problem. Nothing meaningful to build in this repo. |

### Layer 4 — Trust & Enterprise Layer

| # | System | Verdict | Must | Why, against this codebase |
| --- | --- | --- | --- | --- |
| 24 | Time-Travel Record | 🔴 BE-GATED | — | Requires versioned or event-sourced records. There is no honest FE-only version. |
| 25 | What Changed Since My Last Visit? | 🔴 BE-GATED (FE-partial) | ☆ | "Last visit" per record is FE (persisted zustand). The change set is BE — it needs a real audit log with timestamps and actors. Half the feature is one afternoon; the useful half isn't. |
| 26 | Conflict-Aware Collaborative Editing | 🔴 BE-GATED | — | Presence and merge need a realtime channel (WebSocket) and server-side conflict detection. Nothing to prototype here that isn't theatre. |
| 27 | Context-Aware Smart Defaults | 🟢 NOW (partial) | ★ | "Last used value per field per user" in a persisted zustand slice, feeding `defaultValues` in react-hook-form, is real and FE-only. Role/branch/warehouse-derived defaults are BE. The FE half already removes most of the repetition the PDF is complaining about. |
| 28 | Adaptive / Progressive Forms | 🟡 SHELL NOW | ☆ | Workflow-stage-driven field visibility works today with zod discriminated unions + conditional rendering (`demo-forms/schema.ts` is the pattern). **Role**-driven adaptation needs a real permission model → BE. |
| 29 | Temporary Workspace / Scratchpad | 🟢 NOW | ☆ | A persisted zustand slice of pinned `{entity, id, label}` plus a dock strip. No backend. Genuinely differentiating and cheap. |
| 30 | Cross-Entity Compare Mode | 🟢 NOW | ☆ | Select 2–4 rows (selection already exists), render side-by-side with differing fields highlighted. Reuses the #14 diff engine. |
| 31 | Action Memory (Repeat / Smart Duplicate / Calculation Inspector) | 🟢 NOW (2 of 3) / 🟡 (inspector) | ☆ | *Repeat last action* and *Smart Duplicate* are FE — a small action-history stack plus a "copy with exclusions" dialog. *Calculation Inspector* is FE **only while pricing math lives on the client** (`DiscountInput`, `LineItemsEditor`, `ExchangeRateInput` do it today). Once pricing moves server-side, the formula trace must come from BE. |

---

## Roll-up

| Verdict | Count | Items |
| --- | --- | --- |
| 🟢 NOW | **15** | 1, 2, 3, 4, 5, 6, 8, 12, 14, 17, 22, 27, 29, 30, 31 *(2, 27, 31 partial)* |
| 🟡 SHELL NOW, true after BE | **7** | 7, 9, 10, 15, 16, 20, 28 |
| 🔴 BE-GATED | **9** | 11, 13, 18, 19, 21, 23, 24, 25, 26 |

Roughly **two thirds of the backlog has a real frontend deliverable today.** The third that
doesn't is concentrated exactly where you'd expect: audit history, realtime collaboration,
data lineage, and anything requiring an LLM.

---

## What we should actually commit to

### Must build — no debate (★)

Ordered by value ÷ effort, not by PDF order:

1. **#4 Peek Drawer** — already written, just needs promoting out of `demo-drawers`.
2. **#22 Instant Drill-Through** — charts and filters both exist; connect them.
3. **#1 Command Palette** — ⌘K is already bound to the wrong thing; fix it properly.
4. **#3 Bulk Action Bar** — selection state is live and currently does nothing.
5. **#14 Before/After Diff** — leverage: #13, #24, #25, #30 all consume it.
6. **#5 Multi-View Switcher** — four renderers already built, one dataset missing.
7. **#6 Keyboard-First Nav** — consolidates three ad-hoc listeners into one registry.
8. **#17 Auto-Advance** — nearly free once #4 and #6 land.
9. **#2 Saved Views** — one `columnOrderFeature` + one persisted slice away.
10. **#27 Smart Defaults** — small, and it removes the most repetition per line of code.
11. **#7 Universal Undo** — build the *pattern* now so every later mutation inherits it.
12. **#16 Focus Queue** — build the shell; let it dictate the API we ask the backend for.
13. **#20 Exception-First** — same reasoning as #16.

That's 13 items, all shippable without a backend, and it covers **the whole of Layer 1** plus
the parts of Layers 2–3 that don't depend on data we don't have.

### Deliberately deferred

- **#11, #19** (Intent Entry, Semantic Filters) — wait for a server-side model. Meanwhile keep
  `demo-filters/lib/query.ts` serializable so a parser can target it.
- **#13, #21** (Dry Run, Explain This Number) — design the response contracts now, build the
  screens when the endpoints exist.
- **#18, #23, #24, #26** (Workflow Studio, Lineage, Time Travel, Collaborative Editing) — these
  are backend products with a thin UI. Nothing to gain by starting on the frontend.

### What to ask the backend for, in this order

The three FE shells above are what should drive the API conversation:

1. Mutation endpoints that are **reversible** (soft delete + reversal), for #7.
2. A **work-queue** endpoint — "what's assigned to me, ranked" — for #16.
3. An **exception feed** with server-computed thresholds, for #20.
4. A **dry-run** variant of every bulk mutation, for #13.
5. An **audit log** with actor + timestamp + field-level before/after, for #14/#24/#25.

---

## Codebase notes that affect the plan

- **Nothing here needs a new dependency.** Every ★ item is buildable with what's in
  `package.json` today.
- **Undo, drill-through and the command palette all want a shared action registry.** Build it
  once, in `src/components/common/` or a new `src/lib/actions.ts` — not three times per feature.
  Features never import from each other (architecture invariant), so it must live above them.
- **Density is a CSS-custom-property flip, never a prop.** Anything new that renders rows
  (Focus Queue, Compare Mode, Scratchpad dock) must use `DensityScope` / `.nx-dense`, not its
  own sizing.
- **Keyboard work must match on `event.code`.** `Sidebar.tsx:12` documents why: `[` is AltGr+8
  on a Turkish layout and AltGr *is* Ctrl+Alt on Windows.
- **There is no test suite.** `npm run build` (which type-checks via project references) plus a
  manual smoke pass is the whole verification story. For a 13-item programme of this size,
  that's worth revisiting.
