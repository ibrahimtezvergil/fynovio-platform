import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { Card } from '@/components/ui/card'
import { cn } from '@/lib/utils'

interface DemoSectionProps {
  /** Doubles as the anchor the section nav scrolls to. */
  id: string
  title: string
  description: string
  icon: LucideIcon
  /** One column for demos that need the width, two for the rest. */
  columns?: 1 | 2
  children: ReactNode
}

export function DemoSection({
  id,
  title,
  description,
  icon: Icon,
  columns = 2,
  children,
}: DemoSectionProps) {
  return (
    <Card id={id} className="scroll-mt-24 gap-5 px-6 pt-[22px] pb-6">
      <div className="flex items-start gap-3">
        <span aria-hidden className="nx-icon-tile mt-0.5">
          <Icon className="size-4" strokeWidth={1.75} />
        </span>
        <div className="flex min-w-0 flex-col gap-0.5">
          <h2 className="font-heading text-[17px] leading-tight font-[620] tracking-[-0.024em]">
            {title}
          </h2>
          <p className="text-muted-foreground text-[12.5px]">{description}</p>
        </div>
      </div>
      {/* `min-w-0` on the items: a grid item defaults to min-width:auto, which
          lets a wide child (a line table, a chart) push the whole page sideways. */}
      <div className={cn('grid min-w-0 gap-4 [&>*]:min-w-0', columns === 2 && 'lg:grid-cols-2')}>
        {children}
      </div>
    </Card>
  )
}
