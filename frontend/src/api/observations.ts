import { apiGet } from './client'

export type Observation = {
  id: string
  deviceId: string
  deviceIdentifier: string
  assetId?: string | null
  assetName?: string | null
  observedAt: string
  receivedAt: string
  latitude: number
  longitude: number
  altitude?: number | null
  accuracyMeters?: number | null
  speedKmh?: number | null
  headingDegrees?: number | null
  metadata?: string | null
}

export function getObservations() {
  return apiGet<Observation[]>('/api/observations')
}
