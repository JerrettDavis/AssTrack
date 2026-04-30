import { apiGet, apiPost } from './client'

export interface WebhookStatus {
  configured: boolean
  last24hDeliveries: number
  last24hFailures: number
  lastDeliveredAt: string | null
  avgDurationMs: number | null
}

export interface WebhookDeliveryLog {
  id: string
  attemptedAt: string
  eventType: string
  targetUrl: string
  success: boolean
  httpStatusCode: number | null
  durationMs: number
  errorMessage: string | null
  requestPayloadSummary: string | null
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface WebhookTestResult {
  fired: boolean
  eventType: string
  configured: boolean
  message: string
}

export async function getWebhookStatus(): Promise<WebhookStatus> {
  return apiGet<WebhookStatus>('/api/webhooks/status')
}

export async function getWebhookDeliveries(page = 1, pageSize = 20): Promise<PagedResult<WebhookDeliveryLog>> {
  return apiGet<PagedResult<WebhookDeliveryLog>>(`/api/webhooks/deliveries?page=${page}&pageSize=${pageSize}`)
}

export async function fireWebhookTest(eventType: 'speed_alert' | 'geofence_breach' = 'speed_alert'): Promise<WebhookTestResult> {
  return apiPost<WebhookTestResult>('/api/webhooks/test', { eventType })
}
