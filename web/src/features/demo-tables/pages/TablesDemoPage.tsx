import { BookOpen, ExternalLink, Table2 } from 'lucide-react'
import { useSearchParams } from 'react-router-dom'
import { PageHeader } from '@/components/common/PageHeader'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { AdvancedSaasGrid } from '@/features/demo-tables/components/AdvancedSaasGrid'
import { CodeBlock } from '@/features/demo-tables/components/CodeBlock'
import { CoreAdminGrid } from '@/features/demo-tables/components/CoreAdminGrid'
import {
  DocCard,
  DocDefinitionList,
  DocPills,
  type DocPill,
} from '@/features/demo-tables/components/DocCard'
import { CAPABILITY_MATRIX, CONFIG_DOCS } from '@/features/demo-tables/docs/configDocs'
import { FEATURE_DOCS } from '@/features/demo-tables/docs/featureDocs'

type Tab = 'grids' | 'features' | 'config'

const TABS: readonly Segment<Tab>[] = [
  { value: 'grids', label: 'Live grids' },
  { value: 'features', label: 'Feature mechanics' },
  { value: 'config', label: 'Configuration' },
]

const FOLDER_STRUCTURE = [
  'src/components/data-table/          # headless kit — imports no feature',
  '  DataTable.tsx                     # semantic table/thead/tbody renderer',
  '  DataTableHeaderCell.tsx           # sort button + resize handle',
  '  DataTablePagination.tsx',
  '  DataTableToolbar.tsx              # debounced global search',
  '  DataTableViewOptions.tsx          # column visibility menu',
  '  DataTableSelection.tsx            # Subscribe-bound checkboxes',
  '  types.ts                          # DataTableColumnMeta, instance aliases',
  '  hooks/useTableSearchParams.ts     # URL <-> table state',
  '  hooks/useDebouncedValue.ts',
  '  lib/urlState.ts                   # pure codecs (page, size, sort, q)',
  '  lib/columnSizeVars.ts             # sizing -> CSS custom properties',
  '',
  'src/features/demo-tables/',
  '  tables/coreAdminTable.tsx         # tableFeatures + columns for grid 1',
  '  tables/saasGridTable.tsx          # tableFeatures + columns for grid 2',
  '  data/employees.ts                 # deterministic dataset',
  '  data/api.ts                       # client + simulated server queries',
  '  components/                       # grids, docs cards, code block',
  '  docs/                             # documentation content as data',
  '  pages/TablesDemoPage.tsx          # this page',
].join('\n')

const HEADLESS_SNIPPET = [
  '// The renderer knows nothing about features. It reads the instance.',
  'export function DataTable({ table, resizable }) {',
  '  return (',
  '    <table>',
  '      <thead>{table.getHeaderGroups().map(renderHeaderRow)}</thead>',
  '      <tbody>{table.getRowModel().rows.map(renderRow)}</tbody>',
  '    </table>',
  '  )',
  '}',
  '',
  '// Visibility-aware only when the feature is actually registered.',
  'const cells = hasApi(row, "getVisibleCells")',
  '  ? row.getVisibleCells()',
  '  : row.getAllCells()',
].join('\n')

