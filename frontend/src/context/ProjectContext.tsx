import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { apiClient } from '../api/client'
import type { BacklogItem, Dashboard, KnowledgeResponse, Project, Sprint } from '../types'
import { ProjectContext } from './projectContextObject'
import type { ProjectContextValue } from './projectContextObject'
import { useAuth } from './AuthContext'

export function ProjectProvider({ children }: { children: React.ReactNode }) {
  const [projects, setProjects] = useState<Project[]>([])
  const [selectedProjectId, setSelectedProjectId] = useState<string>('')
  const [dashboard, setDashboard] = useState<Dashboard | null>(null)
  const [backlog, setBacklog] = useState<BacklogItem[]>([])
  const [sprints, setSprints] = useState<Sprint[]>([])
  const [knowledge, setKnowledge] = useState<KnowledgeResponse | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const isRefreshingRef = useRef(false)

  const selectedProject = useMemo(
    () => projects.find((project) => project.id === selectedProjectId) ?? null,
    [projects, selectedProjectId],
  )

  const refreshProjectViews = useCallback(async (projectId: string, options?: { silent?: boolean }) => {
    if (!projectId) {
      return
    }

    if (options?.silent && isRefreshingRef.current) {
      return
    }

    if (!options?.silent) {
      setLoading(true)
      setError('')
    }

    isRefreshingRef.current = true
    try {
      const [dashboardResult, backlogResult, sprintResult, knowledgeResult] = await Promise.all([
        apiClient.getDashboard(projectId),
        apiClient.getBacklog(projectId),
        apiClient.getSprints(projectId),
        apiClient.getKnowledge(projectId),
      ])

      setDashboard(dashboardResult)
      setBacklog(backlogResult)
      setSprints(sprintResult)
      setKnowledge(knowledgeResult)
    } catch (requestError) {
      if (!options?.silent) {
        setError(requestError instanceof Error ? requestError.message : 'Erro ao carregar dados')
      }
    } finally {
      if (!options?.silent) {
        setLoading(false)
      }
      isRefreshingRef.current = false
    }
  }, [])

  const refreshProjects = useCallback(async () => {
    try {
      const result = await apiClient.listProjects()
      setProjects(result)
      if (!selectedProjectId && result.length > 0) {
        setSelectedProjectId(result[0].id)
      }
      if (selectedProjectId && !result.some((project) => project.id === selectedProjectId)) {
        setSelectedProjectId(result[0]?.id ?? '')
      }
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Erro ao carregar projetos')
    }
  }, [selectedProjectId])

  const createProject = useCallback(
    async (payload: { name: string; description: string }) => {
      setError('')
      try {
        await apiClient.createProject(payload)
        await refreshProjects()
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : 'Error creating project')
      }
    },
    [refreshProjects],
  )

  const updateProjectConfig = useCallback(
    async (payload: { gitHubUrl?: string; localPath?: string; techStack?: string; mainBranch?: string; adoEnabled?: boolean; adoOrganization?: string; adoProject?: string; adoPat?: string }) => {
      if (!selectedProjectId) return
      setError('')
      try {
        await apiClient.updateProjectConfig(selectedProjectId, payload)
        await refreshProjects()
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : 'Erro ao salvar configurações')
      }
    },
    [selectedProjectId, refreshProjects],
  )

  const { user } = useAuth()

  useEffect(() => {
    if (user) {
      void refreshProjects()
    }
  }, [refreshProjects, user])

  useEffect(() => {
    if (selectedProjectId) {
      void refreshProjectViews(selectedProjectId)
    }
  }, [refreshProjectViews, selectedProjectId])

  useEffect(() => {
    if (!selectedProjectId) {
      return
    }

    const refreshSilently = () => {
      void refreshProjectViews(selectedProjectId, { silent: true })
    }

    const intervalId = window.setInterval(refreshSilently, 5000)
    const handleWindowFocus = () => refreshSilently()
    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        refreshSilently()
      }
    }

    window.addEventListener('focus', handleWindowFocus)
    document.addEventListener('visibilitychange', handleVisibilityChange)

    return () => {
      window.clearInterval(intervalId)
      window.removeEventListener('focus', handleWindowFocus)
      document.removeEventListener('visibilitychange', handleVisibilityChange)
    }
  }, [refreshProjectViews, selectedProjectId])

  const value = useMemo<ProjectContextValue>(
    () => ({
      projects,
      selectedProjectId,
      selectedProject,
      dashboard,
      backlog,
      sprints,
      knowledge,
      loading,
      error,
      setSelectedProjectId,
      refreshProjects,
      refreshProjectViews,
      createProject,
      updateProjectConfig,
    }),
    [
      backlog,
      createProject,
      updateProjectConfig,
      dashboard,
      error,
      knowledge,
      loading,
      projects,
      refreshProjects,
      refreshProjectViews,
      selectedProject,
      selectedProjectId,
      sprints,
    ],
  )

  return <ProjectContext.Provider value={value}>{children}</ProjectContext.Provider>
}
