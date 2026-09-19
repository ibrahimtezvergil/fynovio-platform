export default {
  kind: {
    system: 'System',
    task: 'Task',
    reminder: 'Reminder',
    mention: 'Mention',
    approval: 'Approval',
  },
  filter: {
    all: 'All',
    unread: 'Unread',
    mine: 'Mine',
    system: 'System',
  },
  row: {
    unread: 'Unread',
    markRead: 'Mark as read',
    markUnread: 'Mark as unread',
    dismiss: 'Dismiss notification',
  },
  toast: {
    openDescription: 'This will open the related record.',
    accepted: 'Approved',
    rejected: 'Rejected',
  },
  center: {
    filterLabel: 'Notification filter',
    bellUnread: 'Notifications, {{count}} unread',
    bellAllRead: 'Notifications, all read',
    title: 'Notifications',
    markAllRead: 'Mark all as read',
    emptyTitle: 'No notifications',
    emptyAllReadTitle: 'All caught up',
    emptyAllReadDescription: "You're all caught up — new ones will collect here.",
    emptyFilteredDescription: 'Nothing matches this filter. Try another tab.',
    viewAll: 'View all notifications',
    settingsLink: 'Notification settings',
    drawerTitle: 'Notification center',
    drawerUnreadPrefix: '{{count}} unread · ',
    drawerCount: '{{count}} notifications, newest first.',
    clearAll: 'Clear all',
    close: 'Close',
  },
  seed: {
    decision: {
      approve: 'Approve',
      reject: 'Reject',
    },
    n1: {
      title: 'Discount approval pending',
      body: 'Nordwind Lojistik · 18% discount requested on the fleet-tracking renewal deal.',
    },
    n2: {
      title: 'You were mentioned in a comment',
      body: '"Can we get this quote out by Friday @you?" — Baltic Freight AB',
    },
    n3: {
      title: 'Accounting integration disconnected',
      body: 'The last 4 invoices failed to sync. The access key may have expired.',
    },
    n4: {
      title: 'A task was assigned to you',
      body: 'Prepare the 40-seat expansion quote for Meridian Retail Group.',
    },
    n5: {
      title: 'Tomorrow 9:30 AM · Ege Yapı Malzeme meeting',
      body: 'Annual support package closing meeting. Presentation file not attached yet.',
    },
    n6: {
      title: 'A quote is about to expire',
      body: 'Kuzey Nakliyat A.Ş. · The route optimization module quote expires in 2 days.',
    },
    n7: {
      title: 'Weekly pipeline report is ready',
      body: '38 open deals · ₺3.21M weighted value. The report can be exported.',
    },
    n8: {
      title: 'Purchase request approved',
      body: 'PR-2291 · 12 warehouse supply line items, ₺84,500 — finance approval complete.',
    },
  },
}
