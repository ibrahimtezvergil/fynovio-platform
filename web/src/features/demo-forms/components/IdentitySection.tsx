import type { CountryCode } from 'libphonenumber-js'
import { Fingerprint } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  BarcodeInput,
  CardCvcInput,
  CardExpiryInput,
  CardNumberInput,
  IbanInput,
  MaskedInput,
  PhoneInput,
  TaxIdInput,
  cardBrand,
  isPhoneComplete,
  isValidCvc,
  isValidExpiry,
  isValidIban,
  isValidTaxId,
  sanitizeDigits,
} from '@/components/common/inputs'
import { ControlDemo, show } from '@/features/demo-forms/components/ControlDemo'
import { DemoSection } from '@/components/common/DemoSection'

/** Turkish plate: two-digit province, one to three letters, one to four digits. */
const sanitizePlate = (raw: string) =>
  raw.toLocaleUpperCase('tr').replace(/[^0-9A-ZÇĞİÖŞÜ]/g, '')

const displayPlate = (clean: string) => {
  const match = /^(\d{0,2})([A-ZÇĞİÖŞÜ]{0,3})(\d{0,4})$/.exec(clean)
  if (!match) return clean
  return [match[1], match[2], match[3]].filter(Boolean).join(' ')
}

export function IdentitySection() {
  const { t } = useTranslation('demo-forms')
  const [phone, setPhone] = useState('')
  const [country, setCountry] = useState<CountryCode>('TR')
  const [iban, setIban] = useState('TR330006100519786457841326')
  const [taxId, setTaxId] = useState('')
  const [card, setCard] = useState('')
  const [plate, setPlate] = useState('')
  const [postal, setPostal] = useState('')
  const [expiry, setExpiry] = useState('')
  const [cvc, setCvc] = useState('')
  const [scans, setScans] = useState<string[]>([])

  return (
    <DemoSection
      id="kimlik"
      title={t('identitySection.title')}
      description={t('identitySection.description')}
      icon={Fingerprint}
    >
      <ControlDemo
        name="<PhoneInput/>"
        title={t('identitySection.phone.title')}
        description={t('identitySection.phone.description')}
        value={`${show(phone)} · ${isPhoneComplete(phone) ? t('identitySection.status.valid') : t('identitySection.status.incomplete')}`}
      >
        <PhoneInput
          aria-label={t('identitySection.phone.aria')}
          value={phone}
          onValueChange={setPhone}
          country={country}
          onCountryChange={setCountry}
        />
      </ControlDemo>

      <ControlDemo
        name="<IbanInput/>"
        title={t('identitySection.iban.title')}
        description={t('identitySection.iban.description')}
        value={`${show(iban)} · ${isValidIban(iban) ? t('identitySection.status.valid') : t('identitySection.status.invalid')}`}
      >
        <IbanInput aria-label={t('identitySection.iban.aria')} value={iban} onValueChange={setIban} />
      </ControlDemo>

      <ControlDemo
        name="<TaxIdInput/>"
        title={t('identitySection.taxId.title')}
        description={t('identitySection.taxId.description')}
        value={`${show(taxId)} · ${isValidTaxId(taxId) ? t('identitySection.status.valid') : t('identitySection.status.invalid')}`}
      >
        <TaxIdInput aria-label={t('identitySection.taxId.aria')} value={taxId} onValueChange={setTaxId} />
      </ControlDemo>

      <ControlDemo
        name="<CardNumberInput/>"
        title={t('identitySection.cardNumber.title')}
        description={t('identitySection.cardNumber.description')}
        value={`${show(card)} · ${cardBrand(card) ?? t('identitySection.status.unknown')}`}
      >
        <CardNumberInput aria-label={t('identitySection.cardNumber.aria')} value={card} onValueChange={setCard} />
      </ControlDemo>

      <ControlDemo
        name="<MaskedInput/>"
        title={t('identitySection.plate.title')}
        description={t('identitySection.plate.description')}
        value={show(plate)}
      >
        <MaskedInput
          aria-label={t('identitySection.plate.aria')}
          value={plate}
          onValueChange={setPlate}
          sanitize={sanitizePlate}
          display={displayPlate}
          maxLength={9}
          placeholder="34 ABC 1234"
        />
      </ControlDemo>

      <ControlDemo
        name="<MaskedInput/>"
        title={t('identitySection.postal.title')}
        description={t('identitySection.postal.description')}
        value={show(postal)}
      >
        <MaskedInput
          aria-label={t('identitySection.postal.aria')}
          value={postal}
          onValueChange={setPostal}
          sanitize={sanitizeDigits}
          display={(clean) => clean}
          maxLength={5}
          inputMode="numeric"
          placeholder="34394"
        />
      </ControlDemo>
      <ControlDemo
        name="<CardExpiryInput/> · <CardCvcInput/>"
        title={t('identitySection.expiryCvc.title')}
        description={t('identitySection.expiryCvc.description')}
        value={`${show(expiry)} · ${isValidExpiry(expiry) ? t('identitySection.status.valid') : t('identitySection.status.invalid')} / ${show(cvc)} · ${isValidCvc(cvc, cardBrand(card) === 'amex') ? t('identitySection.status.valid') : t('identitySection.status.invalid')}`}
      >
        <div className="grid grid-cols-2 gap-2.5">
          <CardExpiryInput aria-label={t('identitySection.expiryCvc.expiryAria')} value={expiry} onValueChange={setExpiry} />
          <CardCvcInput
            aria-label={t('identitySection.expiryCvc.cvcAria')}
            value={cvc}
            onValueChange={setCvc}
            cardNumber={card}
          />
        </div>
      </ControlDemo>

      <ControlDemo
        name="<BarcodeInput/>"
        title={t('identitySection.barcode.title')}
        description={t('identitySection.barcode.description')}
        value={scans.length > 0 ? show(scans.slice(-3)) : '[]'}
      >
        <BarcodeInput
          aria-label={t('identitySection.barcode.aria')}
          onSubmit={(code) => setScans((current) => [...current, code])}
        />
      </ControlDemo>
    </DemoSection>
  )
}
