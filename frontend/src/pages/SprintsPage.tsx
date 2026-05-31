import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  Chip,
  Collapse,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  Grid,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import GraphModal from '../components/GraphModal'
import { useMemo, useRef, useState } from 'react'
import type { DragEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { apiClient } from '../api/client'
import { useProjectContext } from '../context/useProjectContext'
import { backlogStatusLabels, toNumberStatus, workItemStatusLabels } from '../types'

function parseCommitIds(value: string): string[] {
  return value
    .split(/[,\n]/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function stringifyCommitIds(commitIds?: string[]): string {
  return (commitIds ?? []).join(', ')
}

export function SprintsPage() {
  const { selectedProjectId, selectedProject, backlog, sprints, refreshProjectViews } = useProjectContext()

  const [searchParams, setSearchParams] = useSearchParams()
  const backlogIdFilter = searchParams.get('backlogId') ?? ''

  const [newSprintName, setNewSprintName] = useState('')
  const [newSprintGoal, setNewSprintGoal] = useState('')
  const [newSprintStartDate, setNewSprintStartDate] = useState('')
  const [newSprintEndDate, setNewSprintEndDate] = useState('')
  const [selectedBacklogIds, setSelectedBacklogIds] = useState<string[]>([])
  const [taskDraftStatus, setTaskDraftStatus] = useState<Record<string, number>>({})
  const [taskDraftAssignee, setTaskDraftAssignee] = useState<Record<string, string>>({})
  const [draggedWorkItemId, setDraggedWorkItemId] = useState('')
  const [dragTargetStatus, setDragTargetStatus] = useState<number | null>(null)
  const [selectedBoardSprintId, setSelectedBoardSprintId] = useState('')
  const [assigneeFilter, setAssigneeFilter] = useState('all')
  const [priorityFilter, setPriorityFilter] = useState('all')
  const [statusFilter, setStatusFilter] = useState('all')
  const [isSprintModalOpen, setSprintModalOpen] = useState(false)
  const [editingWorkItemId, setEditingWorkItemId] = useState('')
  const [editingWorkItemStatus, setEditingWorkItemStatus] = useState(0)
  const [editingWorkItemAssignee, setEditingWorkItemAssignee] = useState('')
  const [editingAgentName, setEditingAgentName] = useState('')
  const [editingModelUsed, setEditingModelUsed] = useState('')
  const [editingIdeUsed, setEditingIdeUsed] = useState('')
  const [editingTokensUsed, setEditingTokensUsed] = useState(0)
  const [editingFeedback, setEditingFeedback] = useState('')
  const [editingMetadataJson, setEditingMetadataJson] = useState('')
  const [editingBranch, setEditingBranch] = useState('')
  const [editingCommitIds, setEditingCommitIds] = useState('')
  const [expandedFeedbackIds, setExpandedFeedbackIds] = useState<Set<string>>(new Set())
  const [viewMode, setViewMode] = useState<'grouped' | 'race'>('grouped')
  const [isSyncingAdo, setIsSyncingAdo] = useState(false)
  const [isClearingAdo, setIsClearingAdo] = useState(false)
  const [graphModal, setGraphModal] = useState<{ id: string; title: string } | null>(null)

  function toggleFeedbacks(workItemId: string) {
    setExpandedFeedbackIds((prev) => {
      const next = new Set(prev)
      if (next.has(workItemId)) {
        next.delete(workItemId)
      } else {
        next.add(workItemId)
      }
      return next
    })
  }
  const draggedWorkItemRef = useRef('')

  const fallbackBoardSprintId = backlogIdFilter
    ? 'all'
    : (sprints.find((sprint) => toNumberStatus(sprint.status) === 1)?.id ?? sprints[0]?.id ?? '')
  const boardSprintId = selectedBoardSprintId || fallbackBoardSprintId

  const boardSprint = useMemo(
    () =>
      sprints.find((sprint) => sprint.id === boardSprintId) ??
      sprints.find((sprint) => toNumberStatus(sprint.status) === 1) ??
      sprints[0] ??
      null,
    [boardSprintId, sprints],
  )

  const workItemPriorityByTitle = useMemo(
    () =>
      backlog.reduce<Record<string, number>>((acc, item) => {
        acc[item.title.trim().toLowerCase()] = item.priority
        return acc
      }, {}),
    [backlog],
  )

  const workItemPriorityByBacklogId = useMemo(
    () =>
      backlog.reduce<Record<string, number>>((acc, item) => {
        acc[item.id] = item.priority
        return acc
      }, {}),
    [backlog],
  )

  function getWorkItemPriority(item: { backlogItemId: string; title: string }) {
    return workItemPriorityByBacklogId[item.backlogItemId] ?? workItemPriorityByTitle[item.title.trim().toLowerCase()]
  }

  function getActivityTimestamp(item: { updatedAt?: string; createdAt: string }) {
    const timestamp = new Date(item.updatedAt ?? item.createdAt).getTime()
    return Number.isFinite(timestamp) ? timestamp : 0
  }

  const boardItems = useMemo(
    () => (boardSprintId === 'all' ? sprints.flatMap((s) => s.workItems) : (boardSprint?.workItems ?? [])),
    [boardSprintId, boardSprint, sprints],
  )

  const raceItems = useMemo(() => {
    const backlogById = new Map(backlog.map((item) => [item.id, item]))
    return sprints
      .flatMap((sprint) =>
        sprint.workItems.map((item) => ({
          ...item,
          sprintId: sprint.id,
          sprintName: sprint.name,
          backlogTitle: backlogById.get(item.backlogItemId)?.title ?? 'Unlinked backlog',
        })),
      )
      .sort((a, b) => {
        const byDateTimeDesc = getActivityTimestamp(b) - getActivityTimestamp(a)
        if (byDateTimeDesc !== 0) {
          return byDateTimeDesc
        }
        return b.id.localeCompare(a.id)
      })
  }, [backlog, sprints])

  const itemsForFilters = useMemo(
    () => (!backlogIdFilter && viewMode === 'race' ? raceItems : boardItems),
    [backlogIdFilter, boardItems, raceItems, viewMode],
  )

  const availableAssignees = useMemo(() => {
    const assignees = new Set<string>()
    for (const item of itemsForFilters) {
      const currentAssignee = (taskDraftAssignee[item.id] ?? item.assignee ?? '').trim()
      if (currentAssignee) {
        assignees.add(currentAssignee)
      }
    }
    return Array.from(assignees).sort((a, b) => a.localeCompare(b))
  }, [itemsForFilters, taskDraftAssignee])

  const availablePriorities = useMemo(() => {
    const priorities = new Set<number>()
    for (const item of itemsForFilters) {
      const priority = getWorkItemPriority(item)
      if (typeof priority === 'number') {
        priorities.add(priority)
      }
    }
    return Array.from(priorities).sort((a, b) => a - b)
  }, [itemsForFilters])

  const sprintBoard = useMemo(() => {
    const columns: Record<number, typeof boardItems> = { 0: [], 1: [], 2: [], 3: [] }
    for (const item of boardItems) {
      const status = toNumberStatus(item.status)
      const assignee = (taskDraftAssignee[item.id] ?? item.assignee ?? '').trim()
      const priority = getWorkItemPriority(item)

      const matchesAssignee = assigneeFilter === 'all' || assignee === assigneeFilter
      const matchesPriority =
        priorityFilter === 'all' ||
        (typeof priority === 'number' && String(priority) === priorityFilter)
      const matchesBacklog = !backlogIdFilter || item.backlogItemId === backlogIdFilter

      if (columns[status] && matchesAssignee && matchesPriority && matchesBacklog) {
        columns[status].push(item)
      }
    }
    return columns
  }, [assigneeFilter, backlogIdFilter, boardItems, priorityFilter, taskDraftAssignee])

  const raceList = useMemo(() => {
    if (backlogIdFilter || viewMode !== 'race') {
      return []
    }

    return raceItems.filter((item) => {
      const assignee = (taskDraftAssignee[item.id] ?? item.assignee ?? '').trim()
      const priority = getWorkItemPriority(item)
      const status = String(toNumberStatus(taskDraftStatus[item.id] ?? item.status))
      const matchesAssignee = assigneeFilter === 'all' || assignee === assigneeFilter
      const matchesPriority =
        priorityFilter === 'all' ||
        (typeof priority === 'number' && String(priority) === priorityFilter)
      const matchesStatus = statusFilter === 'all' || status === statusFilter
      return matchesAssignee && matchesPriority && matchesStatus
    })
  }, [assigneeFilter, backlogIdFilter, priorityFilter, raceItems, statusFilter, taskDraftAssignee, taskDraftStatus, viewMode])

  // For the grouped view: organize sprints by backlog item when no filter is active
  const sprintsGroupedByBacklog = useMemo(() => {
    if (backlogIdFilter) return null
    const groups = new Map<string, { backlogItem: (typeof backlog)[0]; sprintIds: Set<string> }>()
    for (const sprint of sprints) {
      for (const wi of sprint.workItems) {
        if (!wi.backlogItemId) continue
        if (!groups.has(wi.backlogItemId)) {
          const backlogItem = backlog.find((b) => b.id === wi.backlogItemId)
          if (!backlogItem) continue
          groups.set(wi.backlogItemId, { backlogItem, sprintIds: new Set() })
        }
        groups.get(wi.backlogItemId)!.sprintIds.add(sprint.id)
      }
    }
    return Array.from(groups.values()).sort((a, b) => a.backlogItem.priority - b.backlogItem.priority)
  }, [backlog, backlogIdFilter, sprints])

  function toggleBacklogSelection(backlogItemId: string) {
    setSelectedBacklogIds((previous) =>
      previous.includes(backlogItemId)
        ? previous.filter((id) => id !== backlogItemId)
        : [...previous, backlogItemId],
    )
  }

  function handleTaskDragStart(event: DragEvent<HTMLDivElement>, workItemId: string) {
    draggedWorkItemRef.current = workItemId
    event.dataTransfer.setData('text/work-item-id', workItemId)
    event.dataTransfer.effectAllowed = 'move'
    setDraggedWorkItemId(workItemId)
  }

  function handleTaskDragEnd() {
    draggedWorkItemRef.current = ''
    setDraggedWorkItemId('')
    setDragTargetStatus(null)
  }

  function getDroppedWorkItemId(event: DragEvent<HTMLElement>): string {
    const payload = event.dataTransfer.getData('text/work-item-id')
    return payload || draggedWorkItemRef.current
  }

  async function handleTaskDrop(targetStatus: number, workItemId: string) {
    if (!workItemId || !selectedProjectId) {
      setDragTargetStatus(null)
      return
    }

    const task = boardItems.find((item) => item.id === workItemId)
    if (!task) {
      setDragTargetStatus(null)
      return
    }

    setTaskDraftStatus((prev) => ({ ...prev, [workItemId]: targetStatus }))
    await apiClient.updateWorkItemStatus({
      workItemId,
      status: targetStatus,
      assignee: (taskDraftAssignee[workItemId] ?? task.assignee ?? '').trim(),
    })

    handleTaskDragEnd()
    await refreshProjectViews(selectedProjectId)
  }

  function handleOpenTaskModal(item: { id: string; status: number | string; assignee: string; branch?: string; feedbacks?: Array<{ agentName: string; modelUsed: string; ideUsed: string; tokensUsed: number; feedback: string; metadataJson: string }>; commitIds?: string[] }) {
    const latestFeedback = item.feedbacks?.[0]
    setEditingWorkItemId(item.id)
    setEditingWorkItemStatus(taskDraftStatus[item.id] ?? toNumberStatus(item.status))
    setEditingWorkItemAssignee(taskDraftAssignee[item.id] ?? item.assignee ?? '')
    setEditingAgentName(latestFeedback?.agentName ?? '')
    setEditingModelUsed(latestFeedback?.modelUsed ?? '')
    setEditingIdeUsed(latestFeedback?.ideUsed ?? '')
    setEditingTokensUsed(latestFeedback?.tokensUsed ?? 0)
    setEditingFeedback(latestFeedback?.feedback ?? '')
    setEditingMetadataJson(latestFeedback?.metadataJson ?? '')
    setEditingBranch(item.branch ?? '')
    setEditingCommitIds(stringifyCommitIds(item.commitIds))
  }

  async function handleSaveTaskFromModal() {
    if (!editingWorkItemId || !selectedProjectId) {
      return
    }

    await apiClient.updateWorkItemStatus({
      workItemId: editingWorkItemId,
      status: editingWorkItemStatus,
      assignee: editingWorkItemAssignee.trim(),
      branch: editingBranch.trim(),
      agentName: editingAgentName.trim(),
      modelUsed: editingModelUsed.trim(),
      ideUsed: editingIdeUsed.trim(),
      tokensUsed: editingTokensUsed,
      feedback: editingFeedback.trim(),
      metadataJson: editingMetadataJson.trim(),
      commitIds: parseCommitIds(editingCommitIds),
    })

    setTaskDraftStatus((prev) => ({ ...prev, [editingWorkItemId]: editingWorkItemStatus }))
    setTaskDraftAssignee((prev) => ({ ...prev, [editingWorkItemId]: editingWorkItemAssignee }))
    setEditingWorkItemId('')
    await refreshProjectViews(selectedProjectId)
  }

  async function handleCreateSprint(event: React.FormEvent) {
    event.preventDefault()
    if (!selectedProjectId || !newSprintName.trim() || !newSprintGoal.trim()) {
      return
    }

    await apiClient.createSprint(selectedProjectId, {
      name: newSprintName,
      goal: newSprintGoal,
      startDate: newSprintStartDate || new Date().toISOString().slice(0, 10),
      endDate: newSprintEndDate || new Date(Date.now() + 14 * 86400000).toISOString().slice(0, 10),
      backlogItemIds: selectedBacklogIds,
    })

    setNewSprintName('')
    setNewSprintGoal('')
    setNewSprintStartDate('')
    setNewSprintEndDate('')
    setSelectedBacklogIds([])
    setSprintModalOpen(false)
    await refreshProjectViews(selectedProjectId)
  }

  async function handleAdoSync() {
    setIsSyncingAdo(true)
    try {
      if (!selectedProjectId) return;
      const result = await apiClient.syncAzureDevOps(selectedProjectId)
      
      let alertMsg = `Azure DevOps Sync concluído:\nSucesso: ${result.synced}\nFalha: ${result.failed}\n`;
      alertMsg += `\nDetalhes:\n`;
      alertMsg += `- Backlogs: ${result.backlogsSynced} sucesso, ${result.backlogsFailed} erro\n`;
      alertMsg += `- Sprints: ${result.sprintsSynced} sucesso, ${result.sprintsFailed} erro\n`;
      alertMsg += `- Tasks e Subtasks: ${result.tasksSynced} sucesso, ${result.tasksFailed} erro\n`;
      alertMsg += `- Wiki e Docs: ${result.knowledgeSynced} sucesso, ${result.knowledgeFailed} erro\n`;
      
      if (result.errorMessage) {
        alertMsg += `\nErro Global: ${result.errorMessage}`;
      }

      alert(alertMsg)
    } catch (err) {
      console.error('Failed to sync ADO', err)
      alert(`Falha ao sincronizar com Azure DevOps: ${err instanceof Error ? err.message : 'Erro desconhecido'}`)
    } finally {
      setIsSyncingAdo(false)
    }
  }

  async function handleClearAdoBoard() {
    if (!selectedProjectId) return;
    if (!window.confirm('Tem certeza? Isso apagará TODOS os Work Items no Azure DevOps correspondentes a este projeto!')) return;
    
    setIsClearingAdo(true)
    try {
      await apiClient.clearAzureDevOpsBoard(selectedProjectId)
      alert('Board do Azure DevOps limpo com sucesso!')
    } catch (err) {
      console.error('Failed to clear ADO board', err)
      alert(`Falha ao limpar board: ${err instanceof Error ? err.message : 'Erro desconhecido'}`)
    } finally {
      setIsClearingAdo(false)
    }
  }

  if (!selectedProject) {
    return (
      <Card variant="outlined" sx={{ borderStyle: 'dashed' }}>
        <CardContent>
          <Stack
            spacing={1.2}
            sx={{
              alignItems: "center",
              justifyContent: "center",
              py: 4
            }}>
            <Typography variant="h6">No active project</Typography>
            <Typography
              sx={{
                color: "text.secondary",
                textAlign: 'center',
                maxWidth: 520
              }}>
              Select a project to build sprints, distribute tasks on the kanban and track the flow.
            </Typography>
          </Stack>
        </CardContent>
      </Card>
    );
  }

  return (
    <Stack spacing={2}>
      <Card>
        <CardContent>
          <Stack
            direction={{ xs: 'column', md: 'row' }}
            spacing={1.2}
            sx={{
              justifyContent: "space-between",
              alignItems: { md: 'center' }
            }}>
            <Stack>
              <Typography variant="h6">Sprint Planning</Typography>
              <Typography variant="body2" sx={{
                color: "text.secondary"
              }}>
                Define objective, dates and backlog in a modal to keep focus on the board.
              </Typography>
            </Stack>
            <Stack direction="row" spacing={1.2}>
              {selectedProject.adoEnabled && (
                <>
                  <Button 
                    variant="outlined" 
                    color="error"
                    onClick={handleClearAdoBoard}
                    disabled={isClearingAdo || isSyncingAdo}
                  >
                    {isClearingAdo ? 'Limpando...' : 'Limpar Board ADO'}
                  </Button>
                  <Button 
                    variant="outlined" 
                    onClick={handleAdoSync}
                    disabled={isSyncingAdo || isClearingAdo}
                  >
                    {isSyncingAdo ? 'Sincronizando...' : 'Sincronizar com Azure DevOps'}
                  </Button>
                </>
              )}
              <Button variant="contained" onClick={() => setSprintModalOpen(true)}>
                New Sprint
              </Button>
            </Stack>
          </Stack>
        </CardContent>
      </Card>
      {/* Filtros */}
      <Card>
        <CardContent>
          <Stack
            direction="row"
            spacing={1.2}
            useFlexGap
            sx={{
              alignItems: "center",
              flexWrap: "wrap"
            }}>
            <FormControl size="small" sx={{ minWidth: 240 }}>
              <InputLabel id="backlog-filter-label">Backlog</InputLabel>
              <Select
                labelId="backlog-filter-label"
                label="Backlog"
                value={backlogIdFilter}
                onChange={(event) => {
                  setSelectedBoardSprintId('')
                  if (event.target.value) {
                    setSearchParams({ backlogId: event.target.value })
                  } else {
                    setSearchParams({})
                  }
                }}
              >
                <MenuItem value="">All Sprints</MenuItem>
                {backlog.sort((a, b) => a.title.localeCompare(b.title)).map((item) => (
                  <MenuItem key={item.id} value={item.id}>
                    {item.title}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            {!backlogIdFilter && (
              <FormControl size="small" sx={{ minWidth: 180 }}>
                <InputLabel id="view-mode-label">View mode</InputLabel>
                <Select
                  labelId="view-mode-label"
                  label="View mode"
                  value={viewMode}
                  onChange={(event) => setViewMode(event.target.value as 'grouped' | 'race')}
                >
                  <MenuItem value="grouped">Grouped by backlog</MenuItem>
                  <MenuItem value="race">Race (all cards live)</MenuItem>
                </Select>
              </FormControl>
            )}

            {!backlogIdFilter && viewMode === 'race' && (
              <>
                <FormControl size="small" sx={{ minWidth: 150 }}>
                  <InputLabel id="race-assignee-label">Assignee</InputLabel>
                  <Select
                    labelId="race-assignee-label"
                    label="Assignee"
                    value={assigneeFilter}
                    onChange={(event) => setAssigneeFilter(event.target.value)}
                  >
                    <MenuItem value="all">All</MenuItem>
                    {availableAssignees.map((assignee) => (
                      <MenuItem key={assignee} value={assignee}>
                        {assignee}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>

                <FormControl size="small" sx={{ minWidth: 130 }}>
                  <InputLabel id="race-priority-label">Priority</InputLabel>
                  <Select
                    labelId="race-priority-label"
                    label="Priority"
                    value={priorityFilter}
                    onChange={(event) => setPriorityFilter(event.target.value)}
                  >
                    <MenuItem value="all">All</MenuItem>
                    {availablePriorities.map((priority) => (
                      <MenuItem key={priority} value={String(priority)}>
                        P{priority}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>

                <FormControl size="small" sx={{ minWidth: 160 }}>
                  <InputLabel id="race-status-label">Column</InputLabel>
                  <Select
                    labelId="race-status-label"
                    label="Column"
                    value={statusFilter}
                    onChange={(event) => setStatusFilter(event.target.value)}
                  >
                    <MenuItem value="all">All</MenuItem>
                    <MenuItem value="0">To Do</MenuItem>
                    <MenuItem value="1">In Progress</MenuItem>
                    <MenuItem value="2">Review</MenuItem>
                    <MenuItem value="3">Done</MenuItem>
                    <MenuItem value="4">Blocked</MenuItem>
                  </Select>
                </FormControl>
              </>
            )}

            {backlogIdFilter && (
              <>
                <FormControl size="small" sx={{ minWidth: 180 }}>
                  <InputLabel id="board-sprint-label">Sprint</InputLabel>
                  <Select
                    labelId="board-sprint-label"
                    label="Sprint"
                    value={boardSprintId}
                    onChange={(event) => setSelectedBoardSprintId(event.target.value)}
                  >
                    <MenuItem value="all">All</MenuItem>
                    {sprints.map((sprint) => (
                      <MenuItem key={sprint.id} value={sprint.id}>
                        {sprint.name}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>

                <FormControl size="small" sx={{ minWidth: 150 }}>
                  <InputLabel id="board-assignee-label">Assignee</InputLabel>
                  <Select
                    labelId="board-assignee-label"
                    label="Assignee"
                    value={assigneeFilter}
                    onChange={(event) => setAssigneeFilter(event.target.value)}
                  >
                    <MenuItem value="all">All</MenuItem>
                    {availableAssignees.map((assignee) => (
                      <MenuItem key={assignee} value={assignee}>
                        {assignee}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>

                <FormControl size="small" sx={{ minWidth: 130 }}>
                  <InputLabel id="board-priority-label">Priority</InputLabel>
                  <Select
                    labelId="board-priority-label"
                    label="Priority"
                    value={priorityFilter}
                    onChange={(event) => setPriorityFilter(event.target.value)}
                  >
                    <MenuItem value="all">All</MenuItem>
                    {availablePriorities.map((priority) => (
                      <MenuItem key={priority} value={String(priority)}>
                        P{priority}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>

                <Button
                  size="small"
                  variant="outlined"
                  onClick={() => {
                    setSelectedBoardSprintId('')
                    setSearchParams({})
                  }}
                >
                  Clear filter
                </Button>
              </>
            )}
          </Stack>
        </CardContent>
      </Card>
      {/* Grouped view: no backlog filter active */}
      {!backlogIdFilter && viewMode === 'grouped' && (
        <Stack spacing={1.2}>
          <Typography variant="h6">Sprints grouped by Backlog</Typography>
          {sprints.length === 0 ? (
            <Alert severity="info">Create a sprint to view the list.</Alert>
          ) : sprintsGroupedByBacklog && sprintsGroupedByBacklog.length === 0 ? (
            <Alert severity="info">No backlog items associated with sprints yet.</Alert>
          ) : (
            (sprintsGroupedByBacklog ?? []).map(({ backlogItem, sprintIds }) => {
              const relatedSprints = sprints.filter((s) => sprintIds.has(s.id))
              return (
                <Card key={backlogItem.id} variant="outlined">
                  <CardContent>
                    <Stack
                      direction={{ xs: 'column', md: 'row' }}
                      spacing={1}
                      sx={{
                        justifyContent: "space-between",
                        alignItems: { md: 'flex-start' }
                      }}>
                      <Stack spacing={0.4}>
                        <Stack direction="row" spacing={1} sx={{
                          alignItems: "center"
                        }}>
                          <Typography variant="subtitle1" sx={{
                            fontWeight: 700
                          }}>{backlogItem.title}</Typography>
                          <Chip label={`SP ${backlogItem.storyPoints}`} size="small" color="primary" variant="outlined" />
                          <Chip label={`P${backlogItem.priority}`} size="small" color="secondary" variant="outlined" />
                          <Chip label={backlogStatusLabels[toNumberStatus(backlogItem.status)] ?? String(backlogItem.status)} size="small" />
                        </Stack>
                        <Typography variant="body2" sx={{
                          color: "text.secondary"
                        }}>{backlogItem.description}</Typography>
                      </Stack>
                      <Button
                        size="small"
                        variant="contained"
                        onClick={() => setSearchParams({ backlogId: backlogItem.id })}
                      >
                        View Kanban
                      </Button>
                    </Stack>
                    <Stack
                      direction="row"
                      spacing={1}
                      useFlexGap
                      sx={{
                        flexWrap: "wrap",
                        mt: 1.2
                      }}>
                      {relatedSprints.map((sprint) => {
                        const sprintWorkItems = sprint.workItems.filter((w) => w.backlogItemId === backlogItem.id)
                        const totalTokens = sprintWorkItems.reduce((acc, w) => acc + (w.totalTokensSpent ?? 0), 0)
                        const totalFeedbacks = sprintWorkItems.reduce((acc, w) => acc + (w.feedbacks?.length ?? 0), 0)
                        const sprintCommitCount = sprint.commitIds?.length ?? 0
                        const sprintActive = toNumberStatus(sprint.status) === 1
                        const dateRange = sprint.startDate && sprint.endDate
                          ? `${new Date(sprint.startDate).toLocaleDateString('en-US', { day: '2-digit', month: '2-digit' })} – ${new Date(sprint.endDate).toLocaleDateString('en-US', { day: '2-digit', month: '2-digit' })}`
                          : ''
                        return (
                          <Chip
                            key={sprint.id}
                            size="small"
                            clickable
                            color={sprintActive ? 'success' : 'default'}
                            variant="outlined"
                            label={[
                              sprint.name,
                              sprintActive ? 'Active' : 'Planned/Closed',
                              `${sprintWorkItems.length} tasks`,
                              dateRange,
                              totalTokens > 0 ? `${totalTokens} tk` : '',
                              totalFeedbacks > 0 ? `${totalFeedbacks} fb` : '',
                              sprintCommitCount > 0 ? `${sprintCommitCount} commits` : '',
                            ].filter(Boolean).join(' • ')}
                            onClick={() => {
                              setSearchParams({ backlogId: backlogItem.id })
                              setSelectedBoardSprintId(sprint.id)
                            }}
                          />
                        )
                      })}
                    </Stack>
                  </CardContent>
                </Card>
              );
            })
          )}
          {/* Sprints with no backlog items */}
          {sprints.filter((s) => s.workItems.length === 0).length > 0 && (
            <Card variant="outlined" sx={{ borderStyle: 'dashed' }}>
              <CardContent>
                <Typography variant="subtitle2" gutterBottom sx={{
                  color: "text.secondary"
                }}>
                  Sprints without linked tasks
                </Typography>
                <Stack direction="row" spacing={1} useFlexGap sx={{
                  flexWrap: "wrap"
                }}>
                  {sprints.filter((s) => s.workItems.length === 0).map((sprint) => (
                    <Chip
                      key={sprint.id}
                      size="small"
                      variant="outlined"
                      label={sprint.name}
                      clickable
                      onClick={() => setSelectedBoardSprintId(sprint.id)}
                    />
                  ))}
                </Stack>
              </CardContent>
            </Card>
          )}
        </Stack>
      )}
      {!backlogIdFilter && viewMode === 'race' && (
        <Stack spacing={1.2}>
          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            spacing={1}
            sx={{
              alignItems: { sm: 'center' },
              justifyContent: "space-between"
            }}>
            <Typography variant="h6">Race Mode</Typography>
            <Chip size="small" color="success" variant="outlined" label={`Live refresh every 5s • ${raceList.length} cards`} />
          </Stack>

          {raceList.length === 0 ? (
            <Alert severity="info">No cards found for the selected project filters.</Alert>
          ) : (
            <Grid container spacing={1.2}>
              {raceList.map((item) => (
                <Grid key={item.id} size={{ xs: 12, md: 6, xl: 4 }}>
                  <WorkItemCard
                    item={item}
                    priority={getWorkItemPriority(item)}
                    isDragging={false}
                    showDragHandle={false}
                    showActivityTime
                    feedbackExpanded={expandedFeedbackIds.has(item.id)}
                    onToggleFeedbacks={() => toggleFeedbacks(item.id)}
                    onDragStart={() => {}}
                    onDragEnd={() => {}}
                    onEdit={() => handleOpenTaskModal(item)}
                    onGraphClick={() => setGraphModal({ id: item.id, title: item.title })}
                    contextChips={(
                      <Stack direction="row" spacing={0.5} useFlexGap sx={{
                        flexWrap: "wrap"
                      }}>
                        <Chip size="small" label={`Sprint: ${item.sprintName}`} variant="outlined" />
                        <Chip size="small" label={`Backlog: ${item.backlogTitle}`} variant="outlined" />
                      </Stack>
                    )}
                  />
                </Grid>
              ))}
            </Grid>
          )}
        </Stack>
      )}
      {backlogIdFilter && (
        <>
      {sprints.length === 0 ? (
        <Alert severity="info">Create a sprint to view the Kanban board.</Alert>
      ) : (
        <Stack spacing={1.2}>
          <Typography variant="h6">
            {boardSprintId === 'all'
              ? `Kanban — ${backlog.find((b) => b.id === backlogIdFilter)?.title ?? 'Backlog'}`
              : `Kanban da Sprint: ${boardSprint?.name ?? ''}`}
          </Typography>
          <Stack direction="row" spacing={0.75} useFlexGap sx={{
            flexWrap: "wrap"
          }}>
            {(backlog.find((b) => b.id === backlogIdFilter)?.commitIds ?? []).slice(0, 4).map((commitId) => (
              <Chip key={`backlog-${commitId}`} size="small" variant="outlined" label={`BL: ${commitId}`} sx={{ fontFamily: 'monospace', fontSize: 11 }} />
            ))}
            {(boardSprint?.commitIds ?? []).slice(0, 4).map((commitId) => (
              <Chip key={`sprint-${commitId}`} size="small" variant="outlined" label={`SP: ${commitId}`} sx={{ fontFamily: 'monospace', fontSize: 11 }} />
            ))}
          </Stack>
          <Grid container spacing={1.2}>
            {[0, 1, 2, 3].map((columnStatus) => (
              <Grid key={columnStatus} size={{ xs: 12, md: 6, lg: 3 }}>
                <Paper
                  onDragOver={(event) => {
                    event.preventDefault()
                    setDragTargetStatus(columnStatus)
                  }}
                  onDragLeave={() => setDragTargetStatus((prev) => (prev === columnStatus ? null : prev))}
                  onDrop={(event) => {
                    event.preventDefault()
                    const droppedWorkItemId = getDroppedWorkItemId(event)
                    void handleTaskDrop(columnStatus, droppedWorkItemId)
                  }}
                  sx={{
                    minHeight: 360,
                    borderRadius: 3,
                    border: dragTargetStatus === columnStatus ? '1px solid #4f8fda' : '1px solid #d6deea',
                    bgcolor: dragTargetStatus === columnStatus ? '#e7f1ff' : '#edf2f8',
                    transition: 'background-color 120ms ease, border-color 120ms ease',
                  }}
                >
                  <CardContent sx={{ p: 1.3 }}>
                    <Stack
                      direction="row"
                      sx={{
                        alignItems: "center",
                        justifyContent: "space-between",
                        px: 0.5,
                        pb: 0.5
                      }}>
                      <Typography variant="subtitle2" sx={{
                        fontWeight: 800
                      }}>
                        {workItemStatusLabels[columnStatus]}
                      </Typography>
                      <Chip label={(sprintBoard[columnStatus] ?? []).length} size="small" color="default" />
                    </Stack>
                    <Stack spacing={1}>
                      {(sprintBoard[columnStatus] ?? []).map((item) => (
                        <WorkItemCard
                          key={item.id}
                          item={item}
                          priority={getWorkItemPriority(item)}
                          isDragging={draggedWorkItemId === item.id}
                          feedbackExpanded={expandedFeedbackIds.has(item.id)}
                          onToggleFeedbacks={() => toggleFeedbacks(item.id)}
                          onDragStart={(event) => handleTaskDragStart(event, item.id)}
                          onDragEnd={handleTaskDragEnd}
                          onEdit={() => handleOpenTaskModal(item)}
                          onGraphClick={() => setGraphModal({ id: item.id, title: item.title })}
                        />
                      ))}
                      {(sprintBoard[columnStatus] ?? []).length === 0 ? (
                        <Box
                          sx={{
                            borderRadius: 2,
                            border: '1px dashed #b7c7dc',
                            p: 1.2,
                            textAlign: 'center',
                            color: 'text.secondary',
                            fontSize: 13,
                          }}
                        >
                          Drag a task to this list.
                        </Box>
                      ) : null}
                    </Stack>
                  </CardContent>
                </Paper>
              </Grid>
            ))}
          </Grid>
        </Stack>
      )}

        </>
      )}
      <Dialog open={isSprintModalOpen} onClose={() => setSprintModalOpen(false)} fullWidth maxWidth="md">
        <Stack component="form" spacing={1.2} onSubmit={handleCreateSprint}>
          <DialogTitle>New Sprint</DialogTitle>
          <DialogContent>
            <Stack spacing={1.2} sx={{ mt: 1 }}>
              <TextField
                value={newSprintName}
                onChange={(event) => setNewSprintName(event.target.value)}
                label="Sprint name"
                required
              />
              <TextField
                value={newSprintGoal}
                onChange={(event) => setNewSprintGoal(event.target.value)}
                label="Goal"
                required
              />
              <Grid container spacing={1.2}>
                <Grid size={{ xs: 12, sm: 6 }}>
                  <TextField
                    type="date"
                    value={newSprintStartDate}
                    onChange={(event) => setNewSprintStartDate(event.target.value)}
                    label="Start"
                    InputLabelProps={{ shrink: true }}
                    fullWidth
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 6 }}>
                  <TextField
                    type="date"
                    value={newSprintEndDate}
                    onChange={(event) => setNewSprintEndDate(event.target.value)}
                    label="End"
                    InputLabelProps={{ shrink: true }}
                    fullWidth
                  />
                </Grid>
              </Grid>

              <Stack spacing={0.5} sx={{ maxHeight: 260, overflowY: 'auto', p: 1, border: '1px solid #dde6f0', borderRadius: 2 }}>
                {backlog.filter((item) => toNumberStatus(item.status) <= 2).sort((a, b) => a.title.localeCompare(b.title)).map((item) => (
                  <Stack key={item.id} direction="row" spacing={1} sx={{
                    alignItems: "center"
                  }}>
                    <Checkbox
                      checked={selectedBacklogIds.includes(item.id)}
                      onChange={() => toggleBacklogSelection(item.id)}
                    />
                    <Stack>
                      <Typography variant="body2" sx={{
                        fontWeight: 700
                      }}>{item.title}</Typography>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>
                        {backlogStatusLabels[toNumberStatus(item.status)] ?? String(item.status)} | SP {item.storyPoints}
                      </Typography>
                    </Stack>
                  </Stack>
                ))}
              </Stack>
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setSprintModalOpen(false)}>Cancel</Button>
            <Button type="submit" variant="contained">Create sprint</Button>
          </DialogActions>
        </Stack>
      </Dialog>
      <Dialog open={Boolean(editingWorkItemId)} onClose={() => setEditingWorkItemId('')} fullWidth maxWidth="md">
        <DialogTitle>Edit task</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Typography variant="overline" sx={{
              color: "text.secondary"
            }}>Status & Assignee</Typography>
            <Grid container spacing={1.5}>
              <Grid size={6}>
                <FormControl size="small" fullWidth>
                  <InputLabel id="modal-task-status-label">Status</InputLabel>
                  <Select
                    labelId="modal-task-status-label"
                    label="Status"
                    value={editingWorkItemStatus}
                    onChange={(event) => setEditingWorkItemStatus(Number(event.target.value))}
                  >
                    <MenuItem value={0}>To Do</MenuItem>
                    <MenuItem value={1}>In Progress</MenuItem>
                    <MenuItem value={2}>Review</MenuItem>
                    <MenuItem value={3}>Done</MenuItem>
                    <MenuItem value={4}>Blocked</MenuItem>
                  </Select>
                </FormControl>
              </Grid>
              <Grid size={6}>
                <TextField
                  size="small"
                  label="Assignee"
                  fullWidth
                  value={editingWorkItemAssignee}
                  onChange={(event) => setEditingWorkItemAssignee(event.target.value)}
                />
              </Grid>
            </Grid>

            <Typography variant="overline" sx={{
              color: "text.secondary"
            }}>Agent Context</Typography>
            <Grid container spacing={1.5}>
              <Grid size={6}>
                <TextField
                  size="small"
                  label="Agent Name"
                  fullWidth
                  placeholder="ex: GitHub Copilot"
                  value={editingAgentName}
                  onChange={(event) => setEditingAgentName(event.target.value)}
                />
              </Grid>
              <Grid size={6}>
                <TextField
                  size="small"
                  label="Tokens used in this session"
                  fullWidth
                  type="number"
                  inputProps={{ min: 0 }}
                  value={editingTokensUsed}
                  onChange={(event) => setEditingTokensUsed(Math.max(0, Number(event.target.value)))}
                />
              </Grid>
              <Grid size={6}>
                <TextField
                  size="small"
                  label="Modelo usado"
                  fullWidth
                  placeholder="ex: claude-sonnet-4.6"
                  value={editingModelUsed}
                  onChange={(event) => setEditingModelUsed(event.target.value)}
                />
              </Grid>
              <Grid size={6}>
                <TextField
                  size="small"
                  label="IDE used"
                  fullWidth
                  placeholder="ex: VS Code"
                  value={editingIdeUsed}
                  onChange={(event) => setEditingIdeUsed(event.target.value)}
                />
              </Grid>
            </Grid>

            <Typography variant="overline" sx={{
              color: "text.secondary"
            }}>Branch</Typography>
            <TextField
              size="small"
              label="Branch"
              fullWidth
              placeholder="ex: feat/my-feature"
              value={editingBranch}
              onChange={(event) => setEditingBranch(event.target.value)}
            />

            <Typography variant="overline" sx={{
              color: "text.secondary"
            }}>Commit IDs</Typography>
            <TextField
              size="small"
              label="Commits (comma or line separated)"
              fullWidth
              multiline
              minRows={2}
              placeholder="abc123, def456"
              value={editingCommitIds}
              onChange={(event) => setEditingCommitIds(event.target.value)}
            />

            <Typography variant="overline" sx={{
              color: "text.secondary"
            }}>Work Log</Typography>
            <TextField
              size="small"
              label="Feedback / What was done"
              fullWidth
              multiline
              minRows={3}
              placeholder="Describe what was done, decisions made, next steps..."
              value={editingFeedback}
              onChange={(event) => setEditingFeedback(event.target.value)}
            />
            <TextField
              size="small"
              label="Metadata JSON (opcional)"
              fullWidth
              multiline
              minRows={2}
              placeholder='{"branch": "feat/xyz", "commit": "abc123"}'
              value={editingMetadataJson}
              onChange={(event) => setEditingMetadataJson(event.target.value)}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditingWorkItemId('')}>Cancel</Button>
          <Button variant="contained" onClick={handleSaveTaskFromModal}>Save</Button>
        </DialogActions>
      </Dialog>
      {graphModal && selectedProjectId && (
        <GraphModal
          projectId={selectedProjectId}
          workItemId={graphModal.id}
          title={graphModal.title}
          onClose={() => setGraphModal(null)}
        />
      )}
    </Stack>
  );
}

function BoxDragHandle({
  onDragStart,
  onDragEnd,
}: {
  onDragStart: (event: DragEvent<HTMLDivElement>) => void
  onDragEnd: () => void
}) {
  return (
    <Stack
      direction="row"
      draggable
      onDragStart={onDragStart}
      onDragEnd={onDragEnd}
      sx={{
        alignItems: "center",
        justifyContent: "center",
        border: '1px dashed #5f7fa5',
        color: '#30587f',
        borderRadius: 1,
        py: 0.3,
        bgcolor: '#f6f9ff',
        cursor: 'grab'
      }}>
      <Typography variant="caption" sx={{
        fontWeight: 700
      }}>Drag</Typography>
    </Stack>
  );
}

const statusBorderColor: Record<number, string> = {
  0: '#2f78c5',
  1: '#ed9f1d',
  2: '#9c27b0',
  3: '#2e7d32',
  4: '#d32f2f',
}

import type { DragEvent as ReactDragEvent } from 'react'
import type { SprintWorkItem, WorkItemFeedback } from '../types'

function WorkItemCard({
  item,
  priority,
  isDragging,
  showDragHandle = true,
  showActivityTime = false,
  feedbackExpanded,
  onToggleFeedbacks,
  onDragStart,
  onDragEnd,
  onEdit,
  onGraphClick,
  contextChips,
}: {
  item: SprintWorkItem
  priority: number | undefined
  isDragging: boolean
  showDragHandle?: boolean
  showActivityTime?: boolean
  feedbackExpanded: boolean
  onToggleFeedbacks: () => void
  onDragStart: (event: ReactDragEvent<HTMLDivElement>) => void
  onDragEnd: () => void
  onEdit: () => void
  onGraphClick: () => void
  contextChips?: React.ReactNode
}) {
  const statusNum = toNumberStatus(item.status)
  const borderColor = statusBorderColor[statusNum] ?? '#2f78c5'
  const lastActivity = item.updatedAt ?? item.createdAt
  const formattedDateTime = lastActivity
    ? new Date(lastActivity).toLocaleString('en-US', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      })
    : null
  const formattedDate = lastActivity
    ? new Date(lastActivity).toLocaleDateString('en-US', { day: '2-digit', month: '2-digit', year: 'numeric' })
    : null

  return (
    <Paper
      sx={{
        borderRadius: 2,
        border: '1px solid #d7dfeb',
        borderLeft: `4px solid ${borderColor}`,
        bgcolor: '#ffffff',
        boxShadow: '0 1px 1px rgba(9,30,66,0.09)',
        opacity: isDragging ? 0.65 : 1,
      }}
    >
      <CardContent sx={{ p: 1.1, '&:last-child': { pb: 1.1 } }}>
        <Stack spacing={0.75}>
          {/* Drag handle */}
          {showDragHandle && <BoxDragHandle onDragStart={onDragStart} onDragEnd={onDragEnd} />}

          {contextChips}

          {/* Title + Priority chip + Tokens chip */}
          <Stack
            direction="row"
            spacing={0.75}
            useFlexGap
            sx={{
              alignItems: "flex-start",
              flexWrap: "wrap"
            }}>
            <Typography
              variant="subtitle2"
              sx={{
                fontWeight: 700,
                flex: 1,
                minWidth: 0
              }}>
              {item.title}
            </Typography>
            {typeof priority === 'number' && (
              <Chip size="small" label={`P${priority}`} variant="outlined" color="secondary" />
            )}
            {item.totalTokensSpent > 0 && (
              <Tooltip title="Total tokens spent on this task">
                <Chip size="small" label={`${item.totalTokensSpent} tk`} color="info" variant="outlined" />
              </Tooltip>
            )}
          </Stack>

          {/* Description */}
          {item.description && (
            <Typography
              variant="body2"
              sx={{
                color: "text.secondary",
                display: '-webkit-box',
                WebkitLineClamp: 2,
                WebkitBoxOrient: 'vertical',
                overflow: 'hidden'
              }}>
              {item.description}
            </Typography>
          )}

          {/* Sub-task indicator + Tags */}
          {(item.parentWorkItemId ?? item.tags) && (
            <Stack direction="row" spacing={0.5} useFlexGap sx={{
              flexWrap: "wrap"
            }}>
              {item.parentWorkItemId && (
                <Chip size="small" label="↳ sub-task" sx={{ bgcolor: '#fff3e0', color: '#e65100', fontSize: 11 }} />
              )}
              {typeof item.tags === 'string' && item.tags.trim() && item.tags.split(',').map((tag) => tag.trim()).filter(Boolean).map((tag) => (
                <Chip key={tag} size="small" label={tag} variant="outlined" sx={{ fontSize: 11 }} />
              ))}
            </Stack>
          )}

          {/* Branch */}
          {item.branch && (
            <Typography variant="caption" sx={{ color: '#1565c0', fontFamily: 'monospace' }}>
              ⎇ {item.branch}
            </Typography>
          )}

          {Array.isArray(item.commitIds) && item.commitIds.length > 0 && (
            <Stack direction="row" spacing={0.5} useFlexGap sx={{
              flexWrap: "wrap"
            }}>
              {item.commitIds.slice(0, 3).map((commitId) => (
                <Chip
                  key={commitId}
                  size="small"
                  label={commitId}
                  variant="outlined"
                  sx={{ fontSize: 11, fontFamily: 'monospace' }}
                />
              ))}
              {item.commitIds.length > 3 && (
                <Chip size="small" label={`+${item.commitIds.length - 3} commits`} variant="outlined" sx={{ fontSize: 11 }} />
              )}
            </Stack>
          )}

          {/* Agent context row */}
          {item.lastModelUsed && (
            <Stack direction="row" spacing={0.5} useFlexGap sx={{
              flexWrap: "wrap"
            }}>
              <Chip
                size="small"
                label={`Model: ${item.lastModelUsed}`}
                sx={{ bgcolor: '#f0f4ff', fontSize: 11 }}
              />
              {item.lastIdeUsed && (
                <Chip
                  size="small"
                  label={`IDE: ${item.lastIdeUsed}`}
                  sx={{ bgcolor: '#f5f0ff', fontSize: 11 }}
                />
              )}
            </Stack>
          )}

          {/* Assignee + Last updated */}
          <Stack
            direction="row"
            sx={{
              justifyContent: "space-between",
              alignItems: "center"
            }}>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>
              {item.assignee ? `@${item.assignee}` : 'no assignee'}
            </Typography>
            {(showActivityTime ? formattedDateTime : formattedDate) && (
              <Typography variant="caption" sx={{
                color: "text.disabled"
              }}>
                {item.updatedAt ? 'Updated' : 'Created'} {showActivityTime ? formattedDateTime : formattedDate}
              </Typography>
            )}
          </Stack>

          {/* Feedbacks toggle */}
          {Array.isArray(item.feedbacks) && item.feedbacks.length > 0 && (
            <>
              <Button
                size="small"
                variant="text"
                onClick={onToggleFeedbacks}
                sx={{ justifyContent: 'flex-start', px: 0, fontSize: 12, color: 'text.secondary' }}
              >
                {feedbackExpanded ? '▾' : '▸'} {item.feedbacks.length} agent feedback{item.feedbacks.length > 1 ? 's' : ''}
              </Button>
              <Collapse in={feedbackExpanded} unmountOnExit>
                <Stack spacing={0.6}>
                  {item.feedbacks.map((fb) => (
                    <FeedbackEntry key={fb.id} feedback={fb} />
                  ))}
                </Stack>
              </Collapse>
            </>
          )}

          <Stack direction="row" spacing={1}>
            <Button size="small" variant="contained" onClick={onEdit} sx={{ flex: 1 }}>
              Edit task
            </Button>
            <Tooltip title="Visualizar grafo de rastreabilidade desta task">
              <Button size="small" variant="outlined" color="info" onClick={onGraphClick}>
                🕸️ Graph
              </Button>
            </Tooltip>
          </Stack>
        </Stack>
      </CardContent>
    </Paper>
  );
}

function FeedbackEntry({ feedback }: { feedback: WorkItemFeedback }) {
  const date = new Date(feedback.createdAt).toLocaleDateString('en-US', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
  return (
    <Box
      sx={{
        bgcolor: '#f8f9fc',
        borderRadius: 1,
        border: '1px solid #e3e8f0',
        p: 0.8,
      }}
    >
      <Stack
        direction="row"
        sx={{
          justifyContent: "space-between",
          alignItems: "center",
          mb: 0.3
        }}>
        <Stack direction="row" spacing={0.5} sx={{
          alignItems: "center"
        }}>
          <Typography
            variant="caption"
            sx={{
              fontWeight: 700,
              color: "text.primary"
            }}>
            {feedback.agentName || 'agent'}
          </Typography>
          {feedback.modelUsed && (
            <Typography variant="caption" sx={{
              color: "text.disabled"
            }}>
              · {feedback.modelUsed}
            </Typography>
          )}
          {feedback.ideUsed && (
            <Typography variant="caption" sx={{
              color: "text.disabled"
            }}>
              · {feedback.ideUsed}
            </Typography>
          )}
        </Stack>
        <Stack direction="row" spacing={0.5} sx={{
          alignItems: "center"
        }}>
          {feedback.tokensUsed > 0 && (
            <Chip size="small" label={`${feedback.tokensUsed} tk`} sx={{ fontSize: 10, height: 16 }} />
          )}
          <Typography variant="caption" sx={{
            color: "text.disabled"
          }}>{date}</Typography>
        </Stack>
      </Stack>
      {feedback.feedback && (
        <Typography
          variant="caption"
          sx={{
            color: "text.secondary",
            display: 'block'
          }}>
          {feedback.feedback}
        </Typography>
      )}
    </Box>
  );
}
