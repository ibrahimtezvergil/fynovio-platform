import { create } from 'zustand'

export interface ConversationMessage {
  id: string
  author: 'self' | 'other'
  content: string
  time: string
}

export interface Conversation {
  id: string
  name: string
  initials: string
  preview: string
  time: string
  unreadCount: number
  favorite: boolean
  isGroup: boolean
  muted: boolean
  messages: ConversationMessage[]
}

export interface ConversationContact {
  id: string
  name: string
  initials: string
}

interface ConversationState {
  conversations: Conversation[]
  markRead: (id: string) => void
  markAllRead: () => void
  sendMessage: (id: string, content: string, time: string) => void
  startConversation: (contact: ConversationContact) => string
  toggleMute: (id: string) => void
  removeConversation: (id: string) => void
}

const initialConversations: Conversation[] = [
  {
    id: 'ayse-yilmaz', name: 'Ayşe Yılmaz', initials: 'AY', preview: 'Teklifin son halini bugün paylaşabilir misiniz?', time: '10:42', unreadCount: 2, favorite: true, isGroup: false, muted: false,
    messages: [
      { id: 'm1', author: 'other', content: 'Merhaba, teklifin son halini bugün paylaşabilir misiniz?', time: '10:34' },
      { id: 'm2', author: 'self', content: 'Merhaba Ayşe Hanım, son düzenlemeleri tamamlıyorum.', time: '10:38' },
      { id: 'm3', author: 'other', content: 'Harika, teklifin son halini bugün paylaşabilir misiniz?', time: '10:42' },
    ],
  },
  {
    id: 'murat-kaya', name: 'Murat Kaya', initials: 'MK', preview: 'Yarınki toplantı için notları ekledim.', time: '09:18', unreadCount: 1, favorite: false, isGroup: false, muted: false,
    messages: [
      { id: 'm2', author: 'other', content: 'Yarınki toplantı için notları ekledim.', time: '09:18' },
    ],
  },
  {
    id: 'proje-ekibi', name: 'Proje Ekibi', initials: 'PE', preview: 'Murat: Sunumu bugün tamamlayalım.', time: 'Cuma', unreadCount: 3, favorite: false, isGroup: true, muted: false,
    messages: [
      { id: 'm1', author: 'other', content: 'Sunum taslağını paylaştım, göz atar mısınız?', time: '11:02' },
      { id: 'm2', author: 'self', content: 'Bakıyorum, birkaç eklemem olacak.', time: '11:10' },
      { id: 'm3', author: 'other', content: 'Murat: Sunumu bugün tamamlayalım.', time: '11:15' },
    ],
  },
  {
    id: 'pinar-demir', name: 'Pınar Demir', initials: 'PD', preview: 'Sözleşmeyi incelemeye aldık.', time: 'Dün', unreadCount: 1, favorite: true, isGroup: false, muted: false,
    messages: [
      { id: 'm3', author: 'other', content: 'Sözleşmeyi incelemeye aldık.', time: 'Dün' },
    ],
  },
  {
    id: 'emre-arslan', name: 'Emre Arslan', initials: 'EA', preview: 'Dosyaları müşteri alanına yükledim.', time: 'Dün', unreadCount: 1, favorite: false, isGroup: false, muted: true,
    messages: [
      { id: 'm4', author: 'other', content: 'Dosyaları müşteri alanına yükledim.', time: 'Dün' },
    ],
  },
  {
    id: 'selin-koc', name: 'Selin Koç', initials: 'SK', preview: 'Teşekkürler, dönüş yapacağım.', time: 'Paz', unreadCount: 0, favorite: false, isGroup: false, muted: false,
    messages: [
      { id: 'm5', author: 'other', content: 'Teşekkürler, dönüş yapacağım.', time: 'Paz' },
      { id: 'm6', author: 'self', content: 'Rica ederim, bekliyorum.', time: 'Paz' },
    ],
  },
]

/** Contacts with no conversation yet — the pool "Yeni sohbet" starts from. */
export const availableConversationContacts: ConversationContact[] = [
  { id: 'burak-sahin', name: 'Burak Şahin', initials: 'BŞ' },
  { id: 'zeynep-aydin', name: 'Zeynep Aydın', initials: 'ZA' },
]

/** Mocked global inbox state keeps the topbar badge and the drawer in sync. */
export const useConversationStore = create<ConversationState>((set, get) => ({
  conversations: initialConversations,
  markRead: (id) => set((state) => ({
    conversations: state.conversations.map((conversation) => (
      conversation.id === id ? { ...conversation, unreadCount: 0 } : conversation
    )),
  })),
  markAllRead: () => set((state) => ({
    conversations: state.conversations.map((conversation) => ({ ...conversation, unreadCount: 0 })),
  })),
  sendMessage: (id, content, time) => set((state) => {
    const conversation = state.conversations.find((candidate) => candidate.id === id)
    if (!conversation) return state
    const message = { id: crypto.randomUUID(), author: 'self' as const, content, time }
    const updated = { ...conversation, preview: content, time, messages: [...conversation.messages, message] }
    return { conversations: [updated, ...state.conversations.filter((candidate) => candidate.id !== id)] }
  }),
  startConversation: (contact) => {
    const existing = get().conversations.find((candidate) => candidate.id === contact.id)
    if (existing) return existing.id
    const conversation: Conversation = {
      id: contact.id, name: contact.name, initials: contact.initials, preview: '', time: '', unreadCount: 0, favorite: false, isGroup: false, muted: false,
      messages: [],
    }
    set((state) => ({ conversations: [conversation, ...state.conversations] }))
    return conversation.id
  },
  toggleMute: (id) => set((state) => ({
    conversations: state.conversations.map((conversation) => (
      conversation.id === id ? { ...conversation, muted: !conversation.muted } : conversation
    )),
  })),
  removeConversation: (id) => set((state) => ({
    conversations: state.conversations.filter((conversation) => conversation.id !== id),
  })),
}))

export const useUnreadConversationCount = () => useConversationStore(
  (state) => state.conversations.filter((conversation) => conversation.unreadCount > 0 && !conversation.muted).length,
)
