import { useState, type FormEvent } from 'react'
import { ApiError } from '../../lib/api/client'

/** Field messages keyed by input name; `form` is the message for the whole form. */
export type Errors = Record<string, string>
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

// Browser checks for fast feedback only; the server stays authoritative.
export const rules = {
  required: (v: string, msg: string) => (v.trim() ? '' : msg),
  email: (v: string) => (!v.trim() ? 'Enter your email address.' : EMAIL.test(v.trim()) ? '' : 'Enter a valid email, like name@organization.org.'),
  /** Mirrors the ASP.NET Core Identity options: 10+ characters, upper, lower, digit and a non-alphanumeric symbol. */
  password: (v: string) =>
    !v
      ? 'Enter a password.'
      : v.length < 10 || !/[A-Z]/.test(v) || !/[a-z]/.test(v) || !/[0-9]/.test(v) || !/[^A-Za-z0-9]/.test(v)
        ? 'Use at least 10 characters, with upper- and lowercase letters, a number and a symbol.'
        : '',
}

/** Copy for API failures, chosen from status and stable code only; server text is never shown. */
export function apiErrors(error: unknown, byStatus: Record<number, string> = {}): Errors {
  if (!(error instanceof ApiError)) return { form: 'Something went wrong. Please try again.' }
  if (error.code === 'antiforgery.invalid') return { form: 'Your session changed in the meantime. Please try again.' }
  if (byStatus[error.status]) return { form: byStatus[error.status] }
  if (error.status === 0) return { form: 'We couldn’t reach FoodLoop. Check your connection and try again.' }
  if (error.status === 400) {
    const fields = Object.fromEntries(Object.entries(error.errors).map(([k, v]) => [k, v[0]]))
    return Object.keys(fields).length ? fields : { form: 'Check your details and try again.' }
  }
  return { form: 'Something went wrong on our side. Please try again in a moment.' }
}

/**
 * Validate in the browser, then submit once (no automatic retry). `submit` resolves with server errors to
 * show, or nothing on success. The first invalid field, if any, receives focus.
 */
export function useAuthForm(validate: (data: FormData) => Errors, submit: (data: FormData) => Promise<Errors | void>) {
  const [errors, setErrors] = useState<Errors>({})
  const [submitting, setSubmitting] = useState(false)

  function show(form: HTMLFormElement, found: Errors) {
    const shown = Object.fromEntries(Object.entries(found).filter(([, msg]) => msg))
    setErrors(shown)
    const first = Object.keys(shown).find((k) => k !== 'form')
    if (first) form.querySelector<HTMLElement>(`[name="${first}"]`)?.focus()
    return Object.keys(shown).length > 0
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (submitting) return
    const form = e.currentTarget
    const data = new FormData(form)
    if (show(form, validate(data))) return
    setSubmitting(true)
    try {
      show(form, (await submit(data)) ?? {})
    } finally {
      setSubmitting(false)
    }
  }

  return { errors, submitting, onSubmit }
}
