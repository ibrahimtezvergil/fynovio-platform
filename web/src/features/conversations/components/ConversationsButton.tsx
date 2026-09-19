import {
  ArrowLeft,
  Bell,
  BellOff,
  CheckCheck,
  MessageSquare,
  Mic,
  MoreVertical,
  Paperclip,
  Search,
  SendHorizontal,
  Smile,
  SquarePen,
  Trash2,
  X,
} from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type ChangeEvent, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Avatar, AvatarBadge, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import { DrawerClose, DrawerDescription, DrawerHeader, DrawerTitle } from '@/components/ui/drawer'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import { registerCommand } from '@/lib/commands'
import { openDrawer, useOverlayStore } from '@/lib/overlay'
import { cn } from '@/lib/utils'
import { paths } from '@/routes/paths'
import {
  availableConversationContacts,
  useConversationStore,
  useUnreadConversationCount,
  type ConversationContact,
} from '@/features/conversations/store/useConversationStore'

type ConversationFilter = 'all' | 'unread' | 'favorites' | 'groups'

const EMOJIS = ['😀', '😂', '❤️', '👍', '🙏', '🎉', '🔥', '👏']

/** Global communication is a quick-access drawer so the current workspace remains in view. */
export function ConversationsButton() {
  const { t, i18n } = useTranslation('conversations')
  const unread = useUnreadConversationCount()
  const overlayId = useOverlayStore((state) => state.entries.find((entry) => entry.overlayKey === 'conversations')?.id)
  const closeOverlay = useOverlayStore((state) => state.close)

  useEffect(() => {
    registerCommand({
      id: 'conversations.open',
      label: t('command.open'),
      group: t('command.group'),
      icon: MessageSquare,
      when: () => true,
      run: () => void openConversationsDrawer(),
    })
  }, [i18n.language, t])

  const toggleDrawer = () => {
    const activeId = useOverlayStore.getState().entries.find((entry) => entry.overlayKey === 'conversations')?.id
    if (activeId) {
      closeOverlay(activeId)
      return
    }
    openConversationsDrawer()
  }

  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <Button
            variant="secondary"
            size="icon"
            className="relative"
            aria-label={unread > 0 ? t('button.unread', { count: unread }) : t('button.allRead')}
            aria-expanded={Boolean(overlayId)}
            onClick={toggleDrawer}
          />
        }
      >
        <MessageSquare aria-hidden className="size-[17px]" strokeWidth={1.7} />
        {unread > 0 && (
          <span
            aria-hidden
            className="absolute -top-1 -right-1 flex h-[18px] min-w-[18px] items-center justify-center rounded-full border border-white/25 bg-[image:var(--nx-accent-grad)] px-1 text-[10.5px] leading-none font-[750] text-[var(--nx-on-accent)] shadow-[var(--nx-accent-glow)]"
          >
            {unread > 9 ? '9+' : unread}
          </span>
        )}
      </TooltipTrigger>
      <TooltipContent side="bottom" sideOffset={8}>
        {t('button.label')}
        {unread > 0 && <span className="tnum opacity-70">{unread}</span>}
      </TooltipContent>
    </Tooltip>
  )
}

/** Opens the one global conversations surface from the topbar or command palette. */
function openConversationsDrawer() {
  if (useOverlayStore.getState().entries.some((entry) => entry.overlayKey === 'conversations')) return Promise.resolve(undefined)
  return openDrawer({
    content: <ConversationsDrawer />,
    className: 'gap-0 sm:[--drawer-content-width:28rem]',
    overlayKey: 'conversations',
    drawerSwipeDirection: 'right',
  })
}

