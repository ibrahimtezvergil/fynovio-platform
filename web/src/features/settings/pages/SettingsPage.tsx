import { zodResolver } from '@hookform/resolvers/zod'
import { Bell, ShieldCheck, UserRound } from 'lucide-react'
import { useMemo } from 'react'
import { FormProvider, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { PageHeader } from '@/components/common/PageHeader'
import { SectionNav, type NavSection } from '@/components/common/SectionNav'
import { NotificationsSection } from '@/features/settings/components/NotificationsSection'
import { ProfileSection } from '@/features/settings/components/ProfileSection'
import { SaveBar } from '@/features/settings/components/SaveBar'
import { SecuritySection } from '@/features/settings/components/SecuritySection'
import { settingsSchema, type SettingsValues } from '@/features/settings/schema'
import { useSettingsStore } from '@/features/settings/store/useSettingsStore'

/**
 * One form spanning several cards: the sections read it through
 * `FormProvider`, so the save bar can count and commit changes without any
 * section knowing about it. The `<form>` element itself lives in the save bar
 * — the security card carries its own, and forms cannot nest.
 */
export default function SettingsPage() {
  const { t } = useTranslation('settings')
  const saved = useSettingsStore((state) => state.saved)
  const save = useSettingsStore((state) => state.save)

  // Only sections that exist. A nav entry that leads nowhere is worse than none.
  const sections: readonly NavSection[] = useMemo(
    () => [
      { id: 'profil', label: t('page.sectionProfile'), icon: UserRound },
      { id: 'bildirimler', label: t('page.sectionNotifications'), icon: Bell },
      { id: 'guvenlik', label: t('page.sectionSecurity'), icon: ShieldCheck },
    ],
    [t],
  )

  const form = useForm<SettingsValues>({
    resolver: zodResolver(settingsSchema),
    defaultValues: saved,
    mode: 'onBlur',
  })

  const onSubmit = form.handleSubmit((values) => {
    save(values)
    // Rebase the dirty baseline onto what was just committed, so the bar reads
    // "saved" without a second source of truth for the diff.
    form.reset(values)
    toast.success(t('page.saved'))
  })

  return (
    <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
      <PageHeader title={t('page.title')} description={t('page.description')} />

      <div className="grid grid-cols-1 items-start gap-5 lg:grid-cols-[236px_minmax(0,1fr)]">
        <SectionNav sections={sections} label={t('page.sectionNavLabel')} />

        <div className="flex flex-col gap-4">
          <FormProvider {...form}>
            <ProfileSection />
            <NotificationsSection />
            <SecuritySection />
            <SaveBar onSubmit={onSubmit} />
          </FormProvider>
        </div>
      </div>
    </div>
  )
}
