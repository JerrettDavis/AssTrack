import { apiGet } from './client'

export type GeofenceBreach = {
  id: string
  observationId: string
  geofenceId: string
  geofenceName: string
  deviceId: string
  assetId?: string
  detectedAt: string
}

export function getGeofenceBreaches(): Promise<GeofenceBreach[]> {
  return apiGet<GeofenceBreach[]>('/api/geofences/breaches')
}
