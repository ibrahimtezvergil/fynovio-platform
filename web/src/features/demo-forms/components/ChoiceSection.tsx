import { ListChecks } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  CheckboxGroupField,
  ComboboxInput,
  MultiSelect,
  RadioCards,
  RadioGroupField,
  RichSelect,
  TagInput,
  ToggleGroupField,
} from '@/components/common/inputs'
import { SegmentedControl } from '@/components/common/SegmentedControl'
import { Checkbox } from '@/components/ui/checkbox'
import { Select } from '@/components/ui/select'
import { Switch } from '@/components/ui/switch'
import { ControlDemo, show } from '@/features/demo-forms/components/ControlDemo'
import { DemoSection } from '@/components/common/DemoSection'
import {
  CITIES,
  CUSTOMERS,
  DELIVERY_TERMS,
  DOCUMENT_TYPES,
  LEAD_SOURCES,
  PAYMENT_TERMS,
  PERMISSIONS,
  PRIORITIES,
  SECTORS,
  WEEKDAYS,
} from '@/features/demo-forms/data/options'

export function ChoiceSection() {
  const { t } = useTranslation('demo-forms')
  const VIEWS = [
    { value: 'liste', label: t('choiceSection.views.liste') },
    { value: 'pano', label: t('choiceSection.views.pano') },
    { value: 'takvim', label: t('choiceSection.views.takvim') },
  ] as const
  const [sector, setSector] = useState('bilisim')
  const [city, setCity] = useState<string | null>('34')
  const [customer, setCustomer] = useState<string | null>(null)
  const [permissions, setPermissions] = useState<string[]>(['teklif', 'rapor'])
  const [tags, setTags] = useState<string[]>(['yenileme', 'kurumsal'])
  const [priority, setPriority] = useState<string | null>('normal')
  const [documentType, setDocumentType] = useState<string | null>('proforma')
  const [channels, setChannels] = useState<string[]>(['eposta'])
  const [days, setDays] = useState<string[]>(['pzt', 'car', 'cum'])
  const [view, setView] = useState<'liste' | 'pano' | 'takvim'>('liste')
  const [term, setTerm] = useState<string | null>('cif')
  const [autoApprove, setAutoApprove] = useState(true)
  const [consent, setConsent] = useState(false)

  return (
    <DemoSection
      id="secim"
      title={t('choiceSection.title')}
      description={t('choiceSection.description')}
      icon={ListChecks}
    >
      <ControlDemo
        name="<Select/>"
        title={t('choiceSection.localSelect.title')}
        description={t('choiceSection.localSelect.description')}
        value={show(sector)}
      >
        <Select
          aria-label={t('choiceSection.localSelect.aria')}
          value={sector}
          onChange={(event) => setSector(event.target.value)}
        >
          {SECTORS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </Select>
      </ControlDemo>

      <ControlDemo
        name="<RichSelect/>"
        title={t('choiceSection.richSelectGrouped.title')}
        description={t('choiceSection.richSelectGrouped.description')}
        value={show(city)}
      >
        <RichSelect
          aria-label={t('choiceSection.richSelectGrouped.aria')}
          value={city}
          onValueChange={setCity}
          options={CITIES}
          placeholder={t('choiceSection.richSelectGrouped.placeholder')}
        />
      </ControlDemo>

      <ControlDemo
        name="<ComboboxInput/>"
        title={t('choiceSection.combobox.title')}
        description={t('choiceSection.combobox.description')}
        value={show(customer)}
      >
        <ComboboxInput
          aria-label={t('choiceSection.combobox.aria')}
          value={customer}
          onValueChange={setCustomer}
          options={CUSTOMERS}
          placeholder={t('choiceSection.combobox.placeholder')}
        />
      </ControlDemo>

      <ControlDemo
        name="<MultiSelect/>"
        title={t('choiceSection.multiSelect.title')}
        description={t('choiceSection.multiSelect.description')}
        value={show(permissions)}
      >
        <MultiSelect
          aria-label={t('choiceSection.multiSelect.aria')}
          value={permissions}
          onValueChange={setPermissions}
          options={PERMISSIONS}
        />
      </ControlDemo>

      <ControlDemo
        name="<TagInput/>"
        title={t('choiceSection.tagInput.title')}
        description={t('choiceSection.tagInput.description')}
        value={show(tags)}
      >
        <TagInput aria-label={t('choiceSection.tagInput.aria')} value={tags} onValueChange={setTags} maxTags={6} />
      </ControlDemo>

      <ControlDemo
        name="<RadioGroupField/>"
        title={t('choiceSection.radioGroup.title')}
        description={t('choiceSection.radioGroup.description')}
        value={show(priority)}
      >
        <RadioGroupField
          aria-label={t('choiceSection.radioGroup.aria')}
          value={priority}
          onValueChange={setPriority}
          options={PRIORITIES}
          orientation="horizontal"
        />
      </ControlDemo>

      <ControlDemo
        name="<RadioCards/>"
        title={t('choiceSection.radioCards.title')}
        description={t('choiceSection.radioCards.description')}
        value={show(documentType)}
        wide
      >
        <RadioCards
          aria-label={t('choiceSection.radioCards.aria')}
          value={documentType}
          onValueChange={setDocumentType}
          options={DOCUMENT_TYPES}
        />
      </ControlDemo>

      <ControlDemo
        name="<CheckboxGroupField/>"
        title={t('choiceSection.checkboxGroup.title')}
        description={t('choiceSection.checkboxGroup.description')}
        value={show(channels)}
      >
        <CheckboxGroupField
          aria-label={t('choiceSection.checkboxGroup.aria')}
          value={channels}
          onValueChange={setChannels}
          options={LEAD_SOURCES.slice(0, 4)}
        />
      </ControlDemo>

      <ControlDemo
        name="<ToggleGroupField/>"
        title={t('choiceSection.toggleGroup.title')}
        description={t('choiceSection.toggleGroup.description')}
        value={show(days)}
      >
        <ToggleGroupField
          aria-label={t('choiceSection.toggleGroup.aria')}
          value={days}
          onValueChange={setDays}
          options={WEEKDAYS}
        />
      </ControlDemo>

      <ControlDemo
        name="<SegmentedControl/>"
        title={t('choiceSection.segmented.title')}
        description={t('choiceSection.segmented.description')}
        value={show(view)}
      >
        <SegmentedControl
          aria-label={t('choiceSection.segmented.aria')}
          segments={VIEWS}
          value={view}
          onChange={setView}
        />
      </ControlDemo>

      <ControlDemo
        name="<RichSelect/>"
        title={t('choiceSection.deliveryTerm.title')}
        description={t('choiceSection.deliveryTerm.description')}
        value={show(term)}
      >
        <RichSelect
          aria-label={t('choiceSection.deliveryTerm.aria')}
          value={term}
          onValueChange={setTerm}
          options={DELIVERY_TERMS}
        />
      </ControlDemo>

      <ControlDemo
        name="<Switch/> · <Checkbox/>"
        title={t('choiceSection.binaryState.title')}
        description={t('choiceSection.binaryState.description')}
        value={`${show(autoApprove)} · ${show(consent)}`}
      >
        <div className="flex flex-col gap-3">
          <label className="flex items-center justify-between gap-3 text-[13.5px]">
            <span className="flex flex-col">
              {t('choiceSection.binaryState.autoApproveLabel')}
              <span className="text-muted-foreground text-[11.5px]">
                {t('choiceSection.binaryState.autoApproveHint')}
              </span>
            </span>
            <Switch checked={autoApprove} onCheckedChange={setAutoApprove} />
          </label>
          <label className="flex items-start gap-2.5 text-[13.5px]">
            <Checkbox
              checked={consent}
              onChange={(event) => setConsent(event.target.checked)}
            />
            <span className="flex flex-col">
              {t('choiceSection.binaryState.consentLabel')}
              <span className="text-muted-foreground text-[11.5px]">
                {t('choiceSection.binaryState.consentHint')}
              </span>
            </span>
          </label>
        </div>
      </ControlDemo>

      <ControlDemo
        name="<Select/>"
        title={t('choiceSection.paymentTerm.title')}
        description={t('choiceSection.paymentTerm.description')}
        value="—"
      >
        <Select aria-label={t('choiceSection.paymentTerm.aria')} defaultValue="vade-30">
          {PAYMENT_TERMS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </Select>
      </ControlDemo>
    </DemoSection>
  )
}
