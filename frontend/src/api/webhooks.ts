import { apiDelete, apiGet, apiPost } from './client'
import { PagedResult } from './types'

export interface WebhookStatus {
  configured: boolean
  last24hDeliveries: number
  last24hFailures: number
  lastDeliveredAt: string | null
  avgDurationMs: number | null
  retryQueueDepth?: number
  signingEnabled?: boolean
  enabledSubscriptions?: number
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
  attemptNumber?: number
  correlationId?: string | null
}

export interface WebhookTestResult {
  fired: boolean
  eventType: string
  configured: boolean
  message: string
}

export interface WebhookReplayResult {
  replayed: boolean
  sourceDeliveryId: number
  eventType: string
  targetUrl: string
  message: string
}

export interface WebhookSubscriptionTestResult {
  fired: boolean
  subscriptionId: string
  eventType: string
  targetUrl: string
  message: string
}

export interface WebhookSubscription {
  id: string
  name: string
  isEnabled: boolean
  eventTypes: string
  targetUrl: string
  signingEnabled: boolean
  createdAt: string
  updatedAt: string
  lastAttemptedAt: string | null
  lastSuccessAt: string | null
  lastFailureAt: string | null
  lastHttpStatusCode: number | null
  lastErrorMessage: string | null
  last24hDeliveries: number
  last24hFailures: number
  health: string
}

export interface WebhookSubscriptionRequest {
  name: string
  isEnabled: boolean
  eventTypes: string
  targetUrl: string
  signingSecret?: string | null
}

export async function getWebhookStatus(): Promise<WebhookStatus> {
  return apiGet<WebhookStatus>('/api/webhooks/status')
}

export async function getWebhookDeliveries(page = 1, pageSize = 20): Promise<PagedResult<WebhookDeliveryLog>> {
  return apiGet<PagedResult<WebhookDeliveryLog>>(`/api/webhooks/deliveries?page=${page}&pageSize=${pageSize}`)
}

export type WebhookTestEventType = 'speed_alert' | 'geofence_breach' | 'enterprise_signal'

export async function fireWebhookTest(eventType: WebhookTestEventType = 'speed_alert'): Promise<WebhookTestResult> {
  return apiPost<WebhookTestResult>('/api/webhooks/test', { eventType })
}

export function getWebhookSubscriptions(): Promise<WebhookSubscription[]> {
  return apiGet<WebhookSubscription[]>('/api/webhooks/subscriptions')
}

export function createWebhookSubscription(request: WebhookSubscriptionRequest): Promise<WebhookSubscription> {
  return apiPost<WebhookSubscription>('/api/webhooks/subscriptions', request)
}

export function testWebhookSubscription(id: string): Promise<WebhookSubscriptionTestResult> {
  return apiPost<WebhookSubscriptionTestResult>(`/api/webhooks/subscriptions/${id}/test`, {})
}

export async function deleteWebhookSubscription(id: string): Promise<void> {
  await apiDelete(`/api/webhooks/subscriptions/${id}`)
}

export function replayWebhookDelivery(id: string | number): Promise<WebhookReplayResult> {
  return apiPost<WebhookReplayResult>(`/api/webhooks/deliveries/${id}/replay`, {})
}
