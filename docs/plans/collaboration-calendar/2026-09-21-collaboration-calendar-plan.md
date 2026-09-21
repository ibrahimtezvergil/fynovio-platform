# Collaboration personal calendar — execution plan

Owner-approved scope and defaults are preserved in
[`CODEX_EXECUTION_PROMPT.md`](CODEX_EXECUTION_PROMPT.md). A checkbox is completed
only after the corresponding commit exists.

- [x] Create the isolated `feat/collaboration-calendar` worktree and record the
  execution prompt.
  → Commit: `3298776` "docs(plans): add collaboration calendar execution prompt"

- [ ] S0 — Decision gate: add the Collaboration physical schema, ADR and this plan;
  stop for owner approval before generating model code or migrations.

- [ ] S1 — Backend core: create the `Collaboration` module and test project; add the
  aggregate, context, configurations, generated schema/RLS migrations, runtime-role
  grants, capability manifest/catalog, Host composition, create/get/list handlers,
  and domain/persistence/RLS/architecture tests.

- [ ] S2 — Mutations and HTTP: implement full-replace update and hard delete with
  owner scoping, concurrency/idempotency/race handling, Host endpoints, and HTTP tests.

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
| Correct writes | Domain constraints, database CHECK tests, optimistic concurrency, idempotency replay/key-reuse/concurrent-race tests, and state+outbox atomicity. |
| Links | Resolver/PDP agreement; indistinguishable unavailable-target behavior; no hydrated label after access is unavailable. |
| API | 401/400/404/409/422 mappings, bounded range, UTC/offset and exclusive all-day contracts. |
| UI | Zod/color/date/contrast/query-key tests, both-locale parity, drag rollback, accessible/unavailable/no-route link states, and browser verification in both themes. |