export default function TablesDemoPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const raw = searchParams.get('tab')
  const tab: Tab = raw === 'features' || raw === 'config' ? raw : 'grids'

  const setTab = (next: Tab) => {
    setSearchParams(
      (prev) => {
        const params = new URLSearchParams(prev)
        if (next === 'grids') params.delete('tab')
        else params.set('tab', next)
        return params
      },
      { replace: true },
    )
  }

  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        title="Data grid architecture"
        description="TanStack Table v9 · headless engine, Tailwind renderer, URL-owned state"
        actions={
          <SegmentedControl aria-label="Section" segments={TABS} value={tab} onChange={setTab} />
        }
      />

      {tab === 'grids' && (
        <div className="flex flex-col gap-5">
          <GridIntro
            index={1}
            title="Core admin list"
            description="Pagination, column sorting and global search, with the URL as the owner of all three. The mode switch moves processing between the browser and the (simulated) server without touching a column definition."
            pills={[
              { label: 'rowPaginationFeature', tone: 'feature' },
              { label: 'rowSortingFeature', tone: 'feature' },
              { label: 'globalFilteringFeature', tone: 'feature' },
              { label: 'manualPagination', tone: 'option' },
            ]}
          />
          <CoreAdminGrid />

          <GridIntro
            index={2}
            title="Advanced SaaS grid"
            description="Column visibility, multi-row selection with Shift-ranges, and resizable columns. Selection lives in an external TanStack Store atom so the bulk-action bar can own it; the grid itself never re-renders when a checkbox is ticked."
            pills={[
              { label: 'columnVisibilityFeature', tone: 'feature' },
              { label: 'rowSelectionFeature', tone: 'feature' },
              { label: 'columnResizingFeature', tone: 'feature' },
              { label: 'atoms.rowSelection', tone: 'state' },
            ]}
          />
          <AdvancedSaasGrid />
        </div>
      )}

      {tab === 'features' && (
        <div className="flex flex-col gap-5">
          <CapabilityMatrix />
          <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
            {FEATURE_DOCS.map((doc) => (
              <DocCard
                key={doc.id}
                icon={doc.icon}
                title={doc.title}
                summary={doc.summary}
                pills={doc.pills}
                code={doc.code}
                codeFilename={doc.codeFilename}
              >
                <div>
                  <p className="text-muted-foreground mb-1 text-[11px] font-medium tracking-[0.04em] uppercase">
                    Enable · disable · customise
                  </p>
                  <DocDefinitionList items={doc.options} />
                </div>
                <p className="border-warning/40 bg-warning/8 text-foreground/90 rounded-lg border px-3 py-2 text-[12px] leading-5">
                  <span className="text-warning font-medium">Watch out — </span>
                  {doc.pitfall}
                </p>
                <p className="text-muted-foreground text-[11px]">Demonstrated in {doc.grid}</p>
              </DocCard>
            ))}
          </div>
        </div>
      )}

      {tab === 'config' && (
        <div className="flex flex-col gap-4">
          <DocCard
            icon={Table2}
            title="Headless by construction"
            summary="The engine produces state and row models; this app produces markup. Nothing in src/components/data-table imports a feature plugin, which is what lets one renderer serve two grids with different capabilities — and what keeps the bundle honest."
            code={HEADLESS_SNIPPET}
            codeFilename="components/data-table/DataTable.tsx"
          />

          {CONFIG_DOCS.map((doc) => (
            <DocCard
              key={doc.id}
              icon={doc.icon}
              title={doc.title}
              summary={doc.summary}
              code={doc.code}
              codeFilename={doc.codeFilename}
            >
              <DocDefinitionList items={doc.points} />
            </DocCard>
          ))}

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <BookOpen aria-hidden className="text-brand-graphic size-4" strokeWidth={1.75} />
                Folder structure
              </CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-3">
              <p className="text-muted-foreground text-[13px] leading-5">
                The split is the point: a feature-agnostic kit under{' '}
                <code className="bg-muted rounded-sm px-1 py-0.5 font-mono text-[11px]">
                  components/data-table
                </code>{' '}
                and one file per grid that decides which capabilities exist.
              </p>
              <CodeBlock code={FOLDER_STRUCTURE} filename="src/" />
              <a
                href="https://tanstack.com/table/v9/docs/introduction"
                target="_blank"
                rel="noreferrer"
                className="text-brand-graphic inline-flex w-fit items-center gap-1 text-[13px] hover:underline"
              >
                TanStack Table v9 documentation
                <ExternalLink aria-hidden className="size-3.5" />
              </a>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  )
}

function GridIntro({
  index,
  title,
  description,
  pills,
}: {
  index: number
  title: string
  description: string
  pills: readonly DocPill[]
}) {
  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-baseline gap-2">
        <span className="bg-brand-graphic/12 text-brand-graphic flex size-5 items-center justify-center rounded-full text-[11px] font-semibold">
          {index}
        </span>
        <h2 className="text-[17px] leading-6 font-semibold">{title}</h2>
      </div>
      <p className="text-muted-foreground max-w-3xl text-[13px] leading-5">{description}</p>
      <DocPills pills={pills} />
    </div>
  )
}

function CapabilityMatrix() {
  return (
    <Card className="gap-0 overflow-hidden p-0">
      <div className="border-border border-b px-4 py-3">
        <h2 className="text-[15px] leading-5 font-semibold">What registers what</h2>
        <p className="text-muted-foreground mt-0.5 text-xs">
          Every capability on this page, the plugin that provides it, and the row-model slot it
          needs. A dash means the feature annotates the pipeline instead of reshaping it.
        </p>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[640px] text-[13px]">
          <thead>
            <tr>
              {['Capability', 'Feature plugin', 'create*RowModel slot', 'Grid'].map((heading) => (
                <th
                  key={heading}
                  scope="col"
                  className="text-muted-foreground border-border border-b px-4 py-2.5 text-left text-[11px] font-medium tracking-[0.04em] uppercase"
                >
                  {heading}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {CAPABILITY_MATRIX.map((row) => (
              <tr key={row.capability} className="hover:bg-muted/60">
                <td className="border-border/60 border-b px-4 py-2 font-medium">{row.capability}</td>
                <td className="border-border/60 text-stage-meeting border-b px-4 py-2 font-mono text-[11px]">
                  {row.feature}
                </td>
                <td className="border-border/60 text-stage-contacted border-b px-4 py-2 font-mono text-[11px]">
                  {row.model}
                </td>
                <td className="border-border/60 text-muted-foreground border-b px-4 py-2 tabular-nums">
                  Grid {row.grid}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}