function ConversationsDrawer() {
  const { t, i18n } = useTranslation('conversations')
  const navigate = useNavigate()
  const conversations = useConversationStore((state) => state.conversations)
  const markRead = useConversationStore((state) => state.markRead)
  const markAllRead = useConversationStore((state) => state.markAllRead)
  const sendMessage = useConversationStore((state) => state.sendMessage)
  const startConversation = useConversationStore((state) => state.startConversation)
  const toggleMute = useConversationStore((state) => state.toggleMute)
  const removeConversation = useConversationStore((state) => state.removeConversation)
  const unread = useUnreadConversationCount()
  const overlayId = useOverlayStore((state) => state.entries.find((entry) => entry.overlayKey === 'conversations')?.id)
  const closeOverlay = useOverlayStore((state) => state.close)

  const [query, setQuery] = useState('')
  const [filter, setFilter] = useState<ConversationFilter>('all')
  const [selectedId, setSelectedId] = useState<string>()
  const [draft, setDraft] = useState('')
  const [chatSearchOpen, setChatSearchOpen] = useState(false)
  const [chatQuery, setChatQuery] = useState('')
  const [newChatOpen, setNewChatOpen] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const chatSearchInputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (chatSearchOpen) chatSearchInputRef.current?.focus()
  }, [chatSearchOpen])

  const selected = conversations.find((conversation) => conversation.id === selectedId)
  const locale = i18n.resolvedLanguage ?? i18n.language

  const filterSegments: readonly Segment<ConversationFilter>[] = useMemo(() => [
    { value: 'all', label: t('drawer.filters.all') },
    { value: 'unread', label: t('drawer.filters.unread') },
    { value: 'favorites', label: t('drawer.filters.favorites') },
    { value: 'groups', label: t('drawer.filters.groups') },
  ], [t])

  const visible = useMemo(() => {
    const normalized = query.trim().toLocaleLowerCase(locale)
    return conversations.filter((conversation) => {
      if (filter === 'unread' && conversation.unreadCount === 0) return false
      if (filter === 'favorites' && !conversation.favorite) return false
      if (filter === 'groups' && !conversation.isGroup) return false
      if (!normalized) return true
      return `${conversation.name} ${conversation.preview}`.toLocaleLowerCase(locale).includes(normalized)
    })
  }, [conversations, filter, locale, query])

  const newChatContacts = useMemo(
    () => availableConversationContacts.filter((contact) => !conversations.some((conversation) => conversation.id === contact.id)),
    [conversations],
  )

  const chatMessages = useMemo(() => {
    if (!selected) return []
    const normalized = chatQuery.trim().toLocaleLowerCase(locale)
    if (!normalized) return selected.messages
    return selected.messages.filter((message) => message.content.toLocaleLowerCase(locale).includes(normalized))
  }, [selected, chatQuery, locale])

  const closeChat = () => {
    setSelectedId(undefined)
    setChatSearchOpen(false)
    setChatQuery('')
  }
  const selectConversation = (id: string) => {
    markRead(id)
    setSelectedId(id)
    setChatSearchOpen(false)
    setChatQuery('')
  }
  const startNewChat = (contact: ConversationContact) => {
    const id = startConversation(contact)
    setNewChatOpen(false)
    selectConversation(id)
  }
  const goToSettings = () => {
    navigate(paths.settings)
    if (overlayId) closeOverlay(overlayId)
  }
  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const content = draft.trim()
    if (!selected || !content) return
    sendMessage(selected.id, content, t('drawer.now'))
    setDraft('')
  }
  const sendVoiceMessage = () => {
    if (!selected) return
    const seconds = Math.floor(Math.random() * 50) + 5
    sendMessage(selected.id, `🎤 ${t('drawer.voiceMessageContent', { seconds: seconds.toString().padStart(2, '0') })}`, t('drawer.now'))
  }
  const handleFileSelected = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file || !selected) return
    sendMessage(selected.id, `📎 ${file.name}`, t('drawer.now'))
  }

  return (
    <>
      <DrawerHeader className="flex flex-row items-center justify-between gap-3 border-b border-[var(--nx-hairline)] p-4">
        <div className="flex min-w-0 flex-1 items-center gap-3">
          {selected ? (
            <>
              <Button variant="ghost" size="icon-sm" className="-ml-1 shrink-0" onClick={closeChat} aria-label={t('drawer.back')}>
                <ArrowLeft aria-hidden />
              </Button>
              <Avatar className="size-9 shrink-0" size="default">
                <AvatarFallback>{selected.initials}</AvatarFallback>
                <AvatarBadge aria-hidden className="bg-[var(--nx-st-green-fg)]" />
              </Avatar>
              <div className="min-w-0">
                <DrawerTitle className="truncate text-[15px]">{selected.name}</DrawerTitle>
                <DrawerDescription>{t('drawer.online')}</DrawerDescription>
              </div>
            </>
          ) : (
            <>
              <Avatar className="size-9 shrink-0" size="default">
                <AvatarFallback>{t('drawer.title').slice(0, 1)}</AvatarFallback>
              </Avatar>
              <div className="min-w-0">
                <DrawerTitle>{t('drawer.title')}</DrawerTitle>
                <DrawerDescription>
                  {unread > 0 ? t('drawer.unreadDescription', { count: unread }) : t('drawer.allReadDescription')}
                </DrawerDescription>
              </div>
            </>
          )}
        </div>

        <div className="flex shrink-0 items-center gap-1">
          {selected ? (
            <>
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                aria-pressed={chatSearchOpen}
                aria-label={t('drawer.searchInChat')}
                onClick={() => setChatSearchOpen((open) => !open)}
              >
                <Search aria-hidden strokeWidth={1.7} />
              </Button>
              <DropdownMenu>
                <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={t('drawer.chatMenu')} />}>
                  <MoreVertical aria-hidden strokeWidth={1.7} />
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end" className="w-52">
                  <DropdownMenuItem onClick={() => toggleMute(selected.id)}>
                    {selected.muted ? <Bell aria-hidden strokeWidth={1.7} /> : <BellOff aria-hidden strokeWidth={1.7} />}
                    {selected.muted ? t('drawer.unmute') : t('drawer.mute')}
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem variant="destructive" onClick={() => { removeConversation(selected.id); closeChat() }}>
                    <Trash2 aria-hidden strokeWidth={1.7} />
                    {t('drawer.deleteConversation')}
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </>
          ) : (
            <>
              <Popover open={newChatOpen} onOpenChange={setNewChatOpen}>
                <PopoverTrigger render={<Button variant="ghost" size="icon-sm" aria-label={t('drawer.newChat')} />}>
                  <SquarePen aria-hidden strokeWidth={1.7} />
                </PopoverTrigger>
                <PopoverContent align="end" className="w-64 gap-1 p-2">
                  {newChatContacts.length ? newChatContacts.map((contact) => (
                    <button
                      key={contact.id}
                      type="button"
                      onClick={() => startNewChat(contact)}
                      className="flex items-center gap-2.5 rounded-md px-2 py-2 text-left text-[13px] outline-none transition-colors hover:bg-[var(--nx-fill-hover)] focus-visible:ring-3 focus-visible:ring-ring/40"
                    >
                      <Avatar className="size-7" size="sm"><AvatarFallback>{contact.initials}</AvatarFallback></Avatar>
                      {contact.name}
                    </button>
                  )) : (
                    <p className="px-2 py-3 text-[12.5px] text-muted-foreground">{t('drawer.newChatEmpty')}</p>
                  )}
                </PopoverContent>
              </Popover>
              <DropdownMenu>
                <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={t('drawer.menu')} />}>
                  <MoreVertical aria-hidden strokeWidth={1.7} />
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end" className="w-56">
                  <DropdownMenuItem disabled={unread === 0} onClick={() => markAllRead()}>
                    <CheckCheck aria-hidden strokeWidth={1.7} />
                    {t('drawer.markAllRead')}
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onClick={goToSettings}>
                    {t('drawer.settingsLink')}
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </>
          )}
          <DrawerClose render={<Button variant="ghost" size="icon-sm" />} aria-label={t('drawer.close')}>
            <X aria-hidden />
          </DrawerClose>
        </div>
      </DrawerHeader>

      {selected ? (
        <>
          {chatSearchOpen && (
            <div className="shrink-0 border-b border-[var(--nx-hairline)] p-3">
              <Input
                ref={chatSearchInputRef}
                value={chatQuery}
                onChange={(event) => setChatQuery(event.target.value)}
                placeholder={t('drawer.searchInChatPlaceholder')}
                aria-label={t('drawer.searchInChatLabel')}
              />
            </div>
          )}
          <div className="flex-1 overflow-y-auto bg-[image:radial-gradient(circle_at_1px_1px,var(--nx-hairline)_1px,transparent_0)] [background-size:18px_18px]" aria-live="polite">
            {chatMessages.length ? (
              <div className="space-y-2 px-4 py-5">
                <p className="mx-auto w-fit rounded-full bg-[var(--nx-glass-over)] px-3 py-1 text-[11px] font-[620] text-muted-foreground shadow-[inset_0_1px_0_var(--nx-specular)]">
                  {t('drawer.today')}
                </p>
                {chatMessages.map((message) => (
                  <div key={message.id} className={cn('flex', message.author === 'self' ? 'justify-end' : 'justify-start')}>
                    <div className={cn(
                      'max-w-[82%] px-3 py-2 text-[13px] leading-5 shadow-[var(--nx-elev)]',
                      message.author === 'self'
                        ? 'rounded-xl rounded-br-sm bg-[image:var(--nx-accent-grad)] text-[var(--nx-on-accent)]'
                        : 'rounded-xl rounded-bl-sm bg-[var(--nx-glass-over)] text-foreground',
                    )}>
                    <p>{message.content}</p>
                    <span className={cn('mt-1 flex items-center justify-end gap-1 text-[10.5px]', message.author === 'self' ? 'text-[var(--nx-on-accent)]/70' : 'text-muted-foreground')}>
                      <time>{message.time}</time>
                      {message.author === 'self' && <CheckCheck aria-label={t('drawer.delivered')} className="size-3" strokeWidth={1.8} />}
                    </span>
                  </div>
                  </div>
                ))}
              </div>
            ) : selected.messages.length === 0 ? (
              <div className="flex h-full flex-col items-center justify-center px-8 text-center">
                <MessageSquare aria-hidden className="mb-3 size-5 text-muted-foreground" strokeWidth={1.7} />
                <p className="text-[13px] font-[620]">{t('drawer.noMessagesYet')}</p>
              </div>
            ) : (
              <div className="flex h-full flex-col items-center justify-center px-8 text-center">
                <Search aria-hidden className="mb-3 size-5 text-muted-foreground" strokeWidth={1.7} />
                <p className="text-[13px] font-[620]">{t('drawer.noMessagesFound')}</p>
              </div>
            )}
          </div>
          <form className="flex shrink-0 items-center gap-1 border-t border-[var(--nx-hairline)] bg-[var(--nx-glass-over)] p-3" onSubmit={submit}>
            <Popover>
              <PopoverTrigger render={<Button type="button" variant="ghost" size="icon" aria-label={t('drawer.emoji')} />}>
                <Smile aria-hidden strokeWidth={1.7} />
              </PopoverTrigger>
              <PopoverContent align="start" side="top" className="w-auto gap-0 p-2">
                <div className="grid grid-cols-4 gap-1">
                  {EMOJIS.map((emoji) => (
                    <button
                      key={emoji}
                      type="button"
                      onClick={() => setDraft((current) => current + emoji)}
                      className="rounded-md p-1.5 text-lg outline-none transition-colors hover:bg-[var(--nx-fill-hover)] focus-visible:ring-3 focus-visible:ring-ring/40"
                    >
                      {emoji}
                    </button>
                  ))}
                </div>
              </PopoverContent>
            </Popover>
            <input ref={fileInputRef} type="file" className="hidden" onChange={handleFileSelected} />
            <Button type="button" variant="ghost" size="icon" aria-label={t('drawer.attach')} onClick={() => fileInputRef.current?.click()}>
              <Paperclip aria-hidden strokeWidth={1.7} />
            </Button>
            <Input value={draft} onChange={(event) => setDraft(event.target.value)} className="flex-1 rounded-full" placeholder={t('drawer.messagePlaceholder')} aria-label={t('drawer.messageLabel')} />
            {draft.trim() ? (
              <Button type="submit" size="icon" aria-label={t('drawer.send')}>
                <SendHorizontal aria-hidden />
              </Button>
            ) : (
              <Button type="button" size="icon" variant="secondary" aria-label={t('drawer.voiceMessage')} onClick={sendVoiceMessage}>
                <Mic aria-hidden strokeWidth={1.7} />
              </Button>
            )}
          </form>
        </>
      ) : (
        <>
          <div className="relative shrink-0 px-4 pt-4 pb-3">
            <Search aria-hidden className="text-muted-foreground absolute top-1/2 left-7 size-4 -translate-y-1/2" strokeWidth={1.7} />
            <Input value={query} onChange={(event) => setQuery(event.target.value)} className="pl-9" placeholder={t('drawer.searchPlaceholder')} aria-label={t('drawer.searchLabel')} />
          </div>
          <div className="shrink-0 px-4 pb-3">
            <SegmentedControl aria-label={t('drawer.filterLabel')} segments={filterSegments} value={filter} onChange={setFilter} fullWidth />
          </div>
          <div className="min-h-0 flex-1 overflow-y-auto border-t border-[var(--nx-hairline)] p-2">
            {visible.length ? visible.map((conversation) => {
              const lastMessage = conversation.messages.at(-1)
              return (
                <button
                  key={conversation.id}
                  type="button"
                  onClick={() => selectConversation(conversation.id)}
                  className="flex w-full items-center gap-3 rounded-md px-3 py-3 text-left outline-none transition-[background,transform] duration-[250ms] ease-fluid hover:bg-[var(--nx-fill-hover)] focus-visible:ring-3 focus-visible:ring-ring/40 active:scale-[0.99]"
                >
                  <Avatar className="size-9 shrink-0" size="default"><AvatarFallback>{conversation.initials}</AvatarFallback></Avatar>
                  <span className="min-w-0 flex-1">
                    <span className="flex items-baseline gap-2">
                      <span className={cn('flex-1 truncate text-[13.5px]', conversation.unreadCount > 0 && 'font-[650]')}>{conversation.name}</span>
                      <time className={cn('shrink-0 text-[11.5px] text-muted-foreground', conversation.unreadCount > 0 && 'text-foreground font-[620]')}>{conversation.time}</time>
                    </span>
                    <span className="mt-0.5 flex items-center gap-1">
                      {conversation.muted && <BellOff aria-hidden className="size-3 shrink-0 text-muted-foreground" strokeWidth={1.8} />}
                      {lastMessage?.author === 'self' && <CheckCheck aria-hidden className="size-3.5 shrink-0 text-muted-foreground" strokeWidth={1.8} />}
                      <span className={cn('truncate text-[12.5px] text-muted-foreground', conversation.unreadCount > 0 && 'text-foreground')}>
                        {conversation.preview || t('drawer.noMessagesYet')}
                      </span>
                    </span>
                    {conversation.muted && <span className="sr-only">{t('drawer.mutedMarker')}</span>}
                  </span>
                  {conversation.unreadCount > 0 && (
                    <span
                      aria-label={t('drawer.unreadMarker')}
                      className="flex h-[18px] min-w-[18px] shrink-0 items-center justify-center rounded-full bg-[image:var(--nx-accent-grad)] px-1 text-[10.5px] leading-none font-[750] text-[var(--nx-on-accent)] shadow-[var(--nx-accent-glow)]"
                    >
                      {conversation.unreadCount > 9 ? '9+' : conversation.unreadCount}
                    </span>
                  )}
                </button>
              )
            }) : (
              <div className="flex h-full flex-col items-center justify-center px-8 text-center">
                <MessageSquare aria-hidden className="mb-3 size-5 text-muted-foreground" strokeWidth={1.7} />
                <p className="text-[13px] font-[620]">{t('drawer.emptyTitle')}</p>
                <p className="mt-1 text-[12.5px] text-muted-foreground">{t('drawer.emptyDescription')}</p>
              </div>
            )}
          </div>
        </>
      )}
    </>
  )
}
