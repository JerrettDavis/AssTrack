import { apiGet, apiPost } from './client'

export type GeofenceBreach = {
  id: string
  observationId: string
  geofenceId: string
  geofenceName: string
  deviceId: string
  deviceIdentifier?: string | null
  assetName?: string | null
  assetId?: string | null
  eventType: string
  detectedAt: string
  acknowledgedAtUtc?: string | null
  acknowledgedBy?: string | null
}

export type BreachesQueryParams = {
  unacknowledged?: boolean
  limit?: number
  since?: string
}

export function getGeofenceBreaches(params?: BreachesQueryParams): Promise<GeofenceBreach[]> {
  const qs = new URLSearchParams()
  if (params?.unacknowledged != null) qs.set('unacknowledged', String(params.unacknowledged))
  if (params?.limit != null) qs.set('limit', String(params.limit))
  if (params?.since != null) qs.set('since', params.since)
  const query = qs.toString()
  return apiGet<GeofenceBreach[]>(`/api/geofences/breaches${query ? `?${query}` : ''}`)
}

export async function acknowledgeBreach(id: string, acknowledgedBy?: string): Promise<GeofenceBreach> {
  return apiPost<GeofenceBreach>(`/api/geofences/breaches/${id}/acknowledge`, { acknowledgedBy: acknowledgedBy ?? null })
}

export async function bulkAcknowledgeBreaches(ids: string[], acknowledgedBy?: string): Promise<{ count: number }> {
  return apiPost<{ count: number }>('/api/geofences/breaches/bulk-acknowledge', { ids, acknowledgedBy: acknowledgedBy ?? null })
}
