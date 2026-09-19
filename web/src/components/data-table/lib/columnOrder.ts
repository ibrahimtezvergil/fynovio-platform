/**
 * Merges a possibly partial or stale order over the full id set, appending
 * any id the order doesn't mention at the end — the same merge
 * `columnOrderingFeature` does internally (unspecified columns keep their
 * original relative order), so a menu built from this stays in sync with
 * what the grid actually renders.
 */
export function normalizeColumnOrder(order: readonly string[], allIds: readonly string[]): string[] {
  const known = new Set(allIds)
  const seen = new Set<string>()
  const ordered: string[] = []
  for (const id of order) {
    if (known.has(id) && !seen.has(id)) {
      ordered.push(id)
      seen.add(id)
    }
  }
  for (const id of allIds) {
    if (!seen.has(id)) ordered.push(id)
  }
  return ordered
}

/**
 * Swaps `columnId` with its next neighbour in `direction`, skipping over any
 * id not in `movableIds` — a structural column (the selection checkbox)
 * never changes position and never gets swapped into.
 *
 * Returns the order unchanged if `columnId` is already at the edge of its
 * movable neighbours.
 */
export function moveColumnOrder(
  order: readonly string[],
  allIds: readonly string[],
  movableIds: readonly string[],
  columnId: string,
  direction: -1 | 1,
): string[] {
  const normalized = normalizeColumnOrder(order, allIds)
  const index = normalized.indexOf(columnId)
  if (index === -1) return normalized

  let target = index + direction
  while (target >= 0 && target < normalized.length && !movableIds.includes(normalized[target]!)) {
    target += direction
  }
  if (target < 0 || target >= normalized.length) return normalized

  const next = [...normalized]
  ;[next[index], next[target]] = [next[target]!, next[index]!]
  return next
}
