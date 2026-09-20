import { useState } from 'react'
import type { ApiError } from '@/types'

/** Where the server's answer leaves a logical action: settled (release the key) or still unknown (keep it). */
export function isDefinitiveOutcome(error: ApiError | null | undefined): boolean {
  if (!error) return true
  // status 0 = network failure / timeout, 5xx = the server may or may not have committed: the outcome is unknown.
  // 408 and 429 mean the request was not processed, but retrying the same logical action is still the same action.
  if (error.status === 0 || error.status >= 500 || error.status === 408 || error.status === 429) return false
  return true
}

/**
 * The lifecycle of one idempotency key per logical user action (Phase 2 convention: tenant + principal + operation + key).
 *
 * - A key is minted when an action starts and HELD while its outcome is unknown, so the user's retry of
 *   the same payload after a network failure reuses it and the server replays instead of duplicating.
 * - It is RELEASED on a definitive answer (success or a 4xx).
 * - A different payload always gets a new key: the same key with a different body is a 409 `idempotency_key_reused`.
 *
 * The key is handed to the request explicitly at call time — never minted in an axios interceptor — so the
 * 401 → refresh → replay path resends the identical header.
 */
export class AttemptKeys {
  private held: { signature: string; key: string } | null = null
  private readonly mint: () => string

  constructor(mint: () => string = defaultMint) {
    this.mint = mint
  }

  begin(payload: unknown): string {
    const signature = JSON.stringify(payload)
    if (this.held?.signature === signature) return this.held.key
    this.held = { signature, key: this.mint() }
    return this.held.key
  }

  settle(error: ApiError | null | undefined): void {
    if (isDefinitiveOutcome(error)) this.held = null
  }
}

function defaultMint(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(16)}-${Math.random().toString(16).slice(2)}`
}

/** One `AttemptKeys` per mounted form/dialog. */
export function useAttemptKeys(): AttemptKeys {
  const [keys] = useState(() => new AttemptKeys())
  return keys
}
