import { LogOut, ShieldAlert, TriangleAlert, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogMedia,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Input } from '@/components/ui/input'
import { DemoSection } from '@/components/common/DemoSection'
import { OverlayDemo } from '@/components/common/OverlayDemo'

const CUSTOMER_NAME = 'Acme Endüstri A.Ş.'

export function AlertDialogSection() {
  const { t } = useTranslation('demo-overlays')
  const [deleted, setDeleted] = useState(false)
  const [typed, setTyped] = useState('')
  const [purged, setPurged] = useState('—')
  const confirmWord = t('alertDialogSection.typeToConfirm.confirmWord')

  return (
    <DemoSection
      id="alert-dialog"
      title={t('alertDialogSection.sectionTitle')}
      description={t('alertDialogSection.sectionDescription')}
      icon={ShieldAlert}
    >
      <OverlayDemo
        name="<AlertDialog>"
        title={t('alertDialogSection.destructive.title')}
        description={t('alertDialogSection.destructive.description')}
        state={
          deleted
            ? t('alertDialogSection.destructive.stateDeleted')
            : t('alertDialogSection.destructive.stateStanding')
        }
      >
        <AlertDialog>
          <AlertDialogTrigger render={<Button variant="destructive" />}>
            <Trash2 />
            {t('alertDialogSection.destructive.trigger')}
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogMedia data-tone="red">
                <TriangleAlert strokeWidth={1.75} />
              </AlertDialogMedia>
              <AlertDialogTitle>
                {t('alertDialogSection.destructive.confirmTitle', { name: CUSTOMER_NAME })}
              </AlertDialogTitle>
              <AlertDialogDescription>
                {t('alertDialogSection.destructive.confirmDescription')}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>{t('alertDialogSection.destructive.cancel')}</AlertDialogCancel>
              <AlertDialogAction
                variant="destructive"
                onClick={() => {
                  setDeleted(true)
                  toast.success(t('alertDialogSection.destructive.toastTitle'), {
                    description: CUSTOMER_NAME,
                  })
                }}
              >
                {t('alertDialogSection.destructive.confirm')}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
        {deleted && (
          <Button variant="ghost" size="sm" onClick={() => setDeleted(false)}>
            {t('alertDialogSection.destructive.undo')}
          </Button>
        )}
      </OverlayDemo>

      <OverlayDemo
        name="AlertDialogAction disabled"
        title={t('alertDialogSection.typeToConfirm.title')}
        description={t('alertDialogSection.typeToConfirm.description')}
        state={purged}
      >
        <AlertDialog onOpenChange={(open) => !open && setTyped('')}>
          <AlertDialogTrigger render={<Button variant="outline" />}>
            {t('alertDialogSection.typeToConfirm.trigger')}
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogMedia data-tone="red">
                <ShieldAlert strokeWidth={1.75} />
              </AlertDialogMedia>
              <AlertDialogTitle>{t('alertDialogSection.typeToConfirm.confirmTitle')}</AlertDialogTitle>
              <AlertDialogDescription>
                {t('alertDialogSection.typeToConfirm.confirmDescriptionPrefix')}{' '}
                <strong className="text-foreground">{confirmWord}</strong>{' '}
                {t('alertDialogSection.typeToConfirm.confirmDescriptionSuffix')}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <Input
              value={typed}
              onChange={(event) => setTyped(event.target.value)}
              placeholder={confirmWord}
              aria-label={t('alertDialogSection.typeToConfirm.inputAria')}
            />
            <AlertDialogFooter>
              <AlertDialogCancel>{t('alertDialogSection.typeToConfirm.cancel')}</AlertDialogCancel>
              <AlertDialogAction
                variant="destructive"
                disabled={typed !== confirmWord}
                onClick={() => setPurged(t('alertDialogSection.typeToConfirm.resultText'))}
              >
                {t('alertDialogSection.typeToConfirm.confirm')}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </OverlayDemo>

      <OverlayDemo
        name='size="sm"'
        title={t('alertDialogSection.smallConfirm.title')}
        description={t('alertDialogSection.smallConfirm.description')}
      >
        <AlertDialog>
          <AlertDialogTrigger render={<Button variant="outline" />}>
            <LogOut />
            {t('alertDialogSection.smallConfirm.trigger')}
          </AlertDialogTrigger>
          <AlertDialogContent size="sm">
            <AlertDialogHeader>
              <AlertDialogTitle>{t('alertDialogSection.smallConfirm.confirmTitle')}</AlertDialogTitle>
              <AlertDialogDescription>
                {t('alertDialogSection.smallConfirm.confirmDescription')}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>{t('alertDialogSection.smallConfirm.stay')}</AlertDialogCancel>
              <AlertDialogAction onClick={() => toast(t('alertDialogSection.smallConfirm.toastText'))}>
                {t('alertDialogSection.smallConfirm.confirm')}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </OverlayDemo>
    </DemoSection>
  )
}
