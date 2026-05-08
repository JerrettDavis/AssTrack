import { getRuntimeApiKey } from './config'

export type LiveEventType = 'observation' | 'speed_alert' | 'geofence_breach' | 'message' | 'data_changed'

export type ObservationEvent = {
  id: string
  deviceId: string
  assetId: string | null
  latitude: number
  longitude: number
  speedKmh: number | null
  observedAt: string
}

export type SpeedAlertEvent = {
  id: string
  deviceId: string
  assetId: string | null
  observedSpeedKmh: number
  thresholdKmh: number
  triggeredAt: string
}

export type GeofenceBreachEvent = {
  id: string
  deviceId: string
  assetId: string | null
  geofenceId: string
  eventType: string
  detectedAt: string
}

export type DataChangedEvent = {
  entity: string
  action: string
  id?: string | null
  metadata?: unknown
  occurredAt: string
}

type Listener = (type: LiveEventType, data: unknown) => void

const listeners = new Set<Listener>()
let eventSource: EventSource | null = null
let reconnectTimer: ReturnType<typeof setTimeout> | null = null
let status: 'connecting' | 'open' | 'closed' = 'closed'
let sseToken: string | null = null

async function fetchSseToken(): Promise<string | null> {
  try {
    const base = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() ?? ''
    const key = getRuntimeApiKey()

    if (!key) {
      console.error('API key is not configured (config.json not loaded or apiKey empty)')
      return null
    }

    const response = await fetch(`${base}/api/events/token`, {
      method: 'POST',
      headers: {
        'X-Api-Key': key
      }
    })

    if (!response.ok) {
      console.error('Failed to fetch SSE token:', response.status, response.statusText)
      return null
    }

    const data = await response.json() as { token: string; expiresAt: string }
    return data.token
  } catch (err) {
    console.error('Error fetching SSE token:', err)
    return null
  }
}

function getUrl(): string {
  const base = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() ?? ''
  const params = sseToken ? `?token=${encodeURIComponent(sseToken)}` : ''
  return `${base}/api/events${params}`
}

async function connect(): Promise<void> {
  if (eventSource !== null) return
  status = 'connecting'

  // Fetch a fresh token
  sseToken = await fetchSseToken()
  if (!sseToken) {
    status = 'closed'
    console.error('Could not obtain SSE token, reconnecting in 5s...')
    if (listeners.size > 0) {
      reconnectTimer = setTimeout(() => {
        reconnectTimer = null
        if (listeners.size > 0) connect().catch(err => console.error('Connect error:', err))
      }, 5000)
    }
    return
  }

  eventSource = new EventSource(getUrl())

  eventSource.onopen = () => {
    status = 'open'
  }

  const eventTypes: LiveEventType[] = ['observation', 'speed_alert', 'geofence_breach', 'message', 'data_changed']
  for (const type of eventTypes) {
    eventSource.addEventListener(type, (e: MessageEvent) => {
      let data: unknown
      try {
        data = JSON.parse(e.data as string)
      } catch {
        data = e.data
      }
      for (const listener of listeners) {
        listener(type, data)
      }
    })
  }

  eventSource.onerror = () => {
    status = 'closed'
    eventSource?.close()
    eventSource = null
    if (listeners.size > 0) {
      reconnectTimer = setTimeout(() => {
        reconnectTimer = null
        if (listeners.size > 0) connect().catch(err => console.error('Connect error:', err))
      }, 5000)
    }
  }
}

function disconnect(): void {
  if (reconnectTimer !== null) {
    clearTimeout(reconnectTimer)
    reconnectTimer = null
  }
  if (eventSource !== null) {
    eventSource.close()
    eventSource = null
  }
  status = 'closed'
  sseToken = null
}

export function subscribeLiveEvents(listener: Listener): () => void {
  listeners.add(listener)
  if (listeners.size === 1) {
    setTimeout(() => {
      if (listeners.size > 0) {
        connect().catch(err => console.error('Connect error:', err))
      }
    }, 1000)
  }
  return () => {
    listeners.delete(listener)
    if (listeners.size === 0) {
      disconnect()
    }
  }
}

export function getSseStatus(): 'connecting' | 'open' | 'closed' {
  return status
}
