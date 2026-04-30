import { apiGet } from './client'

export type SpeedAlert = {
  id: string
  observationId: string
  deviceId: string
  assetId?: string | null
  observedSpeedKmh: number
  thresholdKmh: number
  triggeredAt: string
}

export function getSpeedAlerts() {
  return apiGet<SpeedAlert[]>('/api/speed-alerts')
}
