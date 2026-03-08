import { useEffect, useState, useCallback } from 'react'
import { config as configBridge } from '@bridge/ipcBridge'
import { logger } from '@utils/logger'
import { SSHTerminal } from '@components/SSHTerminal'
import { AITerminal } from '@components/AITerminal'
import type { SSHConnection } from '@types/ssh'
import './App.css'

const DEFAULT_CONFIG = {
  theme: 'dark',
  fontSize: 12,
  fontFamily: 'Consolas, "Courier New"',
  sshConnections: [] as any[],
}

function App() {
  const [config, setConfig] = useState<any>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [sshConnection, setSshConnection] = useState<SSHConnection | undefined>()
  const [sshConnected, setSshConnected] = useState(false)

  useEffect(() => {
    const initializeApp = async () => {
      try {
        const loaded = await configBridge.load().catch(() => null)
        setConfig(loaded ?? DEFAULT_CONFIG)
        logger.info('App initialized', { source: loaded ? 'ipc' : 'default' })
      } catch (error) {
        logger.error('Failed to initialize app', { error })
        setConfig(DEFAULT_CONFIG)
      } finally {
        setIsLoading(false)
      }
    }

    initializeApp()
  }, [])

  const handleSshConnect = useCallback((conn: SSHConnection) => {
    setSshConnection(conn)
  }, [])

  const handleConnected = useCallback(() => {
    setSshConnected(true)
  }, [])

  const handleDisconnected = useCallback(() => {
    setSshConnected(false)
  }, [])

  if (isLoading) {
    return (
      <div className="app loading">
        <h1>SSH AI Terminal</h1>
        <p>Initializing...</p>
      </div>
    )
  }

  return (
    <div className="app">
      <header className="app-header">
        <h1>Aitty v0.1.0</h1>
        <p className="subtitle">SSH + AI Terminal for Windows</p>
      </header>

      <div className="app-layout">
        <div className="terminal-panel ssh-panel">
          <SSHTerminal
            connection={sshConnection}
            onRequestConnect={handleSshConnect}
            onConnect={handleConnected}
            onDisconnect={handleDisconnected}
          />
        </div>

        <div className="terminal-panel ai-panel">
          <AITerminal />
        </div>
      </div>

      <footer className="app-footer">
        <p>© 2026 Aitty | WPF + WebView2 | Claude API</p>
      </footer>
    </div>
  )
}

export default App
