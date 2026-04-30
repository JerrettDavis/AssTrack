import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { Device } from './assets'

export type { Device }

export type DeviceListItem = Device

export type DeviceSummary = {
  id: string
  identifier: string
  label?: string | null
  assetId?: string | null
  assetName?: string | null
  speedThresholdKmh?: number | null
  lastSeenAt?: string | null
  lastLatitude?: number | null
  lastLongitude?: number | null
  latestSpeedKmh?: number | null
  latestHeadingDegrees?: number | null
  unacknowledgedSpeedAlerts: number
  unacknowledgedGeofenceBreaches: number
}

export function getDevices(): Promise<Device[]> {
  return apiGet<Device[]>('/api/devices')
}

export async function getDeviceSummary(deviceId: string): Promise<DeviceSummary> {
  return apiGet<DeviceSummary>(`/api/devices/${deviceId}/summary`)
}

export async function createDevice(data: {
  identifier: string
  label?: string
  protocol?: string
  assetId?: string
}): Promise<Device> {
  return apiPost<Device>('/api/devices', data)
}

export type UpdateDeviceRequest = {
  identifier: string
  label?: string | null
  protocol?: string | null
  assetId?: string | null
}

export async function updateDevice(id: string, data: UpdateDeviceRequest): Promise<Device> {
  return apiPut<Device>(`/api/devices/${id}`, data)
}

export async function deleteDevice(id: string): Promise<void> {
  await apiDelete(`/api/devices/${id}`)
}
