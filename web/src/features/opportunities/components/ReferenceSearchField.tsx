import { useState } from 'react'
import { AsyncCombobox } from '@/components/common/inputs/AsyncCombobox'
import type { FieldControlProps, SelectOption } from '@/components/common/inputs/types'
import type { ApiError } from '@/types'

interface ReferenceSearchFieldProps extends FieldControlProps {
  value: SelectOption | null
  onValueChange: (value: SelectOption | null) => void
  /** Asks the SERVER for candidates; nothing is filtered, added or reordered here. */
  search: (query: string) => Promise<SelectOption[]>
  placeholder: string
  emptyMessage: string
  errorMessage: string
  /** Shown instead of `errorMessage` when the server answers 403 — the field fails closed, it never offers a manual fallback. */
  forbiddenMessage: string
  disabled?: boolean
}

/**
 * A server-backed picker: the list is whatever the API returned for the typed text, and an empty query asks for the
 * first page. Used for every reference the Opportunity flow needs (customer, new owner) so both fail the same way.
 */
export function ReferenceSearchField({ search, forbiddenMessage, errorMessage, ...control }: ReferenceSearchFieldProps) {
  const [forbidden, setForbidden] = useState(false)

  const onSearch = async (query: string) => {
    try {
      const options = await search(query)
      setForbidden(false)
      return options
    } catch (error) {
      setForbidden((error as Partial<ApiError>).status === 403)
      throw error
    }
  }

  return <AsyncCombobox {...control} onSearch={onSearch} minQueryLength={0} errorMessage={forbidden ? forbiddenMessage : errorMessage} />
}
