import { apiDelete, apiGet, apiPost } from './client'

export type Device = {
  id: string
  identifier: string
  label?: string | null
  protocol: string
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
}

export function getAssets() {
  return apiGet<Asset[]>('/api/assets')
}

export async function createAsset(data: { name: string; description?: string }): Promise<Asset> {
  return apiPost<Asset>('/api/assets', data)
}

export async function deleteAsset(id: string): Promise<void> {
  await apiDelete(`/api/assets/${id}`)
}
