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

export type DeviceListItem = Device

export function getDevices() {
  return apiGet<DeviceListItem[]>('/api/devices')
}

export async function createDevice(data: {
  identifier: string
  label?: string
  protocol?: string
  assetId?: string
}): Promise<Device> {
  return apiPost<Device>('/api/devices', data)
}

export async function deleteDevice(id: string): Promise<void> {
  await apiDelete(`/api/devices/${id}`)
}
