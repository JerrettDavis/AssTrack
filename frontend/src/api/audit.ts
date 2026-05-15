import { apiGet } from './client'
import { getRuntimeApiKey } from './config'

export type AuditEvent = {
  id: string
  occurredAt: string
  actorName: string
  actorRole: string
  action: string
  entityType: string
  entityId?: string | null
  entityName?: string | null
  summary?: string | null
  metadataJson?: string | null
  correlationId?: string | null
}

export type AuditEventResult = {
  items: AuditEvent[]
  totalCount: number
  page: number
  pageSize: number
}

export type AuditEventQuery = {
  action?: string
  entityType?: string
  actor?: string
  page?: number
  pageSize?: number
}

export function getAuditEvents(query: AuditEventQuery = {}) {
  const suffix = toQueryString(query)
  return apiGet<AuditEventResult>(`/api/audit-events${suffix ? `?${suffix}` : ''}`)
}

export async function exportAuditEventsCsv(query: AuditEventQuery = {}): Promise<Blob> {
  const suffix = toQueryString(query)
  const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() || ''
  const apiKey = getRuntimeApiKey()
  const response = await fetch(`${apiBaseUrl}/api/audit-events/export${suffix ? `?${suffix}` : ''}`, {
    headers: apiKey ? { 'X-Api-Key': apiKey } : {},
  })
  if (!response.ok) throw new Error(`Export failed: ${response.statusText}`)
  return response.blob()
}

function toQueryString(query: AuditEventQuery = {}) {
  const params = new URLSearchParams()
  if (query.action) params.set('action', query.action)
  if (query.entityType) params.set('entityType', query.entityType)
  if (query.actor) params.set('actor', query.actor)
  if (query.page) params.set('page', String(query.page))
  if (query.pageSize) params.set('pageSize', String(query.pageSize))
  return params.toString()
}
