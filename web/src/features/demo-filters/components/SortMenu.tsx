import { ArrowDownWideNarrow, ArrowUpNarrowWide, ArrowUpDown, Plus, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Separator } from '@/components/ui/separator'
import {
  SORT_DESC_FIRST,
  SORT_FIELDS,
  useSortLabels,
  type SortField,
  type SortRule,
} from '@/features/demo-filters/types'

interface SortMenuProps {
  rules: readonly SortRule[]
  onChange: (next: SortRule[]) => void
}

/**
 * Multi-sort, with the priority order visible.
 *
 * A grid that sorts by two fields but only ever shows one arrow is lying
 * about its own order. Numbering the rules — and letting them be reordered by
 * removal rather than by drag — is the cheapest honest version of that.
 */
export function SortMenu({ rules, onChange }: SortMenuProps) {
  const { t } = useTranslation('demo-filters')
  const sortLabels = useSortLabels()
  const used = new Set(rules.map((rule) => rule.field))
  const available = SORT_FIELDS.filter((field) => !used.has(field))
  const primary = rules[0]

  const toggleDirection = (field: SortField) =>
    onChange(
      rules.map((rule) =>
        rule.field === field
          ? { ...rule, direction: rule.direction === 'asc' ? 'desc' : 'asc' }
          : rule,
      ),
    )

  return (
    <Popover>
      <PopoverTrigger
        render={
          <Button variant={rules.length > 0 ? 'tinted' : 'secondary'} size="sm" className="rounded-[var(--nx-r-pill)]" />
        }
      >
        <ArrowUpDown aria-hidden strokeWidth={1.7} />
        {primary
          ? t('sortMenu.summary', {
              field: sortLabels[primary.field],
              arrow: primary.direction === 'asc' ? '↑' : '↓',
            })
          : t('sortMenu.label')}
        {rules.length > 1 && <Badge variant="secondary">+{rules.length - 1}</Badge>}
      </PopoverTrigger>

      <PopoverContent align="end" className="w-[312px] gap-3 p-4">
        <div className="flex items-center gap-2">
          <p className="flex-1 text-[13px] font-[590]">{t('sortMenu.rulesHeading')}</p>
          <Button
            variant="ghost"
            size="xs"
            disabled={rules.length === 0}
            onClick={() => onChange([])}
          >
            {t('sortMenu.reset')}
          </Button>
        </div>

        {rules.length === 0 ? (
          <p className="text-muted-foreground text-[12px] leading-[1.5]">
            {t('sortMenu.noRules')}
          </p>
        ) : (
          <ol className="flex flex-col gap-1.5">
            {rules.map((rule, index) => (
              <li key={rule.field} className="flex items-center gap-2">
                <span
                  aria-hidden
                  className="nx-avatar size-[18px] rounded-sm text-[10px] font-[700]"
                >
                  {index + 1}
                </span>
                <span className="min-w-0 flex-1 truncate text-[12.5px] font-[550]">
                  <span className="sr-only">{t('sortMenu.priority', { index: index + 1 })} </span>
                  {sortLabels[rule.field]}
                </span>
                <Button
                  variant="ghost"
                  size="icon-xs"
                  aria-label={t('sortMenu.toggleDirection', {
                    field: sortLabels[rule.field],
                    direction:
                      rule.direction === 'asc' ? t('sortMenu.ascending') : t('sortMenu.descending'),
                  })}
                  onClick={() => toggleDirection(rule.field)}
                >
                  {rule.direction === 'asc' ? (
                    <ArrowUpNarrowWide strokeWidth={1.8} />
                  ) : (
                    <ArrowDownWideNarrow strokeWidth={1.8} />
                  )}
                </Button>
                <Button
                  variant="ghost"
                  size="icon-xs"
                  aria-label={t('sortMenu.removeRule', { field: sortLabels[rule.field] })}
                  onClick={() => onChange(rules.filter((entry) => entry.field !== rule.field))}
                >
                  <X strokeWidth={1.8} />
                </Button>
              </li>
            ))}
          </ol>
        )}

        {available.length > 0 && (
          <>
            <Separator />
            <div className="flex flex-wrap gap-1.5">
              {available.map((field) => (
                <Button
                  key={field}
                  variant="outline"
                  size="xs"
                  onClick={() =>
                    onChange([
                      ...rules,
                      { field, direction: SORT_DESC_FIRST[field] ? 'desc' : 'asc' },
                    ])
                  }
                >
                  <Plus strokeWidth={2} />
                  {sortLabels[field]}
                </Button>
              ))}
            </div>
          </>
        )}
      </PopoverContent>
    </Popover>
  )
}
