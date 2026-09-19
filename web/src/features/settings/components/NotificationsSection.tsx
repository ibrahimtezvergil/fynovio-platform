import { useMemo } from 'react'
import { Controller, useFormContext } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import { Card } from '@/components/ui/card'
import { Switch } from '@/components/ui/switch'
import { SectionHeading } from '@/features/settings/components/SectionHeading'
import type {
  NotificationChannel,
  NotificationToggles,
  SettingsValues,
} from '@/features/settings/schema'

/**
 * Neither control is a native input, so both go through `Controller` rather
 * than `register` — that is what keeps them inside the form's dirty state.
 */
export function NotificationsSection() {
  const { t } = useTranslation('settings')
  const { control } = useFormContext<SettingsValues>()

  const channels: readonly Segment<NotificationChannel>[] = useMemo(
    () => [
      { value: 'app', label: t('notifications.channelApp') },
      { value: 'email', label: t('notifications.channelEmail') },
      { value: 'digest', label: t('notifications.channelDigest') },
    ],
    [t],
  )

  const rules: { key: keyof NotificationToggles; title: string; description: string }[] = useMemo(
    () => [
      {
        key: 'stageChange',
        title: t('notifications.stageChangeTitle'),
        description: t('notifications.stageChangeDescription'),
      },
      {
        key: 'quoteViewed',
        title: t('notifications.quoteViewedTitle'),
        description: t('notifications.quoteViewedDescription'),
      },
      {
        key: 'weeklyDigest',
        title: t('notifications.weeklyDigestTitle'),
        description: t('notifications.weeklyDigestDescription'),
      },
      {
        key: 'closingSoon',
        title: t('notifications.closingSoonTitle'),
        description: t('notifications.closingSoonDescription'),
      },
    ],
    [t],
  )

  return (
    <Card id="bildirimler" className="scroll-mt-24 gap-0 p-0">
      <div className="flex flex-wrap items-center gap-3.5 px-6 pt-5 pb-4">
        <SectionHeading title={t('notifications.title')} description={t('notifications.description')} />
        <Controller
          control={control}
          name="channel"
          render={({ field }) => (
            <SegmentedControl
              aria-label={t('notifications.channelLabel')}
              segments={channels}
              value={field.value}
              onChange={field.onChange}
            />
          )}
        />
      </div>

      <div className="flex flex-col border-t border-[var(--nx-hairline)]">
        {rules.map((rule) => (
          <div key={rule.key} className="nx-row items-start px-5 py-3.5">
            <span className="flex min-w-0 flex-1 flex-col gap-0.5">
              <span className="text-[13.5px] font-[550]">{rule.title}</span>
              <span className="text-muted-foreground text-[12px] leading-[1.45]">
                {rule.description}
              </span>
            </span>
            <Controller
              control={control}
              name={`notifications.${rule.key}`}
              render={({ field }) => (
                <Switch
                  aria-label={rule.title}
                  checked={field.value}
                  onCheckedChange={field.onChange}
                />
              )}
            />
          </div>
        ))}
      </div>
    </Card>
  )
}
