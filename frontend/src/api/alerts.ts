import { apiDelete, apiGet, apiPost, apiPut } from './client'
import { PagedResult } from './types'

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

export type AlertSummary = {
  unacknowledgedSpeedAlerts: number
  unacknowledgedBreaches: number
}

export type AlertsQueryParams = {
  unacknowledged?: boolean
  limit?: number
  since?: string
  page?: number
  pageSize?: number
}

export function getSpeedAlerts(params?: AlertsQueryParams) {
  const qs = new URLSearchParams()
  if (params?.unacknowledged != null) qs.set('unacknowledged', String(params.unacknowledged))
  if (params?.limit != null) qs.set('limit', String(params.limit))
  if (params?.since != null) qs.set('since', params.since)
  if (params?.page != null) qs.set('page', String(params.page))
  if (params?.pageSize != null) qs.set('pageSize', String(params.pageSize))
  const query = qs.toString()
  return apiGet<PagedResult<SpeedAlert>>(`/api/speed-alerts${query ? `?${query}` : ''}`)
}

export async function acknowledgeSpeedAlert(id: string, acknowledgedBy?: string): Promise<SpeedAlert> {
  return apiPost<SpeedAlert>(`/api/speed-alerts/${id}/acknowledge`, { acknowledgedBy: acknowledgedBy ?? null })
}

export async function bulkAcknowledgeSpeedAlerts(ids: string[], acknowledgedBy?: string): Promise<{ count: number }> {
  return apiPost<{ count: number }>('/api/speed-alerts/bulk-acknowledge', { ids, acknowledgedBy: acknowledgedBy ?? null })
}

export function getAlertSummary(): Promise<AlertSummary> {
  return apiGet<AlertSummary>('/api/alerts/summary')
}

export type AlertRoutingRule = {
  id: string
  name: string
  isEnabled: boolean
  eventType: 'all' | 'speed_alert' | 'geofence_breach'
  channel: string
  provider: string
  integrationFeedId?: string | null
  integrationFeedName?: string | null
  externalPeerId?: string | null
  displayName?: string | null
  recipient?: string | null
  messageTemplate?: string | null
  createdAt: string
  updatedAt: string
}

export type AlertRoutingRuleRequest = {
  name: string
  isEnabled: boolean
  eventType: 'all' | 'speed_alert' | 'geofence_breach'
  channel: string
  provider: string
  integrationFeedId?: string | null
  externalPeerId?: string | null
  displayName?: string | null
  recipient?: string | null
  messageTemplate?: string | null
}

export function getAlertRoutes(): Promise<AlertRoutingRule[]> {
  return apiGet<AlertRoutingRule[]>('/api/alert-routes')
}

export function createAlertRoute(data: AlertRoutingRuleRequest): Promise<AlertRoutingRule> {
  return apiPost<AlertRoutingRule>('/api/alert-routes', data)
}

export function updateAlertRoute(id: string, data: AlertRoutingRuleRequest): Promise<AlertRoutingRule> {
  return apiPut<AlertRoutingRule>(`/api/alert-routes/${id}`, data)
}

export async function deleteAlertRoute(id: string): Promise<void> {
  await apiDelete(`/api/alert-routes/${id}`)
}