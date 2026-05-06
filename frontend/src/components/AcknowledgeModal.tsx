import { useState, useEffect, useId } from 'react'

export interface AcknowledgeModalProps {
  open: boolean
  title: string
  onConfirm: (acknowledgedBy: string | undefined) => void
  onCancel: () => void
}

export default function AcknowledgeModal({ open, title, onConfirm, onCancel }: AcknowledgeModalProps) {
  const [name, setName] = useState('')
  const titleId = useId()
  const inputId = useId()

  useEffect(() => {
    if (open) {
      setName('')
      const handleEscape = (event: KeyboardEvent) => {
        if (event.key === 'Escape') {
          onCancel()
        }
      }
      window.addEventListener('keydown', handleEscape)
      return () => window.removeEventListener('keydown', handleEscape)
    }
  }, [onCancel, open])

  const handleConfirm = () => {
    const trimmedName = name.trim()
    onConfirm(trimmedName || undefined)
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleConfirm()
    } else if (e.key === 'Escape') {
      onCancel()
    }
  }

  const handleBackdropClick = (e: React.MouseEvent<HTMLDivElement>) => {
    if (e.target === e.currentTarget) {
      onCancel()
    }
  }

  if (!open) return null

  return (
    <div
      className="modal-backdrop"
      onClick={handleBackdropClick}
    >
      <div aria-labelledby={titleId} aria-modal="true" className="modal-panel" role="dialog">
        <h2 id={titleId}>{title}</h2>
        <div className="modal-field">
          <label htmlFor={inputId}>
            Your name (optional)
          </label>
          <input
            id={inputId}
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={handleKeyDown}
            autoFocus
          />
        </div>
        <div className="modal-actions">
          <button
            onClick={onCancel}
            type="button"
            className="button button-secondary"
          >
            Cancel
          </button>
          <button
            onClick={handleConfirm}
            type="button"
            className="button button-primary"
          >
            Confirm
          </button>
        </div>
      </div>
    </div>
  )
}
