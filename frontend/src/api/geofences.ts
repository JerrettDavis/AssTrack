import { apiDelete, apiGet, apiPost, apiPut } from './client'

export interface Geofence {
  id: string
  name: string
  description?: string | null
  centerLatitude: number
  centerLongitude: number
  radiusMeters: number
  isActive: boolean
  createdAt: string
}

export interface CreateGeofenceRequest {
  name: string
  description?: string
  centerLatitude: number
  centerLongitude: number
  radiusMeters: number
  isActive?: boolean
}

export interface UpdateGeofenceRequest {
  name: string
  description?: string | null
  centerLatitude: number
  centerLongitude: number
  radiusMeters: number
  isActive?: boolean
}

export async function getGeofences(): Promise<Geofence[]> {
  return apiGet<Geofence[]>('/api/geofences')
}

export async function createGeofence(data: CreateGeofenceRequest): Promise<Geofence> {
  return apiPost<Geofence>('/api/geofences', data)
}

export async function updateGeofence(id: string, data: UpdateGeofenceRequest): Promise<Geofence> {
  return apiPut<Geofence>(`/api/geofences/${id}`, data)
}

export async function deleteGeofence(id: string): Promise<void> {
  await apiDelete(`/api/geofences/${id}`)
}
