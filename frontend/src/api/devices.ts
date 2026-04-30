import { apiGet } from './client'

export type DeviceListItem = {
  id: string
  identifier: string
  label?: string | null
  protocol: string
  createdAt: string
  assetId?: string | null
  assetName?: string | null
}

export function getDevices() {
  return apiGet<DeviceListItem[]>('/api/devices')
}
