import { useCallback, useEffect, useState } from 'react'
import type { Control, FieldValues, UseFormReset } from 'react-hook-form'
import { useWatch } from 'react-hook-form'
import { useBlocker, type Blocker } from 'react-router-dom'

export interface UseDraftGuardOptions<T extends FieldValues> {
  /** Unique per route + form — becomes the localStorage key and the `useBlocker` scope. */
  key: string
  control: Control<T>
  isDirty: boolean
  reset: UseFormReset<T>
  debounceMs?: number
}

export interface UseDraftGuardResult {
  /** `state === 'blocked'` while an in-flight navigation is waiting on `proceed()`/`reset()`. */
  blocker: Blocker
  /** A draft from a previous visit is sitting in storage, not yet restored or discarded. */
  hasSavedDraft: boolean
  restoreDraft: () => void
  discardDraft: () => void
}

function storageKey(key: string): string {
  return `draft:${key}`
}

/** `JSON.stringify`'s ISO-8601 coercion of `Date`, reversed on the way back in. */
const ISO_DATETIME = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$/

export function reviveDates(_key: string, value: unknown): unknown {
  return typeof value === 'string' && ISO_DATETIME.test(value) ? new Date(value) : value
}

function readDraft<T>(key: string): T | null {
  try {
    const raw = window.localStorage.getItem(storageKey(key))
    return raw === null ? null : (JSON.parse(raw, reviveDates) as T)
  } catch {
    // Private-mode storage, quota, or a corrupt entry — treat as "no draft".
    return null
  }
}

function writeDraft(key: string, values: unknown): void {
  try {
    window.localStorage.setItem(storageKey(key), JSON.stringify(values))
  } catch {
    // Best-effort: a full or blocked store silently drops the autosave.
  }
}

function clearDraft(key: string): void {
  try {
    window.localStorage.removeItem(storageKey(key))
  } catch {
    // Nothing to reconcile — worst case the stale entry lingers.
  }
}

/**
 * A local, per-form draft (#23, Claude Task C): debounced autosave to
 * `localStorage` while the form is dirty, a navigation-away confirmation via
 * `useBlocker`, and an explicit restore prompt on the next visit. Cross-device
 * sync needs a backend and is out of scope — see `ENTERPRISE_LAYERS_ASSESSMENT.md`.
 *
 * Mount this once per form, alongside the `useForm()` call it guards:
 * `useDraftGuard({ key: 'demo-forms/quote', control, isDirty, reset })`.
 */
export function useDraftGuard<T extends FieldValues>({
  key,
  control,
  isDirty,
  reset,
  debounceMs = 500,
}: UseDraftGuardOptions<T>): UseDraftGuardResult {
  const values = useWatch({ control })
  const [hasSavedDraft, setHasSavedDraft] = useState(() => readDraft<T>(key) !== null)

  useEffect(() => {
    if (!isDirty) return
    const timer = setTimeout(() => {
      writeDraft(key, values)
      setHasSavedDraft(true)
    }, debounceMs)
    return () => clearTimeout(timer)
  }, [values, isDirty, key, debounceMs])

  const restoreDraft = useCallback(() => {
    const draft = readDraft<T>(key)
    if (draft === null) return
    reset(draft)
    clearDraft(key)
    setHasSavedDraft(false)
  }, [key, reset])

  const discardDraft = useCallback(() => {
    clearDraft(key)
    setHasSavedDraft(false)
  }, [key])

  // Re-evaluated on every render is intentional: `isDirty` already reflects
  // the current form state, so the blocker's predicate should too.
  const blocker = useBlocker(useCallback(() => isDirty, [isDirty]))

  return { blocker, hasSavedDraft, restoreDraft, discardDraft }
}
