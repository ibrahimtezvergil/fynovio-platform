import { Placeholder } from '@tiptap/extensions'
import StarterKit from '@tiptap/starter-kit'
import { EditorContent, useEditor, useEditorState } from '@tiptap/react'
import {
  Bold,
  Italic,
  Link2,
  List,
  ListOrdered,
  Quote,
  Redo2,
  Strikethrough,
  Underline as UnderlineIcon,
  Undo2,
} from 'lucide-react'
import { useEffect, type ComponentType } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import type { FieldControlProps } from './types'

/**
 * Prose styling for the editor body. Local to this component rather than a
 * global `.prose`: nothing else in the app renders authored HTML, and the day
 * something does, it will want its own rules.
 */
const CONTENT = [
  '[&_.tiptap]:min-h-[9rem] [&_.tiptap]:px-[13px] [&_.tiptap]:py-2.5 [&_.tiptap]:outline-none',
  '[&_.tiptap]:text-[13.5px] [&_.tiptap]:leading-[1.6]',
  '[&_p]:my-0 [&_p+p]:mt-2.5',
  '[&_h2]:mt-3 [&_h2]:mb-1.5 [&_h2]:text-[16px] [&_h2]:font-[620]',
  '[&_h3]:mt-3 [&_h3]:mb-1 [&_h3]:text-[14.5px] [&_h3]:font-[590]',
  '[&_ul]:my-2 [&_ul]:list-disc [&_ul]:pl-5 [&_ol]:my-2 [&_ol]:list-decimal [&_ol]:pl-5',
  '[&_li]:my-0.5 [&_li>p]:my-0',
  '[&_blockquote]:my-2 [&_blockquote]:border-l-2 [&_blockquote]:border-[var(--nx-tint)] [&_blockquote]:pl-3 [&_blockquote]:text-muted-foreground',
  '[&_a]:text-accent-foreground [&_a]:underline [&_a]:underline-offset-2',
  '[&_code]:rounded-sm [&_code]:bg-[var(--nx-fill)] [&_code]:px-1 [&_code]:py-0.5 [&_code]:font-mono [&_code]:text-[12px]',
  '[&_.tiptap_p.is-editor-empty:first-child::before]:text-[var(--nx-label-3)]',
  '[&_.tiptap_p.is-editor-empty:first-child::before]:float-left',
  '[&_.tiptap_p.is-editor-empty:first-child::before]:h-0',
  '[&_.tiptap_p.is-editor-empty:first-child::before]:pointer-events-none',
  '[&_.tiptap_p.is-editor-empty:first-child::before]:content-[attr(data-placeholder)]',
].join(' ')

const TOOL = [
  'flex size-7 cursor-pointer items-center justify-center rounded-sm border-0 bg-transparent',
  'text-muted-foreground outline-none transition-colors duration-[150ms] ease-fluid',
  'hover:bg-[var(--nx-fill-hover)] hover:text-foreground',
  'aria-pressed:bg-accent aria-pressed:text-accent-foreground',
  'disabled:pointer-events-none disabled:opacity-35',
].join(' ')

export interface RichTextInputProps extends FieldControlProps {
  /** HTML. Sanitise it with `sanitizeHtml()` on the way back in — this control does not. */
  value: string
  onValueChange: (value: string) => void
  placeholder?: string
  disabled?: boolean
  className?: string
}

/**
 * The formatted body of a quote, a contract clause, an e-mail template.
 *
 * It emits HTML, which is the honest format for text that carries emphasis and
 * lists — but HTML from a user is untrusted input, so whatever renders it later
 * sanitises it. This control's job ends at producing it.
 */
