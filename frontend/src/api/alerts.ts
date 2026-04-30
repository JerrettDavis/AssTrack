import { apiGet, apiPost } from './client'

export type SpeedAlert = {
  id: string
  observationId: string
  deviceId: string
  deviceIdentifier?: string | null
  assetId?: string | null
  assetName?: string | null
  observedSpeedKmh: number
  thresholdKmh: number
  triggeredAt: string
  acknowledgedAtUtc?: string | null
  acknowledgedBy?: string | null
}

export function getSpeedAlerts() {
  return apiGet<SpeedAlert[]>('/api/speed-alerts')
}

export async function acknowledgeSpeedAlert(id: string, acknowledgedBy?: string): Promise<SpeedAlert> {
  return apiPost<SpeedAlert>(`/api/speed-alerts/${id}/acknowledge`, { acknowledgedBy: acknowledgedBy ?? null })
}
