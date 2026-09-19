import { CircleHelp, MessageSquareText, MousePointerClick, Percent } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { DiscountInput, resolveDiscount, formatMoney } from '@/components/common/inputs'
import type { Discount } from '@/components/common/inputs'
import { Button } from '@/components/ui/button'
import {
  Popover,
  PopoverContent,
  PopoverDescription,
  PopoverHeader,
  PopoverTitle,
  PopoverTrigger,
} from '@/components/ui/popover'
import { Separator } from '@/components/ui/separator'
import { Textarea } from '@/components/ui/textarea'
import { DemoSection } from '@/components/common/DemoSection'
import { OverlayDemo } from '@/components/common/OverlayDemo'

const LINE_TOTAL = 96500

export function PopoverSection() {
  const { t } = useTranslation('demo-overlays')
  const [discount, setDiscount] = useState<Discount>({ mode: 'percent', value: 7.5 })
  const [note, setNote] = useState('')
  const [savedNote, setSavedNote] = useState('—')
  const [open, setOpen] = useState(false)

  const discounted = LINE_TOTAL - resolveDiscount(LINE_TOTAL, discount)

  return (
    <DemoSection
      id="popover"
      title={t('popoverSection.sectionTitle')}
      description={t('popoverSection.sectionDescription')}
      icon={MousePointerClick}
    >
      <OverlayDemo
        name="<Popover>"
        title={t('popoverSection.infoBox.title')}
        description={t('popoverSection.infoBox.description')}
      >
        <Popover>
          <PopoverTrigger render={<Button variant="ghost" size="icon-sm" />}>
            <CircleHelp />
            <span className="sr-only">{t('popoverSection.infoBox.triggerAria')}</span>
          </PopoverTrigger>
          <PopoverContent>
            <PopoverHeader>
              <PopoverTitle>{t('popoverSection.infoBox.title2')}</PopoverTitle>
              <PopoverDescription>{t('popoverSection.infoBox.description2')}</PopoverDescription>
            </PopoverHeader>
            <Separator />
            <p className="text-muted-foreground font-mono text-[11px]">
              {t('popoverSection.infoBox.formula')}
            </p>
          </PopoverContent>
        </Popover>
        <span className="text-muted-foreground text-[12.5px]">{t('popoverSection.infoBox.marginLabel')}</span>
      </OverlayDemo>

      <OverlayDemo
        name="Popover + kontrol"
        title={t('popoverSection.inlineEdit.title')}
        description={t('popoverSection.inlineEdit.description')}
        state={`${discount.mode === 'percent' ? `%${discount.value ?? 0}` : formatMoney(discount.value ?? 0)} → ${formatMoney(discounted)}`}
      >
        <Popover>
          <PopoverTrigger render={<Button variant="outline" size="sm" />}>
            <Percent />
            {t('popoverSection.inlineEdit.trigger')}
          </PopoverTrigger>
          <PopoverContent align="start" className="w-80">
            <PopoverHeader>
              <PopoverTitle>{t('popoverSection.inlineEdit.panelTitle')}</PopoverTitle>
              <PopoverDescription>
                {t('popoverSection.inlineEdit.listAmountLabel', { amount: formatMoney(LINE_TOTAL) })}
              </PopoverDescription>
            </PopoverHeader>
            <Field label={t('popoverSection.inlineEdit.discountLabel')}>
              {(props) => (
                <DiscountInput
                  {...props}
                  discount={discount}
                  onDiscountChange={setDiscount}
                  base={LINE_TOTAL}
                />
              )}
            </Field>
            <p className="text-[12.5px] font-[590]">
              {t('popoverSection.inlineEdit.netAmountLabel')}{' '}
              <span className="tabular-nums">{formatMoney(discounted)}</span>
            </p>
          </PopoverContent>
        </Popover>
      </OverlayDemo>

      <OverlayDemo
        name="open + onOpenChange"
        title={t('popoverSection.controlledBox.title')}
        description={t('popoverSection.controlledBox.description')}
        state={savedNote}
      >
        <Popover open={open} onOpenChange={setOpen}>
          <PopoverTrigger render={<Button variant="outline" size="sm" />}>
            <MessageSquareText />
            {t('popoverSection.controlledBox.trigger')}
          </PopoverTrigger>
          <PopoverContent align="start" className="w-80">
            <PopoverHeader>
              <PopoverTitle>{t('popoverSection.controlledBox.panelTitle')}</PopoverTitle>
            </PopoverHeader>
            <Textarea
              rows={3}
              value={note}
              onChange={(event) => setNote(event.target.value)}
              placeholder={t('popoverSection.controlledBox.placeholder')}
              aria-label={t('popoverSection.controlledBox.aria')}
            />
            <div className="flex justify-end gap-2">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  setNote('')
                  setOpen(false)
                }}
              >
                {t('popoverSection.controlledBox.cancel')}
              </Button>
              <Button
                size="sm"
                disabled={note.trim().length === 0}
                onClick={() => {
                  setSavedNote(t('popoverSection.controlledBox.savedResult', { note: note.trim() }))
                  setNote('')
                  setOpen(false)
                }}
              >
                {t('popoverSection.controlledBox.save')}
              </Button>
            </div>
          </PopoverContent>
        </Popover>
      </OverlayDemo>

      <OverlayDemo
        name="side + align"
        title={t('popoverSection.positioning.title')}
        description={t('popoverSection.positioning.description')}
        wide
      >
        {(['top', 'right', 'bottom', 'left'] as const).map((side) => (
          <Popover key={side}>
            <PopoverTrigger render={<Button variant="outline" size="sm" />}>{side}</PopoverTrigger>
            <PopoverContent side={side} className="w-56">
              <PopoverDescription>
                {t('popoverSection.positioning.sideDescription', { side })}
              </PopoverDescription>
            </PopoverContent>
          </Popover>
        ))}
      </OverlayDemo>
    </DemoSection>
  )
}
