import { StageBadge } from '@/components/common/StageBadge'
import { Card } from '@/components/ui/card'
import { money } from '@/features/pipeline/data/format'
import { STAGES, type Deal } from '@/types'

/** The board is another projection of the exact filtered dataset used by the grid. */
export function PipelineBoard({ deals }: { deals: Deal[] }) {
  return (
    <div className="grid gap-3 overflow-x-auto pb-1 lg:grid-cols-3 xl:grid-cols-6">
      {STAGES.map((stage) => {
        const column = deals.filter((deal) => deal.stage === stage)
        return (
          <Card key={stage} className="min-w-52 gap-3 rounded-[var(--nx-r-panel)] p-3.5">
            <div className="flex items-center justify-between gap-2"><StageBadge stage={stage} /><span className="text-muted-foreground tnum text-[11.5px]">{column.length}</span></div>
            <div className="flex flex-col gap-2">
              {column.map((deal) => (
                <div key={deal.id} className="rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-3">
                  <p className="truncate text-[12.5px] font-[590]">{deal.title}</p>
                  <p className="text-muted-foreground mt-1 truncate text-[11.5px]">{deal.account}</p>
                  <p className="tnum mt-2 text-[12px] font-[550]">{money.format(deal.value)}</p>
                </div>
              ))}
              {column.length === 0 && <p className="text-muted-foreground py-4 text-center text-[11.5px]">Kayıt yok</p>}
            </div>
          </Card>
        )
      })}
    </div>
  )
}
