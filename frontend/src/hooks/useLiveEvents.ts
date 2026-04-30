import { useEffect, useRef } from 'react'
import { subscribeLiveEvents, type LiveEventType } from '../api/sseClient'

export type LiveEventHandler = (type: LiveEventType, data: unknown) => void

export function useLiveEvents(handler: LiveEventHandler): void {
  const handlerRef = useRef(handler)
  handlerRef.current = handler

  useEffect(() => {
    return subscribeLiveEvents((type, data) => handlerRef.current(type, data))
  }, [])
}
