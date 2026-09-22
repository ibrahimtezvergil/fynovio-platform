# Collaboration Calendar — tech debt backlog (post-merge review)

Source: `code-review` (effort `high`) over the full `feat/collaboration-calendar` merge,
range `0d99298..3be2d1e` (merged to `main` at `932ff0a`, 2026-09-21). None of these are
correctness bugs — the one candidate bug the review raised (`ParseExpectedVersion` allegedly
accepting a negative `expectedVersion` in `CalendarEndpoints.cs`) was verified with a standalone
`dotnet run` repro and is a false positive: `NumberStyles.None` already rejects the `-` sign,
so `TryParse("-1", ...)` returns `false` and the handler throws `ArgumentException` as intended.
No action needed on that one.

The items below are reuse/simplification/efficiency findings, kept here so they can be picked
up as a scoped task rather than fixed ad hoc. Assign to Codex as its own branch/worktree per
`AGENTS.md` (1 task = 1 branch = 1 worktree = 1 agent session); read `docs/codex-workflow.md` first.

## Cross-module idempotency infrastructure (touches Collaboration, CRM, Access, MasterData)

- [ ] **Deduplicate `IdempotencyRecord` — deferred.** Identical structure + `Create()` factory copied into
      (at least) `Collaboration`, `CRM`, `Access`, `MasterData` idempotency namespaces. The Access
      copy already has a code comment admitting it: "Deliberately duplicated from
      `CRM.Idempotency.IdempotencyRecord`". Move to a shared `Contracts.Idempotency` type; update
      every module's `DbContext` mapping and the request-hash call sites.
- [ ] **Deduplicate `IdempotencyKeyReusedException` — deferred with the record move.** Same near-identical class copied per module
      alongside the record above; fold into the same `Contracts.Idempotency` move.
- [ ] **Resolve the deferred idempotency-retention decision — needs owner decision.**
      already logged in `docs/plans/collaboration-calendar/2026-09-21-collaboration-calendar-plan.md`
      ("Deferred platform decision" section): `expires_at` exists per module but there is no
      platform-wide retention policy or Worker cleanup job. Decide retention duration, cleanup
      ownership/schedule, and observability once, in the shared module, rather than per-module.

## Collaboration module — internal duplication

- [ ] **`CollaborationProblemDetailsExceptionHandler` vs `CrmProblemDetailsExceptionHandler` — rejected.**
      (`src/Modules/Collaboration/Application/CollaborationProblemDetailsExceptionHandler.cs`):
      identical exception → `(status, type, title)` switch pattern. Extract a shared base/helper
      that both modules configure rather than re-implementing the mapping.
- [ ] **Redundant idempotency lookup in Create/Update/Delete handlers — rejected.**
      (`src/Modules/Collaboration/Application/CreateCalendarEntryHandler.cs:34` and the matching
      spot in `UpdateCalendarEntryHandler`/`DeleteCalendarEntryHandler`): the initial lookup runs,
      then on `DbUpdateException` the handler calls `FindAsync()` again with the same key instead
      of reusing the first result. Cache the first lookup and reuse it in the exception path.
- [ ] **Manually built idempotency request hash — deferred.**
      (`CreateCalendarEntryHandler.cs:110`, `HashRequest`): fields are listed by hand. Any new
      command field added later without a matching update to `HashRequest` silently breaks
      idempotency (replay hash diverges from a legitimate retry's hash). Either generate the hash
      from the command's serialized form or add a test that fails when a command property has no
      corresponding hash input.
- [x] **`CalendarEntry.Create()` / `Replace()` duplicate their normalization+validation chain.**
      (`src/Modules/Collaboration/Domain/CalendarEntry.cs:39` and `:73`): both call
      `NormalizeTitle` → `ValidateNotes` → `NormalizeColor` → `NormalizeTimestamp` (start/end) →
      `ValidateTiming` in the same order. Extract the shared sequence into one private method used
      by both.
- [ ] **Repeated transaction + tenant-context boilerplate in every endpoint/handler — deferred.**
      (`src/Host/Endpoints/CalendarEndpoints.cs:22` onward, and each
      Create/Update/Delete/Get/List handler): `BeginTransactionAsync()` +
      `SetTenantContextAsync(...)` is copy-pasted per handler. Consider a decorator or a shared
      pipeline step, consistent with how other modules that hit the same pattern are (or aren't)
      already handling it — check CRM/Access before deciding the shape so this doesn't become a
      sixth divergent copy.

## Efficiency (non-blocking)

- [ ] **`findCachedEntry` full-cache prefix scan — rejected.**
      (`web/src/features/calendar/api.ts:54`): uses `getQueriesData({ queryKey: entriesKey })`,
      which prefix-matches every cached date-range query. After a user has browsed several months
      this scans hundreds of cached entries to find one. Use a targeted range lookup or a direct
      `find()` over a smaller candidate set instead.
- [ ] **Sequential per-record authorization in `OpportunityLinkTargetResolver` — deferred.**
      (`src/Modules/CRM/Application/OpportunityLinkTargetResolver.cs:56`): loops over linked
      opportunities and calls `OpportunityReadAuthorization.IsAllowedAsync()` once per record —
      no batching, since `IAuthorizer` is single-resource only. Fine at current calendar-link
      volumes; revisit if `IAuthorizer` grows a batch API or link counts per entry get large.

## Scope note for whoever executes this

The idempotency dedup (first section) is the only piece with real cross-module blast radius —
touches four modules' `DbContext` configuration and their exception-mapping call sites. Everything
else is contained to `Collaboration`, its `Host` endpoints, or `web/src/features/calendar`. Treat
these as separable: the idempotency dedup deserves its own PR/review pass; the rest can land
together.

## 2026-09-22 validation outcome

- `CalendarEntry` normalization is now shared by `Create()` and `Replace()`; its existing domain
  tests cover both paths.
- The exception handlers are both in `Host`, not Collaboration, and their mappings intentionally
  differ (notably CRM's validation and lifecycle cases). A common switch/helper would hide
  module-specific HTTP policy without removing meaningful duplication.
- The retry lookup is not redundant: its first result is necessarily absent before the competing
  transaction commits. The fresh transaction is required to observe the winner after rollback.
- `findCachedEntry` has no target range at its call sites and deliberately selects the highest
  version across overlapping active and inactive windows. Replacing the prefix query with a
  direct lookup would either miss a cached entry or retain the same scan by another route.
- Shared idempotency persistence and retention cleanup require a platform ownership and lifecycle
  decision; no cross-module change was made here.
