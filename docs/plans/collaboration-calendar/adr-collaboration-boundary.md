# ADR: Collaboration boundary for personal calendar entries

**Status:** Accepted for implementation on 2026-09-21.

## Context

The existing `/calendar` is a frontend-only MSW mock. The first real capability is a
private user-created entry with title, timing, notes, a chosen color, and an optional
link to another domain record. It must be tenant-isolated, idempotent and accessible
only to its owner without making Calendar a shadow owner of CRM dates or records.

## Decisions

1. **Boundary:** add the `Collaboration` module and `collaboration` schema. Its first
   aggregate is `CalendarEntry` in `calendar_entries`. It owns only user-created
   entries; `/calendar` can later compose read-only facts from other modules but does
   not own their dates.
2. **Enablement:** ship module capability manifest `collaboration` v1, with the
   `collaboration_user` role and explicit owner-relation actions:
   `collaboration.calendar_entry.create`, `.read`, `.list`, `.update`, and `.delete`.
   No wildcard or `All` grant. Module templates are copied once when enabled; existing
   tenants are never reconciled automatically, so future template changes need a new
   explicit upgrade decision. Development provisioning enables Collaboration alongside
   CRM.
3. **Durable intent:** every create/update/delete saves entry state, a module-local
   idempotency record, and a thin CloudEvents-shaped outbox record in one transaction.
   v1 adds no Worker dispatcher. Outbox payloads exclude title, notes and target labels.
4. **Delete:** personal entries are hard-deleted. This complies with the no-soft-delete
   rule; the deletion fact remains in the outbox.
5. **Privacy:** handlers authorize the requested action and always owner-scope reads
   and mutations by the caller's `PrincipalRef`. Non-owners receive `404`, including
   managers. RLS isolates tenants; application ownership filtering isolates users.
6. **Links:** a row holds an optional `EntityRef`-shaped, no-FK link. v1 resolves
   `crm/opportunity` and `masterdata/party`; only an accessible opportunity has a web
   route. Host composes resolvers. Unknown, cross-tenant, absent and unauthorized
   targets all become `link_target_unavailable` on writes; hydration never exposes
   unavailable labels. CRM temporarily gates party reads with its existing reference
   action because MasterData has no PDP (L-3 limitation).
7. **Time:** timed values are offset-bearing wire timestamps stored as `timestamptz`;
   all-day values are timezone-free dates. End values are exclusive. No conversion
   from local wall-clock strings is allowed at the API boundary.

## Consequences

- The module references `Contracts` only. Host is the composition root for link
  resolvers; no module imports another module's domain or persistence namespace.
- `EntityRef.Id` is currently a `long`, so v1 cannot link to non-long identifier
  schemes. That limitation is recorded rather than hidden behind a lossy adapter.
- Color is data, normalized to lowercase `#rrggbb`; visual color never conveys the
  entry's sole meaning. Title, note and link state remain textually available.
- Personal entries are not evidence-risk-catalogued. Therefore no evidence row is
  required by the current enforcement scope; this is an explicit NOT APPLICABLE state,
  not an omitted control.

## Non-goals

No completion state (Human Tasks owns that), reminders, attendees/invites/sharing,
recurrence, external calendar synchronization, other-module event projections, or
dialog-level opportunity search. v1 creates linked entries from an opportunity's
"Add to calendar" affordance and permits link removal in the entry editor.
