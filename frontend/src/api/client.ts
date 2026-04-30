import { getRuntimeApiKey } from './config'

const apiBaseUrl =
  (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() || ''

function authHeaders(): Record<string, string> {
  const key = getRuntimeApiKey()
  return key ? { 'X-Api-Key': key } : {}
}

export async function apiGet<T>(path: string): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: { ...authHeaders() },
  })
  if (!response.ok) {
    throw new Error(`GET ${path} failed with ${response.status}`)
  }
  return (await response.json()) as T
}

export async function apiPost<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify(body),
  })
  if (!response.ok) {
    throw new Error(`POST ${path} failed with ${response.status}`)
  }
  return (await response.json()) as T
}

export async function apiPut<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify(body),
  })
  if (!response.ok) {
    throw new Error(`PUT ${path} failed with ${response.status}`)
  }
  return (await response.json()) as T
}

export async function apiDelete(path: string): Promise<void> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'DELETE',
    headers: { ...authHeaders() },
  })
  if (!response.ok) {
    throw new Error(`DELETE ${path} failed with ${response.status}`)
  }
}
