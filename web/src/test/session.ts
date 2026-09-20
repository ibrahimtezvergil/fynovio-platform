import { useSessionStore } from '@/lib/auth'
import { resetBootstrapForTests } from '@/lib/auth/sessionClient'

/** Back to a cold page load: nothing resolved, no token, boot sequence not yet run. */
export function resetSession() {
  useSessionStore.setState({
    status: 'unknown',
    user: null,
    memberships: [],
    activeTenantId: null,
    accessToken: null,
    noMembership: false,
    explicitSignOut: false,
  })
  resetBootstrapForTests()
}
