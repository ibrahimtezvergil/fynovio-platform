import { ChevronDown, Wrench } from 'lucide-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { buildNavGroups } from '@/layouts/navigation'
import { APPLICATION_ICON } from '@/lib/applications/icons'
import { applicationRegistry } from '@/lib/applications/registry'
import { isNavParent } from '@/types'

/**
 * The Topbar's "Araçlar" directory — every cross-domain tool, plus (in `DEV`
 * only) the Developer section that used to be appended to every domain/system
 * sidebar. Developer's route tree, guard, and labels are untouched — only
 * where it's reached from moved (see `buildSidebarNav` in `layouts/navigation`).
 */
export function ToolsMenu() {
  const { t } = useTranslation('nav')
  const { t: tHome } = useTranslation('home')
  const navigate = useNavigate()

  const utilities = useMemo(
    () => applicationRegistry.filter((app) => app.kind === 'utility').sort((a, b) => a.order - b.order),
    [],
  )
  const developerItems = useMemo(
    () => (import.meta.env.DEV ? (buildNavGroups(t, 'sidebar', 'developer')[0]?.items ?? []) : []),
    [t],
  )

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="secondary" size="sm">
            <Wrench aria-hidden strokeWidth={1.7} />
            {t('topbar.tools')}
            <ChevronDown aria-hidden className="text-muted-foreground" strokeWidth={1.7} />
          </Button>
        }
      />
      <DropdownMenuContent align="start" className="w-60">
        {utilities.map((app) => {
          const Icon = APPLICATION_ICON[app.icon]
          const comingSoon = app.status === 'comingSoon'
          return (
            <DropdownMenuItem key={app.id} disabled={comingSoon} onClick={() => app.route && navigate(app.route)}>
              <Icon aria-hidden strokeWidth={1.7} />
              <span className="flex-1">{tHome(app.labelKey)}</span>
              {comingSoon && <span className="text-[10px] font-[600] text-muted-foreground">{tHome('applications.comingSoon')}</span>}
            </DropdownMenuItem>
          )
        })}

        {developerItems.length > 0 && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuLabel>{t('groups.developer')}</DropdownMenuLabel>
            {developerItems.map((item) =>
              isNavParent(item) ? (
                <div key={item.id}>
                  <DropdownMenuLabel className="text-[10.5px] uppercase tracking-[0.04em]">{item.label}</DropdownMenuLabel>
                  {item.children.map((leaf) => (
                    <DropdownMenuItem key={leaf.id} onClick={() => navigate(leaf.to)}>
                      {leaf.icon && <leaf.icon aria-hidden strokeWidth={1.7} />}
                      {leaf.label}
                    </DropdownMenuItem>
                  ))}
                </div>
              ) : (
                <DropdownMenuItem key={item.id} onClick={() => navigate(item.to)}>
                  <item.icon aria-hidden strokeWidth={1.7} />
                  {item.label}
                </DropdownMenuItem>
              ),
            )}
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
