import { useEffect, useMemo, useRef } from 'react'
import { subscribeLiveEvents, type LiveEventType } from '../api/sseClient'

type RefreshOptions = {
  eventTypes?: LiveEventType[]
  debounceMs?: number
  enabled?: boolean
}

export function useLiveDataRefresh(load: () => void | Promise<void>, options: RefreshOptions = {}): void {
  const loadRef = useRef(load)
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const inFlightRef = useRef(false)
  const pendingRef = useRef(false)
  const eventTypes = options.eventTypes ?? ['data_changed']
  const debounceMs = options.debounceMs ?? 750
  const enabled = options.enabled ?? true
  const eventKey = eventTypes.join('|')
  const eventSet = useMemo(() => new Set(eventTypes), [eventKey])

  loadRef.current = load

  useEffect(() => {
    if (!enabled) return undefined

    const run = () => {
      if (timerRef.current !== null) {
        clearTimeout(timerRef.current)
      }

      timerRef.current = setTimeout(() => {
        timerRef.current = null
        if (inFlightRef.current) {
          pendingRef.current = true
          return
        }

        inFlightRef.current = true
        Promise.resolve(loadRef.current()).finally(() => {
          inFlightRef.current = false
          if (pendingRef.current) {
            pendingRef.current = false
            run()
          }
        })
      }, debounceMs)
    }

    const unsubscribe = subscribeLiveEvents((type) => {
      if (eventSet.has(type)) run()
    })

    return () => {
      unsubscribe()
      if (timerRef.current !== null) {
        clearTimeout(timerRef.current)
        timerRef.current = null
      }
    }
  }, [debounceMs, enabled, eventSet])
}
