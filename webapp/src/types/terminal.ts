export interface TerminalState {
  cols: number
  rows: number
  output: string
  cursorX: number
  cursorY: number
}

export interface TerminalEmitterEvent {
  type: 'data' | 'resize' | 'close' | 'error'
  data?: string
  cols?: number
  rows?: number
  error?: string
}

export interface LocalTerminalState {
  isRunning: boolean
  processId?: number
  cwd: string
  shell: string
}

export type TerminalType = 'ssh' | 'local'
