import { Check, Copy } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

/**
 * Token classes are deliberately few. This is a documentation aid, not a
 * syntax-highlighting engine — every token is produced by the tokenizer below,
 * so nothing user-supplied is ever interpreted as markup.
 */
const TOKEN_CLASS = {
  comment: 'text-muted-foreground italic',
  string: 'text-stage-ready',
  keyword: 'text-stage-meeting',
  api: 'text-stage-contacted',
  number: 'text-stage-quoted',
  plain: '',
} as const

type TokenKind = keyof typeof TOKEN_CLASS

const KEYWORDS = new Set([
  'const', 'let', 'export', 'import', 'from', 'function', 'return', 'type',
  'interface', 'as', 'true', 'false', 'null', 'undefined', 'new', 'await',
  'async', 'if', 'else', 'default', 'extends', 'typeof', 'keyof', 'void',
])

/** Anything from the table API surface reads as one colour. */
const API_PATTERN =
  /^(tableFeatures|useTable|createColumnHelper|createTableHook|Subscribe|FlexRender|functionalUpdate|[a-z][A-Za-z]*(Feature|RowModel)|create[A-Z][A-Za-z]*|sortFn_[a-z]+|filterFn_[a-zA-Z]+|aggregationFn_[a-zA-Z]+)$/

const TOKEN_PATTERN =
  /(\/\/[^\n]*|\/\*[\s\S]*?\*\/)|('[^'\n]*'|"[^"\n]*"|`[^`]*`)|(\b\d[\d_]*\b)|([A-Za-z_$][\w$]*)/g

function tokenize(source: string): ReactNode[] {
  const nodes: ReactNode[] = []
  let lastIndex = 0
  let key = 0

  const push = (text: string, kind: TokenKind) => {
    if (!text) return
    nodes.push(
      kind === 'plain' ? (
        text
      ) : (
        <span key={`t${key++}`} className={TOKEN_CLASS[kind]}>
          {text}
        </span>
      ),
    )
  }

  for (const match of source.matchAll(TOKEN_PATTERN)) {
    const [raw, comment, quoted, numeric, word] = match
    const index = match.index ?? 0
    push(source.slice(lastIndex, index), 'plain')

    if (comment) push(raw, 'comment')
    else if (quoted) push(raw, 'string')
    else if (numeric) push(raw, 'number')
    else if (word && KEYWORDS.has(word)) push(raw, 'keyword')
    else if (word && API_PATTERN.test(word)) push(raw, 'api')
    else push(raw, 'plain')

    lastIndex = index + raw.length
  }

  push(source.slice(lastIndex), 'plain')
  return nodes
}

interface CodeBlockProps {
  code: string
  /** Shown top-left, e.g. the file the snippet belongs in. */
  filename?: string
  className?: string
}

export function CodeBlock({ code, filename, className }: CodeBlockProps) {
  const [copied, setCopied] = useState(false)

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(code)
      setCopied(true)
      setTimeout(() => setCopied(false), 1600)
    } catch {
      // Clipboard is unavailable outside a secure context — the code is still
      // selectable, so there is nothing to recover from.
    }
  }

  return (
    <div className={cn('bg-muted rounded-lg ring-1 ring-[var(--nx-hairline)]', className)}>
      <div className="border-border/70 flex items-center justify-between gap-2 border-b px-3 py-1.5">
        <span className="text-muted-foreground truncate font-mono text-[11px]">
          {filename ?? 'typescript'}
        </span>
        <Button variant="ghost" size="icon-xs" aria-label="Copy snippet" onClick={copy}>
          {copied ? <Check aria-hidden className="text-success" /> : <Copy aria-hidden />}
        </Button>
      </div>
      <pre className="overflow-x-auto px-3 py-2.5 text-[12px] leading-[1.6]">
        <code className="font-mono">{tokenize(code)}</code>
      </pre>
    </div>
  )
}
