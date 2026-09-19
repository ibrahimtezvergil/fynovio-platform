import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { CodeBlock } from '@/features/demo-tables/components/CodeBlock'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { cn } from '@/lib/utils'

export interface DocPill {
  label: string
  tone?: 'feature' | 'model' | 'option' | 'state'
}

const PILL_TONE = {
  feature: 'text-stage-meeting bg-stage-meeting/12',
  model: 'text-stage-contacted bg-stage-contacted/12',
  option: 'text-stage-quoted bg-stage-quoted/12',
  state: 'text-stage-ready bg-stage-ready/12',
} as const

export function DocPills({ pills }: { pills: readonly DocPill[] }) {
  if (pills.length === 0) return null
  return (
    <div className="flex flex-wrap gap-1.5">
      {pills.map((pill) => (
        <code
          key={pill.label}
          className={cn(
            'rounded-sm px-1.5 py-0.5 font-mono text-[11px] leading-4',
            PILL_TONE[pill.tone ?? 'feature'],
          )}
        >
          {pill.label}
        </code>
      ))}
    </div>
  )
}

export interface DocCardProps {
  icon?: LucideIcon
  title: string
  summary: string
  pills?: readonly DocPill[]
  code?: string
  codeFilename?: string
  children?: ReactNode
  className?: string
}

/** One capability, one card: what it is, what registers it, how to change it. */
export function DocCard({
  icon: Icon,
  title,
  summary,
  pills = [],
  code,
  codeFilename,
  children,
  className,
}: DocCardProps) {
  return (
    <Card className={cn('h-full', className)}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          {Icon && <Icon aria-hidden className="text-brand-graphic size-4" strokeWidth={1.75} />}
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <p className="text-muted-foreground text-[13px] leading-5">{summary}</p>
        <DocPills pills={pills} />
        {code && <CodeBlock code={code} filename={codeFilename} />}
        {children}
      </CardContent>
    </Card>
  )
}

/** Two-column key/value list used for the option reference tables. */
export function DocDefinitionList({
  items,
}: {
  items: readonly { term: string; description: string }[]
}) {
  return (
    <dl className="divide-border/70 divide-y text-[13px]">
      {items.map((item) => (
        <div key={item.term} className="grid gap-1 py-2 sm:grid-cols-[minmax(0,180px)_1fr] sm:gap-4">
          <dt className="text-foreground font-mono text-[12px] leading-5">{item.term}</dt>
          <dd className="text-muted-foreground leading-5">{item.description}</dd>
        </div>
      ))}
    </dl>
  )
}
