import { ArrowLeft } from 'lucide-react'
import type { ReactNode } from 'react'

interface PageHeaderProps {
  title: string
  description?: string
  eyebrow?: string
  actions?: ReactNode
  /** Renders a small back link above the title. Pass a translated `backLabel` alongside it. */
  onBack?: () => void
  backLabel?: string
}

export function PageHeader({ title, description, eyebrow, actions, onBack, backLabel }: PageHeaderProps) {
  return (
    <header className="flex flex-col gap-2">
      {onBack && (
        <button
          type="button"
          onClick={onBack}
          className="inline-flex w-fit items-center gap-1.5 rounded-md text-[12.5px] font-[590] text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:ring-3 focus-visible:ring-ring/40"
        >
          <ArrowLeft aria-hidden className="size-3.5" strokeWidth={2} />
          {backLabel}
        </button>
      )}
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div className="min-w-0">
          {eyebrow && <p className="nx-eyebrow mb-1.5">{eyebrow}</p>}
          <h1 className="text-[26px] leading-[1.15] font-[620] tracking-[-0.03em]">{title}</h1>
          {description && (
            <p className="text-muted-foreground mt-1.5 text-[13px] leading-5">{description}</p>
          )}
        </div>
        {actions && <div className="flex shrink-0 flex-wrap items-center gap-2.5">{actions}</div>}
      </div>
    </header>
  )
}
