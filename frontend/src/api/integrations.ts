import { apiDelete, apiGet, apiPost, apiPut } from './client'

export type IntegrationProvider = {
  id: string
  name: string
  category: string
  connectionMode: string
  supportsDirectApi: boolean
  supportsWebhookIngest: boolean
  supportsPolling: boolean
  status: string
  description: string
  setupNotes: string
  recommendedTags: string[]
}

export type IntegrationFeed = {
  id: string
  name: string
  provider: string
  providerName: string
  isEnabled: boolean
  autoCreateDevices: boolean
  defaultTags?: string | null
  configurationJson?: string | null
  createdAt: string
  updatedAt: string
  deviceCount: number
}

export type CreateIntegrationFeedRequest = {
  name: string
  provider: string
  isEnabled: boolean
  autoCreateDevices: boolean
  defaultTags?: string | null
  configurationJson?: string | null
}

export type UpdateIntegrationFeedRequest = {
  name: string
  isEnabled: boolean
  autoCreateDevices: boolean
  defaultTags?: string | null
  configurationJson?: string | null
}

export function getIntegrationProviders(): Promise<IntegrationProvider[]> {
  return apiGet<IntegrationProvider[]>('/api/integrations/providers')
}

export function getIntegrationFeeds(): Promise<IntegrationFeed[]> {
  return apiGet<IntegrationFeed[]>('/api/integrations')
}

export function createIntegrationFeed(data: CreateIntegrationFeedRequest): Promise<IntegrationFeed> {
  return apiPost<IntegrationFeed>('/api/integrations', data)
}

export function updateIntegrationFeed(id: string, data: UpdateIntegrationFeedRequest): Promise<IntegrationFeed> {
  return apiPut<IntegrationFeed>(`/api/integrations/${id}`, data)
}

export async function deleteIntegrationFeed(id: string): Promise<void> {
  await apiDelete(`/api/integrations/${id}`)
}
