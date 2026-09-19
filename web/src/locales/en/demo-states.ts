export default {
  page: {
    eyebrow: 'Design system',
    title: 'Empty & Loading States',
    description: 'Illustrations that guide when there is no data yet, skeletons that hold the coming layout while data is on the way.',
    summary: '{{count}} sections · {{illustrations}} illustrations',
    sectionsNavLabel: 'State sections',
    sections: {
      choosing: 'Which one, and when',
      illustrations: 'Illustrations',
      emptyStates: 'Empty states',
      skeleton: 'Skeleton Loader',
      inlineLoading: 'Inline loading',
    },
    rulesHeading: 'Rules on this page',
    rules: [
      [
        'A skeleton is the measure of the coming content',
        "A skeleton carries the exact height and layout of the component it stands in for. A different measure is a second layout shift once the data lands — you'd be lengthening the wait, not shortening it.",
      ],
      [
        'A request under 200ms shows no skeleton',
        'A skeleton that appears and vanishes on an instantly-returning request is more distracting than the wait itself. Show nothing for short requests; open straight into the skeleton for ones known to be long.',
      ],
      [
        "Data already on screen never turns back into a skeleton",
        "During a background refresh, the existing content stays in place and its opacity drops. Turning a table the user is reading into grey blocks takes back information they already had.",
      ],
      [
        'An empty state says three things at once',
        'What it is (title), why it is that way (description), and what to do next (action). Missing any one of the three leaves the user stuck on screen.',
      ],
      [
        'The action depends on the reason for the gap',
        'If the result is empty because of a filter, "create" is the wrong suggestion — clearing the filter is the right one. In an error, the one correct primary action is "retry".',
      ],
      [
        "An illustration carries no color of its own",
        'Drawings are built from currentColor; the caller supplies the tone. That is how one file works in both light and dark themes, and in both neutral and error contexts.',
      ],
      [
        'A wait is always announced',
        'Every skeleton block is aria-hidden; the region wrapping them carries role="status" + aria-live="polite". A screen reader does not count rectangles — it hears "Loading" once.',
      ],
      [
        'The shimmer is decoration, the layout is the message',
        'When prefers-reduced-motion is on, the sweep animation stops; the layout the skeleton holds stays exactly the same. The information lives in the shape, not the motion.',
      ],
    ],
  },
  choosing: {
    title: 'Which one, and when',
    description:
      "When there is no content on screen, the reason is never just one thing. What picks the state is not appearance but why there is nothing — and the reason determines the action offered to the user.",
    headers: {
      state: 'State',
      when: 'When',
      shows: 'Shows',
      action: 'Action',
    },
    rows: [
      {
        name: 'Loading',
        when: 'The request is in flight, the result is not yet known.',
        shows: "A skeleton of the coming content — identical size and layout.",
        action: 'None. A waiting screen asks for no action.',
      },
      {
        name: 'Empty for the first time',
        when: 'The request succeeded; no record has ever been created.',
        shows: 'An illustration plus one sentence explaining why it is empty.',
        action: 'Primary: create the first record.',
      },
      {
        name: 'Empty after filtering',
        when: 'Data exists; this slice does not.',
        shows: 'An illustration echoing the search, a summary of the applied filter.',
        action: 'Primary: clear the filters. Creating is not suggested.',
      },
      {
        name: 'Error',
        when: 'The request failed.',
        shows: 'What happened and that it is not the user\'s fault — not technical detail.',
        action: 'Primary: retry.',
      },
      {
        name: 'No connection',
        when: 'The network is unreachable; the panel itself works.',
        shows: 'That the problem is the connection, not the panel.',
        action: 'Primary: reconnect. Secondary: offline view.',
      },
      {
        name: 'No access',
        when: 'The record exists, but is closed to this user.',
        shows: 'How to get access — the record\'s content is never leaked.',
        action: 'Primary: request access.',
      },
      {
        name: 'All done',
        when: 'The queue is empty because the work is finished.',
        shows: 'A positive close — the gap is not a shortfall.',
        action: 'None, or secondary: go to the archive.',
      },
    ],
  },
  illustrations: {
    title: 'Illustrations',
    description:
      'src/components/common/illustrations.tsx — six drawings, one file. There is no color: strokes are currentColor, fills are tokens, the tone always comes from the caller.',
    entries: {
      emptyBox: 'The collection was never populated',
      noResults: 'The query matched no records',
      broken: 'The request failed',
      offline: 'No network connection',
      noAccess: 'The record exists, no access',
      allDone: 'The queue is finished, the gap is positive',
    },
  },
  emptyStates: {
    title: 'Empty states',
    description:
      "Each one is built from EmptyState's illustration prop. The title says what it is, the description says why, the button says the way out — all three, complete.",
    firstEmpty: {
      cardTitle: 'Empty for the first time',
      cardDescription: 'No record has ever been created. The primary action is creating the first one.',
      title: 'No deals yet',
      description: 'The pipeline comes alive here once you create the first deal.',
      action: 'Create deal',
    },
    filteredEmpty: {
      cardTitle: 'Empty after filtering',
      cardDescription: 'Data exists, this slice does not. Creating is not suggested — loosening the filter is.',
      title: 'No matching records',
      description: 'The "Pending" + last 7 days filter matched no deals.',
      action: 'Clear filters',
    },
    error: {
      cardTitle: 'Error',
      cardDescription: 'The request failed. The user is not blamed, and no technical detail is shown.',
      title: 'The list could not load',
      description: "The server could not be reached. It's not you — retrying is usually enough.",
      action: 'Retry',
    },
    offline: {
      cardTitle: 'No connection',
      cardDescription: 'The panel works, the network does not. Naming the difference saves the user a wrong search.',
      title: "You're offline",
      description: 'Data will pick up where it left off once the connection returns.',
      action: 'Reconnect',
    },
    noAccess: {
      cardTitle: 'No access',
      cardDescription: "The record exists but is closed to this user. No hint about its content is leaked.",
      title: "You don't have access to this area",
      description: 'The Reports module is open only to managers. An access request goes to your team lead.',
      action: 'Request access',
    },
    allDone: {
      cardTitle: 'All done',
      cardDescription: 'The queue is empty because the work is finished. This gap is a result, not a shortfall.',
      title: 'Everything is set for today',
      description: 'No approvals are pending. New requests will show up here.',
      action: 'Go to archive',
    },
  },
  skeleton: {
    title: 'Skeleton Loader',
    description:
      'A spinner does not say what is coming — a skeleton does. Each example is drawn to the real component\'s dimensions; use "Retry" to watch the swap again.',
    metricCard: {
      title: 'Metric tiles',
      description: "Mark, label, 32px figure and delta line — an exact trace of MetricCard.",
    },
    tableCard: {
      title: 'Data table',
      description: 'Column headers are known before the rows are, so they stay real in the skeleton too.',
    },
    listCard: {
      title: 'List',
      description: 'A 30px mark, a title, a shorter meta line — the row height stays fixed.',
    },
    chartCard: {
      title: 'Chart',
      description: 'Bars, never a grey rectangle: the reader learns the shape of the answer before the numbers.',
    },
    formCard: {
      title: 'Form',
      description: 'Fields sit at the real control height (--nx-control-height); labels are short.',
    },
    profileCard: {
      title: 'Identity block',
      description: 'The avatar is a circle; so is the skeleton. Shape carries as much information as size.',
    },
    textCard: {
      title: 'Text',
      description: "The last line is short — a paragraph skeleton that ends full-width reads as a bar.",
    },
    labels: {
      summaryMetrics: 'Summary metrics',
      dealsList: 'Deal list',
      upcomingActivities: 'Upcoming activities',
      weeklyChart: 'Weekly deal chart',
      dealForm: 'Deal form',
      userCard: 'User card',
      descriptionText: 'Description text',
    },
  },
  loadingPreview: {
    loadingPill: 'Loading',
    loadedPill: 'Loaded',
    retry: 'Retry',
    regionLabel: '{{label}} loading',
  },
  tableHeaders: {
    company: 'Company',
    stage: 'Stage',
    amount: 'Amount',
  },
  loaded: {
    metrics: {
      openDeals: { label: 'Open deals', context: 'vs. last month' },
      won: { label: 'Won', context: 'this quarter' },
      avgAmount: { label: 'Average amount', context: 'per deal' },
      conversion: { label: 'Conversion', context: 'lead → win' },
    },
    activities: {
      quoteFollowUp: 'Quote follow-up — {{company}}',
      demoCall: 'Demo call — {{company}}',
      contractReview: 'Contract review',
      weeklyMeeting: 'Weekly pipeline meeting',
      metaLegal: 'Legal',
      metaSalesTeam: 'Sales team',
    },
    chart: {
      days: {
        mon: 'Mon',
        tue: 'Tue',
        wed: 'Wed',
        thu: 'Thu',
        fri: 'Fri',
        sat: 'Sat',
        sun: 'Sun',
      },
      dealsLabel: 'Deal',
      title: 'New deals this week',
      weeklyChange: '+18% this week',
    },
    form: {
      companyLabel: 'Company',
      contactLabel: 'Contact',
      amountLabel: 'Amount',
      cancel: 'Cancel',
      save: 'Save',
    },
    profile: {
      salesPill: 'Sales',
      activePill: 'Active',
    },
    text: 'A deal stays pending for 14 days after the quote is sent. If no reply arrives from the contact in that time, an automatic reminder fires and the deal owner is notified. The record does not close when the period ends — it just moves to the "Pending" stage, so it drops out of the forecast without being deleted from history.',
  },
  inlineLoading: {
    title: 'Inline loading',
    description: "When only part of a page is waiting, not the whole thing, a skeleton is the wrong tool — what's already on screen needs to be preserved.",
    refetchCard: {
      title: 'Background refresh',
      description: 'When data is already on screen, it never reverts to a skeleton; the existing content stays readable at 50% opacity.',
    },
    loadMoreCard: {
      title: 'Appending a page',
      description: 'A new page is appended below the list. Rows already loaded never move.',
    },
    pendingCard: {
      title: 'Pending action',
      description: 'The button locks, the icon becomes a spinner, the width stays fixed — the row does not move.',
    },
    saving: 'Saving',
    save: 'Save',
    pendingHint: 'Request in flight — button locked.',
    idleHint: 'Press the button.',
    updating: 'Updating — old data stays readable',
    current: 'Up to date',
    refresh: 'Refresh',
    nextPageLoading: 'Loading next page',
    loadMore: 'Load more',
  },
}
