import type { AllowListEntry, BacklogItem, BatchIngestJob, BatchStats, BoxApiKey, BoxLog, BoxUsageSummary, BoxUser, ContextChunkFile, ContextSearchResponse, Dashboard, KnowledgeResponse, MemoryItem, Project, Sprint } from '../types'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8480'

let currentAuthToken: string | null = null
export function setAuthToken(token: string | null) {
  currentAuthToken = token
}

let currentApiKey: string | null = null
export function setApiKey(key: string | null) {
  currentApiKey = key
}

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options?.headers as Record<string, string> ?? {}),
  }
  if (currentAuthToken) {
    headers['Authorization'] = `Bearer ${currentAuthToken}`
  } else if (currentApiKey) {
    headers['X-Briefapp-Api-Key'] = currentApiKey
  } else {
    const storedKey = localStorage.getItem('briefappApiKey')
    if (storedKey) {
      headers['X-Briefapp-Api-Key'] = storedKey
    }
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...options,
    headers,
  })

  if (!response.ok) {
    const text = await response.text()
    throw new Error(text || `Request failed: ${response.status}`)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}

export const apiClient = {
  listProjects: () => request<Project[]>('/api/projects'),
  createProject: (payload: { name: string; description: string }) =>
    request<Project>('/api/projects', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  getDashboard: (projectId: string) => request<Dashboard>(`/api/projects/${projectId}/dashboard`),
  getBacklog: (projectId: string) => request<BacklogItem[]>(`/api/projects/${projectId}/backlog`),
  createBacklogItem: (
    projectId: string,
    payload: { title: string; description: string; storyPoints: number; priority: number; commitIds?: string[] },
  ) =>
    request<BacklogItem>(`/api/projects/${projectId}/backlog`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  getSprints: (projectId: string) => request<Sprint[]>(`/api/projects/${projectId}/sprints`),
  createSprint: (
    projectId: string,
    payload: { name: string; goal: string; startDate: string; endDate: string; backlogItemIds: string[]; commitIds?: string[] },
  ) =>
    request(`/api/projects/${projectId}/sprints`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  updateWorkItemStatus: (payload: {
    workItemId: string
    status: number
    assignee: string
    branch?: string
    agentName?: string
    modelUsed?: string
    ideUsed?: string
    tokensUsed?: number
    feedback?: string
    metadataJson?: string
    commitIds?: string[]
  }) =>
    request(`/api/work-items/${payload.workItemId}/status`, {
      method: 'POST',
      body: JSON.stringify({
        status: payload.status,
        assignee: payload.assignee,
        branch: payload.branch ?? '',
        agentName: payload.agentName ?? '',
        modelUsed: payload.modelUsed ?? '',
        ideUsed: payload.ideUsed ?? '',
        tokensUsed: payload.tokensUsed ?? 0,
        feedback: payload.feedback ?? '',
        metadataJson: payload.metadataJson ?? '',
        commitIds: payload.commitIds ?? [],
      }),
    }),
  updateSprintCommitIds: (sprintId: string, payload: { commitIds: string[] }) =>
    request(`/api/sprints/${sprintId}/commits`, {
      method: 'PATCH',
      body: JSON.stringify(payload),
    }),
  getKnowledge: (projectId: string) => request<KnowledgeResponse>(`/api/projects/${projectId}/knowledge`),
  createWikiPage: (
    projectId: string,
    payload: { title: string; contentMarkdown: string; category: string; tags: string },
  ) =>
    request(`/api/projects/${projectId}/wiki`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  createCheckpoint: (
    projectId: string,
    payload: {
      name: string
      category: string
      contextSnapshot: string
      decisions: string
      risks: string
      nextActions: string
    },
  ) =>
    request(`/api/projects/${projectId}/checkpoints`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  createDocumentation: (
    projectId: string,
    payload: { title: string; contentMarkdown: string; category: string; tags: string },
  ) =>
    request(`/api/projects/${projectId}/documentation`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  updateProjectConfig: (
    projectId: string,
    payload: { gitHubUrl?: string; localPath?: string; techStack?: string; mainBranch?: string; adoEnabled?: boolean; adoOrganization?: string; adoProject?: string; adoPat?: string },
  ) =>
    request<Project>(`/api/projects/${projectId}/config`, {
      method: 'PATCH',
      body: JSON.stringify(payload),
    }),
  addSubTask: (
    parentWorkItemId: string,
    payload: { title: string; description: string; assignee?: string; branch?: string; tags?: string },
  ) =>
    request(`/api/work-items/${parentWorkItemId}/sub-tasks`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  updateBacklogContext: (
    backlogItemId: string,
    payload: { tags?: string; wikiRefs?: string; constraints?: string; commitIds?: string[] },
  ) =>
    request(`/api/backlog-items/${backlogItemId}/context`, {
      method: 'PATCH',
      body: JSON.stringify(payload),
    }),

  // Box Users (v3)
  listBoxUsers: (boxId: string) => request<BoxUser[]>(`/api/boxes/${boxId}/users`),
  addBoxUser: (boxId: string, payload: { email: string; role?: string; groups?: string }) =>
    request<BoxUser>(`/api/boxes/${boxId}/users`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  updateBoxUser: (boxId: string, userId: string, payload: { role?: string; groups?: string }) =>
    request<BoxUser>(`/api/boxes/${boxId}/users/${userId}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  deleteBoxUser: (boxId: string, userId: string) =>
    request(`/api/boxes/${boxId}/users/${userId}`, { method: 'DELETE' }),

  // Memory-Box (v3)
  listMemory: (boxId: string, tag?: string) =>
    request<MemoryItem[]>(`/api/boxes/${boxId}/memory${tag ? `?tag=${encodeURIComponent(tag)}` : ''}`),
  getMemory: (boxId: string, key: string) =>
    request<MemoryItem>(`/api/boxes/${boxId}/memory/${encodeURIComponent(key)}`),
  upsertMemory: (boxId: string, payload: { key: string; value: string; tags?: string }) =>
    request<MemoryItem>(`/api/boxes/${boxId}/memory`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  deleteMemory: (boxId: string, key: string) =>
    request(`/api/boxes/${boxId}/memory/${encodeURIComponent(key)}`, { method: 'DELETE' }),

  // Box Logs (v3)
  listLogs: (boxId: string, opts?: { level?: string; source?: string; limit?: number }) => {
    const params = new URLSearchParams()
    if (opts?.level) params.set('level', opts.level)
    if (opts?.source) params.set('source', opts.source)
    if (opts?.limit) params.set('limit', String(opts.limit))
    const qs = params.toString()
    return request<BoxLog[]>(`/api/boxes/${boxId}/logs${qs ? `?${qs}` : ''}`)
  },
  createLog: (boxId: string, payload: { level?: string; source?: string; message: string; details?: string }) =>
    request<BoxLog>(`/api/boxes/${boxId}/logs`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  // Box API Keys (v3)
  listApiKeys: (boxId: string) => request<BoxApiKey[]>(`/api/boxes/${boxId}/api-keys`),
  createApiKey: (boxId: string, payload: { name: string; scopes?: string }) =>
    request<BoxApiKey>(`/api/boxes/${boxId}/api-keys`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  revokeApiKey: (boxId: string, keyId: string) =>
    request(`/api/boxes/${boxId}/api-keys/${keyId}`, { method: 'DELETE' }),

  // Allow-List (v3)
  listAllowList: (boxId: string) => request<AllowListEntry[]>(`/api/boxes/${boxId}/allow-list`),
  upsertAllowList: (boxId: string, payload: { appName: string; callbackUrl?: string; scopes?: string }) =>
    request<AllowListEntry>(`/api/boxes/${boxId}/allow-list`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  toggleAllowList: (boxId: string, id: string) =>
    request(`/api/boxes/${boxId}/allow-list/${id}/toggle`, { method: 'PATCH' }),
  deleteAllowList: (boxId: string, id: string) =>
    request(`/api/boxes/${boxId}/allow-list/${id}`, { method: 'DELETE' }),

  // Usage Module (v3)
  getUsageSummary: (boxId: string) => request<BoxUsageSummary>(`/api/boxes/${boxId}/usage`),

  // Context-Box RAG (v3) — talks to Python FastAPI on port 8482
  contextListFiles: () =>
    request<ContextChunkFile[]>(`/api/context/files`),
  contextDeleteFile: (filePath: string) =>
    request(`/api/context/files/${encodeURIComponent(filePath)}`, { method: 'DELETE' }),
  contextSearch: (query: string, limit = 10, fileType?: string) =>
    request<ContextSearchResponse>(`/api/context/query`, {
      method: 'POST',
      body: JSON.stringify({ query, limit, ...(fileType ? { file_type: fileType } : {}) }),
    }),
  contextBatchIngest: (files: File[]) => {
    const form = new FormData()
    files.forEach((f) => form.append('files', f))
    return fetch(`${apiBaseUrl}/api/context/ingest/batch`, { method: 'POST', body: form }).then(async (res) => {
      if (!res.ok) throw new Error(await res.text())
      return res.json() as Promise<{ status: string; jobs_queued: number; jobs: BatchIngestJob[] }>
    })
  },
  contextGetBatchJobs: (status?: string) =>
    request<{ jobs: BatchIngestJob[] }>(`/api/context/ingest/jobs${status ? `?status=${status}` : ''}`),
  contextGetBatchJob: (jobId: string) =>
    request<BatchIngestJob>(`/api/context/ingest/jobs/${jobId}`),
  contextBatchStats: () =>
    request<BatchStats>(`/api/context/ingest/stats`),

  // Agent Planner
  agentExecutePlan: (projectId: string, planPayload: Record<string, any>, complexityMultiplier: number = 1.0) => {
    return fetch(`http://localhost:8483/api/agent/execute`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ project_id: projectId, plan_payload: planPayload, complexity_multiplier: complexityMultiplier })
    }).then(r => r.json())
  },

  // Azure DevOps
  syncAzureDevOps: (projectId?: string) => request<{ synced: number; failed: number; backlogsSynced: number; backlogsFailed: number; sprintsSynced: number; sprintsFailed: number; tasksSynced: number; tasksFailed: number; knowledgeSynced: number; knowledgeFailed: number; errorMessage?: string }>(`/api/azuredevops/sync${projectId ? `?projectId=${projectId}` : ''}`, { method: 'POST' }),
  clearAzureDevOpsBoard: (projectId: string) => request(`/api/projects/${projectId}/ado/clear`, { method: 'DELETE' }),
  testAzureDevOpsConnection: (payload: { organization: string; project: string; pat: string }) =>
    request<{ success: boolean }>('/api/azuredevops/test', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
}

