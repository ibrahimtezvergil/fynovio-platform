import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { CapabilityProvider, useCapability } from '@/lib/capabilities'

function CapabilityProbe() {
  return <span>{useCapability('reports.advanced') ? 'enabled' : 'disabled'}</span>
}

describe('capability seam', () => {
  it('uses the closest tenant-package provider', () => {
    render(
      <CapabilityProvider capabilities={['reports.advanced']}>
        <CapabilityProbe />
      </CapabilityProvider>,
    )

    expect(screen.getByText('enabled')).toBeInTheDocument()
  })
})
