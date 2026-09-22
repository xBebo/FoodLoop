import { useCallback, useSyncExternalStore } from 'react'

/** Desktop with a real mouse: the only place pointer-driven parallax runs. */
export const PARALLAX_QUERY = '(hover: hover) and (pointer: fine) and (min-width: 1024px)'

export function useMediaQuery(query: string) {
  const subscribe = useCallback(
    (onChange: () => void) => {
      const mq = window.matchMedia(query)
      mq.addEventListener('change', onChange)
      return () => mq.removeEventListener('change', onChange)
    },
    [query],
  )
  return useSyncExternalStore(subscribe, () => window.matchMedia(query).matches)
}
