import fs from 'fs/promises'
import path from 'path'
import { getSSHKeysDir } from '@utils/pathUtils'
import { logger } from '@utils/logger'

export class KeyManager {
  private keyDir: string

  constructor() {
    this.keyDir = getSSHKeysDir()
  }

  /**
   * Find all SSH keys in standard locations
   */
  async findKeys(): Promise<string[]> {
    const keys: string[] = []
    const commonNames = ['id_rsa', 'id_ed25519', 'id_ecdsa', 'id_dsa']

    for (const name of commonNames) {
      const keyPath = path.join(this.keyDir, name)
      try {
        await fs.access(keyPath)
        keys.push(keyPath)
        logger.debug('SSH key found', { name, path: keyPath })
      } catch {
        // Key not found, continue
      }
    }

    return keys
  }

  /**
   * Check if key file exists
   */
  async keyExists(keyPath: string): Promise<boolean> {
    try {
      await fs.access(keyPath)
      return true
    } catch {
      return false
    }
  }

  /**
   * Read SSH config file (~/.ssh/config)
   */
  async readSSHConfig(): Promise<Record<string, Record<string, string>>> {
    const configPath = path.join(this.keyDir, 'config')
    try {
      const content = await fs.readFile(configPath, 'utf-8')
      return this.parseSSHConfig(content)
    } catch (error) {
      logger.warn('SSH config not found or unreadable', { path: configPath })
      return {}
    }
  }

  /**
   * Parse SSH config file format
   */
  private parseSSHConfig(content: string): Record<string, Record<string, string>> {
    const config: Record<string, Record<string, string>> = {}
    let currentHost = ''

    for (const line of content.split('\n')) {
      const trimmed = line.trim()

      // Skip empty lines and comments
      if (!trimmed || trimmed.startsWith('#')) continue

      // Parse Host line
      if (trimmed.toLowerCase().startsWith('host ')) {
        currentHost = trimmed.substring(5).trim()
        config[currentHost] = {}
      } else if (currentHost) {
        // Parse key-value pairs
        const [key, ...valueParts] = trimmed.split(/\s+/)
        const value = valueParts.join(' ')
        if (key && value) {
          config[currentHost][key.toLowerCase()] = value
        }
      }
    }

    logger.debug('SSH config parsed', { hosts: Object.keys(config).length })
    return config
  }

  /**
   * Get key file size and permissions
   */
  async getKeyInfo(keyPath: string): Promise<{ size: number; isReadable: boolean } | null> {
    try {
      const stats = await fs.stat(keyPath)
      return {
        size: stats.size,
        isReadable: true,
      }
    } catch (error) {
      logger.warn('Failed to get key info', { path: keyPath, error })
      return null
    }
  }

  /**
   * Validate key file (basic checks)
   */
  async isValidKeyFile(keyPath: string): Promise<boolean> {
    try {
      const content = await fs.readFile(keyPath, 'utf-8')

      // Check for OpenSSH private key header
      const isOpenSSH = content.includes('BEGIN OPENSSH PRIVATE KEY')
      const isRSA = content.includes('BEGIN RSA PRIVATE KEY')
      const isEC = content.includes('BEGIN EC PRIVATE KEY')
      const isEd25519 = content.includes('BEGIN PRIVATE KEY')

      return isOpenSSH || isRSA || isEC || isEd25519
    } catch (error) {
      logger.warn('Failed to validate key file', { path: keyPath, error })
      return false
    }
  }

  /**
   * Get SSH keys directory
   */
  getKeyDirectory(): string {
    return this.keyDir
  }
}

// Export singleton instance
export const keyManager = new KeyManager()
