export default {
  page: {
    eyebrow: 'Design system',
    title: 'Toast & Alert',
    description: 'Immediate action feedback goes through Toast; persistent in-page state is told through Alert.',
    rulesHeading: 'Rules on this page',
    rules: [
      ['Toast is temporary, Alert is persistent', "An action's outcome is announced with a Toast and closes itself; the page's own state is told with an Alert and stays put until the user dismisses it."],
      ['Colour never carries meaning alone', 'Every tone repeats both an icon and a title — the tone changes the icon, not just the colour.'],
      ['Position stays fixed', 'A Toast always appears in the same corner of the screen (bottom right) — the user never has to relearn where to look.'],
      ['Short and action-focused', "The title says what happened, the description adds why or what to do next in one sentence. Anything longer than two sentences belongs in an Alert."],
      ['Error feedback is never silent', 'A failed operation is always announced with an error Toast — logging to the console alone is not enough.'],
      ['Reversible actions carry an action', 'Reversible operations like a delete offer an "Undo" option through the Toast\'s action button.'],
    ],
  },
  sections: {
    toast: {
      title: 'Toast',
      description: 'A notification that appears in a corner of the screen for a few seconds and disappears on its own. Triggered with sonner, fixed at the bottom-right corner (see src/App.tsx).',
    },
    alert: {
      title: 'Alert',
      description: "A notification card describing the page's own state, staying in place until the user dismisses it or the condition changes.",
    },
  },
  toast: {
    success: {
      title: 'Success',
      description: 'When an operation completes as expected.',
      message: 'Deal stage updated',
      trigger: 'Show',
    },
    error: {
      title: 'Error',
      description: 'When an operation fails — always announced rather than silently logged.',
      message: "Couldn't delete the record — server unreachable",
      trigger: 'Show',
    },
    warning: {
      title: 'Warning',
      description: 'The operation completed, but there is something that needs attention.',
      message: '3 deals have passed their close date',
      trigger: 'Show',
    },
    info: {
      title: 'Info',
      description: 'A development the user should know about, with no action required.',
      message: 'Report is being generated in the background',
      trigger: 'Show',
    },
    action: {
      title: 'With action',
      description: 'For a reversible operation, the Toast offers a way out before it closes.',
      message: 'Deal deleted',
      deletedDescription: 'Ayşe Kaya — Corporate Package',
      undoLabel: 'Undo',
      restoredMessage: 'Deal restored',
      trigger: 'Show',
    },
    promise: {
      title: 'Pending (promise)',
      description: "An operation whose outcome isn't known in advance: it starts as loading and resolves to success or error.",
      loading: 'Preparing the quote as a PDF…',
      success: 'Quote ready — you can download it',
      error: 'Could not prepare the quote',
      trigger: 'Show',
    },
  },
  alerts: {
    twoFactor: {
      title: 'Two-factor authentication is on',
      description: 'The account is protected with an authenticator app.',
    },
    payment: {
      title: 'Payment method is invalid',
      description: 'The card on file has expired — update it from billing.',
    },
    storage: {
      title: 'Storage space is running low',
      description: 'Used space is at 92% — no new files can be uploaded in 30 days.',
    },
    maintenance: {
      title: 'Maintenance scheduled',
      description: 'The panel will be briefly unreachable Oct 3, 02:00–02:30.',
    },
  },
}
