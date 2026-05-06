// Runtime config loaded from /config.json before app mounts.
// Do not use import.meta.env for secrets — those are build-time only.

type RuntimeConfig = {
  apiKey: string
}

let _config: RuntimeConfig = { apiKey: '' }

export async function loadRuntimeConfig(): Promise<void> {
  try {
    const res = await fetch('/config.json')
    if (!res.ok) {
      console.warn(`Failed to load /config.json: ${res.status}`)
      loadDevFallback()
      return
    }
    const data = (await res.json()) as Partial<RuntimeConfig>
    _config = { apiKey: data.apiKey?.trim() ?? '' }
    if (!_config.apiKey) loadDevFallback()
  } catch (err) {
    console.warn('Could not load /config.json, falling back to empty config:', err)
    loadDevFallback()
  }
}

export function getRuntimeApiKey(): string {
  return _config.apiKey
}

function loadDevFallback(): void {
  if (!import.meta.env.DEV) return
  const devApiKey = (import.meta.env.VITE_DEV_API_KEY as string | undefined)?.trim()
  if (devApiKey) _config = { apiKey: devApiKey }
}
