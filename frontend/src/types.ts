export type Project = {
  id: string
  name: string
  description: string
  createdAt: string
  gitHubUrl?: string
  localPath?: string
  techStack?: string
  mainBranch?: string
  adoEnabled?: boolean
  adoOrganization?: string
  adoProject?: string
  adoPat?: string
}

export type BoxUser = {
  id: string
  projectId: string
  email: string
  role: string
  groups: string
  createdAt: string
  updatedAt?: string
}

export type MemoryItem = {
  id: string
  projectId: string
  key: string
  value: string
  tags: string
  createdAt: string
  updatedAt: string
}

export type BoxLog = {
  id: string
  projectId: string
  level: string
  source: string
  message: string
  details: string
  timestamp: string
}

export type BoxApiKey = {
  id: string
  name: string
  prefix: string
  scopes: string
  createdAt: string
  expiresAt?: string
  lastUsedAt?: string
  isRevoked: boolean
  key?: string // only present on creation response
}

export type AllowListEntry = {
  id: string
  projectId: string
  appName: string
  callbackUrl: string
  scopes: string
  isActive: boolean
  createdAt: string
}

// ── Context-Box RAG Types ──────────────────────────────────────
export type ContextChunkFile = {
  file_path: string
  file_name: string
  chunks: number
}

export type ContextSearchResult = {
  chunk_id: string
  file_path: string
  content: string
  score: number | null
  metadata: string | null
}

export type ContextSearchResponse = {
  query: string
  results: ContextSearchResult[]
}

export type BatchIngestJob = {
  id: string
  file_name: string
  file_size: number
  status: 'pending' | 'processing' | 'done' | 'failed'
  chunks_total: number
  chunks_processed: number
  progress_pct: number
  error: string | null
  created_at: string
  started_at: string | null
  completed_at: string | null
  processing_time_ms: number | null
}

export type BatchStats = {
  total_jobs: number
  pending: number
  processing: number
  done: number
  failed: number
  total_chunks_processed: number
  avg_processing_time_ms: number
  workers_active: number
  batch_size: number
  queue_depth: number
}

export type BoxUsageSummary = {
  totalRuns: number
  totalTokensInput: number
  totalTokensOutput: number
  totalCostUsd: number
  successRatePct: number
  runsByModel: Record<string, number>
}

export type BacklogItem = {
  id: string
  title: string
  description: string
  storyPoints: number
  priority: number
  status: number | string
  tags?: string
  wikiRefs?: string
  constraints?: string
  commitIds?: string[]
}

export type WorkItemFeedback = {
  id: string
  agentName: string
  modelUsed: string
  ideUsed: string
  tokensUsed: number
  feedback: string
  metadataJson: string
  createdAt: string
}

export type SprintWorkItem = {
  id: string
  backlogItemId: string
  title: string
  description: string
  status: number | string
  assignee: string
  totalTokensSpent: number
  lastModelUsed: string
  lastIdeUsed: string
  createdAt: string
  updatedAt?: string
  feedbacks: WorkItemFeedback[]
  branch?: string
  tags?: string
  parentWorkItemId?: string
  commitIds?: string[]
}

export type Sprint = {
  id: string
  name: string
  goal: string
  status: number | string
  startDate: string
  endDate: string
  workItems: SprintWorkItem[]
  commitIds?: string[]
}

export type KnowledgeWikiPage = {
  id: string
  title: string
  tags: string
  category: string
  contentMarkdown: string
  updatedAt: string
}

export type KnowledgeCheckpoint = {
  id: string
  name: string
  category: string
  contextSnapshot: string
  decisions: string
  risks: string
  nextActions: string
  createdAt: string
}

export type KnowledgeDocumentation = {
  id: string
  title: string
  tags: string
  category: string
  contentMarkdown: string
  updatedAt: string
}

export type AgentRun = {
  id: string
  agentName: string
  status: string
  startedAt: string
}

export type KnowledgeResponse = {
  wikiPages: KnowledgeWikiPage[]
  checkpoints: KnowledgeCheckpoint[]
  documentationPages: KnowledgeDocumentation[]
  agentRuns: AgentRun[]
}

export type Dashboard = {
  projectId: string
  projectName: string
  backlogTotal: number
  backlogDone: number
  activeSprints: number
  workItemsTodo: number
  workItemsInProgress: number
  workItemsReview: number
  workItemsDone: number
  knowledgeCheckpoints: number
  wikiPages: number
  agentRuns: number
}

export const workItemStatusLabels: Record<number, string> = {
  0: 'To Do',
  1: 'In Progress',
  2: 'Review',
  3: 'Done',
  4: 'Blocked',
}

export const backlogStatusLabels: Record<number, string> = {
  0: 'New',
  1: 'Planned',
  2: 'In Sprint',
  3: 'Done',
  4: 'Blocked',
}

export function toNumberStatus(value: number | string): number {
  if (typeof value === 'number') return value
  if (!value || typeof value !== 'string') return Number(value)

  const str = value.trim().toLowerCase().replace(/_/g, '').replace(/-/g, '')
  if (str === 'new') return 0
  if (str === 'todo') return 0
  if (str === 'planned') return 1
  if (str === 'inprogress') return 1
  if (str === 'insprint') return 2
  if (str === 'review') return 2
  if (str === 'done') return 3
  if (str === 'blocked') return 4
  
  if (str === 'active') return 1
  if (str === 'closed') return 2

  return Number(value)
}

export function groupByCategory<T extends { category?: string }>(items: T[]): Record<string, T[]> {
  return items.reduce<Record<string, T[]>>((acc, item) => {
    const category = item.category?.trim() || 'General'
    if (!acc[category]) {
      acc[category] = []
    }

    acc[category].push(item)
    return acc
  }, {})
}
