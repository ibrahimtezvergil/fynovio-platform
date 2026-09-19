import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Skeleton } from '@/components/ui/skeleton'

/**
 * Skeletons for the shapes this panel actually renders. Each one is drawn
 * from the component it stands in for — the metric tile's 32px figure, the
 * grid's 52px row, the activity list's 30px tile — so the swap to real data
 * moves nothing on the page.
 *
 * The wrapper carries the live-region attributes; the blocks themselves are
 * `aria-hidden` (see `Skeleton`), so a screen reader hears "Yükleniyor" once
 * instead of counting rectangles.
 */
export function LoadingRegion({
  label,
  children,
}: {
  label: string
  children: ReactNode
}) {
  return (
    <div role="status" aria-busy="true" aria-live="polite">
      <span className="sr-only">{label}</span>
      {children}
    </div>
  )
}

/** Four KPI tiles — mirrors `MetricCard`: mark, label, figure, delta line. */
export function MetricGridSkeleton({ count = 4 }: { count?: number }) {
  return (
    <div className="grid gap-3 p-4 sm:grid-cols-2">
      {Array.from({ length: count }).map((_, i) => (
        <div
          key={i}
          className="flex flex-col gap-[13px] rounded-xl border border-[var(--nx-hairline)] px-[21px] py-[19px]"
        >
          <div className="flex items-center gap-2.5">
            <Skeleton className="size-[26px] rounded-tile" />
            <Skeleton className="h-3 w-24" />
          </div>
          <Skeleton className="h-8 w-32 rounded-md" />
          <div className="flex items-center gap-2">
            <Skeleton className="h-[22px] w-14 rounded-full" />
            <Skeleton className="h-3 w-20" />
          </div>
        </div>
      ))}
    </div>
  )
}

/**
 * A data grid mid-load. The header stays solid: column names are known
 * before the rows are, and blanking them out loses the one piece of
 * structure the reader could already have been reading.
 */
export function TableSkeleton({ rows = 5 }: { rows?: number }) {
  const { t } = useTranslation('demo-states')
  const widths = ['w-[38%]', 'w-[26%]', 'w-[30%]', 'w-[22%]', 'w-[34%]']

  return (
    <div className="flex flex-col">
      <div className="text-muted-foreground flex items-center gap-4 border-b border-[var(--nx-hairline)] px-4 py-2.5 text-[11px] font-[590] tracking-[0.02em] uppercase">
        <span className="flex-1">{t('tableHeaders.company')}</span>
        <span className="w-28">{t('tableHeaders.stage')}</span>
        <span className="w-24 text-right">{t('tableHeaders.amount')}</span>
      </div>
      {Array.from({ length: rows }).map((_, i) => (
        <div
          key={i}
          className="flex h-[52px] items-center gap-4 border-b border-[var(--nx-hairline-soft)] px-4 last:border-b-0"
        >
          <Skeleton className={`h-3.5 flex-none ${widths[i % widths.length]}`} />
          <div className="flex-1" />
          <Skeleton className="h-[22px] w-24 rounded-full" />
          <Skeleton className="ml-auto h-3.5 w-20" />
        </div>
      ))}
    </div>
  )
}

/** The activity list: 30px tile, a title line, a shorter meta line. */
export function ListSkeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className="flex flex-col">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="nx-row px-4">
          <Skeleton className="size-[30px] rounded-sm" />
          <div className="flex flex-1 flex-col gap-1.5">
            <Skeleton className="h-3 w-[62%]" />
            <Skeleton className="h-2.5 w-[38%]" />
          </div>
          <Skeleton className="h-3 w-12" />
        </div>
      ))}
    </div>
  )
}

/**
 * A chart placeholder is bars, never a grey rectangle: the reader learns the
 * shape of the answer — a series over a time axis — before the numbers land.
 */
export function ChartSkeleton() {
  const bars = [46, 68, 38, 82, 58, 74, 50]

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-center justify-between">
        <Skeleton className="h-3.5 w-32" />
        <Skeleton className="h-[26px] w-24 rounded-full" />
      </div>
      <div className="flex h-[128px] items-end gap-2.5">
        {bars.map((height, i) => (
          <Skeleton key={i} className="flex-1 rounded-t-sm" style={{ height: `${height}%` }} />
        ))}
      </div>
      <div className="flex gap-2.5">
        {bars.map((_, i) => (
          <Skeleton key={i} className="h-2 flex-1" />
        ))}
      </div>
    </div>
  )
}

/** Label-and-field pairs at the real control height (`--nx-control-height`). */
export function FormSkeleton({ fields = 3 }: { fields?: number }) {
  return (
    <div className="flex flex-col gap-4 p-4">
      {Array.from({ length: fields }).map((_, i) => (
        <div key={i} className="flex flex-col gap-2">
          <Skeleton className="h-2.5 w-24" />
          <Skeleton className="h-control w-full rounded-md" />
        </div>
      ))}
      <div className="flex justify-end gap-2 pt-1">
        <Skeleton className="h-control w-20 rounded-md" />
        <Skeleton className="h-control w-28 rounded-md" />
      </div>
    </div>
  )
}

/** Identity block: avatar, name, one meta line, two chips. */
export function ProfileSkeleton() {
  return (
    <div className="flex items-center gap-3.5 p-4">
      <Skeleton className="size-12 rounded-full" />
      <div className="flex flex-1 flex-col gap-2">
        <Skeleton className="h-3.5 w-40" />
        <Skeleton className="h-2.5 w-52" />
        <div className="mt-0.5 flex gap-2">
          <Skeleton className="h-[22px] w-16 rounded-full" />
          <Skeleton className="h-[22px] w-20 rounded-full" />
        </div>
      </div>
    </div>
  )
}

/** A paragraph. The last line is short — a full-width last line reads as a bar. */
export function TextSkeleton({ lines = 4 }: { lines?: number }) {
  return (
    <div className="flex flex-col gap-2.5 p-4">
      {Array.from({ length: lines }).map((_, i) => (
        <Skeleton
          key={i}
          className={`h-2.5 ${i === lines - 1 ? 'w-[45%]' : 'w-full'}`}
        />
      ))}
    </div>
  )
}
