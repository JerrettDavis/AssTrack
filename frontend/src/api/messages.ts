import { apiGet, apiPost } from './client'

export type MessageEntry = {
  id: string
  threadId: string
  direction: 'inbound' | 'outbound' | 'system'
  status: 'received' | 'queued' | 'sent' | 'delivered' | 'failed'
  sender?: string | null
  recipient?: string | null
  body: string
  providerMessageId?: string | null
  sentAt?: string | null
  receivedAt?: string | null
  createdAt: string
  errorMessage?: string | null
  metadata?: string | null
}

export type MessageThread = {
  id: string
  channel: string
  provider: string
  integrationFeedId?: string | null
  integrationFeedName?: string | null
  deviceId?: string | null
  deviceIdentifier?: string | null
  deviceLabel?: string | null
  assetId?: string | null
  assetName?: string | null
  externalPeerId?: string | null
  displayName?: string | null
  subject?: string | null
  status: string
  metadata?: string | null
  createdAt: string
  updatedAt: string
  lastMessageAt?: string | null
  lastMessage?: MessageEntry | null
}

export type CreateMessageThreadRequest = {
  channel: string
  provider: string
  integrationFeedId?: string | null
  deviceId?: string | null
  assetId?: string | null
  externalPeerId?: string | null
  displayName?: string | null
  subject?: string | null
  metadata?: string | null
}

export function getMessageThreads(): Promise<MessageThread[]> {
  return apiGet<MessageThread[]>('/api/messages/threads')
}

export function getThreadMessages(threadId: string): Promise<MessageEntry[]> {
  return apiGet<MessageEntry[]>(`/api/messages/threads/${threadId}/messages`)
}

export function createMessageThread(data: CreateMessageThreadRequest): Promise<MessageThread> {
  return apiPost<MessageThread>('/api/messages/threads', data)
}

export function sendMessage(threadId: string, body: string, recipient?: string | null): Promise<MessageEntry> {
  return apiPost<MessageEntry>(`/api/messages/threads/${threadId}/messages`, {
    body,
    recipient: recipient || null,
  })
}
