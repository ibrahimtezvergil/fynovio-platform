# Collaboration personal calendar — execution plan

Owner-approved scope and defaults are preserved in
[`CODEX_EXECUTION_PROMPT.md`](CODEX_EXECUTION_PROMPT.md). A checkbox is completed
only after the corresponding commit exists.

- [x] Create the isolated `feat/collaboration-calendar` worktree and record the
  execution prompt.
  → Commit: `3298776` "docs(plans): add collaboration calendar execution prompt"

- [x] S0 — Decision gate: add the Collaboration physical schema, ADR and this plan;
  stop for owner approval before generating model code or migrations.
  → Commit: `f3fbcbb` "docs(collaboration): define calendar boundary and schema"

- [x] S1 — Backend core: create the `Collaboration` module and test project; add the
  aggregate, context, configurations, generated schema/RLS migrations, runtime-role
  grants, capability manifest/catalog, Host composition, create/get/list handlers,
  and domain/persistence/RLS/architecture tests.
  → Commit: `ffa2111` "chore(collaboration): format module and wire it into e2e, playwright env and README"
  (follow-up fix: `a3eb6cf` "fix(collaboration): close S1 review findings" — control characters in
  title/notes, microsecond truncation of instants, Record-stage denial without an entry id, schema-doc
  `row_version` default, dead `IHasRowVersion`)

- [x] S2 — Mutations and HTTP: implement full-replace update and hard delete with
  owner scoping, concurrency/idempotency/race handling, Host endpoints, and HTTP tests.
  → Commit: `4cab5db` "test(host): calendar HTTP contract tests"
  (handlers `4892bcc`, endpoints `a46f307`, handler tests `208847e`, frozen contract `107d785`;
  follow-up fix: `57a7e78` "fix(collaboration): replay when the key owner commits between lookup and entry load")
  Deviation (closed by S3): until S3 validated link targets on write, any non-null `link` in a request was
  `422 link_target_unavailable` (endpoint layer); that interim rule is removed in `65a88c5`.

- [x] S3 — Links: add Contracts link-directory abstractions, CRM opportunity/party
  resolvers, Host composition, authenticated write validation and read hydration,
  together with authorization-agreement and privacy tests.
  → Commit: `65a88c5` "feat(collaboration): validate links on write and hydrate them on read"
  (S3a on `feat/calendar-links-crm`: Contracts directory `aa9ff72`, resolvers `914f702`, tests `18786e6`,
  head `2f2f6bf`, merged as `0d7ca82`; docs `2a60059`)
  Deviation from the plan text: on `PUT` a link identical to the stored one is not re-validated (owner decision,
  keeps an entry editable after its target became unavailable); only a new or changed link is resolved.
  The interim `masterdata/party` resolver lives in CRM under L-3.

- [x] S4 — Frontend: replace calendar mocks with real range-aware React Query APIs;
  add Zod contracts, dialog/create/edit/delete flows, `ColorInput`, contrast helper,
  drag/resize rollback, link navigation, i18n/tests and mock guard updates.
  → Commit: `5bb7014` "feat(web): calendar on the real API — dialogs, drag/resize, links, no mock"
  (on `feat/calendar-web`: overlay hook + contrast helper `56ab8af`, unit/component tests `d558d31`, merged as
  `e3255a1`; follow-ups on this branch: `cd46f99` "fix(web): keep entries with an unavailable link draggable and
  editable", `7d0e855` "test(web): real-API calendar E2E — all-day, real-grid drag, keyboard and focus trap")
  Deviations: `56ab8af` also carries the mock-era deletions and does not compile on its own (the branch tip does).
  "Add to calendar" is a `/calendar?link=crm/opportunity/:id` URL parameter consumed by the calendar page (features
  cannot import each other); only the opportunity detail page has the entry point (the grid has no row menu). An entry
  whose link is unavailable is draggable/editable and re-sent with its stored ref (follows the S3 unchanged-link rule).
  The calendar E2E does not cover "link became unavailable after save" (no API revokes a grant); Host.Tests and vitest do.

- [ ] S5 — Closure: run format/build/test/migration/frontend checks, native review,
  browser flow and isolation verification; refresh Graphify; update `AGENTS.md` status;
  commit derived graph files and complete merge/worktree hygiene after owner review.
  Progress (not yet closed — only the owner review, the merge and worktree hygiene remain; `AGENTS.md` status `8dbf39e` "docs: record the Collaboration module and calendar status in AGENTS.md" and Graphify refresh `eab8c92` "chore(graphify): refresh graph after Collaboration module and calendar" are done):
  the independent review (Critical: none) and the Playwright visual walkthrough (both themes, tr/en, 390 px) are
  done and their findings fixed — `f532085` "fix(collaboration): reject a trailing newline in color and identifiers",
  `bd9eff0` "fix(collaboration): authorize the list request before validating its range", `ac566e2` "fix(collaboration):
  make the title CHECK match the domain's control-character rule" (initial migration regenerated, it had never
  shipped), `ea8c1b1` (client title rule), `8d70fdb` (freshest cached copy on drag), `a9dce2c` (toolbar wraps on
  phones, ring and ellipsis on event chrome), `1ae5c5c` (date fields and month grid follow the UI language).
  Owner decision: the calendar navigation and "Add to calendar" affordance remain open to every authenticated tenant
  member; there is deliberately no frontend capability gate. API action authorization remains the enforcement boundary.

## Acceptance matrix

| Concern | Evidence before closure |
|---|---|
| Isolation | Runtime-role RLS read and `WITH CHECK` write tests for all Collaboration tables; owner-scoped 404 tests. |
| Correct writes | Domain constraints, database CHECK tests (including trimmed single-line titles), optimistic concurrency, idempotency replay/key-reuse/concurrent-race tests, and state+outbox atomicity. |
| Links | Resolver/PDP agreement; indistinguishable unavailable-target behavior; no hydrated label after access is unavailable. |
| API | 401/400/404/409/422 mappings, bounded range, offset-less timed values rejected as 400, and UTC/offset plus exclusive all-day contracts. |
| UI | Zod/color/date/contrast/query-key tests, both-locale parity, drag rollback, accessible/unavailable/no-route link states, and browser verification in both themes. |
