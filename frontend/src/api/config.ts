// Runtime config loaded from /config.json before app mounts.
// Do not use import.meta.env for secrets — those are build-time only.

type RuntimeConfig = {
  apiKey: string
}

let _config: RuntimeConfig = { apiKey: '' }

export async function loadRuntimeConfig(): Promise<void> {
  if (loadDevFallback()) return

  try {
    const res = await fetch('/config.json')
    if (!res.ok) {
      console.warn(`Failed to load /config.json: ${res.status}`)
      return
    }

    const contentType = res.headers.get('content-type') ?? ''
    if (!contentType.includes('application/json')) {
      return
    }

    const data = (await res.json()) as Partial<RuntimeConfig>
    _config = { apiKey: data.apiKey?.trim() ?? '' }
  } catch (err) {
    console.warn('Could not load /config.json, falling back to empty config:', err)
  }
}

export function getRuntimeApiKey(): string {
  return _config.apiKey
}

function loadDevFallback(): boolean {
  const devApiKey = (import.meta.env.VITE_DEV_API_KEY as string | undefined)?.trim()
  if (!devApiKey) return false

  _config = { apiKey: devApiKey }
  return true
}
