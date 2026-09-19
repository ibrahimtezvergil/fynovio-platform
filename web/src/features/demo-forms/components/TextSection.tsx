import { Type } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  OtpInput,
  PasswordInput,
  SearchInput,
  TextareaCounter,
} from '@/components/common/inputs'
import { Input } from '@/components/ui/input'
import { ControlDemo, show } from '@/features/demo-forms/components/ControlDemo'
import { DemoSection } from '@/components/common/DemoSection'

export function TextSection() {
  const { t } = useTranslation('demo-forms')
  const [company, setCompany] = useState('Fynovio Teknoloji A.Ş.')
  const [email, setEmail] = useState('')
  const [website, setWebsite] = useState('')
  const [query, setQuery] = useState('')
  const [password, setPassword] = useState('')
  const [note, setNote] = useState('')
  const [code, setCode] = useState('')

  return (
    <DemoSection
      id="metin"
      title={t('textSection.title')}
      description={t('textSection.description')}
      icon={Type}
    >
      <ControlDemo
        name="<Input/>"
        title={t('textSection.singleLine.title')}
        description={t('textSection.singleLine.description')}
        value={show(company)}
      >
        <Input
          aria-label={t('textSection.singleLine.aria')}
          value={company}
          onChange={(event) => setCompany(event.target.value)}
          placeholder={t('textSection.singleLine.placeholder')}
        />
      </ControlDemo>

      <ControlDemo
        name='<Input type="email"/>'
        title={t('textSection.email.title')}
        description={t('textSection.email.description')}
        value={show(email)}
      >
        <Input
          aria-label={t('textSection.email.aria')}
          type="email"
          inputMode="email"
          autoComplete="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          placeholder={t('textSection.email.placeholder')}
        />
      </ControlDemo>

      <ControlDemo
        name='<Input type="url"/>'
        title={t('textSection.url.title')}
        description={t('textSection.url.description')}
        value={show(website)}
      >
        <Input
          aria-label={t('textSection.url.aria')}
          type="url"
          inputMode="url"
          value={website}
          onChange={(event) => setWebsite(event.target.value)}
          placeholder={t('textSection.url.placeholder')}
        />
      </ControlDemo>

      <ControlDemo
        name="<SearchInput/>"
        title={t('textSection.search.title')}
        description={t('textSection.search.description')}
        value={show(query)}
      >
        <SearchInput
          aria-label={t('textSection.search.aria')}
          value={query}
          onValueChange={setQuery}
          placeholder={t('textSection.search.placeholder')}
        />
      </ControlDemo>

      <ControlDemo
        name="<PasswordInput/>"
        title={t('textSection.password.title')}
        description={t('textSection.password.description')}
        value={`"${'•'.repeat(password.length)}"`}
      >
        <PasswordInput
          aria-label={t('textSection.password.aria')}
          value={password}
          onValueChange={setPassword}
          autoComplete="new-password"
          showStrength
        />
      </ControlDemo>

      <ControlDemo
        name="<OtpInput/>"
        title={t('textSection.otp.title')}
        description={t('textSection.otp.description')}
        value={show(code)}
      >
        <OtpInput aria-label={t('textSection.otp.aria')} value={code} onValueChange={setCode} />
      </ControlDemo>

      <ControlDemo
        name="<TextareaCounter/>"
        title={t('textSection.textarea.title')}
        description={t('textSection.textarea.description')}
        value={t('textSection.textarea.characterCount', { count: note.length })}
        wide
      >
        <TextareaCounter
          aria-label={t('textSection.textarea.aria')}
          value={note}
          onValueChange={setNote}
          maxLength={240}
          placeholder={t('textSection.textarea.placeholder')}
        />
      </ControlDemo>
    </DemoSection>
  )
}
