import { FormEvent, useEffect, useMemo, useState } from 'react'
import { createMessageThread, getMessageThreads, getThreadMessages, sendMessage, type MessageEntry, type MessageThread } from '../api/messages'
import { getIntegrationFeeds, type IntegrationFeed } from '../api/integrations'
import { useLiveEvents } from '../hooks/useLiveEvents'

type ThreadForm = {
  channel: string
  provider: string
  integrationFeedId: string
  externalPeerId: string
  displayName: string
  subject: string
}

const initialThreadForm: ThreadForm = {
  channel: 'direct',
  provider: 'meshtastic',
  integrationFeedId: '',
  externalPeerId: '',
  displayName: '',
  subject: '',
}

function formatTime(value?: string | null) {
  return value ? new Date(value).toLocaleString() : 'No messages'
}

function statusClass(status: string) {
  if (['received', 'sent', 'delivered'].includes(status)) return 'badge-success'
  if (status === 'queued') return 'badge-warning'
  if (status === 'failed') return 'badge-danger'
  return ''
}

function displayPeer(thread: MessageThread) {
  return thread.displayName || thread.deviceLabel || thread.deviceIdentifier || thread.externalPeerId || thread.subject || 'Conversation'
}

export default function MessagesPage() {
  const [threads, setThreads] = useState<MessageThread[]>([])
  const [messages, setMessages] = useState<MessageEntry[]>([])
  const [feeds, setFeeds] = useState<IntegrationFeed[]>([])
  const [selectedThreadId, setSelectedThreadId] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [composeBody, setComposeBody] = useState('')
  const [showNewThread, setShowNewThread] = useState(false)
  const [threadForm, setThreadForm] = useState<ThreadForm>(initialThreadForm)
  const selectedThread = useMemo(
    () => threads.find((thread) => thread.id === selectedThreadId) ?? null,
    [threads, selectedThreadId],
  )

  async function loadThreads(preferredThreadId?: string | null) {
    const items = await getMessageThreads()
    setThreads(items)
    setSelectedThreadId((current) => preferredThreadId ?? current ?? items[0]?.id ?? null)
  }

  async function load() {
    try {
      setError(null)
      const [threadItems, feedItems] = await Promise.all([
        getMessageThreads(),
        getIntegrationFeeds().catch(() => [] as IntegrationFeed[]),
      ])
      setThreads(threadItems)
      setFeeds(feedItems)
      setSelectedThreadId((current) => current ?? threadItems[0]?.id ?? null)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setLoading(false)
    }
  }

  async function loadMessages(threadId: string | null) {
    if (!threadId) {
      setMessages([])
      return
    }

    try {
      setError(null)
      setMessages(await getThreadMessages(threadId))
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : String(err))
    }
  }

  useEffect(() => {
    void load()
  }, [])

  useEffect(() => {
    void loadMessages(selectedThreadId)
  }, [selectedThreadId])

  useLiveEvents((type, data) => {
    if (type !== 'message') return
    const entry = data as Partial<MessageEntry>
    void loadThreads(entry.threadId ?? selectedThreadId)
    if (entry.threadId === selectedThreadId) {
      void loadMessages(selectedThreadId)
    }
  })

  async function handleCreateThread(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    try {
      setError(null)
      const thread = await createMessageThread({
        channel: threadForm.channel.trim(),
        provider: threadForm.provider.trim(),
        integrationFeedId: threadForm.integrationFeedId || null,
        externalPeerId: threadForm.externalPeerId.trim() || null,
        displayName: threadForm.displayName.trim() || null,
        subject: threadForm.subject.trim() || null,
      })
      setThreadForm(initialThreadForm)
      setShowNewThread(false)
      await loadThreads(thread.id)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : String(err))
    }
  }

  async function handleSend(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selectedThread || !composeBody.trim()) return

    try {
      setError(null)
      await sendMessage(selectedThread.id, composeBody.trim(), selectedThread.externalPeerId)
      setComposeBody('')
      await Promise.all([loadThreads(selectedThread.id), loadMessages(selectedThread.id)])
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : String(err))
    }
  }

  if (loading) return <div className="card">Loading messages...</div>

  return (
    <div className="section">
      <div className="page-header">
        <div>
          <h1>Messages</h1>
          <p className="muted">Native conversations and bridge-delivered messages across mesh, chat, and automation channels.</p>
        </div>
        <button className="button button-secondary" onClick={() => setShowNewThread((value) => !value)} type="button">
          {showNewThread ? 'Cancel' : 'New Thread'}
        </button>
      </div>

      {error && (
        <div className="notice notice-danger">
          <strong>Messaging error</strong>
          <span className="muted">{error}</span>
        </div>
      )}

      {showNewThread && (
        <form className="card inline-form" onSubmit={handleCreateThread}>
          <div className="field-grid">
            <label className="field">
              <span>Provider</span>
              <select onChange={(event) => setThreadForm((value) => ({ ...value, provider: event.target.value }))} value={threadForm.provider}>
                <option value="meshtastic">Meshtastic</option>
                <option value="signal">Signal</option>
                <option value="telegram">Telegram</option>
                <option value="generic-webhook">Webhook</option>
              </select>
            </label>
            <label className="field">
              <span>Channel</span>
              <input onChange={(event) => setThreadForm((value) => ({ ...value, channel: event.target.value }))} required value={threadForm.channel} />
            </label>
            <label className="field">
              <span>Bridge feed</span>
              <select onChange={(event) => setThreadForm((value) => ({ ...value, integrationFeedId: event.target.value }))} value={threadForm.integrationFeedId}>
                <option value="">Native only</option>
                {feeds.map((feed) => (
                  <option key={feed.id} value={feed.id}>{feed.name}</option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>External peer</span>
              <input onChange={(event) => setThreadForm((value) => ({ ...value, externalPeerId: event.target.value }))} placeholder="!12f4fb74, +15551234567, @team" value={threadForm.externalPeerId} />
            </label>
            <label className="field">
              <span>Display name</span>
              <input onChange={(event) => setThreadForm((value) => ({ ...value, displayName: event.target.value }))} placeholder="DT01" value={threadForm.displayName} />
            </label>
            <label className="field">
              <span>Subject</span>
              <input onChange={(event) => setThreadForm((value) => ({ ...value, subject: event.target.value }))} value={threadForm.subject} />
            </label>
          </div>
          <div className="button-row">
            <button className="button" type="submit">Create thread</button>
          </div>
        </form>
      )}

      <div className="message-workspace">
        <aside className="thread-list" aria-label="Message threads">
          {threads.map((thread) => (
            <button
              className={`thread-row${thread.id === selectedThreadId ? ' active' : ''}`}
              key={thread.id}
              onClick={() => setSelectedThreadId(thread.id)}
              type="button"
            >
              <span>
                <strong>{displayPeer(thread)}</strong>
                <small>{thread.provider} / {thread.channel}</small>
              </span>
              <span className="muted">{formatTime(thread.lastMessageAt)}</span>
              {thread.lastMessage && <span className="thread-preview">{thread.lastMessage.body}</span>}
            </button>
          ))}
          {threads.length === 0 && (
            <div className="empty-state">
              <h3>No conversations</h3>
              <p className="muted">Create a native thread or let a bridge ingest the first inbound message.</p>
            </div>
          )}
        </aside>

        <section className="message-panel" aria-label="Message detail">
          {selectedThread ? (
            <>
              <header className="message-panel-header">
                <div>
                  <h2>{displayPeer(selectedThread)}</h2>
                  <p className="muted">
                    {selectedThread.integrationFeedName ?? 'Native'} · {selectedThread.externalPeerId ?? selectedThread.provider}
                  </p>
                </div>
                <span className="badge">{selectedThread.status}</span>
              </header>

              <div className="message-log">
                {messages.map((message) => (
                  <article className={`message-bubble ${message.direction}`} key={message.id}>
                    <div className="message-meta">
                      <span>{message.direction === 'outbound' ? 'AssTrack' : message.sender ?? selectedThread.externalPeerId ?? 'Inbound'}</span>
                      <span className={`badge ${statusClass(message.status)}`}>{message.status}</span>
                    </div>
                    <p>{message.body}</p>
                    <time className="muted">{formatTime(message.receivedAt ?? message.sentAt ?? message.createdAt)}</time>
                    {message.errorMessage && <span className="error-text">{message.errorMessage}</span>}
                  </article>
                ))}
                {messages.length === 0 && <p className="muted">No messages in this thread.</p>}
              </div>

              <form className="message-compose" onSubmit={handleSend}>
                <textarea
                  onChange={(event) => setComposeBody(event.target.value)}
                  placeholder="Write a message"
                  rows={3}
                  value={composeBody}
                />
                <button className="button" disabled={!composeBody.trim()} type="submit">Queue Send</button>
              </form>
            </>
          ) : (
            <div className="empty-state">
              <h3>Select a conversation</h3>
              <p className="muted">Messages from bridges and native threads will appear here.</p>
            </div>
          )}
        </section>
      </div>
    </div>
  )
}
