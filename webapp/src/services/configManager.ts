import fs from 'fs/promises'
import path from 'path'
import { AppConfig } from '@types/config'
import { SSHConnection } from '@types/ssh'
import { getConfigDir } from '@utils/pathUtils'
import { logger } from '@utils/logger'

export class ConfigManager {
  private configDir: string
  private configPath: string
  private defaultConfig: AppConfig = {
    theme: 'dark',
    fontSize: 12,
    fontFamily: 'Consolas, "Courier New"',
    sshConnections: [],
  }

  constructor() {
    this.configDir = getConfigDir()
    this.configPath = path.join(this.configDir, 'config.json')
  }

  async init(): Promise<void> {
    try {
      await fs.mkdir(this.configDir, { recursive: true })

      // Create config file if it doesn't exist
      const exists = await this.fileExists(this.configPath)
      if (!exists) {
        await this.save(this.defaultConfig)
        logger.info('Config file created', { path: this.configPath })
      }
    } catch (error) {
      logger.error('Failed to initialize config directory', { path: this.configDir, error })
    }
  }

  async load(): Promise<AppConfig> {
    try {
      const content = await fs.readFile(this.configPath, 'utf-8')
      const config = JSON.parse(content) as AppConfig
      logger.debug('Config loaded', { path: this.configPath })
      return config
    } catch (error) {
      logger.warn('Failed to load config, using defaults', { error })
      return this.defaultConfig
    }
  }

  async save(config: AppConfig): Promise<void> {
    try {
      await fs.writeFile(this.configPath, JSON.stringify(config, null, 2), 'utf-8')
      logger.info('Config saved', { path: this.configPath })
    } catch (error) {
      logger.error('Failed to save config', { path: this.configPath, error })
      throw error
    }
  }

  async addSSHConnection(connection: SSHConnection): Promise<void> {
    const config = await this.load()
    config.sshConnections.push(connection)
    await this.save(config)
    logger.info('SSH connection added', { host: connection.host })
  }

  async removeSSHConnection(host: string): Promise<void> {
    const config = await this.load()
    config.sshConnections = config.sshConnections.filter(c => c.host !== host)
    await this.save(config)
    logger.info('SSH connection removed', { host })
  }

  async updateSSHConnection(host: string, connection: Partial<SSHConnection>): Promise<void> {
    const config = await this.load()
    const index = config.sshConnections.findIndex(c => c.host === host)
    if (index !== -1) {
      config.sshConnections[index] = { ...config.sshConnections[index], ...connection }
      await this.save(config)
      logger.info('SSH connection updated', { host })
    }
  }

  async getSSHConnection(host: string): Promise<SSHConnection | undefined> {
    const config = await this.load()
    return config.sshConnections.find(c => c.host === host)
  }

  async listSSHConnections(): Promise<SSHConnection[]> {
    const config = await this.load()
    return config.sshConnections
  }

  async updateTheme(theme: 'light' | 'dark'): Promise<void> {
    const config = await this.load()
    config.theme = theme
    await this.save(config)
    logger.info('Theme updated', { theme })
  }

  async updateFontSize(fontSize: number): Promise<void> {
    const config = await this.load()
    config.fontSize = fontSize
    await this.save(config)
    logger.info('Font size updated', { fontSize })
  }

  private async fileExists(filePath: string): Promise<boolean> {
    try {
      await fs.access(filePath)
      return true
    } catch {
      return false
    }
  }
}

// Export singleton instance
export const configManager = new ConfigManager()
