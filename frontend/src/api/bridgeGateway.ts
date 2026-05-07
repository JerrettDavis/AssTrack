export type BridgeRuntimeStatus = {
  dryRun: boolean
  feeds: BridgeFeedRuntimeStatus[]
}

export type BridgeFeedRuntimeStatus = {
  feedKey: string
  feedId?: string | null
  provider: string
  state: string
  host?: string | null
  topic?: string | null
  connectedAt?: string | null
  lastMessageAt?: string | null
  lastDeliveryAt?: string | null
  lastTrackerId?: string | null
  lastError?: string | null
  messagesReceived: number
  observationsParsed: number
  observationsDelivered: number
  deliveryFailures: number
  updatedAt: string
}

export type BridgeLogEntry = {
  timestamp: string
  feedKey: string
  level: string
  message: string
}

export type BridgeRawMessageEntry = {
  timestamp: string
  feedKey: string
  provider: string
  topic?: string | null
  trackerId?: string | null
  messageType?: string | null
  summary: string
  payload: string
}

async function bridgeGet<T>(path: string): Promise<T> {
  const response = await fetch(path)
  if (!response.ok) throw new Error(`${path} failed with ${response.status}`)
  return (await response.json()) as T
}

async function bridgePost<T>(path: string): Promise<T> {
  const response = await fetch(path, { method: 'POST' })
  if (!response.ok) throw new Error(`${path} failed with ${response.status}`)
  return (await response.json()) as T
}

export function getBridgeStatus(): Promise<BridgeRuntimeStatus> {
  return bridgeGet<BridgeRuntimeStatus>('/bridge/status')
}

export function getBridgeLogs(feedKey?: string, limit = 80): Promise<BridgeLogEntry[]> {
  const params = new URLSearchParams({ limit: String(limit) })
  if (feedKey) params.set('feedKey', feedKey)
  return bridgeGet<BridgeLogEntry[]>(`/bridge/logs?${params}`)
}

export type BridgeMessageFilters = {
  feedKey?: string
  search?: string
  trackerId?: string
  topic?: string
  messageType?: string
  payloadOnly?: boolean
  limit?: number
}

export function getBridgeMessages(filters: BridgeMessageFilters | string = {}): Promise<BridgeRawMessageEntry[]> {
  const normalizedFilters: BridgeMessageFilters = typeof filters === 'string'
    ? { feedKey: filters || undefined }
    : filters
  const limit = normalizedFilters.limit ?? 100
  const params = new URLSearchParams({ limit: String(limit) })
  if (normalizedFilters.feedKey) params.set('feedKey', normalizedFilters.feedKey)
  if (normalizedFilters.search?.trim()) params.set('search', normalizedFilters.search.trim())
  if (normalizedFilters.trackerId?.trim()) params.set('trackerId', normalizedFilters.trackerId.trim())
  if (normalizedFilters.topic?.trim()) params.set('topic', normalizedFilters.topic.trim())
  if (normalizedFilters.messageType?.trim()) params.set('messageType', normalizedFilters.messageType.trim())
  if (normalizedFilters.payloadOnly) params.set('payloadOnly', 'true')
  return bridgeGet<unknown[]>(`/bridge/messages?${params}`).then((items) => items.map(normalizeBridgeMessage))
}

function normalizeBridgeMessage(value: unknown): BridgeRawMessageEntry {
  const item = typeof value === 'object' && value !== null ? value as Record<string, unknown> : {}
  const get = (...names: string[]) => {
    for (const name of names) {
      const raw = item[name]
      if (raw !== undefined && raw !== null) return String(raw)
    }
    return undefined
  }

  const payload = get('payload', 'Payload') ?? JSON.stringify(value, null, 2)
  return {
    timestamp: get('timestamp', 'Timestamp') ?? new Date().toISOString(),
    feedKey: get('feedKey', 'FeedKey') ?? '(unknown feed)',
    provider: get('provider', 'Provider') ?? 'unknown',
    topic: get('topic', 'Topic') ?? null,
    trackerId: get('trackerId', 'TrackerId') ?? null,
    messageType: get('messageType', 'MessageType') ?? null,
    summary: get('summary', 'Summary') ?? payload.slice(0, 300),
    payload,
  }
}

export function resyncBridgeFeed(feedKey: string): Promise<{ feedKey: string; resyncVersion: number }> {
  return bridgePost(`/bridge/${encodeURIComponent(feedKey)}/resync`)
}
