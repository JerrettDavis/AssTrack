import { apiGet, apiPost } from './client'
import { getRuntimeApiKey } from './config'
import type { PagedResult } from './types'

export type IntegrationEvent = {
  id: string
  occurredAt: string
  source: string
  externalEventId: string | null
  eventType: string
  severity: string
  subjectType: string | null
  subjectId: string | null
  subjectName: string | null
  message: string
  payloadJson: string | null
  correlationId: string | null
  status: string
  acknowledgedAt: string | null
  acknowledgedBy: string | null
  resolvedAt: string | null
  resolvedBy: string | null
  resolutionNote: string | null
}

export type IntegrationEventQuery = {
  source?: string
  externalEventId?: string
  eventType?: string
  severity?: string
  status?: string
  subjectType?: string
  subjectId?: string
  page?: number
  pageSize?: number
}

export type CreateIntegrationEventRequest = {
  source: string
  externalEventId?: string | null
  eventType: string
  severity: string
  subjectType?: string | null
  subjectId?: string | null
  subjectName?: string | null
  message: string
  payload?: unknown
}

export type IntegrationEventResult = PagedResult<IntegrationEvent>

function toQueryString(query: IntegrationEventQuery): string {
  const params = new URLSearchParams()
  Object.entries(query).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      params.set(key, String(value))
    }
  })
  return params.toString()
}

export function getIntegrationEvents(query: IntegrationEventQuery = {}): Promise<IntegrationEventResult> {
  const suffix = toQueryString({ page: 1, pageSize: 50, ...query })
  return apiGet<IntegrationEventResult>(`/api/integration-events${suffix ? `?${suffix}` : ''}`)
}

export async function exportIntegrationEventsCsv(query: IntegrationEventQuery = {}): Promise<Blob> {
  const suffix = toQueryString(query)
  const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() || ''
  const apiKey = getRuntimeApiKey()
  const response = await fetch(`${apiBaseUrl}/api/integration-events/export${suffix ? `?${suffix}` : ''}`, {
    headers: apiKey ? { 'X-Api-Key': apiKey } : {},
  })
  if (!response.ok) throw new Error(`Export failed: ${response.statusText}`)
  return response.blob()
}

export function publishIntegrationEvent(request: CreateIntegrationEventRequest): Promise<IntegrationEvent> {
  return apiPost<IntegrationEvent>('/api/integration-events', request)
}

export function acknowledgeIntegrationEvent(id: string): Promise<IntegrationEvent> {
  return apiPost<IntegrationEvent>(`/api/integration-events/${id}/acknowledge`, {})
}

export function resolveIntegrationEvent(id: string, resolutionNote?: string): Promise<IntegrationEvent> {
  return apiPost<IntegrationEvent>(`/api/integration-events/${id}/resolve`, { resolutionNote: resolutionNote || null })
}
