import { ArrowRight } from 'lucide-react'

type DiffRecord = Record<string, unknown>

export interface ObjectDiffProps {
  before: DiffRecord
  after: DiffRecord
  /** Maps technical field names to a reader-facing label. */
  labels?: Record<string, string>
}

function display(value: unknown): string {
  if (value === null || value === undefined || value === '') return '—'
  if (typeof value === 'string' || typeof value === 'number') return String(value)
  if (typeof value === 'boolean') return value ? 'Evet' : 'Hayır'
  return JSON.stringify(value)
}

/**
 * Field-level before/after presentation. It intentionally accepts plain
 * objects, so audit entries, dry-runs and comparison views share one visual
 * language without sharing domain types.
 */
export function ObjectDiff({ before, after, labels = {} }: ObjectDiffProps) {
  const keys = [...new Set([...Object.keys(before), ...Object.keys(after)])].filter(
    (key) => JSON.stringify(before[key]) !== JSON.stringify(after[key]),
  )
  if (keys.length === 0) return null

  return (
    <dl className="flex flex-col divide-y divide-[var(--nx-hairline-soft)]">
      {keys.map((key) => (
        <div key={key} className="flex flex-wrap items-center gap-x-2 gap-y-1 py-2 text-[11.5px]">
          <dt className="text-muted-foreground mr-auto">{labels[key] ?? key}</dt>
          <dd data-tone="gray" className="nx-pill h-[22px] max-w-full truncate px-2 line-through decoration-current/50">{display(before[key])}</dd>
          <ArrowRight aria-hidden className="text-muted-foreground size-3 shrink-0" strokeWidth={2} />
          <dd data-tone="blue" className="nx-pill h-[22px] max-w-full truncate px-2">{display(after[key])}</dd>
        </div>
      ))}
    </dl>
  )
}
