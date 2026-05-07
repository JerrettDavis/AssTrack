import { apiGet, apiPost } from './client'

export type CustodyEvent = {
  id: string
  assetId: string
  assetName?: string | null
  eventType: string
  fromCustodianName?: string | null
  toCustodianName?: string | null
  toCustodianContact?: string | null
  location?: string | null
  notes?: string | null
  occurredAt: string
  createdAt: string
}

export function getCustodyEvents(options: { assetId?: string, limit?: number } = {}) {
  const query = new URLSearchParams()
  if (options.assetId) query.set('assetId', options.assetId)
  if (options.limit) query.set('limit', options.limit.toString())
  return apiGet<CustodyEvent[]>(`/api/custody/events?${query}`)
}

export function createCustodyEvent(data: {
  assetId: string
  eventType: string
  toCustodianName?: string | null
  toCustodianContact?: string | null
  custodyStatus?: string | null
  location?: string | null
  notes?: string | null
  occurredAt?: string | null
}) {
  return apiPost<CustodyEvent>('/api/custody/events', data)
}
