import type { LucideIcon } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Card } from '@/components/ui/card'

export interface NavSection {
  id: string
  label: string
  icon: LucideIcon
}

/**
 * Anchor nav with a scroll spy, for any page long enough to need one.
 *
 * The current entry is whichever section heading sits nearest the top of the
 * viewport — driven by an observer rather than by the last click, so the
 * highlight stays true when the reader scrolls by hand. The top margin clears
 * the sticky topbar; the bottom one keeps the next section from winning as
 * soon as it appears at the fold.
 */
export function SectionNav({
  sections,
  label,
  sticky = true,
}: {
  sections: readonly NavSection[]
  label: string
  sticky?: boolean
}) {
  const [active, setActive] = useState(sections[0]?.id ?? '')

  useEffect(() => {
    const targets = sections
      .map((section) => document.getElementById(section.id))
      .filter((node): node is HTMLElement => node != null)
    if (targets.length === 0) return

    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((entry) => entry.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top)
        if (visible[0]) setActive(visible[0].target.id)
      },
      { rootMargin: '-88px 0px -55% 0px', threshold: 0 },
    )

    targets.forEach((target) => observer.observe(target))
    return () => observer.disconnect()
  }, [sections])

  return (
    <Card className={`${sticky ? 'sticky top-[88px] ' : ''}gap-0 px-2 py-2.5`}>
      <nav aria-label={label} className="flex flex-col gap-0.5">
        {sections.map(({ id, label, icon: Icon }) => (
          <a
            key={id}
            href={`#${id}`}
            aria-current={active === id ? 'page' : undefined}
            className="nx-nav-item"
          >
            <Icon aria-hidden className="size-[18px] shrink-0" strokeWidth={1.7} />
            <span className="flex-1">{label}</span>
          </a>
        ))}
      </nav>
    </Card>
  )
}
