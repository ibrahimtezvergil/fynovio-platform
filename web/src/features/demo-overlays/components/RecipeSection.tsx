import { Layers, Undo2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Field } from '@/components/common/Field'
import { Button } from '@/components/ui/button'
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

export function RecipeSection() {
  const { t } = useTranslation('demo-overlays')
  const initial = { name: t('recipeSection.initialName'), note: '' }
  const [draft, setDraft] = useState(initial)
  const [saved, setSaved] = useState(initial)
  const [editorOpen, setEditorOpen] = useState(false)
  const [guardOpen, setGuardOpen] = useState(false)
  const [stackOpen, setStackOpen] = useState(false)

  const dirty = draft.name !== saved.name || draft.note !== saved.note

  const requestClose = (open: boolean) => {
    if (open) {
      setEditorOpen(true)
      return
    }
    // Closing a dirty form is the one case where the dialog does not obey the
    // dismissal: it stays open and hands the decision to the alert dialog.
    if (dirty) setGuardOpen(true)
    else setEditorOpen(false)
  }

  return (
    <DemoSection
      id="kaliplar"
      title={t('page.sections.kaliplar')}
      description={t('recipeSection.description')}
      icon={Layers}
    >
      <OverlayDemo
        name="Dialog + AlertDialog"
        title={t('recipeSection.dirtyGuard.title')}
        description={t('recipeSection.dirtyGuard.description')}
        state={dirty ? t('recipeSection.dirtyGuard.stateDirty') : t('recipeSection.dirtyGuard.stateClean')}
        wide
      >
        <Dialog open={editorOpen} onOpenChange={requestClose}>
          <DialogTrigger render={<Button variant="outline" />}>
            {t('recipeSection.dirtyGuard.trigger')}
          </DialogTrigger>
          <DialogContent showCloseButton={false} className="sm:max-w-lg">
            <DialogHeader>
              <DialogTitle>{t('recipeSection.dirtyGuard.editTitle')}</DialogTitle>
              <DialogDescription>{t('recipeSection.dirtyGuard.editDescription')}</DialogDescription>
            </DialogHeader>
            <Field label={t('recipeSection.dirtyGuard.nameLabel')}>
              {(props) => (
                <Input
                  {...props}
                  value={draft.name}
                  onChange={(event) => setDraft({ ...draft, name: event.target.value })}
                />
              )}
            </Field>
            <Field label={t('recipeSection.dirtyGuard.noteLabel')}>
              {(props) => (
                <Textarea
                  {...props}
                  rows={3}
                  value={draft.note}
                  onChange={(event) => setDraft({ ...draft, note: event.target.value })}
                  placeholder={t('recipeSection.dirtyGuard.notePlaceholder')}
                />
              )}
            </Field>
            <DialogFooter>
              <Button variant="outline" onClick={() => requestClose(false)}>
                {t('recipeSection.dirtyGuard.cancel')}
              </Button>
              <Button
                disabled={!dirty}
                onClick={() => {
                  setSaved(draft)
                  setEditorOpen(false)
                  toast.success(t('recipeSection.dirtyGuard.savedToast'))
                }}
              >
                {t('recipeSection.dirtyGuard.save')}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Kept outside the editor's tree: it has to survive the moment the
            editor unmounts, and it must not inherit the editor's focus trap. */}
        <AlertDialog open={guardOpen} onOpenChange={setGuardOpen}>
          <AlertDialogContent size="sm">
            <AlertDialogHeader>
              <AlertDialogTitle>{t('recipeSection.dirtyGuard.guardTitle')}</AlertDialogTitle>
              <AlertDialogDescription>
                {t('recipeSection.dirtyGuard.guardDescription')}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>{t('recipeSection.dirtyGuard.backToEdit')}</AlertDialogCancel>
              <AlertDialogAction
                variant="destructive"
                onClick={() => {
                  setDraft(saved)
                  setEditorOpen(false)
                }}
              >
                {t('recipeSection.dirtyGuard.discard')}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        {dirty && (
          <Button variant="ghost" size="sm" onClick={() => setDraft(saved)}>
            <Undo2 />
            {t('recipeSection.dirtyGuard.undoDraft')}
          </Button>
        )}
      </OverlayDemo>

      <OverlayDemo
        name="Dialog içinde Dialog"
        title={t('recipeSection.nestedModal.title')}
        description={t('recipeSection.nestedModal.description')}
        state={stackOpen ? t('recipeSection.nestedModal.stateOpen') : t('recipeSection.nestedModal.stateEmpty')}
        wide
      >
        <Dialog>
          <DialogTrigger render={<Button variant="outline" />}>
            {t('recipeSection.nestedModal.trigger')}
          </DialogTrigger>
          <DialogContent className="sm:max-w-lg">
            <DialogHeader>
              <DialogTitle>{t('recipeSection.nestedModal.sendTitle')}</DialogTitle>
              <DialogDescription>{t('recipeSection.nestedModal.sendDescription')}</DialogDescription>
            </DialogHeader>
            <Field label={t('recipeSection.nestedModal.recipientLabel')}>
              {(props) => <Input {...props} defaultValue="satinalma@acme.com.tr" type="email" />}
            </Field>

            <Dialog open={stackOpen} onOpenChange={setStackOpen}>
              <DialogTrigger render={<Button variant="secondary" size="sm" />}>
                {t('recipeSection.nestedModal.addPersonTrigger')}
              </DialogTrigger>
              <DialogContent className="sm:max-w-sm">
                <DialogHeader>
                  <DialogTitle>{t('recipeSection.nestedModal.addPersonTitle')}</DialogTitle>
                  <DialogDescription>
                    {t('recipeSection.nestedModal.addPersonDescription')}
                  </DialogDescription>
                </DialogHeader>
                <Field label={t('recipeSection.nestedModal.emailLabel')}>
                  {(props) => <Input {...props} type="email" />}
                </Field>
                <DialogFooter>
                  <DialogClose render={<Button variant="outline" />}>
                    {t('recipeSection.nestedModal.cancel')}
                  </DialogClose>
                  <DialogClose render={<Button />}>{t('recipeSection.nestedModal.add')}</DialogClose>
                </DialogFooter>
              </DialogContent>
            </Dialog>

            <DialogFooter>
              <DialogClose render={<Button variant="outline" />}>
                {t('recipeSection.nestedModal.cancel')}
              </DialogClose>
              <DialogClose render={<Button />}>{t('recipeSection.nestedModal.send')}</DialogClose>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </OverlayDemo>
    </DemoSection>
  )
}
