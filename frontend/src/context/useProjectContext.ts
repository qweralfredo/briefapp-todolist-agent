import { useContext } from 'react'
import { ProjectContext } from './projectContextObject'

export function useProjectContext() {
  const context = useContext(ProjectContext)
  if (!context) {
    throw new Error('useProjectContext must be used within ProjectProvider')
  }

  return context
}
