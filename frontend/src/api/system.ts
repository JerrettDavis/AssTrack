import { apiGet, apiPost } from './client'

export interface SystemStatus {
  environment: string
  simulationEnabled: boolean
  webhookConfigured: boolean
  apiKeyConfigured: boolean
  adminApiKeyConfigured: boolean
  ingestApiKeyConfigured: boolean
  accessTier: string
  swaggerEnabled: boolean
  rateLimitPermitLimit: number
  rateLimitWindowSeconds: number
  databaseProvider: string
  hasData: boolean
}

export interface SeedResult {
  alreadySeeded: boolean
  resetPerformed: boolean
  assetsCreated: number
  devicesCreated: number
  geofencesCreated: number
}

export interface EnterpriseRetentionCleanupResult {
  matchingAuditEvents: number
  deletedAuditEvents: number
  matchingResolvedIntegrationEvents: number
  deletedResolvedIntegrationEvents: number
  matchingWebhookDeliveries: number
  deletedWebhookDeliveries: number
  auditRetentionDays: number
  signalRetentionDays: number
  webhookRetentionDays: number
  dryRun: boolean
}

export function getSystemStatus(): Promise<SystemStatus> {
  return apiGet<SystemStatus>('/api/system/status')
}

export function seedDemoData(reset = false): Promise<SeedResult> {
  return apiPost<SeedResult>('/api/system/seed', { reset })
}

export function applyEnterpriseRetention(options: {
  auditDays: number
  signalDays: number
  webhookDays: number
  dryRun: boolean
}): Promise<EnterpriseRetentionCleanupResult> {
  const query = new URLSearchParams()
  query.set('auditDays', String(options.auditDays))
  query.set('signalDays', String(options.signalDays))
  query.set('webhookDays', String(options.webhookDays))
  query.set('dryRun', String(options.dryRun))
  return apiPost<EnterpriseRetentionCleanupResult>(`/api/system/maintenance/apply-retention?${query}`, {})
}
