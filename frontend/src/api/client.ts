import { getRuntimeApiKey } from './config'

const apiBaseUrl =
  (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() || ''

function authHeaders(): Record<string, string> {
  const key = getRuntimeApiKey()
  return key ? { 'X-Api-Key': key } : {}
}

async function extractErrorMessage(response: Response, method: string, path: string): Promise<string> {
  const status = response.status
  const contentType = response.headers.get('content-type')
  
  if (contentType?.includes('application/problem+json') || contentType?.includes('application/json')) {
    return response.json().then(json => {
      if (typeof json === 'object' && json !== null) {
        if ('detail' in json && typeof json.detail === 'string') {
          return json.detail
        }
        if ('title' in json && typeof json.title === 'string') {
          return json.title
        }
      }
      return `${method} ${path} failed with ${status}`
    }).catch(() => `${method} ${path} failed with ${status}`)
  }
  return `${method} ${path} failed with ${status}`
}

export async function apiGet<T>(path: string): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: { ...authHeaders() },
  })
  if (!response.ok) {
    const message = await extractErrorMessage(response, 'GET', path)
    throw new Error(message)
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
    const message = await extractErrorMessage(response, 'POST', path)
    throw new Error(message)
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
    const message = await extractErrorMessage(response, 'PUT', path)
    throw new Error(message)
  }
  return (await response.json()) as T
}

export async function apiDelete(path: string): Promise<void> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'DELETE',
    headers: { ...authHeaders() },
  })
  if (!response.ok) {
    const message = await extractErrorMessage(response, 'DELETE', path)
    throw new Error(message)
  }
}
