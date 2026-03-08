import { SSHConnection } from './ssh'

export interface AppConfig {
  theme: 'light' | 'dark'
  fontSize: number
  fontFamily: string
  sshConnections: SSHConnection[]
  lastConnection?: string
}

export interface ConfigState {
  config: AppConfig
  isLoading: boolean
  isSaving: boolean
  error?: string
}
