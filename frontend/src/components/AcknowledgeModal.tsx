import { useState, useEffect } from 'react'

export interface AcknowledgeModalProps {
  open: boolean
  title: string
  onConfirm: (acknowledgedBy: string | undefined) => void
  onCancel: () => void
}

export default function AcknowledgeModal({ open, title, onConfirm, onCancel }: AcknowledgeModalProps) {
  const [name, setName] = useState('')

  useEffect(() => {
    if (open) {
      setName('')
    }
  }, [open])

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
      style={{
        position: 'fixed',
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.5)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1000,
      }}
      onClick={handleBackdropClick}
    >
      <div
        style={{
          background: 'rgba(15, 23, 42, 0.97)',
          border: '1px solid rgba(148, 163, 184, 0.2)',
          borderRadius: '16px',
          padding: '2rem',
          minWidth: '360px',
          maxWidth: '480px',
          width: '90%',
          boxShadow: '0 10px 30px rgba(0, 0, 0, 0.5)',
        }}
      >
        <h2 style={{ marginTop: 0, marginBottom: '1rem', color: '#e5eef7' }}>{title}</h2>
        <div style={{ marginBottom: '1.5rem' }}>
          <label htmlFor="ack-name" style={{ display: 'block', marginBottom: '0.5rem', color: '#cbd5e1', fontSize: '0.9rem' }}>
            Your name (optional):
          </label>
          <input
            id="ack-name"
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={handleKeyDown}
            autoFocus
            style={{
              width: '100%',
              padding: '0.75rem 0.9rem',
              border: '1px solid rgba(148, 163, 184, 0.25)',
              borderRadius: '10px',
              background: 'rgba(15, 23, 42, 0.9)',
              color: '#e5eef7',
              boxSizing: 'border-box',
              fontSize: '1rem',
            }}
          />
        </div>
        <div style={{ display: 'flex', gap: '0.5rem', justifyContent: 'flex-end' }}>
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
