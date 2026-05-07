import { apiGet, apiPost } from './client'
import type { SensorReading } from './assets'

export type SensorReadingParams = {
  assetId?: string
  deviceId?: string
  sensorType?: string
  fromDate?: string
  toDate?: string
  limit?: number
}

export type CreateSensorReadingRequest = {
  assetId?: string | null
  deviceId?: string | null
  deviceIdentifier?: string | null
  integrationFeedId?: string | null
  sensorType: string
  name?: string | null
  numericValue?: number | null
  textValue?: string | null
  unit?: string | null
  observedAt?: string | null
  metadata?: string | null
}

export function getSensorReadings(params: SensorReadingParams = {}) {
  const query = new URLSearchParams()
  if (params.assetId) query.append('assetId', params.assetId)
  if (params.deviceId) query.append('deviceId', params.deviceId)
  if (params.sensorType) query.append('sensorType', params.sensorType)
  if (params.fromDate) query.append('from', params.fromDate)
  if (params.toDate) query.append('to', params.toDate)
  if (params.limit) query.append('limit', params.limit.toString())
  return apiGet<SensorReading[]>(`/api/sensors/readings?${query}`)
}

export function createSensorReading(data: CreateSensorReadingRequest) {
  return apiPost<SensorReading>('/api/sensors/readings', data)
}
