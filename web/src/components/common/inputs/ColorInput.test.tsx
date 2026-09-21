import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ColorInput } from './ColorInput'

describe('ColorInput', () => {
  it('visually marks the custom picker when its value is outside the preset palette', () => {
    const { container } = render(<ColorInput value="#112233" onValueChange={vi.fn()} />)

    const picker = container.querySelector('input[type="color"]')
    expect(picker).not.toBeNull()
    expect(picker!.closest('label')).toHaveClass('ring-2')
    expect(picker!.closest('label')!.querySelector('svg')).not.toBeNull()
  })

  it('does not mark the custom picker when a preset swatch is selected', () => {
    const { container } = render(<ColorInput value="#3c8cf0" onValueChange={vi.fn()} />)

    const picker = container.querySelector('input[type="color"]')
    expect(picker!.closest('label')).not.toHaveClass('ring-2')
    expect(screen.getByRole('button', { name: '#3C8CF0' })).toHaveAttribute('aria-pressed', 'true')
  })
})
