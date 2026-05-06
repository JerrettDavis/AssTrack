import { apiDelete, apiGet, apiPost, apiPut } from './client'

export interface Geofence {
  id: string
  name: string
  description?: string | null
  shapeType: 'circle' | 'polygon' | string
  centerLatitude: number
  centerLongitude: number
  radiusMeters: number
  polygonCoordinates?: GeofencePoint[] | null
  isActive: boolean
  isSeeded: boolean
  createdAt: string
}

export interface GeofencePoint {
  latitude: number
  longitude: number
}

export interface CreateGeofenceRequest {
  name: string
  description?: string
  shapeType?: 'circle' | 'polygon'
  centerLatitude: number
  centerLongitude: number
  radiusMeters: number
  polygonCoordinates?: GeofencePoint[]
  isActive?: boolean
}

export interface UpdateGeofenceRequest {
  name: string
  description?: string | null
  shapeType?: 'circle' | 'polygon'
  centerLatitude: number
  centerLongitude: number
  radiusMeters: number
  polygonCoordinates?: GeofencePoint[] | null
  isActive?: boolean
}

export interface GeofenceBreach {
  id: string
  observationId: string
  geofenceId: string
  geofenceName: string
  deviceId: string
  deviceIdentifier?: string | null
  assetName?: string | null
  assetId?: string | null
  eventType: string
  detectedAt: string
  acknowledgedAtUtc?: string | null
  acknowledgedBy?: string | null
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

export async function getGeofenceBreaches(params: { deviceId?: string; geofenceId?: string; limit?: number; unacknowledged?: boolean } = {}): Promise<GeofenceBreach[]> {
  const query = new URLSearchParams()
  if (params.deviceId) query.set('deviceId', params.deviceId)
  if (params.geofenceId) query.set('geofenceId', params.geofenceId)
  if (params.limit) query.set('limit', String(params.limit))
  if (params.unacknowledged != null) query.set('unacknowledged', String(params.unacknowledged))
  return apiGet<GeofenceBreach[]>(`/api/geofences/breaches?${query}`)
}
