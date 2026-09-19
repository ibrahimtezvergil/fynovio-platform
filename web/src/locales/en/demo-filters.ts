export default {
  panel: {
    statusLabel: 'Status',
    statusHint: 'Leaving it empty means "all".',
    statusPlaceholder: 'All statuses',
    ownerLabel: 'Owner',
    ownerPlaceholder: 'All owners',
    cityLabel: 'City',
    cityPlaceholder: 'All cities',
    amountLabel: 'Amount range',
    amountHint: 'An empty end is unbounded.',
    dateLabel: 'Order date',
    channelLabel: 'Sales channel',
    onlyTagged: 'Tagged orders only',
    clearFilters: 'Clear filters',
  },
  grid: {
    order: 'Order',
    account: 'Account',
    owner: 'Owner',
    caption:
      'Order list. Click a column header to sort, hold Shift to sort by multiple fields. City and channel are not shown as columns; narrow them from the filter panel, where they appear in the active-chip row.',
    sortByField: 'Sort by {{field}}',
  },
  orderStatus: {
    pending: 'Pending',
    approved: 'Approved',
    paid: 'Paid',
    shipped: 'Shipped',
    cancelled: 'Cancelled',
    refunded: 'Refunded',
  },
  channel: {
    direct: 'Direct sales',
    partner: 'Partner',
    online: 'Online',
  },
  sortField: {
    createdAt: 'Date',
    amount: 'Amount',
    account: 'Account name',
    items: 'Item count',
    status: 'Status',
  },
  sortMenu: {
    summary: '{{field}} · {{arrow}}',
    label: 'Sort',
    rulesHeading: 'Sort rules',
    reset: 'Reset',
    noRules: 'No sort rules yet. Add a field below.',
    priority: 'priority {{index}}',
    toggleDirection: 'Toggle {{field}} direction: {{direction}}',
    ascending: 'ascending',
    descending: 'descending',
    removeRule: 'Remove {{field}} rule',
  },
  chips: {
    activeLabel: 'Active:',
    removeAria: 'Remove {{label}} filter',
    clearAll: 'Clear all',
    search: 'Search: “{{value}}”',
    status: 'Status: {{label}}',
    owner: 'Owner: {{owner}}',
    channel: 'Channel: {{label}}',
    city: 'City: {{city}}',
    amount: 'Amount: {{min}} – {{max}}',
    dates: 'Date: {{from}} – {{to}}',
    onlyTagged: 'Tagged only',
  },
  explorer: {
    searchPlaceholder: 'Order no, account, owner, city…',
    searchAria: 'Search orders',
    filtersButton: 'Filters',
    recordsLabel: 'records',
    emptyTitle: 'No orders match these filters',
    emptyDescription:
      'The gap isn\'t a lack of records — the filter is too narrow. The right move isn\'t "create new", it\'s loosening the filter.',
    emptyAction: 'Clear filters',
  },
  savedViews: {
    customView: 'Custom view — edited from a saved view',
    saveCurrent: 'Save view',
    nameLabel: 'View name',
    namePlaceholder: 'e.g. "This week, high value"',
    cancel: 'Cancel',
    save: 'Save',
    removeAria: 'Remove "{{label}}" view',
    all: { label: 'All orders', description: 'Unfiltered list, newest first.' },
    waiting: {
      label: 'Awaiting approval',
      description: 'Pending orders, oldest first — that is the next thing to do.',
    },
    highValue: {
      label: 'High value',
      description: 'Open orders above ₺100,000, by amount.',
    },
    risk: {
      label: 'Cancelled & refunded',
      description: 'Closed negative records — for reviewing the refund rate.',
    },
    mine: {
      label: 'My portfolio',
      description: 'Every order owned by Deniz Kaya.',
    },
  },
  page: {
    eyebrow: 'Design system',
    title: 'Filter & Sort',
    description:
      'An expandable panel, active filter chips and multi-field sorting for complex filtering over a data grid.',
    summary: '{{count}} records · {{views}} views',
    sectionsNavLabel: 'Filter sections',
    sections: {
      filterModule: 'Filter module',
      layers: 'Three layers',
      sorting: 'Multi-sort',
      views: 'Saved views',
    },
    filterModuleTitle: 'A working filter module',
    filterModuleDescription:
      'Search, an expandable panel, chips, multi-sort and saved views — all bound to one query object. Shift-click a column header: it adds a second sort rule. The panel narrows on fields the table does not show (city, channel) — which is why the list cannot be explained without the chip row.',
    layersTitle: 'Three layers',
    layersDescription:
      'A filter module is not one component; it is three layers with different visibility lifetimes.',
    layers: [
      [
        '1 · Always visible',
        'The search box, filter button and sort control. They are there the moment the screen opens; none of them expand or collapse.',
      ],
      [
        '2 · Expandable panel',
        'Six to twelve fields. Closed by default — a permanently open panel costs everyone vertical space to serve the minority looking for the seventh field.',
      ],
      [
        '3 · Active filter chips',
        'The only thing that says why the list got shorter while the panel is closed. Each chip removes exactly one predicate; a bulk "clear" is a separate button.',
      ],
    ],
    sortingTitle: 'Multi-sort',
    sortingDescription:
      'Rules apply in priority order: the first one that separates two rows wins, the rest break ties. The menu numbers the rules; the column header shows the same rule.',
    sortingCard1Title: 'Two inputs, one state',
    sortingCard1Body:
      'Clicking a column header replaces the rules; Shift-clicking appends one. The same rule list shows up in the sort menu — a table whose headers act independently of the menu carries a second sort order the user cannot see.',
    sortingCard2Title: 'The default direction follows the field type',
    sortingCard2Body:
      'Money and dates open descending, text opens ascending. "Smallest amount first" is rarely the question being asked; the right default is the click the user never has to make. Status is neither alphabetical nor numeric — it sorts by process order.',
    viewsTitle: 'Saved views',
    viewsDescription:
      'The three or four combinations a team actually uses. Named so they are not rebuilt every morning — and not rebuilt slightly differently each time.',
    viewsNoSort: 'no sort',
    rulesHeading: 'Rules on this page',
    rules: [
      [
        'An empty control is not a filter',
        'A multi-select with nothing chosen means "all", not "none". Otherwise the list empties the moment the panel opens, and the user never opens it again.',
      ],
      [
        'Its effect is visible even while the panel is closed',
        'The active-chip row is the filter panel\'s receipt. In a design with no chips, nothing on screen explains why the list got shorter.',
      ],
      [
        'Each chip removes exactly one predicate',
        'Three selected statuses means three chips. A single "Status: 3 selected" chip forces the user to open the panel just to remove the two they didn\'t want.',
      ],
      [
        'Filters combine with AND',
        'Values within one field combine with OR; fields combine with AND. This rule is the same everywhere — logic that changes per screen cannot be learned.',
      ],
      [
        'Sorting is visible and can be multiple',
        'A table sorted by two fields does not show a single arrow: rules are listed with a priority number. The header and the menu read the same state.',
      ],
      [
        'The sort matches the field\'s type',
        'Status sorts by process order, not alphabetically; money and dates open descending. The right default is the click the user never has to make.',
      ],
      [
        'An empty result blames the filter',
        'When there are no results, the primary action is "clear filters", not "create new". The reason for the gap determines the action.',
      ],
      [
        'An edited view stops being that view',
        "Once a saved view is selected, touching any control drops the selection. Still showing an edited query as that view would make someone trust the wrong list.",
      ],
    ],
  },
}
