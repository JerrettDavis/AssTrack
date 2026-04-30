import { apiDelete, apiGet, apiPost, apiPut } from './client'

export type Device = {
  id: string
  identifier: string
  label?: string | null
  protocol: string
  isSeeded: boolean
  createdAt: string
  assetId?: string | null
  assetName?: string | null
}

export type Asset = {
  id: string
  name: string
  description?: string | null
  category?: string | null
  createdAt: string
  updatedAt: string
  devices: Device[]
  speedThresholdKmh?: number | null
  isSeeded: boolean
}

export function getAssets() {
  return apiGet<Asset[]>('/api/assets')
}

export async function createAsset(data: { name: string; description?: string; speedThresholdKmh?: number | null }): Promise<Asset> {
  return apiPost<Asset>('/api/assets', data)
}

export type UpdateAssetRequest = {
  name: string
  description?: string | null
  category?: string | null
  speedThresholdKmh?: number | null
}

export async function updateAsset(id: string, data: UpdateAssetRequest): Promise<Asset> {
  return apiPut<Asset>(`/api/assets/${id}`, data)
}

export async function deleteAsset(id: string): Promise<void> {
  await apiDelete(`/api/assets/${id}`)
}