export function RichTextInput({
  value,
  onValueChange,
  placeholder,
  disabled,
  className,
  id,
  ...aria
}: RichTextInputProps) {
  const { t } = useTranslation('common')
  const resolvedPlaceholder = placeholder ?? t('richTextInput.placeholder')
  const editor = useEditor({
    extensions: [
      StarterKit.configure({ link: { openOnClick: false } }),
      Placeholder.configure({ placeholder: resolvedPlaceholder }),
    ],
    content: value,
    editable: !disabled,
    onUpdate: ({ editor: instance }) => onValueChange(instance.getHTML()),
    editorProps: {
      attributes: {
        role: 'textbox',
        'aria-multiline': 'true',
        ...(id ? { id } : {}),
        ...(aria['aria-label'] ? { 'aria-label': aria['aria-label'] } : {}),
        ...(aria['aria-describedby'] ? { 'aria-describedby': aria['aria-describedby'] } : {}),
      },
    },
  })

  // Only for changes that did not come from typing — a reset, or a template
  // being loaded. Writing back what the editor already has would move the caret.
  //
  // The `isDestroyed` guard is not defensive padding: under StrictMode React
  // tears the editor down and replays passive effects on remount, and reading
  // `getHTML()` off a destroyed editor throws inside ProseMirror's serializer.
  useEffect(() => {
    if (!editor || editor.isDestroyed || editor.getHTML() === value) return
    editor.commands.setContent(value, { emitUpdate: false })
  }, [editor, value])

  const state = useEditorState({
    editor,
    selector: ({ editor: instance }) =>
      instance
        ? {
            bold: instance.isActive('bold'),
            italic: instance.isActive('italic'),
            underline: instance.isActive('underline'),
            strike: instance.isActive('strike'),
            bulletList: instance.isActive('bulletList'),
            orderedList: instance.isActive('orderedList'),
            blockquote: instance.isActive('blockquote'),
            link: instance.isActive('link'),
            canUndo: instance.can().undo(),
            canRedo: instance.can().redo(),
          }
        : null,
  })

  if (!editor) return null

  const toggleLink = () => {
    if (state?.link) {
      editor.chain().focus().unsetLink().run()
      return
    }
    const href = window.prompt(t('richTextInput.linkPrompt'))
    if (!href) return
    editor.chain().focus().setLink({ href }).run()
  }

  return (
    <div
      className={cn(
        'flex flex-col overflow-hidden rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-fill)]',
        'transition-[background,border-color,box-shadow] duration-[250ms] ease-fluid',
        'focus-within:border-ring focus-within:bg-[var(--nx-surface)] focus-within:shadow-[0_0_0_4px_var(--nx-tint-fill)]',
        'has-[[aria-invalid=true]]:border-destructive',
        disabled && 'pointer-events-none opacity-45',
        className,
      )}
    >
      <div
        role="toolbar"
        aria-label={t('richTextInput.toolbar')}
        className="flex flex-wrap items-center gap-0.5 border-b border-[var(--nx-hairline)] px-1.5 py-1.5"
      >
        <Tool icon={Bold} label={t('richTextInput.bold')} pressed={state?.bold} onClick={() => editor.chain().focus().toggleBold().run()} />
        <Tool icon={Italic} label={t('richTextInput.italic')} pressed={state?.italic} onClick={() => editor.chain().focus().toggleItalic().run()} />
        <Tool icon={UnderlineIcon} label={t('richTextInput.underline')} pressed={state?.underline} onClick={() => editor.chain().focus().toggleUnderline().run()} />
        <Tool icon={Strikethrough} label={t('richTextInput.strikethrough')} pressed={state?.strike} onClick={() => editor.chain().focus().toggleStrike().run()} />
        <Divider />
        <Tool icon={List} label={t('richTextInput.bulletList')} pressed={state?.bulletList} onClick={() => editor.chain().focus().toggleBulletList().run()} />
        <Tool icon={ListOrdered} label={t('richTextInput.orderedList')} pressed={state?.orderedList} onClick={() => editor.chain().focus().toggleOrderedList().run()} />
        <Tool icon={Quote} label={t('richTextInput.blockquote')} pressed={state?.blockquote} onClick={() => editor.chain().focus().toggleBlockquote().run()} />
        <Tool icon={Link2} label={t('richTextInput.link')} pressed={state?.link} onClick={toggleLink} />
        <Divider />
        <Tool icon={Undo2} label={t('richTextInput.undo')} disabled={!state?.canUndo} onClick={() => editor.chain().focus().undo().run()} />
        <Tool icon={Redo2} label={t('richTextInput.redo')} disabled={!state?.canRedo} onClick={() => editor.chain().focus().redo().run()} />
      </div>
      <EditorContent editor={editor} className={CONTENT} />
    </div>
  )
}

function Tool({
  icon: Icon,
  label,
  pressed,
  disabled,
  onClick,
}: {
  icon: ComponentType<{ className?: string; strokeWidth?: number; 'aria-hidden'?: boolean }>
  label: string
  pressed?: boolean
  disabled?: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      aria-pressed={pressed ?? false}
      disabled={disabled}
      onClick={onClick}
      className={TOOL}
    >
      <Icon aria-hidden className="size-4" strokeWidth={1.9} />
    </button>
  )
}

function Divider() {
  return <span aria-hidden className="mx-1 h-5 w-px bg-[var(--nx-hairline)]" />
}
