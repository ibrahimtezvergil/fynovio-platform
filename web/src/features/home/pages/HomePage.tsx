import { useAttentionItems, useRecentWork } from '@/features/home/api'
import { ApplicationsSection } from '@/features/home/components/ApplicationsSection'
import { AttentionSection } from '@/features/home/components/AttentionSection'
import { CommandBar } from '@/features/home/components/CommandBar'
import { ContinueWorkingSection } from '@/features/home/components/ContinueWorkingSection'
import { GreetingSection } from '@/features/home/components/GreetingSection'

/**
 * Global Home / Business Home — the day's starting point, not a KPI report.
 * Topbar-only, no sidebar (`DashboardLayout` drops the rail for this one
 * route — an app-launcher surface, Applications is the way back into the
 * rail-bearing screens). Applications lead on the left, with focus and recent
 * work stacked on the right. Only the panels scroll when content overflows.
 */
export default function HomePage() {
  const { data: attentionItems = [], isLoading: attentionLoading } = useAttentionItems()
  const { data: recentWork = [], isLoading: recentWorkLoading } = useRecentWork()

  return (
    <div className="mx-auto flex h-[calc(100dvh-var(--nx-topbar-height)-3rem)] min-h-0 max-w-[1320px] flex-col gap-4">
      <GreetingSection attentionCount={attentionItems.length} />
      <CommandBar />

      <div className="grid min-h-0 flex-1 grid-cols-1 grid-rows-2 gap-4 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)] lg:grid-rows-1">
        <ApplicationsSection />
        <div className="grid min-h-0 grid-rows-[minmax(0,3fr)_minmax(0,2fr)] gap-4">
          <AttentionSection items={attentionItems} isLoading={attentionLoading} />
          <ContinueWorkingSection items={recentWork} isLoading={recentWorkLoading} />
        </div>
      </div>

    </div>
  )
}
