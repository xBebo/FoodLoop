import { AlertCircle, Eye, EyeOff } from 'lucide-react'
import { useState, type InputHTMLAttributes, type ReactNode } from 'react'
import { cn } from '../../lib/cn'
import './ui.css'

type FrameProps = {
  id: string
  label: string
  hint?: string
  error?: string
  className?: string
  /** Receives the aria-describedby value for the control. */
  children: (describedBy: string | undefined) => ReactNode
}

/** Label + hint + error around any control (input, select, textarea), wired through aria-describedby. */
export function FieldFrame({ id, label, hint, error, className, children }: FrameProps) {
  const describedBy = [hint && `${id}-hint`, error && `${id}-error`].filter(Boolean).join(' ') || undefined
  return (
    <div className={cn('field', error && 'has-error', className)}>
      <label htmlFor={id} className="field__label">
        {label}
      </label>
      <div className="field__control">{children(describedBy)}</div>
      {hint && (
        <p id={`${id}-hint`} className="field__hint">
          {hint}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} className="field__error">
          <AlertCircle aria-hidden="true" />
          {error}
        </p>
      )}
    </div>
  )
}

type FieldProps = InputHTMLAttributes<HTMLInputElement> & {
  id: string
  label: string
  hint?: string
  error?: string
}

/** Labelled input. Password inputs get a show/hide toggle. */
export function Field({ id, label, hint, error, className, type = 'text', ...input }: FieldProps) {
  const [reveal, setReveal] = useState(false)
  const isPassword = type === 'password'

  return (
    <FieldFrame id={id} label={label} hint={hint} error={error} className={className}>
      {(describedBy) => (
        <>
          <input
            id={id}
            name={id}
            type={isPassword && reveal ? 'text' : type}
            className="field__input"
            aria-invalid={error ? true : undefined}
            aria-describedby={describedBy}
            {...input}
          />
          {isPassword && (
            <button
              type="button"
              className="field__toggle"
              aria-label={reveal ? 'Hide password' : 'Show password'}
              aria-pressed={reveal}
              aria-controls={id}
              onClick={() => setReveal((r) => !r)}
            >
              {reveal ? <EyeOff aria-hidden="true" /> : <Eye aria-hidden="true" />}
            </button>
          )}
        </>
      )}
    </FieldFrame>
  )
}
