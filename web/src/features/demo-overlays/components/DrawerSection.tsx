import { PanelBottom, Plus, Smartphone } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { MoneyInput, RichSelect } from '@/components/common/inputs'
import { Button } from '@/components/ui/button'
import {
  Drawer,
  DrawerClose,
  DrawerContent,
  DrawerDescription,
  DrawerFooter,
  DrawerHeader,
  DrawerTitle,
  DrawerTrigger,
} from '@/components/ui/drawer'
import { Separator } from '@/components/ui/separator'
import { DemoSection } from '@/components/common/DemoSection'
import { OverlayDemo } from '@/components/common/OverlayDemo'
import { DEAL_OWNERS, DEAL_STAGES } from '@/features/demo-overlays/data/content'

export function DrawerSection() {
  const { t } = useTranslation('demo-overlays')

  const QUICK_ACTIONS = [
    { label: t('drawerSection.quickActions.newDeal.label'), note: t('drawerSection.quickActions.newDeal.note') },
    { label: t('drawerSection.quickActions.createQuote.label'), note: t('drawerSection.quickActions.createQuote.note') },
    { label: t('drawerSection.quickActions.scheduleMeeting.label'), note: t('drawerSection.quickActions.scheduleMeeting.note') },
    { label: t('drawerSection.quickActions.addNote.label'), note: t('drawerSection.quickActions.addNote.note') },
  ]

  const [stage, setStage] = useState<string | null>('teklif')
  const [owner, setOwner] = useState<string | null>('mert')
  const [amount, setAmount] = useState<number | null>(96500)
  const [snap, setSnap] = useState<string | number | null>(0.4)

  return (
    <DemoSection
      id="drawer"
      title={t('drawerSection.sectionTitle')}
      description={t('drawerSection.sectionDescription')}
      icon={PanelBottom}
    >
      <OverlayDemo
        name="<Drawer>"
        title={t('drawerSection.bottom.title')}
        description={t('drawerSection.bottom.description')}
      >
        <Drawer showSwipeHandle>
          <DrawerTrigger render={<Button variant="outline" />}>
            <Smartphone />
            {t('drawerSection.bottom.trigger')}
          </DrawerTrigger>
          <DrawerContent>
            <DrawerHeader>
              <DrawerTitle>{t('drawerSection.bottom.panelTitle')}</DrawerTitle>
              <DrawerDescription>{t('drawerSection.bottom.panelDescription')}</DrawerDescription>
            </DrawerHeader>
            <div className="flex flex-col px-5 py-3">
              {QUICK_ACTIONS.map((action) => (
                <DrawerClose
                  key={action.label}
                  render={
                    <button
                      type="button"
                      aria-label={action.label}
                      className="hover:bg-[var(--nx-fill-hover)] -mx-2 flex items-center gap-3 rounded-md px-2 py-3 text-left transition-colors"
                    />
                  }
                >
                  <span aria-hidden className="nx-icon-tile">
                    <Plus className="size-4" strokeWidth={1.75} />
                  </span>
                  <span className="min-w-0">
                    <span className="block text-[13.5px] font-[590]">{action.label}</span>
                    <span className="text-muted-foreground block text-[12px]">{action.note}</span>
                  </span>
                </DrawerClose>
              ))}
            </div>
          </DrawerContent>
        </Drawer>
      </OverlayDemo>

      <OverlayDemo
        name="snapPoints"
        title={t('drawerSection.snap.title')}
        description={t('drawerSection.snap.description')}
        state={t('drawerSection.snap.state', { value: snap === null ? 'null' : String(snap) })}
      >
        <Drawer
          showSwipeHandle
          snapPoints={[0.4, 0.92]}
          snapPoint={snap}
          onSnapPointChange={setSnap}
        >
          <DrawerTrigger render={<Button variant="outline" />}>{t('drawerSection.snap.trigger')}</DrawerTrigger>
          <DrawerContent>
            <DrawerHeader>
              <DrawerTitle>{t('drawerSection.snap.panelTitle')}</DrawerTitle>
              <DrawerDescription>
                {t('drawerSection.snap.panelDescription')}
              </DrawerDescription>
            </DrawerHeader>
            <div className="flex flex-1 flex-col gap-4 overflow-y-auto px-5 py-4">
              <Field label={t('drawerSection.snap.stageLabel')}>
                {(props) => (
                  <RichSelect
                    {...props}
                    options={DEAL_STAGES}
                    value={stage}
                    onValueChange={setStage}
                  />
                )}
              </Field>
              <Field label={t('drawerSection.snap.ownerLabel')}>
                {(props) => (
                  <RichSelect
                    {...props}
                    options={DEAL_OWNERS}
                    value={owner}
                    onValueChange={setOwner}
                  />
                )}
              </Field>
              <Field label={t('drawerSection.snap.amountLabel')}>
                {(props) => <MoneyInput {...props} value={amount} onValueChange={setAmount} />}
              </Field>
              <Separator />
              <p className="text-muted-foreground text-[12px] leading-5">
                {t('drawerSection.snap.footerNote')}
              </p>
            </div>
            <DrawerFooter>
              <Button>{t('drawerSection.snap.save')}</Button>
              <DrawerClose render={<Button variant="outline" />}>{t('drawerSection.snap.cancel')}</DrawerClose>
            </DrawerFooter>
          </DrawerContent>
        </Drawer>
      </OverlayDemo>

      <OverlayDemo
        name='swipeDirection="right"'
        title={t('drawerSection.directional.title')}
        description={t('drawerSection.directional.description')}
        wide
      >
        {(['right', 'left', 'up'] as const).map((direction) => (
          <Drawer key={direction} swipeDirection={direction} showSwipeHandle>
            <DrawerTrigger render={<Button variant="outline" size="sm" />}>
              {direction === 'right'
                ? t('drawerSection.directional.right')
                : direction === 'left'
                  ? t('drawerSection.directional.left')
                  : t('drawerSection.directional.up')}
            </DrawerTrigger>
            <DrawerContent>
              <DrawerHeader>
                <DrawerTitle>swipeDirection="{direction}"</DrawerTitle>
                <DrawerDescription>
                  {t('drawerSection.directional.panelDescription')}
                </DrawerDescription>
              </DrawerHeader>
              <DrawerFooter>
                <DrawerClose render={<Button variant="outline" />}>{t('drawerSection.directional.close')}</DrawerClose>
              </DrawerFooter>
            </DrawerContent>
          </Drawer>
        ))}
      </OverlayDemo>
    </DemoSection>
  )
}
