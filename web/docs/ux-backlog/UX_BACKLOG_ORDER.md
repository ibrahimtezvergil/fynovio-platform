# UX Backlog — Execution Order (all 31 items)

Sequenced build queue derived from `UX_BACKLOG_ASSESSMENT.md`. Ordered by
buildability first (no backend blockers before backend-gated work), then
value ÷ effort and dependency order within each phase. Work top to bottom.

## Phase 1 — Build now, ★ must-have, zero backend blockers

| Order | # | Item | Notes |
| --- | --- | --- | --- |
| 1 | 4 | Peek Drawer | Done — `ed6f10c`: shared peek drawer + Pipeline row-click wiring. |
| 2 | 22 | Instant Drill-Through | Done — `8a5d4cb`: dashboard stage distribution opens Pipeline with a URL-carried stage filter. |
| 3 | 1 | Command Palette | Done — `ce14cc6`: registry-backed command palette. |
| 4 | 3 | Contextual Bulk Action Bar | Done — `50eae22`, extended by `61f2865`: selection-driven pipeline bulk actions. |
| 5 | 14 | Before/After Diff | Done — `18a8419`: shared field-level object diff, used by the audit timeline. |
| 6 | 5 | Multi-View Switcher | Done — `ee9c36a`: table and board projections share Pipeline's filtered dataset and switcher. |
| 7 | 6 | Keyboard-First Nav | Done — `3fb42aa`: prioritized, input-safe global registry; existing chords match `event.code`. |
| 8 | 17 | Next Record Auto-Advance | Done — `e410ad3`: completing a deal from its peek advances to the next visible record. |
| 9 | 2 | Saved Table Views (personal) | Done — `6947352`: column order and persisted personal views. Team-shared views wait for BE. |
| 10 | 27 | Context-Aware Smart Defaults (FE half) | Done — `4411636`: persisted personal last-used defaults feed RHF defaults. |

## Phase 2 — Build the pattern/shell now, ★ must-have, true only after BE

| Order | # | Item | Notes |
| --- | --- | --- | --- |
| 11 | 7 | Universal Undo | Optimistic mutation + `sonner` undo toast now; durable reversal is BE. Every later mutation should inherit this pattern. |
| 12 | 16 | Focus Queue / Work Inbox | Build over merged mock sources now; let the shell dictate the "assigned to me, ranked" API ask. |
| 13 | 20 | Exception-First UX | Threshold rules over mock data now; real anomaly detection is BE. |

## Phase 3 — Build now, ☆ valuable, no dependency blockers

| Order | # | Item | Notes |
| --- | --- | --- | --- |
| 14 | 12 | Smart Paste | Pure clipboard/TSV parsing on `LineItemsEditor`. Zero backend. |
| 15 | 30 | Cross-Entity Compare Mode | Reuses #14's diff engine — do after #14. |
| 16 | 31 | Action Memory (Repeat / Smart Duplicate) | FE-only 2 of 3 sub-features; Calculation Inspector stays FE only while pricing math stays client-side. |
| 17 | 29 | Temporary Workspace / Scratchpad | Persisted pinned-entity dock strip. |
| 18 | 8 | Focus / Zen Mode | Pure layout state, same precedent as `sidebarCollapsed`. Lowest effort, lowest value — fine to slot last in this phase. |

## Phase 4 — Build the shell now, ☆ valuable, true only after BE

| Order | # | Item | Notes |
| --- | --- | --- | --- |
| 19 | 28 | Adaptive / Progressive Forms | Stage-driven field visibility works now (zod discriminated unions); role-driven adaptation needs real permissions → BE. |
| 20 | 9 | Predictive Next Action | Deterministic rule table now; learned ranking is BE. |
| 21 | 10 | One-Key Workflow Completion | Ship the ⌘Enter keybinding + validate/submit now; the cross-entity chain (advance + task + notify) is a BE transaction. |
| 22 | 15 | Ghost Save / Autosave | Debounced save-state chip now; localStorage drafts are per-device, be explicit about that limit. |

## Phase 5 — Design the contract now, screen waits for BE

| Order | # | Item | Notes |
| --- | --- | --- | --- |
| 23 | 25 | What Changed Since My Last Visit? | "Last visit" timestamp is FE (persisted zustand) — build that half now. The change-set itself needs a real audit log. |
| 24 | 13 | Transaction Preview / Dry Run | Design the `POST /…/dry-run` response shape now; the before/after table renders once the endpoint exists. |
| 25 | 21 | Explain This Number | Design the waterfall panel now; needs a server-side decomposition query to fill it. |

## Phase 6 — Parked, backend product with a thin UI, nothing to gain starting FE-only

| Order | # | Item | Notes |
| --- | --- | --- | --- |
| 26 | 19 | Semantic / Natural-Language Filters | Needs a server-side LLM call. Keep `demo-filters/lib/query.ts` serializable as prep. |
| 27 | 11 | Intent-Based Data Entry | Same LLM dependency as #19; a hand-rolled parser would be throwaway work. |
| 28 | 18 | Batch Workflow Studio | Condition-builder UI is FE, but needs a server-side job runner + audit trail to mean anything. |
| 29 | 23 | Data Provenance / Number Lineage | Metadata pipeline problem, not a UI problem. |
| 30 | 24 | Time-Travel Record | Needs versioned/event-sourced records. No honest FE-only version. |
| 31 | 26 | Conflict-Aware Collaborative Editing | Needs a realtime channel + server-side conflict detection. |

---

Start Codex at Phase 1, item 1. Within a phase, order is not strict — the
table order reflects value ÷ effort and stated dependencies (e.g. #30 after
#14, #17 after #4/#6), but items in the same phase can be reshuffled without
breaking anything downstream. Phase 6 items should not be started at all
until the corresponding backend capability exists — see "What to ask the
backend for" in `UX_BACKLOG_ASSESSMENT.md`.
