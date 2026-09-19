import { TrendingDown, TrendingUp, type LucideIcon } from 'lucide-react'
import { Card } from '@/components/ui/card'

interface MetricCardProps {
  label: string
  value: string
  context?: string
  icon?: LucideIcon
  delta?: { value: string; direction: 'up' | 'down' }
  /** 0–100. Renders the accent meter in place of the context line. */
  progress?: number
}

/**
 * The figure is the card. The delta pill is the only saturated element;
 * everything else is the label ramp. Figures are always tabular.
 */
export function MetricCard({
  label,
  value,
  context,
  icon: Icon,
  delta,
  progress,
}: MetricCardProps) {
  const TrendIcon = delta?.direction === 'down' ? TrendingDown : TrendingUp

  return (
    <Card className="nx-lift gap-[13px] px-[21px] py-[19px]">
      <div className="flex items-center gap-2.5">
        {Icon && (
          <span aria-hidden className="nx-icon-tile size-[26px] rounded-[var(--nx-r-tile)]">
            <Icon className="size-[15px]" strokeWidth={1.7} />
          </span>
        )}
        <p className="text-muted-foreground text-[12.5px] font-[550]">{label}</p>
      </div>

      <p className="font-heading tnum text-[32px] leading-none font-semibold tracking-[-0.04em]">
        {value}
      </p>

      {progress != null ? (
        <div
          className="nx-meter"
          role="img"
          aria-label={`${label}: ${value}`}
        >
          <div className="nx-meter__bar" style={{ width: `${progress}%` }} />
        </div>
      ) : (
        <div className="text-muted-foreground flex items-center gap-2 text-[12.5px]">
          {delta && (
            <span
              className="nx-pill tnum"
              data-tone={delta.direction === 'up' ? 'green' : 'red'}
            >
              <TrendIcon aria-hidden className="size-3.5" strokeWidth={2} />
              {delta.value}
            </span>
          )}
          {context && <span>{context}</span>}
        </div>
      )}
    </Card>
  )
}
