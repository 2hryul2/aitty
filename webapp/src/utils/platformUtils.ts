/**
 * Check if Windows has ConPTY support (Windows 10+ with Node.js 6+)
 */
export function isWindowsWithConPTY(): boolean {
  if (process.platform !== 'win32') return false

  const [major] = process.versions.node.split('.').map(Number)

  // node-pty supports ConPTY on Node 6.0+
  return major >= 6
}

/**
 * Get shell for current platform
 */
export function getShellForPlatform(): string {
  if (process.platform === 'win32') {
    // Prefer PowerShell if available, fallback to cmd.exe
    return process.env.PSModulePath ? 'powershell.exe' : 'cmd.exe'
  }
  return process.env.SHELL || '/bin/bash'
}

/**
 * Check if running on Windows
 */
export function isWindows(): boolean {
  return process.platform === 'win32'
}

/**
 * Check if running on macOS
 */
export function isDarwin(): boolean {
  return process.platform === 'darwin'
}

/**
 * Check if running on Linux
 */
export function isLinux(): boolean {
  return process.platform === 'linux'
}

/**
 * Get platform display name
 */
export function getPlatformName(): string {
  const platform = process.platform
  const names: Record<string, string> = {
    win32: 'Windows',
    darwin: 'macOS',
    linux: 'Linux',
  }
  return names[platform] || 'Unknown'
}
