import { apiGet } from './client'

export interface SystemStatus {
  environment: string
  simulationEnabled: boolean
  webhookConfigured: boolean
  apiKeyConfigured: boolean
  swaggerEnabled: boolean
  rateLimitPermitLimit: number
  rateLimitWindowSeconds: number
  databaseProvider: string
}

export function getSystemStatus(): Promise<SystemStatus> {
  return apiGet<SystemStatus>('/api/system/status')
}
