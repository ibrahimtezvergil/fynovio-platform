import { Loader2, SquareStack } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { DatePicker, MoneyInput, RichSelect } from '@/components/common/inputs'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { DemoSection } from '@/components/common/DemoSection'
import { OverlayDemo } from '@/components/common/OverlayDemo'
import { CONSENT_PARAGRAPHS, DEAL_OWNERS, DEAL_STAGES } from '@/features/demo-overlays/data/content'

export function DialogSection() {
  const { t } = useTranslation('demo-overlays')

  /** Every width the page uses, so the ladder is one list rather than five literals. */
  const SIZES = [
    { key: 'sm', label: t('dialogSection.sizes.sm.label'), width: 'sm:max-w-sm', note: t('dialogSection.sizes.sm.note') },
    { key: 'md', label: t('dialogSection.sizes.md.label'), width: 'sm:max-w-md', note: t('dialogSection.sizes.md.note') },
    { key: 'lg', label: t('dialogSection.sizes.lg.label'), width: 'sm:max-w-2xl', note: t('dialogSection.sizes.lg.note') },
    { key: 'xl', label: t('dialogSection.sizes.xl.label'), width: 'sm:max-w-4xl', note: t('dialogSection.sizes.xl.note') },
  ] as const

  const [controlledOpen, setControlledOpen] = useState(false)
  const [formOpen, setFormOpen] = useState(false)
  const [lastReason, setLastReason] = useState('—')
  const [busy, setBusy] = useState(false)
  const [busyOpen, setBusyOpen] = useState(false)
  const [saved, setSaved] = useState('—')

  // The blocking dialog owns a fake job so `disablePointerDismissal` has
  // something real to protect: the overlay stays until the work is done.
  useEffect(() => {
    if (!busy) return
    const timer = setTimeout(() => {
      setBusy(false)
      setBusyOpen(false)
    }, 2400)
    return () => clearTimeout(timer)
  }, [busy])

  return (
    <DemoSection
      id="dialog"
      title={t('dialogSection.sectionTitle')}
      description={t('dialogSection.sectionDescription')}
      icon={SquareStack}
    >
      <OverlayDemo
        name="<Dialog>"
        title={t('dialogSection.basic.title')}
        description={t('dialogSection.basic.description')}
      >
        <Dialog>
          <DialogTrigger render={<Button variant="outline" />}>{t('dialogSection.basic.trigger')}</DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>{t('dialogSection.basic.shareTitle')}</DialogTitle>
              <DialogDescription>
                {t('dialogSection.basic.shareDescription')}
              </DialogDescription>
            </DialogHeader>
            <Field label={t('dialogSection.basic.recipientEmailLabel')}>
              {(props) => <Input {...props} type="email" defaultValue="satinalma@acme.com.tr" />}
            </Field>
            <DialogFooter>
              <DialogClose render={<Button variant="outline" />}>{t('dialogSection.basic.cancel')}</DialogClose>
              <DialogClose render={<Button />}>{t('dialogSection.basic.sendLink')}</DialogClose>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </OverlayDemo>

      <OverlayDemo
        name="open + onOpenChange"
        title={t('dialogSection.controlled.title')}
        description={t('dialogSection.controlled.description')}
        state={t('dialogSection.controlled.state', { open: controlledOpen, reason: lastReason })}
      >
        <Button variant="outline" onClick={() => setControlledOpen(true)}>
          {t('dialogSection.controlled.openExternal')}
        </Button>
        <Dialog
          open={controlledOpen}
          onOpenChange={(open, details) => {
            setControlledOpen(open)
            if (!open) setLastReason(details.reason ?? 'none')
          }}
        >
          <DialogContent>
            <DialogHeader>
              <DialogTitle>{t('dialogSection.controlled.readReasonTitle')}</DialogTitle>
              <DialogDescription>
                {t('dialogSection.controlled.readReasonDescription')}
              </DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <DialogClose render={<Button variant="outline" />}>{t('dialogSection.controlled.close')}</DialogClose>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </OverlayDemo>

      <OverlayDemo
        name="<Dialog> + form"
        title={t('dialogSection.formModal.title')}
        description={t('dialogSection.formModal.description')}
        state={saved}
      >
        <Dialog open={formOpen} onOpenChange={setFormOpen}>
          <DialogTrigger render={<Button />}>{t('dialogSection.formModal.trigger')}</DialogTrigger>
          <DialogContent className="sm:max-w-2xl">
            {/* `contents` keeps the form out of the popup's own grid, so the
                header, body and footer stay on the dialog's row rhythm. */}
            <form
              className="contents"
              onSubmit={(event) => {
                event.preventDefault()
                const title = new FormData(event.currentTarget).get('title')
                setSaved(
                  t('dialogSection.formModal.savedMessage', {
                    title: String(title || t('dialogSection.formModal.unnamedDeal')),
                  }),
                )
                setFormOpen(false)
              }}
            >
              <DialogHeader>
                <DialogTitle>{t('dialogSection.formModal.newDealTitle')}</DialogTitle>
                <DialogDescription>
                  {t('dialogSection.formModal.newDealDescription')}
                </DialogDescription>
              </DialogHeader>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label={t('dialogSection.formModal.nameLabel')} className="sm:col-span-2">
                  {(props) => (
                    <Input {...props} name="title" required placeholder={t('dialogSection.formModal.namePlaceholder')} />
                  )}
                </Field>
                <NewDealFields />
              </div>
              <DialogFooter>
                <DialogClose render={<Button variant="outline" />}>{t('dialogSection.formModal.cancel')}</DialogClose>
                <Button type="submit">{t('dialogSection.formModal.save')}</Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
      </OverlayDemo>

      <OverlayDemo
        name="overflow-y-auto"
        title={t('dialogSection.longContent.title')}
        description={t('dialogSection.longContent.description')}
      >
        <Dialog>
          <DialogTrigger render={<Button variant="outline" />}>{t('dialogSection.longContent.trigger')}</DialogTrigger>
          <DialogContent className="grid-rows-[auto_minmax(0,1fr)_auto] sm:max-w-2xl">
            <DialogHeader>
              <DialogTitle>{t('dialogSection.longContent.noticeTitle')}</DialogTitle>
              <DialogDescription>{t('dialogSection.longContent.noticeDescription')}</DialogDescription>
            </DialogHeader>
            <div className="-mr-2 flex flex-col gap-3 overflow-y-auto pr-2 text-[12.5px] leading-5 text-muted-foreground">
              {CONSENT_PARAGRAPHS.map((paragraph, index) => (
                <p key={index}>{paragraph}</p>
              ))}
            </div>
            <DialogFooter>
              <DialogClose render={<Button variant="outline" />}>{t('dialogSection.longContent.haventRead')}</DialogClose>
              <DialogClose render={<Button />}>{t('dialogSection.longContent.haveReadAgree')}</DialogClose>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </OverlayDemo>

      <OverlayDemo
        name="className: max-w-*"
        title={t('dialogSection.widthSteps.title')}
        description={t('dialogSection.widthSteps.description')}
        wide
      >
        {SIZES.map((size) => (
          <Dialog key={size.key}>
            <DialogTrigger render={<Button variant="outline" size="sm" />}>
              {size.label}
            </DialogTrigger>
            <DialogContent className={size.width}>
              <DialogHeader>
                <DialogTitle>{size.label} {t('dialogSection.widthSteps.dialogTitleSuffix')}</DialogTitle>
                <DialogDescription>{size.note}</DialogDescription>
              </DialogHeader>
              <DialogFooter showCloseButton />
            </DialogContent>
          </Dialog>
        ))}
      </OverlayDemo>

      <OverlayDemo
        name="disablePointerDismissal"
        title={t('dialogSection.lockedClose.title')}
        description={t('dialogSection.lockedClose.description')}
        state={busy ? t('dialogSection.lockedClose.stateBusy') : t('dialogSection.lockedClose.stateIdle')}
      >
        <Dialog
          open={busyOpen}
          onOpenChange={(open) => {
            if (!busy) setBusyOpen(open)
          }}
          disablePointerDismissal={busy}
        >
          <DialogTrigger
            render={<Button variant="outline" />}
            onClick={() => {
              setBusyOpen(true)
              setBusy(true)
            }}
          >
            {t('dialogSection.lockedClose.startTrigger')}
          </DialogTrigger>
          <DialogContent showCloseButton={!busy} className="sm:max-w-sm">
            <DialogHeader>
              <DialogTitle>{t('dialogSection.lockedClose.title2')}</DialogTitle>
              <DialogDescription>
                {busy
                  ? t('dialogSection.lockedClose.busyDescription')
                  : t('dialogSection.lockedClose.doneDescription')}
              </DialogDescription>
            </DialogHeader>
            {busy && (
              <p className="text-muted-foreground flex items-center gap-2 text-[12.5px]">
                <Loader2 aria-hidden className="size-4 animate-spin" />
                {t('dialogSection.lockedClose.pleaseWait')}
              </p>
            )}
          </DialogContent>
        </Dialog>
      </OverlayDemo>
    </DemoSection>
  )
}

