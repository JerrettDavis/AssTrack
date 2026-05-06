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

export function resyncBridgeFeed(feedKey: string): Promise<{ feedKey: string; resyncVersion: number }> {
  return bridgePost(`/bridge/${encodeURIComponent(feedKey)}/resync`)
}
