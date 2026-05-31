import { createContext } from 'react'
import type { BacklogItem, Dashboard, KnowledgeResponse, Project, Sprint } from '../types'

export type ProjectContextValue = {
  projects: Project[]
  selectedProjectId: string
  selectedProject: Project | null
  dashboard: Dashboard | null
  backlog: BacklogItem[]
  sprints: Sprint[]
  knowledge: KnowledgeResponse | null
  loading: boolean
  error: string
  setSelectedProjectId: (projectId: string) => void
  refreshProjects: () => Promise<void>
  refreshProjectViews: (projectId: string, options?: { silent?: boolean }) => Promise<void>
  createProject: (payload: { name: string; description: string }) => Promise<void>
  updateProjectConfig: (payload: { gitHubUrl?: string; localPath?: string; techStack?: string; mainBranch?: string; adoEnabled?: boolean; adoOrganization?: string; adoProject?: string; adoPat?: string }) => Promise<void>
}

export const ProjectContext = createContext<ProjectContextValue | undefined>(undefined)
