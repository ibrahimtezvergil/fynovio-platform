import { PanelRightOpen, Ruler } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { DemoSection } from '@/components/common/DemoSection'
import { OverlayDemo } from '@/components/common/OverlayDemo'
import { Button } from '@/components/ui/button'
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet'

const WIDTH_KEYS = ['sm', 'md', 'lg', 'xl', '3xl'] as const
const SIDE_KEYS = ['right', 'left', 'bottom'] as const

/** Width and side are the two decisions a slide-over makes before its content. */
export function SizeSection() {
  const { t } = useTranslation('demo-drawers')

  const widths = WIDTH_KEYS.map((key) => ({
    key,
    label: t(`sizeSection.widths.${key}.label`),
    use: t(`sizeSection.widths.${key}.use`),
  }))

  const sides = SIDE_KEYS.map((side) => ({
    side,
    label: t(`sizeSection.sides.${side}.label`),
    note: t(`sizeSection.sides.${side}.note`),
  }))

  return (
    <DemoSection
      id="olculer"
      title={t('sizeSection.title')}
      description={t('sizeSection.description')}
      icon={Ruler}
    >
      <OverlayDemo
        name="sm:max-w-*"
        title={t('sizeSection.widthLadderTitle')}
        description={t('sizeSection.widthLadderDescription')}
        wide
      >
        {widths.map((width) => (
          <Sheet key={width.key}>
            <SheetTrigger render={<Button variant="outline" size="sm" />}>{width.key}</SheetTrigger>
            <SheetContent width={width.key}>
              <SheetHeader>
                <SheetTitle>{width.label}</SheetTitle>
                <SheetDescription>{width.use}</SheetDescription>
              </SheetHeader>
              <div className="flex-1 px-5 py-4">
                <p className="text-muted-foreground text-[12.5px] leading-[1.5]">
                  {t('sizeSection.widthCheck')}
                </p>
              </div>
              <SheetFooter>
                <SheetClose render={<Button variant="outline" />}>{t('sizeSection.close')}</SheetClose>
              </SheetFooter>
            </SheetContent>
          </Sheet>
        ))}
      </OverlayDemo>

      <OverlayDemo
        name='side="…"'
        title={t('sizeSection.sideChoiceTitle')}
        description={t('sizeSection.sideChoiceDescription')}
        wide
      >
        {sides.map((entry) => (
          <Sheet key={entry.side}>
            <SheetTrigger render={<Button variant="outline" size="sm" />}>{entry.label}</SheetTrigger>
            <SheetContent side={entry.side}>
              <SheetHeader>
                <SheetTitle>{t('sizeSection.sideTitle', { side: entry.label })}</SheetTitle>
                <SheetDescription>{entry.note}</SheetDescription>
              </SheetHeader>
              <SheetFooter>
                <SheetClose render={<Button variant="outline" />}>{t('sizeSection.close')}</SheetClose>
              </SheetFooter>
            </SheetContent>
          </Sheet>
        ))}
      </OverlayDemo>

      <OverlayDemo
        name="Sheet + liste"
        title={t('sizeSection.listVisibleTitle')}
        description={t('sizeSection.listVisibleDescription')}
        wide
      >
        <Sheet>
          <SheetTrigger render={<Button variant="outline" />}>
            <PanelRightOpen strokeWidth={1.75} />
            {t('sizeSection.openExample')}
          </SheetTrigger>
          <SheetContent className="sm:max-w-md">
            <SheetHeader>
              <SheetTitle>{t('sizeSection.lookBehindTitle')}</SheetTitle>
              <SheetDescription>{t('sizeSection.lookBehindDescription')}</SheetDescription>
            </SheetHeader>
            <SheetFooter>
              <SheetClose render={<Button variant="outline" />}>{t('sizeSection.close')}</SheetClose>
            </SheetFooter>
          </SheetContent>
        </Sheet>
      </OverlayDemo>
    </DemoSection>
  )
}
