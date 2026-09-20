import { useEffect, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'

const MAX_TOKEN_LENGTH = 512

function readToken(hash: string): string | null {
  const token = new URLSearchParams(hash.replace(/^#/, '')).get('token')
  return token && token.length <= MAX_TOKEN_LENGTH ? token : null
}

/**
 * The single-use token of an invitation / password-reset link, taken from the URL *fragment*
 * (`/accept-invite#token=…`). A fragment is never sent to a server, a proxy or in `Referer`; this keeps it
 * out of the page's own address bar and history too: it is read once into memory, then the fragment is
 * replaced (not pushed). `null` when the link carried none.
 */
export function useFragmentToken(): string | null {
  const location = useLocation()
  const navigate = useNavigate()
  const [token] = useState(() => readToken(location.hash))

  useEffect(() => {
    if (location.hash) navigate({ pathname: location.pathname, search: location.search, hash: '' }, { replace: true, state: location.state })
  }, [location.hash, location.pathname, location.search, location.state, navigate])

  return token
}
