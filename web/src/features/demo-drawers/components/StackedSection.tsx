import { Layers, Trash2, TriangleAlert } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { DemoSection } from '@/components/common/DemoSection'
import { OverlayDemo } from '@/components/common/OverlayDemo'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
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

/**
 * What is allowed to sit on top of a drawer, and what is not.
 *
 * One level: a drawer may raise a dialog that *ends* in a decision — a
 * confirmation, a single-field prompt. It may not raise a second drawer, and
 * the dialog it raises may not raise a third thing. Past one level nobody can
 * say what escape closes, and every layer has its own unsaved state.
 */
export function StackedSection() {
  const { t } = useTranslation('demo-drawers')
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [promptOpen, setPromptOpen] = useState(false)
  const [last, setLast] = useState('—')

  return (
    <DemoSection
      id="katmanli"
      title={t('stackedSection.title')}
      description={t('stackedSection.description')}
      icon={Layers}
    >
      <OverlayDemo
        name="Sheet → AlertDialog"
        title={t('stackedSection.destructiveTitle')}
        description={t('stackedSection.destructiveDescription')}
        state={last}
        wide
      >
        <Sheet open={drawerOpen} onOpenChange={setDrawerOpen}>
          <SheetTrigger render={<Button variant="outline" />}>{t('stackedSection.openPanel')}</SheetTrigger>
          <SheetContent className="gap-0 sm:max-w-md">
            <SheetHeader>
              <SheetTitle>Nordwind Lojistik</SheetTitle>
              <SheetDescription>{t('stackedSection.panelDescription')}</SheetDescription>
            </SheetHeader>

            <div className="flex flex-1 flex-col gap-3 px-5 py-5">
              <Button variant="outline" onClick={() => setPromptOpen(true)}>
                {t('stackedSection.addNote')}
              </Button>
              <Button variant="destructive" onClick={() => setConfirming(true)}>
                <Trash2 strokeWidth={1.75} />
                {t('stackedSection.deleteAccount')}
              </Button>
              <p className="text-muted-foreground text-[11.5px] leading-4">
                {t('stackedSection.panelHint')}
              </p>
            </div>

            <SheetFooter>
              <SheetClose render={<Button variant="outline" />}>{t('stackedSection.close')}</SheetClose>
            </SheetFooter>
          </SheetContent>
        </Sheet>

        <AlertDialog open={confirming} onOpenChange={setConfirming}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>
                <TriangleAlert
                  aria-hidden
                  className="mr-1.5 inline size-4 align-[-2px] text-[var(--nx-neg)]"
                  strokeWidth={2}
                />
                {t('stackedSection.deleteConfirmTitle')}
              </AlertDialogTitle>
              <AlertDialogDescription>{t('stackedSection.deleteConfirmDescription')}</AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel render={<Button variant="outline" />}>
                {t('stackedSection.cancel')}
              </AlertDialogCancel>
              <AlertDialogAction
                render={<Button variant="destructive" />}
                onClick={() => {
                  setConfirming(false)
                  setLast(t('stackedSection.deleteConfirmedState'))
                  toast.error(t('stackedSection.toastDeleted'), { description: 'Nordwind Lojistik' })
                }}
              >
                {t('stackedSection.delete')}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        <Dialog open={promptOpen} onOpenChange={setPromptOpen}>
          <DialogContent className="sm:max-w-sm">
            <DialogHeader>
              <DialogTitle>{t('stackedSection.addNoteTitle')}</DialogTitle>
              <DialogDescription>{t('stackedSection.addNoteDescription')}</DialogDescription>
            </DialogHeader>
            <div className="px-5">
              <textarea
                rows={3}
                aria-label={t('stackedSection.noteAria')}
                placeholder={t('stackedSection.notePlaceholder')}
                className="w-full resize-none rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-fill)] px-3 py-2 text-[13px] outline-none focus-visible:border-ring focus-visible:shadow-[0_0_0_4px_var(--nx-tint-fill)]"
              />
            </div>
            <DialogFooter>
              <DialogClose render={<Button variant="outline" />}>{t('stackedSection.cancel')}</DialogClose>
              <Button
                onClick={() => {
                  setPromptOpen(false)
                  setLast(t('stackedSection.noteAddedState'))
                }}
              >
                {t('stackedSection.add')}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </OverlayDemo>

      <OverlayDemo
        name={t('stackedSection.layerRuleName')}
        title={t('stackedSection.noSecondPanelTitle')}
        description={t('stackedSection.noSecondPanelDescription')}
        wide
      >
        <p className="text-muted-foreground text-[12px] leading-[1.6]">
          {t('stackedSection.practicalTestPre')}{' '}
          <span className="text-foreground font-[550]">Esc</span>{' '}
          {t('stackedSection.practicalTestPost')}
        </p>
      </OverlayDemo>
    </DemoSection>
  )
}
