export enum LogLevel {
  DEBUG = 'DEBUG',
  INFO = 'INFO',
  WARN = 'WARN',
  ERROR = 'ERROR',
}

class Logger {
  private write(level: LogLevel, message: string, data?: unknown): void {
    const timestamp = new Date().toISOString()
    const consoleMethod = level === LogLevel.ERROR
      ? console.error
      : level === LogLevel.WARN
        ? console.warn
        : console.log
    consoleMethod(`[${timestamp}] [${level}] ${message}`, data ?? '')
  }

  debug(message: string, data?: unknown): void {
    this.write(LogLevel.DEBUG, message, data)
  }

  info(message: string, data?: unknown): void {
    this.write(LogLevel.INFO, message, data)
  }

  warn(message: string, data?: unknown): void {
    this.write(LogLevel.WARN, message, data)
  }

  error(message: string, data?: unknown): void {
    this.write(LogLevel.ERROR, message, data)
  }
}

export const logger = new Logger()
