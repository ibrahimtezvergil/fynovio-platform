import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { OverlayHost } from '@/components/common/OverlayHost'
import { DialogTitle } from '@/components/ui/dialog'
import { openDialog, useOverlayClose, useOverlayStore } from '@/lib/overlay'

function SelfClosing() {
  const close = useOverlayClose()
  return (
    <>
      <DialogTitle>Self closing</DialogTitle>
      <button type="button" onClick={close}>
        done
      </button>
    </>
  )
}

beforeEach(() => useOverlayStore.setState({ entries: [] }))

describe('useOverlayClose', () => {
  it('dismisses the calling overlay through the manager and resolves the opener’s promise', async () => {
    render(<OverlayHost />)
    let resolved = false
    act(() => {
      void openDialog({ content: <SelfClosing /> }).then(() => (resolved = true))
    })
    fireEvent.click(await screen.findByRole('button', { name: 'done' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(useOverlayStore.getState().entries).toHaveLength(0)
    await waitFor(() => expect(resolved).toBe(true))
  })

  it('closes only its own overlay when two are stacked', async () => {
    render(<OverlayHost />)
    act(() => {
      void openDialog({ content: <p>bottom</p> })
      void openDialog({ content: <SelfClosing /> })
    })
    expect(useOverlayStore.getState().entries).toHaveLength(2)
    // By text, not role: a modal marks everything outside the top overlay inaccessible, which is not what is under test.
    fireEvent.click(await screen.findByText('done'))
    await waitFor(() => expect(useOverlayStore.getState().entries).toHaveLength(1))
    expect(screen.getByText('bottom')).toBeInTheDocument()
  })

  it('fails loudly when used outside an overlay', () => {
    const quiet = console.error
    console.error = () => {}
    try {
      expect(() => render(<SelfClosing />)).toThrow(/openDialog/)
    } finally {
      console.error = quiet
    }
  })
})
