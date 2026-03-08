import { useState, useCallback, useRef } from 'react'
import { LocalTerminalState } from '@types/terminal'
import { logger } from '@utils/logger'

export function useLocalTerminal() {
  const [state, setState] = useState<LocalTerminalState>({
    isRunning: false,
    cwd: process.env.HOME || '/home/user',
    shell: process.platform === 'win32' ? 'cmd.exe' : '/bin/bash',
  })

  const outputRef = useRef<string>('')
  const commandHistoryRef = useRef<string[]>([])

  const startTerminal = useCallback(() => {
    try {
      setState(prev => ({
        ...prev,
        isRunning: true,
      }))

      logger.info('Local terminal started', {
        shell: state.shell,
        cwd: state.cwd,
      })
    } catch (error) {
      logger.error('Failed to start terminal', { error })
      setState(prev => ({
        ...prev,
        isRunning: false,
      }))
      throw error
    }
  }, [state.shell, state.cwd])

  const stopTerminal = useCallback(() => {
    try {
      setState(prev => ({
        ...prev,
        isRunning: false,
      }))

      logger.info('Local terminal stopped')
    } catch (error) {
      logger.error('Failed to stop terminal', { error })
    }
  }, [])

  const writeCommand = useCallback((command: string) => {
    try {
      commandHistoryRef.current.push(command)
      outputRef.current += `> ${command}\n`

      logger.debug('Command written to terminal', {
        command: command.substring(0, 100),
      })

      return true
    } catch (error) {
      logger.error('Failed to write command', { error })
      return false
    }
  }, [])

  const getOutput = useCallback(() => {
    return outputRef.current
  }, [])

  const clearOutput = useCallback(() => {
    outputRef.current = ''
  }, [])

  const getHistory = useCallback(() => {
    return [...commandHistoryRef.current]
  }, [])

  const changeCwd = useCallback((path: string) => {
    setState(prev => ({
      ...prev,
      cwd: path,
    }))
  }, [])

  return {
    state,
    startTerminal,
    stopTerminal,
    writeCommand,
    getOutput,
    clearOutput,
    getHistory,
    changeCwd,
  }
}
