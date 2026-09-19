import { Paperclip } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ColorInput, FileDropzone, RatingInput } from '@/components/common/inputs'
import { ControlDemo, show } from '@/features/demo-forms/components/ControlDemo'
import { DemoSection } from '@/components/common/DemoSection'

const MAX_ATTACHMENT = 5 * 1024 * 1024

export function MediaSection() {
  const { t } = useTranslation('demo-forms')
  const [files, setFiles] = useState<File[]>([])
  const [color, setColor] = useState('#6355C7')
  const [score, setScore] = useState(4)

  return (
    <DemoSection
      id="medya"
      title={t('mediaSection.title')}
      description={t('mediaSection.description')}
      icon={Paperclip}
    >
      <ControlDemo
        name="<FileDropzone/>"
        title={t('mediaSection.dropzone.title')}
        description={t('mediaSection.dropzone.description')}
        value={files.length > 0 ? show(files.map((file) => file.name)) : '[]'}
        wide
      >
        <FileDropzone
          aria-label={t('mediaSection.dropzone.aria')}
          value={files}
          onValueChange={setFiles}
          accept=".pdf,.png,.jpg,.jpeg,.xlsx,.csv"
          maxSize={MAX_ATTACHMENT}
          hint={t('mediaSection.dropzone.hint')}
        />
      </ControlDemo>

      <ControlDemo
        name="<ColorInput/>"
        title={t('mediaSection.color.title')}
        description={t('mediaSection.color.description')}
        value={show(color)}
      >
        <ColorInput aria-label={t('mediaSection.color.aria')} value={color} onValueChange={setColor} />
      </ControlDemo>

      <ControlDemo
        name="<RatingInput/>"
        title={t('mediaSection.rating.title')}
        description={t('mediaSection.rating.description')}
        value={show(score)}
      >
        <RatingInput aria-label={t('mediaSection.rating.aria')} value={score} onValueChange={setScore} />
      </ControlDemo>
    </DemoSection>
  )
}
