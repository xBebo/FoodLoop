import { useEffect, useState } from 'react'

/**
 * Loads `load` whenever `key` changes (or `reload()` is called); the previous request is aborted.
 * `data` keeps the last successful result while the next one loads, so lists don't flash empty.
 * A 401 needs no handling here: the API client ends the session and the route guard sends the visitor to Login.
 */
export function useLoad<T>(key: string, load: (signal: AbortSignal) => Promise<T>) {
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<{ id: string; data?: T; error?: unknown }>()
  const id = `${attempt}:${key}`

  useEffect(() => {
    const controller = new AbortController()
    load(controller.signal).then(
      (data) => setState({ id, data }),
      // Keep stale data only when retrying the same request. If the key changed (for example,
      // a new filter), showing the previous result under the new controls would be misleading.
      (error) => !controller.signal.aborted && setState((s) => ({ id, data: s?.id === id ? s.data : undefined, error })),
    )
    return () => controller.abort()
    // `id` identifies the request; `load` is recreated every render by design.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id])

  const current = state?.id === id
  return {
    loading: !current,
    data: state?.data,
    /** True when `data` belongs to the current key, not a previous one. */
    fresh: current && state?.error === undefined,
    error: current ? state?.error : undefined,
    reload: () => setAttempt((n) => n + 1),
  }
}
