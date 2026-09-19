import type { ReactNode } from 'react'
import { Card } from '@/components/ui/card'
import { cn } from '@/lib/utils'

interface ChartCardProps {
  /** Doubles as the anchor the section nav scrolls to. */
  id?: string
  title: string
  description: string
  /** The one figure the chart is there to support, printed where the eye lands first. */
  hero?: { value: string; label: string }
  /** What the chart is for, in CRM/ERP terms — the reason it earns a place. */
  note?: ReactNode
  className?: string
  children: ReactNode
}

/**
 * One chart, one card. The heading names the single series when there is only
 * one, which is why those charts carry no legend box.
 */
export function ChartCard({
  id,
  title,
  description,
  hero,
  note,
  className,
  children,
}: ChartCardProps) {
  return (
    <Card id={id} className={cn('scroll-mt-24 gap-4 px-5 pt-[19px] pb-5', className)}>
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h3 className="font-heading text-[15.5px] leading-snug font-[620] tracking-[-0.022em]">
            {title}
          </h3>
          <p className="text-muted-foreground mt-1 text-[12.5px] leading-[1.5]">{description}</p>
        </div>
        {hero && (
          <div className="flex shrink-0 flex-col items-end">
            <span className="font-heading tnum text-[22px] leading-none font-semibold tracking-[-0.03em]">
              {hero.value}
            </span>
            <span className="text-muted-foreground mt-1.5 text-[11.5px]">{hero.label}</span>
          </div>
        )}
      </div>

      {children}

      {note && (
        <p className="text-muted-foreground border-border/70 border-t pt-3 text-[12px] leading-[1.5]">
          {note}
        </p>
      )}
    </Card>
  )
}
