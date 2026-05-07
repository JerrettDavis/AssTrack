import { apiDelete, apiGet, apiPost, apiPut } from './client'

export type MaintenanceStatus = 'current' | 'upcoming' | 'due' | 'overdue'

export type MaintenanceSchedule = {
  id: string
  assetId: string
  assetName?: string | null
  title: string
  serviceType: string
  intervalDays?: number | null
  intervalOdometerKm?: number | null
  intervalRuntimeHours?: number | null
  diagnosticSensorType?: string | null
  diagnosticTextContains?: string | null
  lastServiceAt?: string | null
  lastOdometerKm?: number | null
  lastRuntimeHours?: number | null
  notes?: string | null
  createdAt: string
  updatedAt: string
  status: MaintenanceStatus
  nextDueAt?: string | null
  nextOdometerKm?: number | null
  nextRuntimeHours?: number | null
  latestOdometerKm?: number | null
  latestRuntimeHours?: number | null
  latestDiagnosticAt?: string | null
  latestDiagnosticValue?: string | null
}

export type MaintenanceReminder = {
  scheduleId: string
  assetId: string
  assetName?: string | null
  title: string
  serviceType: string
  status: MaintenanceStatus
  reason: string
  dueAt?: string | null
  diagnosticAt?: string | null
  diagnosticValue?: string | null
}

export type MaintenanceServiceRecord = {
  id: string
  maintenanceScheduleId: string
  assetId: string
  assetName?: string | null
  scheduleTitle: string
  serviceType: string
  completedAt: string
  odometerKm?: number | null
  runtimeHours?: number | null
  performedBy?: string | null
  cost?: number | null
  notes?: string | null
  createdAt: string
}

export type MaintenanceScheduleRequest = {
  assetId: string
  title: string
  serviceType?: string | null
  intervalDays?: number | null
  intervalOdometerKm?: number | null
  intervalRuntimeHours?: number | null
  diagnosticSensorType?: string | null
  diagnosticTextContains?: string | null
  lastServiceAt?: string | null
  lastOdometerKm?: number | null
  lastRuntimeHours?: number | null
  notes?: string | null
}

export type CompleteMaintenanceScheduleRequest = {
  completedAt?: string | null
  odometerKm?: number | null
  runtimeHours?: number | null
  performedBy?: string | null
  cost?: number | null
  notes?: string | null
}

export function getMaintenanceSchedules(assetId?: string) {
  const query = new URLSearchParams()
  if (assetId) query.append('assetId', assetId)
  return apiGet<MaintenanceSchedule[]>(`/api/maintenance/schedules?${query}`)
}

export function getMaintenanceServiceRecords(params: { assetId?: string; scheduleId?: string; limit?: number } = {}) {
  const query = new URLSearchParams()
  if (params.assetId) query.append('assetId', params.assetId)
  if (params.scheduleId) query.append('scheduleId', params.scheduleId)
  if (params.limit) query.append('limit', params.limit.toString())
  return apiGet<MaintenanceServiceRecord[]>(`/api/maintenance/records?${query}`)
}

export function getMaintenanceReminders(assetId?: string) {
  const query = new URLSearchParams()
  if (assetId) query.append('assetId', assetId)
  return apiGet<MaintenanceReminder[]>(`/api/maintenance/reminders?${query}`)
}

export function createMaintenanceSchedule(data: MaintenanceScheduleRequest) {
  return apiPost<MaintenanceSchedule>('/api/maintenance/schedules', data)
}

export function updateMaintenanceSchedule(id: string, data: MaintenanceScheduleRequest) {
  return apiPut<MaintenanceSchedule>(`/api/maintenance/schedules/${id}`, data)
}

export function completeMaintenanceSchedule(id: string, data: CompleteMaintenanceScheduleRequest) {
  return apiPost<MaintenanceServiceRecord>(`/api/maintenance/schedules/${id}/complete`, data)
}

export async function deleteMaintenanceSchedule(id: string): Promise<void> {
  await apiDelete(`/api/maintenance/schedules/${id}`)
}
