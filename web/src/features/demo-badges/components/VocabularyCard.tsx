import { StatusBadge } from '@/components/common/StatusBadge'
import type { Vocabulary } from '@/features/demo-badges/data/registries'

/**
 * One domain vocabulary, in display order. The order is part of the
 * definition: a set drawn alphabetically here and by process order in a
 * filter teaches two different mental models of the same field.
 */
export function VocabularyCard({ vocabulary }: { vocabulary: Vocabulary }) {
  return (
    <div className="flex flex-col gap-2.5 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4">
      <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <p className="text-[13px] leading-5 font-[590]">{vocabulary.label}</p>
        <code className="text-brand-graphic rounded-sm bg-[var(--nx-tint-fill)] px-1.5 py-0.5 font-mono text-[10.5px]">
          {vocabulary.entries.length} durum
        </code>
      </div>
      <p className="text-muted-foreground -mt-1 text-[11.5px] leading-4">
        {vocabulary.description}
      </p>
      <ul className="flex flex-wrap gap-1.5 pt-0.5">
        {vocabulary.entries.map((entry) => (
          <li key={entry.key}>
            <StatusBadge label={entry.label} tone={entry.tone} icon={entry.icon} />
          </li>
        ))}
      </ul>
    </div>
  )
}
