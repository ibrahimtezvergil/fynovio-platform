import { Filter, PanelRight, SlidersHorizontal } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { DateRange } from 'react-day-picker'
import { Field } from '@/components/common/Field'
import { CheckboxGroupField, DateRangePicker, NumberRangeInput } from '@/components/common/inputs'
import type { NumberRange } from '@/components/common/inputs'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
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
import { ACTIVITY_TRAIL, DEAL_STAGES } from '@/features/demo-overlays/data/content'

import { DemoSection } from '@/components/common/DemoSection'
import { OverlayDemo } from '@/components/common/OverlayDemo'

const SIDES = ['right', 'left', 'top', 'bottom'] as const

const STAGE_OPTIONS = DEAL_STAGES.map(({ value, label }) => ({ value, label }))

export function SheetSection() {
  const { t } = useTranslation('demo-overlays')
  const [stages, setStages] = useState<string[]>(['teklif', 'pazarlik'])
  const [amount, setAmount] = useState<NumberRange>({ min: 50000, max: null })
  const [range, setRange] = useState<DateRange | null>(null)
  const [applied, setApplied] = useState('—')

  const sideLabels: Record<(typeof SIDES)[number], string> = {
    right: t('sheetSection.sideLabels.right'),
    left: t('sheetSection.sideLabels.left'),
    top: t('sheetSection.sideLabels.top'),
    bottom: t('sheetSection.sideLabels.bottom'),
  }

  return (
    <DemoSection
      id="sheet"
      title={t('sheetSection.sectionTitle')}
      description={t('sheetSection.sectionDescription')}
      icon={PanelRight}
    >
      <OverlayDemo
        name='<Sheet side="…">'
        title={t('sheetSection.fourSides.title')}
        description={t('sheetSection.fourSides.description')}
        wide
      >
        {SIDES.map((side) => (
          <Sheet key={side}>
            <SheetTrigger render={<Button variant="outline" size="sm" />}>
              {sideLabels[side]}
            </SheetTrigger>
            <SheetContent side={side}>
              <SheetHeader>
                <SheetTitle>
                  {sideLabels[side]} {t('sheetSection.fourSides.panelSuffix')}
                </SheetTitle>
                <SheetDescription>
                  {t('sheetSection.fourSides.panelDescription', { side })}
                </SheetDescription>
              </SheetHeader>
              <SheetFooter>
                <SheetClose render={<Button variant="outline" />}>{t('sheetSection.fourSides.close')}</SheetClose>
              </SheetFooter>
            </SheetContent>
          </Sheet>
        ))}
      </OverlayDemo>

      <OverlayDemo
        name="Sheet + filtre"
        title={t('sheetSection.filter.title')}
        description={t('sheetSection.filter.description')}
        state={applied}
      >
        <Sheet>
          <SheetTrigger render={<Button variant="outline" />}>
            <SlidersHorizontal />
            {t('sheetSection.filter.trigger')}
            <Badge variant="secondary">{stages.length}</Badge>
          </SheetTrigger>
          <SheetContent className="gap-0">
            <SheetHeader>
              <SheetTitle>{t('sheetSection.filter.panelTitle')}</SheetTitle>
              <SheetDescription>{t('sheetSection.filter.panelDescription')}</SheetDescription>
            </SheetHeader>
            <div className="flex flex-1 flex-col gap-5 overflow-y-auto px-5 py-5">
              <Field label={t('sheetSection.filter.stageLabel')}>
                {(props) => (
                  <CheckboxGroupField
                    {...props}
                    options={STAGE_OPTIONS}
                    value={stages}
                    onValueChange={setStages}
                  />
                )}
              </Field>
              <Separator />
              <Field label={t('sheetSection.filter.amountLabel')} hint={t('sheetSection.filter.amountHint')}>
                {(props) => (
                  <NumberRangeInput {...props} value={amount} onValueChange={setAmount} />
                )}
              </Field>
              <Field label={t('sheetSection.filter.closeDateLabel')}>
                {(props) => <DateRangePicker {...props} value={range} onValueChange={setRange} />}
              </Field>
            </div>
            <SheetFooter>
              <Button
                variant="ghost"
                onClick={() => {
                  setStages([])
                  setAmount({ min: null, max: null })
                  setRange(null)
                }}
              >
                {t('sheetSection.filter.clear')}
              </Button>
              <SheetClose
                render={<Button />}
                onClick={() => setApplied(t('sheetSection.filter.appliedResult', { count: stages.length }))}
              >
                {t('sheetSection.filter.apply')}
              </SheetClose>
            </SheetFooter>
          </SheetContent>
        </Sheet>
      </OverlayDemo>

      <OverlayDemo
        name="Sheet + detay"
        title={t('sheetSection.detail.title')}
        description={t('sheetSection.detail.description')}
        wide
      >
        <Sheet>
          <SheetTrigger render={<Button variant="outline" />}>
            <Filter />
            {t('sheetSection.detail.trigger')}
          </SheetTrigger>
          <SheetContent className="gap-0 sm:max-w-lg">
            <SheetHeader>
              <Badge variant="info" className="mb-1 w-fit">
                {t('sheetSection.detail.badgeLabel')}
              </Badge>
              <SheetTitle>{t('sheetSection.detail.panelTitle')}</SheetTitle>
              <SheetDescription>{t('sheetSection.detail.panelDescription')}</SheetDescription>
            </SheetHeader>
            <div className="flex-1 overflow-y-auto px-5 py-5">
              <p className="nx-eyebrow mb-3">{t('sheetSection.detail.activitiesHeading')}</p>
              <ol className="flex flex-col">
                {ACTIVITY_TRAIL.map((entry) => (
                  <li
                    key={entry.title}
                    className="flex gap-3 border-b border-[var(--nx-hairline-soft)] py-3 last:border-0"
                  >
                    <span className="text-muted-foreground w-[86px] shrink-0 text-right font-mono text-[11px] tabular-nums">
                      {entry.time}
                    </span>
                    <span className="min-w-0">
                      <span className="block text-[13px] font-[590]">{entry.title}</span>
                      <span className="text-muted-foreground block text-[12px]">{entry.note}</span>
                    </span>
                  </li>
                ))}
              </ol>
            </div>
            <SheetFooter>
              <SheetClose render={<Button variant="outline" />}>{t('sheetSection.detail.close')}</SheetClose>
              <Button>{t('sheetSection.detail.edit')}</Button>
            </SheetFooter>
          </SheetContent>
        </Sheet>
      </OverlayDemo>
    </DemoSection>
  )
}
