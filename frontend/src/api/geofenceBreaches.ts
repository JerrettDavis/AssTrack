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

export function getGeofenceBreaches(): Promise<GeofenceBreach[]> {
  return apiGet<GeofenceBreach[]>('/api/geofences/breaches')
}

export async function acknowledgeBreach(id: string, acknowledgedBy?: string): Promise<GeofenceBreach> {
  return apiPost<GeofenceBreach>(`/api/geofences/breaches/${id}/acknowledge`, { acknowledgedBy: acknowledgedBy ?? null })
}
