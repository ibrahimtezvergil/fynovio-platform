# Calendar API contract (frozen for parallel backend / frontend work)

Status: frozen 2026-09-21. Backend (S2/S3) and frontend (S4) both implement exactly this. Any change goes through the
owner-session (Claude controller), never unilaterally on one side.

Base path: the Host root (no `/api` prefix). All routes `RequireAuthorization()`; tenant and principal come from the
authenticated actor, **never** from the request body or query.

## Entry (response shape)

```json
{
  "id": 42,
  "rowVersion": 3,
  "title": "Call with vendor",
  "notes": "Bring the price list" ,
  "color": "#3b82f6",
  "allDay": false,
  "startAt": "2026-09-21T06:00:00+00:00",
  "endAt": "2026-09-21T07:00:00+00:00",
  "startDate": null,
  "endDate": null,
  "link": {
    "ref": { "boundedContext": "crm", "entityType": "opportunity", "id": 17 },
    "state": "accessible",
    "label": "OPP-17 — Acme",
    "subtitle": "Proposal"
  }
}
```

- `notes`: string or `null`. `color`: lowercase `#rrggbb` (server normalizes).
- Timed entry (`allDay=false`): `startAt` required, `endAt` optional (`null` = point in time, otherwise `endAt > startAt`,
  exclusive); `startDate`/`endDate` are `null`. The server returns instants normalized to UTC (`+00:00`); clients convert
  to local for display.
- All-day entry (`allDay=true`): `startDate` and `endDate` (both `YYYY-MM-DD`, timezone-free) required, `endDate`
  **exclusive** and `> startDate` (FullCalendar semantics; a one-day entry on the 21st is `2026-09-21` → `2026-09-22`);
  `startAt`/`endAt` are `null`.
- `link` is `null` when absent. Otherwise `ref` is always present; `state` is `"accessible"` or `"unavailable"`;
  `label`/`subtitle` are present only when `accessible` (absent, not empty string, when `unavailable`). An
  `unavailable` link means: no navigation, render as a plain entry.

## Routes

| Route | Success | Notes |
|---|---|---|
| `GET /calendar/entries?from=&to=` | `200 { "items": Entry[] }` | `from`, `to` are offset-bearing ISO 8601 instants, `to > from`. **Clients send UTC (`toISOString()`, `Z` suffix)** — a raw `+03:00` in a query string must be URL-encoded (`%2B`). Range ≤ 100 days and ≤ 500 matches, else `422 range_too_large`. Only the caller's own entries are returned. All-day entries are matched using the dates of the range widened by one day each side (over-fetch is harmless; the grid filters). |
| `GET /calendar/entries/{id}` | `200 Entry` | Not found, other owner, other tenant, or denied ⇒ `404 not_found` (same body). |
| `POST /calendar/entries` | `201 { "id", "rowVersion", "replayed" }` | Header `Idempotency-Key` required (non-blank, ≤ 128 chars). Body below. `Location: /calendar/entries/{id}`. |
| `PUT /calendar/entries/{id}` | `200 { "id", "rowVersion", "replayed" }` | Full replace (every field, `link` included: omitted/`null` link clears it). Body + `expectedVersion`. `Idempotency-Key` required. |
| `DELETE /calendar/entries/{id}?expectedVersion=N` | `204` | `Idempotency-Key` required. Hard delete. |

### Create / replace body

```json
{
  "title": "Call with vendor",
  "notes": null,
  "color": "#3b82f6",
  "allDay": false,
  "startAt": "2026-09-21T09:00:00+03:00",
  "endAt": null,
  "startDate": null,
  "endDate": null,
  "link": { "boundedContext": "crm", "entityType": "opportunity", "id": 17 },
  "expectedVersion": 3
}
```

- `expectedVersion` is required on `PUT` only. Timed values **must** carry an offset (`Z` or `±hh:mm`); an offset-less
  timed value is `400 validation_error`.
- `title`: trimmed single line, 1–200 chars (an untrimmed or multi-line title is `400`). `notes` ≤ 4000 chars.
  `color`: `#rrggbb` (case-insensitive on input).
- `link` (optional): `{ boundedContext, entityType, id }`. v1 supported targets: `crm/opportunity`, `masterdata/party`.
  Unknown type, non-existent target, other-tenant target and unauthorized target all return the SAME
  `422 link_target_unavailable` (no existence oracle).

## Errors

`application/problem+json`-style body `{ "status": number, "type": string, "title": string }` (the existing Host
`ProblemDetails` shape used by CRM). `type` values:

| Status | `type` | When |
|---|---|---|
| 400 | `validation_error` | invalid/missing field, offset-less timed value, bad range order, missing/oversized `Idempotency-Key`, malformed `expectedVersion` |
| 401 | (framework) | unauthenticated |
| 403 | `forbidden` | the caller lacks the collaboration capability for the action (coarse denial) |
| 404 | `not_found` | entry absent / not the caller's / other tenant (indistinguishable) |
| 409 | `concurrency_conflict` | `expectedVersion` is stale |
| 409 | `idempotency_key_reused` | same key with a different request body |
| 422 | `range_too_large` | list window > 100 days or > 500 matches |
| 422 | `link_target_unavailable` | any link that cannot be resolved for this actor |

Replays (same `Idempotency-Key` + same body) return the original result with `replayed: true`.

## Navigation mapping (frontend)

`crm/opportunity` → `/crm/opportunities/:id`. Every other type (including `masterdata/party` in v1) has no route:
show the label, no link.
