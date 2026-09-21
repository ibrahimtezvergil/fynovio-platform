import { act, fireEvent, render, screen } from '@testing-library/react'
import { I18nextProvider } from 'react-i18next'
import { afterEach, describe, expect, it } from 'vitest'
import { i18n } from '@/lib/i18n'
import { DatePicker, DateRangePicker, formatDate, formatDateShort } from './DatePicker'

const SEPTEMBER_21 = new Date(2026, 8, 21)

const inLanguage = async (language: string) => {
  await act(async () => {
    await i18n.changeLanguage(language)
  })
}

afterEach(() => inLanguage('tr'))

describe('DatePicker follows the active UI language', () => {
  it('writes the date in Turkish under tr and in English under en', async () => {
    const { rerender } = render(
      <I18nextProvider i18n={i18n}>
        <DatePicker value={SEPTEMBER_21} onValueChange={() => {}} aria-label="date" />
      </I18nextProvider>,
    )
    expect(screen.getByRole('button', { name: 'date' })).toHaveTextContent('21 Eylül 2026')

    await inLanguage('en')
    rerender(
      <I18nextProvider i18n={i18n}>
        <DatePicker value={SEPTEMBER_21} onValueChange={() => {}} aria-label="date" />
      </I18nextProvider>,
    )
    expect(screen.getByRole('button', { name: 'date' })).toHaveTextContent('September 21, 2026')
  })

  it('names the months of the open grid in the active language', async () => {
    await inLanguage('en')
    render(
      <I18nextProvider i18n={i18n}>
        <DatePicker value={SEPTEMBER_21} onValueChange={() => {}} aria-label="date" />
      </I18nextProvider>,
    )
    fireEvent.click(screen.getByRole('button', { name: 'date' }))

    expect(await screen.findByText('September 2026')).toBeInTheDocument()
    expect(screen.queryByText(/Eylül/i)).not.toBeInTheDocument()
  })

  it('formats the range trigger in the active language too', async () => {
    await inLanguage('en')
    render(
      <I18nextProvider i18n={i18n}>
        <DateRangePicker value={{ from: SEPTEMBER_21, to: new Date(2026, 8, 25) }} onValueChange={() => {}} aria-label="range" />
      </I18nextProvider>,
    )

    expect(screen.getByRole('button', { name: 'range' })).toHaveTextContent('09/21/2026 – 09/25/2026')
  })

  it('keeps the exported helpers Turkish by default, so existing callers do not change', () => {
    expect(formatDate(SEPTEMBER_21)).toBe('21 Eylül 2026')
    expect(formatDateShort(SEPTEMBER_21)).toBe('21.09.2026')
  })
})
