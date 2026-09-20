import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderPage } from '@/test/render'
import { useFragmentToken } from './useFragmentToken'

function Probe() {
  const token = useFragmentToken()
  return <p data-testid="token">{token ?? 'none'}</p>
}

describe('useFragmentToken', () => {
  it('reads the token from the URL fragment and removes the fragment from the address (replace, not push)', () => {
    const { router } = renderPage(<Probe />, '/accept-invite?keep=1#token=abc.def')
    const historyLength = window.history.length

    expect(screen.getByTestId('token')).toHaveTextContent('abc.def')
    expect(router.state.location.hash).toBe('')
    expect(router.state.location.pathname).toBe('/accept-invite')
    expect(router.state.location.search).toBe('?keep=1')
    expect(router.state.historyAction).toBe('REPLACE')
    expect(window.history.length).toBe(historyLength)
  })

  it('keeps the token in memory after the fragment is gone', () => {
    renderPage(<Probe />, '/x#token=abc.def')
    expect(screen.getByTestId('token')).toHaveTextContent('abc.def')
  })

  it('is null when the link carried no token', () => {
    renderPage(<Probe />, '/x')
    expect(screen.getByTestId('token')).toHaveTextContent('none')
  })

  it('is null for an empty, foreign or oversized fragment', () => {
    for (const hash of ['#', '#other=1', '#token=', `#token=${'a'.repeat(513)}`]) {
      const { unmount } = renderPage(<Probe />, `/x${hash}`)
      expect(screen.getByTestId('token')).toHaveTextContent('none')
      unmount()
    }
  })

  it('never writes the token to web storage', () => {
    renderPage(<Probe />, '/x#token=secret-token-value')
    for (const store of [localStorage, sessionStorage]) {
      for (let i = 0; i < store.length; i++) expect(store.getItem(store.key(i)!)).not.toContain('secret-token-value')
    }
  })
})