/** Split out so the form modal's grid stays readable at a glance. */
function NewDealFields() {
  const { t } = useTranslation('demo-overlays')
  const [stage, setStage] = useState<string | null>('teklif')
  const [owner, setOwner] = useState<string | null>('zeynep')
  const [amount, setAmount] = useState<number | null>(180000)
  const [closeDate, setCloseDate] = useState<Date | null>(null)

  return (
    <>
      <Field label={t('dialogSection.formModal.stageLabel')}>
        {(props) => (
          <RichSelect {...props} options={DEAL_STAGES} value={stage} onValueChange={setStage} />
        )}
      </Field>
      <Field label={t('dialogSection.formModal.ownerLabel')}>
        {(props) => (
          <RichSelect {...props} options={DEAL_OWNERS} value={owner} onValueChange={setOwner} />
        )}
      </Field>
      <Field label={t('dialogSection.formModal.amountLabel')}>
        {(props) => <MoneyInput {...props} value={amount} onValueChange={setAmount} />}
      </Field>
      <Field label={t('dialogSection.formModal.closeDateLabel')}>
        {(props) => <DatePicker {...props} value={closeDate} onValueChange={setCloseDate} />}
      </Field>
      <Field label={t('dialogSection.formModal.noteLabel')} className="sm:col-span-2">
        {(props) => <Textarea {...props} rows={3} placeholder={t('dialogSection.formModal.notePlaceholder')} />}
      </Field>
    </>
  )
}
