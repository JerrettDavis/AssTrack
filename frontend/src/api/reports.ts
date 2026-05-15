import { apiGet } from './client'

export type UtilizationReportItem = {
  deviceId: string
  deviceIdentifier: string
  assetId?: string | null
  assetName?: string | null
  firstObservedAt?: string | null
  lastObservedAt?: string | null
  observationCount: number
  distanceKm: number
  movingMinutes: number
  idleMinutes: number
  stopCount: number
  maxSpeedKmh?: number | null
  averageMovingSpeedKmh?: number | null
}

export type UtilizationReport = {
  from: string
  to: string
  generatedAt: string
  assetCount: number
  deviceCount: number
  observationCount: number
  totalDistanceKm: number
  totalMovingMinutes: number
  totalIdleMinutes: number
  items: UtilizationReportItem[]
}

export type UtilizationReportParams = {
  assetId?: string
  deviceId?: string
  fromDate?: string
  toDate?: string
}

export function getUtilizationReport(params: UtilizationReportParams) {
  const query = new URLSearchParams()
  if (params.assetId) query.append('assetId', params.assetId)
  if (params.deviceId) query.append('deviceId', params.deviceId)
  if (params.fromDate) query.append('from', params.fromDate)
  if (params.toDate) query.append('to', params.toDate)

  const suffix = query.toString()
  return apiGet<UtilizationReport>(`/api/reports/utilization${suffix ? `?${suffix}` : ''}`)
}
