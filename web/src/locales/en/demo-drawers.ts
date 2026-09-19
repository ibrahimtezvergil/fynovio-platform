export default {
  page: {
    eyebrow: 'Design system',
    title: 'Detail Drawers & Slide-overs',
    description: 'A detail panel that slides in from the right when a row is clicked, without leaving the screen — with reading, editing and confirmation layers.',
    summary: '{{count}} sections · {{surfaces}} surfaces',
    sectionsNavLabel: 'Panel sections',
    sections: {
      choosing: 'Which one, and when',
      rowToDetail: 'Row → detail',
      sizeAndSide: 'Size and side',
      stacked: 'Stacked panel',
    },
    rowToDetailTitle: 'Row → detail panel',
    rowToDetailDescription:
      'Click a row: the panel slides in from the right, the table stays behind it. Use the header arrows to move between records; "Edit" switches to the editing panel with its dirty-form guard.',
    readVsEditTitle: 'Read panel or edit panel',
    readVsEditDescription: 'The two are not the same component; their close behavior differs.',
    readPanelTitle: 'Read panel — closes freely',
    readPanelBody:
      'Esc, clicking the backdrop and the close button all do the same thing. With nothing to lose, the user is never held up by a question.',
    editPanelTitle: 'Edit panel — asks while dirty',
    editPanelBodyPre:
      'It behaves like a read panel while the form is untouched. After the first change, a close request becomes a decision:',
    editPanelBackToPanel: 'back to panel',
    editPanelBodyMid: 'or',
    editPanelDiscard: 'discard changes',
    editPanelBodyPost:
      '"Save" is never forced as a third option — the user is free to discard work left unfinished.',
    rulesHeading: 'Rules on this page',
    rules: [
      [
        'A panel keeps context, a page changes it',
        'A detail panel leaves the list behind it: scroll position, selection and filters stay put. If any of those would be lost, a full page is the right surface.',
      ],
      [
        'A panel says where it stands',
        'The header carries "2 / 5" and previous/next buttons. Without them, the panel loses its tie to the clicked row and becomes an arbitrary modal.',
      ],
      [
        'Navigation follows the same order as the list',
        'Next is the next row in the table — as filtered and sorted. Navigating by record id breaks the order the user is actually seeing.',
      ],
      [
        'A dirty form does not close on an outside click',
        'A read-only panel closes on a background click; once a form inside it has been typed into, the same gesture becomes a decision. Otherwise one stray click erases what was written.',
      ],
      [
        'The one-layer rule',
        'Only a dialog that ends in a decision may appear on top of a panel. A second panel leaves the user with two "back" steps and two unsaved forms.',
      ],
      [
        'Width comes from content, and 768px is the limit',
        'A panel that crosses half the screen cannot keep its promise that "what is behind stays visible". At that point the content already deserves a page.',
      ],
      [
        'A row click cannot be the only way in',
        'Every row carries a real button; clicking the whole row is the convenience layered on top of it. Keyboard and screen-reader users rely on the button.',
      ],
      [
        "A panel's header is the record's identity",
        'The name, status badge and record number sit in the header. A user forced to ask which record they are looking at while the panel is open ends up editing the wrong one.',
      ],
    ],
  },
  choosing: {
    title: 'Which one, and when',
    description:
      'The question "more about this row" has four answers. Picking the wrong surface gets fixed later by information architecture, not by swapping a component.',
    headers: {
      surface: 'Surface',
      when: 'When',
      why: 'Why',
      avoid: 'Avoid',
    },
    choices: {
      drawer: {
        surface: 'Detail panel (slide-over)',
        when: 'Seeing a full row, or making a short edit.',
        why: 'The list stays visible behind it, scroll position is preserved, and closing leaves you over the same row.',
        avoid: 'When content needs 768px of width or more than two tabs.',
      },
      dialog: {
        surface: 'Dialog (modal)',
        when: 'Work that ends in a single decision: a confirmation, a one-field question, a short form.',
        why: 'It sits at the center of the screen and asks one question; the context behind it will not be used.',
        avoid: 'Content that needs scrolling, tabs, or side-by-side reading.',
      },
      fullPage: {
        surface: 'Full page',
        when: 'The record itself is a workspace: multi-tab detail, a long form, an editor.',
        why: 'It gets its own URL, can be shared, bookmarked, and the browser back button works.',
        avoid: 'When the user will return to the list right away — every round trip rebuilds it.',
      },
      inline: {
        surface: 'Inline expansion',
        when: 'A few extra fields and comparison across rows.',
        why: 'The user can keep two records open at once; nothing covers anything else.',
        avoid: 'Detail with actions or longer than five fields — a table row should not become a form.',
      },
    },
  },
  recordExplorer: {
    caption: 'Account list — clicking a row opens the detail panel from the right',
    headers: {
      account: 'Account',
      owner: 'Owner',
      city: 'City',
      openDeals: 'Open deals',
      lifetimeValue: 'Lifetime value',
      status: 'Status',
    },
    openDetail: 'open detail',
  },
  detailDrawer: {
    tabs: {
      summary: 'Summary',
      contacts: 'Contacts',
      documents: 'Documents',
      history: 'History',
    },
    tabsAria: 'Detail tab',
    prevRecord: 'Previous record',
    nextRecord: 'Next record',
    identityLine: '{{segment}} · {{city}} · Owned by {{owner}}',
    emailAction: 'Email',
    callAction: 'Call',
    openFullPage: 'Open full page',
    fields: {
      email: 'Email',
      phone: 'Phone',
      taxId: 'Tax ID',
      paymentTerm: 'Payment term',
      openDeals: 'Open deals',
      lifetimeValue: 'Lifetime value',
      lastContact: 'Last contact',
    },
    accountNote: 'Account note',
    close: 'Close',
    edit: 'Edit',
  },
  editDrawer: {
    title: 'Edit account',
    description: 'Changes take effect only once saved. The panel does not close outright on a dirty form.',
    fields: {
      name: 'Name',
      owner: 'Owner',
      status: 'Status',
      statusHint: 'The badge updates within the form as the selection changes.',
      paymentTerm: 'Payment term',
      note: 'Account note',
    },
    cancel: 'Cancel',
    save: 'Save',
    toastUpdated: 'Record updated',
    discardTitle: 'You have unsaved changes',
    discardDescription: 'Closing the panel discards the changes in this form. Go back to the panel to save them.',
    backToPanel: 'Back to panel',
    discardChanges: 'Discard changes',
  },
  sizeSection: {
    title: 'Size and side',
    description:
      'Panel width is chosen based on content, not the other way around. A panel that exceeds 768px cannot keep its promise to leave the list behind it visible — at that point the right answer is a full page.',
    widthLadderTitle: 'The width ladder',
    widthLadderDescription:
      "Five steps. The panel's job is to keep the context behind it visible; width is the limit of that promise.",
    widths: {
      sm: { label: 'sm · 384px', use: 'A single-column summary, a few fields.' },
      md: { label: 'md · 448px', use: 'Default. A read panel, a short form.' },
      lg: { label: 'lg · 512px', use: 'Tabbed detail, a medium-sized form.' },
      xl: { label: 'xl · 576px', use: 'A two-column summary, side by side.' },
      '3xl': { label: '3xl · 768px', use: 'The limit. Anything wider is a full page.' },
    },
    widthCheck: 'Is the table behind it still visible? If not, the panel is too wide.',
    close: 'Close',
    sideChoiceTitle: 'Choosing a side',
    sideChoiceDescription:
      'A detail panel always enters from the right. One entering from the left belongs to navigation; one entering from the bottom belongs to a narrow screen.',
    sides: {
      right: { label: 'Right', note: 'Default. The end of the reading direction; it does not move the list.' },
      left: { label: 'Left', note: 'Navigation and tree structures. Not used for detail.' },
      bottom: { label: 'Bottom', note: 'Narrow screens. Close to the thumb, closes with one hand.' },
    },
    sideTitle: '{{side}} side',
    listVisibleTitle: 'The list stays visible while the panel is open',
    listVisibleDescription:
      "The overlay is transparent and the panel never crosses half the screen. When the user closes it, they're back over the row they clicked — the one thing a full page cannot offer.",
    openExample: 'Open example',
    lookBehindTitle: 'Look behind it',
    lookBehindDescription:
      'The table stays in place, scroll position is preserved. Nothing reloads when the panel closes.',
  },
  stackedSection: {
    title: 'Stacked panel',
    description:
      'Only a dialog that ends in a decision — a confirmation or a single-field question — may appear on top of a detail panel. A second panel may not.',
    destructiveTitle: 'Destructive action confirmation',
    destructiveDescription:
      "Deletion is triggered from inside the panel but does not complete there. Once the confirmation dialog closes, the panel is still there — the user never loses their context.",
    openPanel: 'Open panel',
    panelDescription: 'A destructive action inside the panel opens a confirmation dialog on top of it.',
    addNote: 'Add note (single-field dialog)',
    deleteAccount: 'Delete account',
    panelHint: 'The panel stays in place once either dialog closes; Esc closes only the topmost layer.',
    close: 'Close',
    deleteConfirmTitle: 'Delete this account?',
    deleteConfirmDescription: 'Nordwind Lojistik and its 3 linked open deals will be removed. This cannot be undone.',
    cancel: 'Cancel',
    delete: 'Delete',
    deleteConfirmedState: 'deletion confirmed — panel stayed open',
    toastDeleted: 'Account deleted',
    addNoteTitle: 'Add account note',
    addNoteDescription: "A single-field question — the heaviest second layer that can sit on top of the panel.",
    noteAria: 'Note',
    notePlaceholder: 'Short note…',
    add: 'Add',
    noteAddedState: 'note added — panel stayed open',
    layerRuleName: 'Layering rule',
    noSecondPanelTitle: 'No second panel',
    noSecondPanelDescription:
      'Opening a second panel from a panel leaves the user with two "back" steps and two unsaved forms. If the detail of a detail is needed, that is a page.',
    practicalTestPre: 'Practical test:',
    practicalTestPost:
      'can the user say in advance what will close when they press it? If the answer is "it depends", there is one layer too many.',
  },
  schema: {
    nameMin: 'Enter at least 2 characters.',
    nameMax: 'At most 80 characters.',
    ownerRequired: 'Choose an owner.',
    paymentTermRequired: 'Enter a payment term.',
    noteMax: 'At most 400 characters.',
  },
  customerStatus: {
    active: 'Active customer',
    prospect: 'Prospect',
    risk: 'At risk',
    churned: 'Churned',
  },
  customers: {
    cr1042: {
      segment: 'Enterprise · Logistics',
      paymentTerm: 'Net 45',
      lastContact: '2 hours ago',
      note: 'Discount cap is 15%. Anything above needs regional manager sign-off.',
      contacts: {
        0: { role: 'Procurement manager' },
        1: { role: 'Fleet operations lead' },
      },
      documents: {
        0: { name: 'Master-agreement-2026.pdf', date: 'Jan 12, 2026' },
        1: { name: 'Nordwind-proposal-v3.pdf', date: 'Sep 3, 2026' },
      },
      history: {
        0: { time: '2h ago', title: 'Stage updated', note: 'Quote Sent → Meeting' },
        1: { time: 'Yesterday 14:20', title: 'Revised quote sent', note: 'Three-scenario price table' },
        2: { time: 'Aug 28', title: 'On-site demo', note: 'Gebze warehouse, operations team' },
      },
    },
    cr1088: {
      segment: 'Enterprise · Shipping',
      paymentTerm: 'Net 30',
      lastContact: '4 days ago',
      note: 'In tender; the decision board meets mid-October. Discussion centers on API scope, not price.',
      contacts: {
        0: { role: 'IT director' },
        1: { role: 'Procurement' },
      },
      documents: {
        0: { name: 'RFP-2026-warehouse-api.pdf', date: 'Aug 22, 2026' },
      },
      history: {
        0: { time: '4 days ago', title: 'Technical evaluation', note: 'API scope and SLA questions' },
        1: { time: 'Aug 22', title: 'Tender file received', note: 'Due Sep 30' },
      },
    },
    cr0977: {
      segment: 'Mid-market · Construction',
      paymentTerm: 'Net 60',
      lastContact: '11 days ago',
      note: 'Two invoices are past due. Needs finance sign-off before the renewal conversation.',
      contacts: {
        0: { role: 'Deputy general manager' },
      },
      documents: {
        0: { name: 'Payment-plan-revised.xlsx', date: 'Aug 19, 2026' },
        1: { name: 'Support-agreement-2025.pdf', date: 'Nov 4, 2025' },
      },
      history: {
        0: { time: '11 days ago', title: 'Collections reminder', note: 'FT-2026-0764 · 32 days overdue' },
        1: { time: 'Aug 19', title: 'Payment plan proposed', note: 'Three equal installments' },
      },
    },
    cr1130: {
      segment: 'Enterprise · Retail',
      paymentTerm: 'Prepaid',
      lastContact: 'Yesterday',
      note: 'The 40-seat expansion went to approval. A separate mobile licence for stores is under discussion.',
      contacts: {
        0: { role: 'IT manager' },
        1: { role: 'Operations director' },
        2: { role: 'Finance' },
      },
      documents: {
        0: { name: 'Expansion-proposal-40-seats.pdf', date: 'Sep 1, 2026' },
      },
      history: {
        0: { time: 'Yesterday 09:40', title: 'Expansion proposal sent', note: '40 seats, annual' },
        1: { time: 'Aug 26', title: 'Usage report shared', note: '78% monthly active users' },
      },
    },
    cr0851: {
      segment: 'Mid-market · Shipping',
      paymentTerm: 'Net 30',
      lastContact: '3 months ago',
      note: 'Contract not renewed; moved to a competing solution. Re-contact planned in six months.',
      contacts: {
        0: { role: 'Operations manager' },
      },
      documents: {
        0: { name: 'Termination-notice.pdf', date: 'May 30, 2026' },
      },
      history: {
        0: { time: '3 months ago', title: 'Contract terminated', note: 'Not renewed' },
        1: { time: 'May 12', title: 'Loss review call', note: 'Price and integration scope' },
      },
    },
  },
}
