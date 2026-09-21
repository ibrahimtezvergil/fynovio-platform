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
  Deviation: until S3 validates link targets on write, any non-null `link` in a request is
  `422 link_target_unavailable` (endpoint layer, marked `// S3 replaces this`); stored links, if any, read
  back as `state: "unavailable"` without label.

- [ ] S3 — Links: add Contracts link-directory abstractions, CRM opportunity/party
  resolvers, Host composition, authenticated write validation and read hydration,
  together with authorization-agreement and privacy tests.

- [ ] S4 — Frontend: replace calendar mocks with real range-aware React Query APIs;
  add Zod contracts, dialog/create/edit/delete flows, `ColorInput`, contrast helper,
  drag/resize rollback, link navigation, i18n/tests and mock guard updates.

- [ ] S5 — Closure: run format/build/test/migration/frontend checks, native review,
  browser flow and isolation verification; refresh Graphify; update `AGENTS.md` status;
  commit derived graph files and complete merge/worktree hygiene after owner review.

## Acceptance matrix

| Concern | Evidence before closure |
|---|---|
| Isolation | Runtime-role RLS read and `WITH CHECK` write tests for all Collaboration tables; owner-scoped 404 tests. |
| Correct writes | Domain constraints, database CHECK tests (including trimmed single-line titles), optimistic concurrency, idempotency replay/key-reuse/concurrent-race tests, and state+outbox atomicity. |
| Links | Resolver/PDP agreement; indistinguishable unavailable-target behavior; no hydrated label after access is unavailable. |
| API | 401/400/404/409/422 mappings, bounded range, offset-less timed values rejected as 400, and UTC/offset plus exclusive all-day contracts. |
| UI | Zod/color/date/contrast/query-key tests, both-locale parity, drag rollback, accessible/unavailable/no-route link states, and browser verification in both themes. |
