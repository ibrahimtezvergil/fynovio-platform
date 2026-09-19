import { PenLine } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ImageUpload, RichTextInput, SignaturePad } from '@/components/common/inputs'
import { ControlDemo } from '@/features/demo-forms/components/ControlDemo'
import { DemoSection } from '@/components/common/DemoSection'
import { i18n } from '@/lib/i18n'
import { sanitizeHtml } from '@/lib/sanitizeHtml'

/**
 * Resolved once at module load via the shared `i18n` instance — this seeds a
 * `useState` initial value, not a live render, so it does not switch language
 * reactively. See `stageMeta()` in `StageBadge.tsx` for the same tradeoff.
 */
const SEED = [
  `<h2>${i18n.t('editorSection.seed.heading', { ns: 'demo-forms' })}</h2>`,
  `<p>${i18n.t('editorSection.seed.intro', { ns: 'demo-forms' })}</p>`,
  `<ul><li>${i18n.t('editorSection.seed.item1', { ns: 'demo-forms' })}</li><li>${i18n.t('editorSection.seed.item2', { ns: 'demo-forms' })}</li></ul>`,
  `<blockquote>${i18n.t('editorSection.seed.quote', { ns: 'demo-forms' })}</blockquote>`,
].join('')

export function EditorSection() {
  const { t } = useTranslation('demo-forms')
  const [body, setBody] = useState(SEED)
  const [signature, setSignature] = useState<string | null>(null)
  const [logo, setLogo] = useState<string | null>(null)

  return (
    <DemoSection
      id="editor"
      title={t('editorSection.title')}
      description={t('editorSection.description')}
      icon={PenLine}
    >
      <ControlDemo
        name="<RichTextInput/>"
        title={t('editorSection.richText.title')}
        description={t('editorSection.richText.description')}
        value={t('editorSection.richText.characterCount', { count: body.length })}
        wide
      >
        <RichTextInput
          aria-label={t('editorSection.richText.aria')}
          value={body}
          onValueChange={setBody}
          placeholder={t('editorSection.richText.placeholder')}
        />
        <div className="rounded-md border border-[var(--nx-hairline-soft)] bg-[var(--nx-surface)] p-3">
          <p className="mb-2 text-[11.5px] font-[590] text-muted-foreground">
            {t('editorSection.richText.previewLabel')}
          </p>
          <div
            className="text-[13.5px] leading-[1.6] [&_a]:text-accent-foreground [&_a]:underline [&_blockquote]:border-l-2 [&_blockquote]:border-[var(--nx-tint)] [&_blockquote]:pl-3 [&_code]:rounded-sm [&_code]:bg-[var(--nx-fill)] [&_code]:px-1 [&_code]:py-0.5 [&_h2]:mb-1.5 [&_h2]:text-[16px] [&_h2]:font-[620] [&_h3]:mb-1 [&_h3]:text-[14.5px] [&_h3]:font-[590] [&_li]:my-0.5 [&_ol]:my-2 [&_ol]:list-decimal [&_ol]:pl-5 [&_p]:my-0 [&_p+p]:mt-2.5 [&_ul]:my-2 [&_ul]:list-disc [&_ul]:pl-5"
            dangerouslySetInnerHTML={{ __html: sanitizeHtml(body) }}
          />
        </div>
      </ControlDemo>

      <ControlDemo
        name="<SignaturePad/>"
        title={t('editorSection.signature.title')}
        description={t('editorSection.signature.description')}
        value={
          signature
            ? t('editorSection.signature.characterCount', { count: signature.length })
            : 'null'
        }
      >
        <SignaturePad aria-label={t('editorSection.signature.aria')} value={signature} onValueChange={setSignature} />
      </ControlDemo>

      <ControlDemo
        name="<ImageUpload/>"
        title={t('editorSection.image.title')}
        description={t('editorSection.image.description')}
        value={logo ? `data:image/png · 256×256` : 'null'}
      >
        <ImageUpload
          aria-label={t('editorSection.image.aria')}
          value={logo}
          onValueChange={setLogo}
          maxSize={2 * 1024 * 1024}
          round
        />
      </ControlDemo>
    </DemoSection>
  )
}
