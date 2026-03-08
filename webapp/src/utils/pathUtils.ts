// Browser-safe path utilities (no Node.js dependencies)

export function normalizePath(filePath: string): string {
  return filePath.replace(/\\/g, '/')
}

export function toAbsolutePath(filePath: string): string {
  if (filePath.startsWith('~')) {
    return filePath // Let the backend resolve ~ paths
  }
  return filePath
}

export function toHomePath(filePath: string): string {
  return filePath
}

export function isUNCPath(filePath: string): boolean {
  return /^\\\\[^\\]+\\[^\\]+/.test(filePath)
}
