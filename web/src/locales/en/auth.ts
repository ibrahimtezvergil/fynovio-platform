export default {
  loginPage: {
    title: 'Sign in to your panel',
    description: 'Sign in with your account.',
  },
  loginForm: {
    emailLabel: 'Email',
    passwordLabel: 'Password',
    submitting: 'Signing in',
    submit: 'Sign in',
    genericError: 'Unable to sign in. Please try again.',
    invalidCredentials: 'Incorrect email or password.',
    rateLimited: 'Too many attempts. Try again in {{seconds}} seconds.',
    rateLimitedNoWait: 'Too many attempts. Try again in a moment.',
  },
  schema: {
    emailInvalid: 'Enter a valid email address.',
    passwordRequired: 'Enter your password.',
  },
  session: {
    restoring: 'Restoring your session',
  },
  tenantSelector: {
    title: 'Choose an organisation',
    description: 'Your account belongs to more than one organisation. Choose one to continue.',
    tenantLabel: 'Organisation {{id}}',
    notPermitted: 'You do not have access to this organisation.',
    genericError: 'Could not select the organisation. Please try again.',
  },
  tenantSwitcher: {
    label: 'Organisation',
    failed: 'Could not switch organisation.',
  },
  noAccess: {
    title: 'No access',
    noMembershipDescription: 'Your account is not a member of any organisation yet. Ask your administrator for an invitation.',
    forbiddenDescription: 'You do not have permission to view this content.',
    backToDashboard: 'Back to dashboard',
    signOut: 'Sign out',
  },
}
