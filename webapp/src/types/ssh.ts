export interface SSHConnection {
  host: string
  port: number
  username: string
  privateKey?: string
  password?: string
  passphrase?: string
}

export interface SSHConnectionState {
  isConnected: boolean
  isConnecting: boolean
  error?: string
  connection?: SSHConnection
  connectionTime?: Date
}

export interface SSHCommand {
  id: string
  command: string
  timestamp: Date
  output: string
  status: 'pending' | 'running' | 'completed' | 'error'
}
