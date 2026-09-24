import { EntryDialog } from './components/EntryDialog'
import type { EntryPrefill } from './components/EntryForm'
import { parseLinkParam } from './lib/links'
import { defaultFields } from './lib/time'
import { openDialog } from '@/lib/overlay'

const DIALOG_CLASS = 'sm:max-w-[560px]'

/** Opens the calendar's linked-entry form over the current page, without changing the current route. */
export function openLinkedOpportunityEntryDialog(opportunityId: number) {
  const link = parseLinkParam(`crm/opportunity/${opportunityId}`)
  if (!link) return

  const prefill: EntryPrefill = { link, fields: defaultFields() }
  void openDialog({ content: <EntryDialog prefill={prefill} />, className: DIALOG_CLASS })
}
