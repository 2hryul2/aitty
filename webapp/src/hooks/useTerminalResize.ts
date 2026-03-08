import { useEffect, useRef } from 'react'

export interface TerminalSize {
  cols: number
  rows: number
  width: number
  height: number
}

const DEFAULT_CHAR_WIDTH = 8
const DEFAULT_CHAR_HEIGHT = 16

export function useTerminalResize(onResize?: (size: TerminalSize) => void) {
  const containerRef = useRef<HTMLDivElement>(null)
  const resizeObserverRef = useRef<ResizeObserver | null>(null)

  useEffect(() => {
    if (!containerRef.current) return

    // Initial calculation
    const updateSize = () => {
      if (!containerRef.current) return

      const { width, height } = containerRef.current.getBoundingClientRect()

      // Calculate columns and rows based on character size
      const cols = Math.floor(width / DEFAULT_CHAR_WIDTH)
      const rows = Math.floor(height / DEFAULT_CHAR_HEIGHT)

      if (cols > 0 && rows > 0) {
        onResize?.({
          cols,
          rows,
          width,
          height,
        })
      }
    }

    // Set up ResizeObserver for dynamic resizing
    resizeObserverRef.current = new ResizeObserver(() => {
      updateSize()
    })

    resizeObserverRef.current.observe(containerRef.current)
    updateSize() // Initial call

    // Handle window resize as fallback
    const handleWindowResize = () => updateSize()
    window.addEventListener('resize', handleWindowResize)

    return () => {
      resizeObserverRef.current?.disconnect()
      window.removeEventListener('resize', handleWindowResize)
    }
  }, [onResize])

  return containerRef
}
