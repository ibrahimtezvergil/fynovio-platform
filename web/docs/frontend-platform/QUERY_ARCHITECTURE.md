# Query Architecture Conventions

Companion to [`ENTERPRISE_LAYERS_ASSESSMENT.md`](./ENTERPRISE_LAYERS_ASSESSMENT.md) (#10, Claude
Task E). Zero new code — this writes down a convention four feature slices already follow by
coincidence: `pipelineKeys` (`src/features/pipeline/api.ts`), `dashboardKeys`
(`src/features/dashboard/api.ts`), `calendarKeys` (`src/features/calendar/api.ts`), and
`demoTableKeys` (`src/features/demo-tables/data/api.ts`). Follow it for every new feature's
`api.ts` rather than inventing a shape per feature.

## Key factory shape

One `const <feature>Keys` object per feature, colocated with its `useQuery`/`useMutation` hooks
in `api.ts`. Every leaf extends the same `all` root, so `invalidateQueries({ queryKey:
xKeys.all })` always invalidates the whole feature:

```ts
export const pipelineKeys = {
  all: ['pipeline'] as const,
  deals: () => [...pipelineKeys.all, 'deals'] as const,
}
```

For a resource with several dependent lists, nest one level deeper before the leaf, as
`dashboardKeys` does (`deals()`, `stages()`, `activities()`, all off one `all`) and as
`demoTableKeys` does (`employees()` under `all`, then `list()` and `page(request)` under
`employees()`). A parameterised leaf takes the request object itself as the key segment
(`page: (request: EmployeePageRequest) => [...demoTableKeys.employees(), 'page', request]`) —
React Query serializes it, so no separate cache-key builder is needed. Don't skip the
intermediate level for a single-resource feature (`pipelineKeys`, `calendarKeys`) — add it only
when a second list actually exists, mirroring `dashboardKeys`.

Export the keys object; other modules (mutations, prefetch calls, tests) reference it instead of
writing array literals.

## Stale-time tiers

Global default lives in `src/api/queryClient.ts`: `staleTime: 60_000, retry: 1,
refetchOnWindowFocus: false`. Two tiers exist today:

- **Default (60s)** — anything that changes during a session: deals, calendar events, dashboard
  metrics. Leave `staleTime` unset and let the global default apply.
- **Reference-ish data (5 min)** — data that's expensive to refetch and rarely changes inside one
  session. `useAllEmployees` sets `staleTime: 5 * 60 * 1000` (`src/features/demo-tables/data/api.ts`)
  for exactly this reason. Set `staleTime` explicitly on the query when you're in this tier; don't
  change the global default for one feature.

There is no third "live" tier in the codebase yet — that's #16 (Real-Time Event Layer),
BE-gated. When it lands, a live query sets `staleTime: 0` and relies on the event layer for
invalidation rather than polling; don't invent this early.

## Invalidation / cache-write convention

Two patterns, chosen by whether the mutation's result is already the full next state:

- **Direct `setQueryData`** — when the mutation handler already computes the exact next cache
  value (a client-side patch, a local move), write it directly instead of invalidating and
  refetching. `useUpdateDeals`, `useRemoveDeals` (`pipeline/api.ts`) and `useMoveCalendarEvent`
  (`calendar/api.ts`) all do this: `onSuccess` (or the handler itself, for the non-mutation
  calendar case) calls `queryClient.setQueryData<T>(xKeys.leaf(), updater)`.
- **`invalidateQueries`** — once a mutation talks to a real backend and the server may have
  changed more than the fields the client sent (computed fields, side effects on other rows),
  invalidate the affected key(s) instead of hand-rolling the merge. Prefer the narrowest key that
  covers what changed (`xKeys.deals()`, not `xKeys.all`) unless the mutation is known to affect
  every list under the feature.

`useAppMutation` (Claude Task B, #12) centralizes the second pattern behind one wrapper; until
that lands, call `queryClient.invalidateQueries` directly in `onSuccess`.

## Prefetch-on-hover

No feature does this yet. When adding it (typically a row or nav link that opens a detail view),
prefetch on hover/focus, not on mount:

```ts
<Link
  to={dealPath(deal.id)}
  onMouseEnter={() =>
    queryClient.prefetchQuery({ queryKey: pipelineKeys.deals(), queryFn: fetchDeal })
  }
>
```

Reuse the same key factory and `queryFn` the corresponding `useQuery` hook uses — a prefetch with
a different key just populates a cache entry nothing will read. Guard against duplicate work with
React Query's own dedup (a `prefetchQuery` against a key already in flight or fresh is a no-op) —
don't add a manual "already prefetched" flag.
