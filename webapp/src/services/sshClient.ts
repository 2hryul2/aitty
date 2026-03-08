import { NodeSSH } from 'node-ssh'
import path from 'path'
import { SSHConnection, SSHConnectionState } from '@types/ssh'
import { toAbsolutePath } from '@utils/pathUtils'
import { logger } from '@utils/logger'

export class SSHClient {
  private ssh: NodeSSH
  private state: SSHConnectionState = {
    isConnected: false,
    isConnecting: false,
  }

  constructor() {
    this.ssh = new NodeSSH()
  }

  /**
   * Connect to SSH server
   */
  async connect(connection: SSHConnection): Promise<void> {
    try {
      this.state.isConnecting = true
      this.state.error = undefined

      // Resolve private key path
      const privateKey = connection.privateKey
        ? toAbsolutePath(connection.privateKey)
        : undefined

      // Log connection attempt (without sensitive data)
      logger.info('SSH connection attempt', {
        host: connection.host,
        port: connection.port,
        username: connection.username,
        useKey: !!privateKey,
      })

      // Connect with node-ssh
      const connectConfig: any = {
        host: connection.host,
        port: connection.port,
        username: connection.username,
        readyTimeout: 30000, // 30 second timeout
      }

      if (privateKey) {
        connectConfig.privateKey = privateKey
        if (connection.passphrase) {
          connectConfig.passphrase = connection.passphrase
        }
      } else if (connection.password) {
        connectConfig.password = connection.password
      }

      await this.ssh.connect(connectConfig)

      this.state.isConnected = true
      this.state.isConnecting = false
      this.state.connection = connection
      this.state.connectionTime = new Date()

      logger.info('SSH connection successful', {
        host: connection.host,
      })
    } catch (error) {
      this.state.isConnected = false
      this.state.isConnecting = false
      this.state.error = error instanceof Error ? error.message : 'Unknown error'

      logger.error('SSH connection failed', {
        host: connection.host,
        error: this.state.error,
      })

      throw error
    }
  }

  /**
   * Disconnect from SSH server
   */
  async disconnect(): Promise<void> {
    try {
      if (this.ssh.isConnected()) {
        await this.ssh.dispose()
      }
      this.state.isConnected = false
      logger.info('SSH disconnected')
    } catch (error) {
      logger.error('SSH disconnection error', { error })
    }
  }

  /**
   * Execute command on remote server
   */
  async exec(command: string): Promise<string> {
    if (!this.state.isConnected) {
      throw new Error('SSH not connected')
    }

    try {
      logger.debug('Executing SSH command', { command: command.substring(0, 100) })

      const result = await this.ssh.exec(command)

      logger.debug('Command executed', {
        command: command.substring(0, 100),
        outputLength: result.length,
      })

      return result
    } catch (error) {
      logger.error('SSH command execution failed', {
        command: command.substring(0, 100),
        error,
      })
      throw error
    }
  }

  /**
   * Get connection state
   */
  getState(): SSHConnectionState {
    return { ...this.state }
  }

  /**
   * Check if connected
   */
  isConnected(): boolean {
    return this.state.isConnected && this.ssh.isConnected()
  }

  /**
   * Test connection with a simple command
   */
  async testConnection(): Promise<boolean> {
    try {
      const result = await this.exec('echo "SSH connection test"')
      return result.includes('connection test')
    } catch {
      return false
    }
  }

  /**
   * List files in remote directory
   */
  async listDir(remotePath: string = '.'): Promise<string[]> {
    try {
      const result = await this.exec(`ls -la ${remotePath}`)
      return result
        .split('\n')
        .filter(line => line.trim())
        .map(line => line.trim())
    } catch (error) {
      logger.error('Failed to list directory', { path: remotePath, error })
      throw error
    }
  }

  /**
   * Get current working directory
   */
  async getPWD(): Promise<string> {
    try {
      const result = await this.exec('pwd')
      return result.trim()
    } catch (error) {
      logger.error('Failed to get PWD', { error })
      throw error
    }
  }

  /**
   * Create a stream for large file transfers
   */
  getStreamConnection() {
    if (!this.isConnected()) {
      throw new Error('SSH not connected')
    }
    return this.ssh
  }
}

// Export singleton instance
export const sshClient = new SSHClient()
