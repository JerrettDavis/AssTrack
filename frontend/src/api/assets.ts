import { apiDelete, apiGet, apiPost, apiPut } from './client'

export type Device = {
  id: string
  identifier: string
  label?: string | null
  protocol: string
  provider: string
  externalId?: string | null
  tags?: string | null
  integrationFeedId?: string | null
  integrationFeedName?: string | null
  providerLabel?: string | null
  providerLongName?: string | null
  providerShortName?: string | null
  providerHardwareModel?: string | null
  providerRole?: string | null
  providerProfileJson?: string | null
  providerProfileUpdatedAt?: string | null
  isSeeded: boolean
  createdAt: string
  assetId?: string | null
  assetName?: string | null
}

export type Asset = {
  id: string
  name: string
  description?: string | null
  assetClass: string
  category?: string | null
  criticality: string
  custodyStatus: string
  custodianName?: string | null
  custodianContact?: string | null
  custodySince?: string | null
  createdAt: string
  updatedAt: string
  devices: Device[]
  latestSensorReadings: SensorReading[]
  speedThresholdKmh?: number | null
  isSeeded: boolean
}

export type SensorReading = {
  id: string
  assetId?: string | null
  assetName?: string | null
  deviceId?: string | null
  deviceIdentifier?: string | null
  integrationFeedId?: string | null
  sensorType: string
  name?: string | null
  numericValue?: number | null
  textValue?: string | null
  unit?: string | null
  observedAt: string
  receivedAt: string
  metadata?: string | null
}

export type AssetClass = {
  id: string
  name: string
  description: string
  recommendedSensors: string[]
}

export function getAssets() {
  return apiGet<Asset[]>('/api/assets')
}

export function getAssetClasses() {
  return apiGet<AssetClass[]>('/api/assets/classes')
}

export async function createAsset(data: { name: string; description?: string; category?: string; speedThresholdKmh?: number | null; assetClass?: string | null; criticality?: string | null }): Promise<Asset> {
  return apiPost<Asset>('/api/assets', data)
}

export type UpdateAssetRequest = {
  name: string
  description?: string | null
  assetClass?: string | null
  category?: string | null
  criticality?: string | null
  custodyStatus?: string | null
  custodianName?: string | null
  custodianContact?: string | null
  speedThresholdKmh?: number | null
}

export async function updateAsset(id: string, data: UpdateAssetRequest): Promise<Asset> {
  return apiPut<Asset>(`/api/assets/${id}`, data)
}

export async function deleteAsset(id: string): Promise<void> {
  await apiDelete(`/api/assets/${id}`)
}
