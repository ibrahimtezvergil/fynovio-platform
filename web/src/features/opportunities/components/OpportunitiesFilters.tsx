import { CalendarClock, ChevronDown } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { DensityToggle } from '@/components/common/DensityToggle'
import { Toolbar, ToolbarGroup, ToolbarSearch, ToolbarSpacer } from '@/components/common/Toolbar'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import type { CustomFieldDefinition } from '@/lib/custom-fields/schema'
import type { RowFilters } from '../lib/rows'
import { CustomFieldFilterPills } from './CustomFieldFilterPills'

/**
 * The pill reads tinted exactly while it is narrowing the result set. `size="sm"` is a fixed step in the button
 * scale, so the height follows the density token — the pills sit one step under the search box at both densities.
 */
function pill(active: boolean) {
  return {
    variant: active ? ('tinted' as const) : ('secondary' as const),
    size: 'sm' as const,
    className: 'h-[var(--nx-d-control-sm)] rounded-[var(--nx-r-pill)]',
  }
}

interface OpportunitiesFiltersProps {
  filters: RowFilters
  onChange: (next: RowFilters) => void
  /** Assignees present on the loaded page — the list contract has no owner lookup to offer more. */
  owners: readonly string[]
  /** Density only resizes the grid, so the board view leaves the toggle out. */
  showDensity?: boolean
  /** Server-side custom field filters (field key → option key or 'true'/'false'). */
  fieldFilters?: {
    definitions: readonly CustomFieldDefinition[]
    value: Readonly<Record<string, string>>
    onChange: (next: Record<string, string>) => void
  }
}

/** Search and filter pills over the loaded page (status, paging and custom field filters are the server's). */
export function OpportunitiesFilters({ filters, onChange, owners, showDensity = true, fieldFilters }: OpportunitiesFiltersProps) {
  const { t } = useTranslation('opportunities')

  return (
    <Toolbar>
      <ToolbarSearch value={filters.query} onChange={(query) => onChange({ ...filters, query })} placeholder={t('list.filters.searchPlaceholder')} />

      <ToolbarGroup>
        <DropdownMenu>
          <DropdownMenuTrigger
            render={
              <Button {...pill(filters.owner !== 'all')}>
                {t('list.filters.ownerPrefix')} {filters.owner === 'all' ? t('list.filters.all') : filters.owner}
                <ChevronDown aria-hidden strokeWidth={1.7} />
              </Button>
            }
          />
          <DropdownMenuContent className="w-64">
            <DropdownMenuRadioGroup value={filters.owner} onValueChange={(owner) => onChange({ ...filters, owner })}>
              <DropdownMenuRadioItem value="all">{t('list.filters.all')}</DropdownMenuRadioItem>
              {owners.length > 0 && <DropdownMenuSeparator />}
              {owners.map((owner) => (
                <DropdownMenuRadioItem key={owner} value={owner}>
                  <code className="truncate text-[12px]">{owner}</code>
                </DropdownMenuRadioItem>
              ))}
            </DropdownMenuRadioGroup>
          </DropdownMenuContent>
        </DropdownMenu>

        <Button
          {...pill(filters.quarterOnly)}
          aria-pressed={filters.quarterOnly}
          onClick={() => onChange({ ...filters, quarterOnly: !filters.quarterOnly })}
        >
          <CalendarClock aria-hidden strokeWidth={1.7} />
          {t('list.filters.closeWindowPrefix')} {filters.quarterOnly ? t('list.filters.thisQuarter') : t('list.filters.all')}
        </Button>

        {fieldFilters && <CustomFieldFilterPills {...fieldFilters} pillProps={pill} />}
      </ToolbarGroup>

      <ToolbarSpacer />

      {showDensity && <DensityToggle />}
    </Toolbar>
  )
}
