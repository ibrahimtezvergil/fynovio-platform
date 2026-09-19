export default {
  activityType: {
    call: 'Call',
    email: 'Email',
    meeting: 'Meeting',
    note: 'Note',
    stage: 'Stage change',
    document: 'Document',
    payment: 'Payment',
    task: 'Task',
    system: 'System',
  },
  direction: {
    in: 'Inbound',
    out: 'Outbound',
  },
  feedFilter: {
    all: 'All',
  },
  composer: {
    segmentAriaLabel: 'Activity type to log',
    textareaAriaLabel: 'Activity description',
    placeholder: 'What was discussed? One-line summary, add detail if needed…',
    helper: 'The entry lands at the top of the feed, timestamped "now".',
    submit: 'Save',
  },
  feed: {
    emptyTitle: 'No activity',
    defaultEmptyDescription: 'No activity matches this filter. Widen the filter.',
    pinnedHeading: 'Pinned',
  },
  customer: {
    a1: {
      title: 'Pinned note — negotiation ceiling',
      detail:
        'The discount ceiling on this account is 15%. Anything above needs regional manager approval; the last two renewals both closed right at this limit.',
    },
    a2: {
      title: 'Deal stage updated',
      change: { field: 'Stage', from: 'Quote Sent', to: 'Meeting' },
    },
    a3: {
      title: 'Call with the purchasing manager',
      detail: 'They want to move to the 120-vehicle package for the fleet-tracking renewal. Budget approval should firm up next week.',
      duration: '18 min',
    },
    a4: {
      title: 'Revised quote sent',
      detail: 'Added a three-scenario price table and a 24-month total cost of ownership.',
    },
    a5: {
      title: 'Quote viewed by customer',
      detail: 'Nordwind-teklif-v3.pdf · 6 min read, 2 min on the pricing page.',
    },
    a6: {
      title: 'On-site demo — Gebze warehouse',
      detail: 'The operations team tried out the routing screen. The mobile driver app opened up as a separate opportunity.',
      duration: '1 hr 20 min',
    },
    a7: {
      title: 'Task completed: reference customer list',
      detail: 'Got contact permission from three reference accounts in the logistics sector.',
    },
    a8: {
      title: 'Missed call',
      detail: 'Customer called through the switchboard, unanswered. A callback task was created automatically.',
    },
    a9: {
      title: 'Deal stage updated',
      change: { field: 'Stage', from: 'Contacted', to: 'Quote Sent' },
    },
    a10: {
      title: 'First quote sent',
    },
  },
  audit: {
    u1: {
      title: 'Payment matched',
      detail: 'FT-2026-0841 · Matched automatically from the bank statement.',
      change: { field: 'Invoice status', from: 'Overdue', to: 'Paid' },
    },
    u2: {
      title: 'Order line updated',
      detail: 'SP-11482 · Line 3, 40x40 aluminum profile.',
      change: { field: 'Quantity', from: '250 pcs', to: '400 pcs' },
    },
    u3: {
      title: 'Delivery note created',
      detail: 'IR-2026-3312 · 4 items, shipped from the Gebze warehouse.',
    },
    u4: {
      title: 'Supplier record updated',
      change: { field: 'Payment term', from: '30 days', to: '45 days' },
    },
    u5: {
      title: 'Purchase request approved',
      detail: 'PR-2291 · 12 warehouse-supply items, ₺84,500.',
      change: { field: 'Request status', from: 'Awaiting approval', to: 'Approved' },
    },
    u6: {
      title: 'Stock level fell below the critical threshold',
      detail: 'SKU 44-1180 · Gebze warehouse.',
      change: { field: 'On-hand stock', from: '84 pcs', to: '31 pcs' },
    },
  },
  page: {
    eyebrow: 'Design system',
    title: 'Activity Feed & Timeline',
    description:
      'A chronological feed of what happened on a customer or a project: calls, emails, stage changes, audit trail.',
    summary: '{{count}} sections · 2 feed types',
    navLabel: 'Feed sections',
    sections: {
      customerHistory: 'Customer history',
      auditTrail: 'Audit trail',
      compactFeed: 'Compact feed',
    },
    customerSection: {
      title: 'Customer history (CRM)',
      description: 'Every touchpoint on an account in one feed. The type filter narrows the feed; the composer adds new entries at the top.',
    },
    auditSection: {
      title: 'Audit trail (ERP)',
      description:
        'Same component, different weight: most rows here are a field change. The value pair is drawn as "old → new", never turned into a sentence.',
    },
    compactSection: {
      title: 'Compact feed',
      description:
        'The dense variant used in a detail panel or a narrow card. The rail and tiles go; a fixed time column and a tone dot stay.',
      recentHeading: 'Recent activity',
      auditHeading: 'Audit trail',
    },
    visibleCount: '{{visible}} / {{total}} activities',
    customerEmptyDescription: 'No activity in the selected types. Remove a type or go back to "All".',
    rulesHeading: 'Rules on this page',
    rules: [
      ['The feed always runs newest to oldest', 'Reading a history top-down means dragging the reader to the bottom of the page for the answer to "what just happened".'],
      ['One row is one event', 'Two things happening at once means two rows. "Called and sent a quote" squeezed into one row can\'t be filtered, counted, or timestamped.'],
      ['Who, what, when — all three are required', 'A row with no actor belongs to the system and is written as "System". Leaving it blank makes the record useless for an audit.'],
      ['A field change is a pair, not a sentence', 'On the ERP side, a change is drawn as "old → new". An audit row turned into plain prose can\'t be scanned down a column.'],
      ['Time is relative, the heading is absolute', 'The row itself says "3 hours ago"; the group heading says "Today" or a full date. Together they give both recency and calendar.'],
      ['Pinning deliberately breaks chronology', 'A pinned entry sits under its own heading, outside the flow. Otherwise it reads as the ordering being broken.'],
      ['Type color never carries alone', 'Every row spells out the type name; the tone only speeds up scanning. The feed still reads fine in greyscale.'],
      ['The composer sits at the head of the feed', 'Moving note-taking into a modal lets the feed go stale. The entry is captured where the result will appear.'],
    ],
  },
}
