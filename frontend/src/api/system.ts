import { apiGet, apiPost } from './client'

export interface SystemStatus {
  environment: string
  simulationEnabled: boolean
  webhookConfigured: boolean
  apiKeyConfigured: boolean
  ingestApiKeyConfigured: boolean
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

export function getSystemStatus(): Promise<SystemStatus> {
  return apiGet<SystemStatus>('/api/system/status')
}

export function seedDemoData(reset = false): Promise<SeedResult> {
  return apiPost<SeedResult>('/api/system/seed', { reset })
}
