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

export type ObservationHistoryParams = {
  deviceId?: string
  assetId?: string
  fromDate?: string
  toDate?: string
  page?: number
  pageSize?: number
}

export type PagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export function getObservations() {
  return apiGet<Observation[]>('/api/observations')
}

export function getLatestPositions() {
  return apiGet<Observation[]>('/api/observations/latest-positions')
}

export async function getObservationHistory(params: ObservationHistoryParams): Promise<PagedResult<Observation>> {
  const query = new URLSearchParams()
  if (params.deviceId) query.append('deviceId', params.deviceId)
  if (params.assetId) query.append('assetId', params.assetId)
  if (params.fromDate) query.append('from', params.fromDate)
  if (params.toDate) query.append('to', params.toDate)
  if (params.page) query.append('page', params.page.toString())
  if (params.pageSize) query.append('pageSize', params.pageSize.toString())

  return apiGet<PagedResult<Observation>>(`/api/observations/history?${query}`)
}

export async function exportObservationsCsv(params: ObservationHistoryParams): Promise<Blob> {
  const query = new URLSearchParams()
  if (params.deviceId) query.append('deviceId', params.deviceId)
  if (params.assetId) query.append('assetId', params.assetId)
  if (params.fromDate) query.append('from', params.fromDate)
  if (params.toDate) query.append('to', params.toDate)
  if (params.pageSize) query.append('pageSize', params.pageSize.toString())
  query.append('format', 'csv')

  const apiBaseUrl =
    (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() || ''
  const apiKey =
    (import.meta.env.VITE_API_KEY as string | undefined)?.trim() || ''

  const response = await fetch(`${apiBaseUrl}/api/observations/history?${query}`, {
    headers: apiKey ? { 'X-Api-Key': apiKey } : {},
  })

  if (!response.ok) {
    throw new Error(`Export failed: ${response.statusText}`)
  }

  return response.blob()
}
