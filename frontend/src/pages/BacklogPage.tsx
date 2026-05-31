import AddTaskOutlinedIcon from '@mui/icons-material/AddTaskOutlined'
import {
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Grid,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import { MarkdownField } from '../components/MarkdownField'
import GraphModal from '../components/GraphModal'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { apiClient } from '../api/client'
import { useProjectContext } from '../context/useProjectContext'
import { backlogStatusLabels, toNumberStatus } from '../types'

function parseCommitIds(value: string): string[] {
  return value
    .split(/[,\n]/)
    .map((item) => item.trim())
    .filter(Boolean);
}

export function BacklogPage() {
  const { selectedProjectId, selectedProject, backlog, refreshProjectViews } = useProjectContext()
  const navigate = useNavigate()
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [storyPoints, setStoryPoints] = useState(3)
  const [priority, setPriority] = useState(1)
  const [isCreateModalOpen, setCreateModalOpen] = useState(false)
  const [contextItemId, setContextItemId] = useState('')
  const [contextTags, setContextTags] = useState('')
  const [contextWikiRefs, setContextWikiRefs] = useState('')
  const [contextConstraints, setContextConstraints] = useState('')
  const [contextCommitIds, setContextCommitIds] = useState('')
  const [graphModal, setGraphModal] = useState<{ id: string; title: string } | null>(null)

  function handleOpenContextModal(id: string, tags = '', wikiRefs = '', constraints = '', commitIds: string[] = []) {
    setContextItemId(id)
    setContextTags(tags)
    setContextWikiRefs(wikiRefs)
    setContextConstraints(constraints)
    setContextCommitIds(commitIds.join(', '))
  }

  async function handleSaveContext() {
    if (!contextItemId || !selectedProjectId) return
    await apiClient.updateBacklogContext(contextItemId, {
      tags: contextTags,
      wikiRefs: contextWikiRefs,
      constraints: contextConstraints,
      commitIds: parseCommitIds(contextCommitIds),
    })
    setContextItemId('')
    await refreshProjectViews(selectedProjectId)
  }

  async function handleCreateBacklogItem(event: React.FormEvent) {
    event.preventDefault()
    if (!selectedProjectId || !title.trim() || !description.trim()) {
      return
    }

    await apiClient.createBacklogItem(selectedProjectId, {
      title,
      description,
      storyPoints,
      priority,
    })

    setTitle('')
    setDescription('')
    setStoryPoints(3)
    setPriority(1)
    setCreateModalOpen(false)
    await refreshProjectViews(selectedProjectId)
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
              Choose a project to create stories, prioritize the backlog and organize sprint planning.
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
              <Typography variant="h6">Story Backlog</Typography>
              <Typography variant="body2" sx={{
                color: "text.secondary"
              }}>
                Structure items with priority and story points in an objective planning flow.
              </Typography>
            </Stack>
            <Button variant="contained" startIcon={<AddTaskOutlinedIcon />} onClick={() => setCreateModalOpen(true)}>
              New Story
            </Button>
          </Stack>
        </CardContent>
      </Card>
      <Stack spacing={1}>
        <Typography variant="h6">Backlog List</Typography>
        {backlog.length === 0 ? (
          <Typography sx={{
            color: "text.secondary"
          }}>No items registered for this project.</Typography>
        ) : (
          backlog.map((item) => (
            <Card key={item.id}>
              <CardContent>
                <Stack direction={{ xs: 'column', md: 'row' }} spacing={1} sx={{
                  justifyContent: "space-between"
                }}>
                  <Stack spacing={0.6}>
                    <Typography variant="subtitle1" sx={{
                      fontWeight: 700
                    }}>{item.title}</Typography>
                    <Typography sx={{
                      color: "text.secondary"
                    }}>{item.description}</Typography>
                  </Stack>
                  <Stack
                    direction="row"
                    spacing={1}
                    useFlexGap
                    sx={{
                      alignItems: "center",
                      flexWrap: "wrap"
                    }}>
                    <Chip label={`SP ${item.storyPoints}`} color="primary" variant="outlined" />
                    <Chip label={`P${item.priority}`} color="secondary" variant="outlined" />
                    <Chip label={backlogStatusLabels[toNumberStatus(item.status)] ?? String(item.status)} color="default" />
                    <Button
                      size="small"
                      variant="outlined"
                      onClick={() => navigate(`/sprints?backlogId=${item.id}`)}
                    >
                      View Sprints
                    </Button>
                    <Tooltip title="Edit tags, wiki refs and constraints for this backlog item">
                      <Button
                        size="small"
                        variant="outlined"
                        color="secondary"
                        onClick={() => handleOpenContextModal(item.id, item.tags, item.wikiRefs, item.constraints, item.commitIds ?? [])}
                      >
                        Context
                      </Button>
                    </Tooltip>
                    <Tooltip title="Visualizar grafo de rastreabilidade macro do backlog">
                      <Button
                        size="small"
                        variant="outlined"
                        color="info"
                        onClick={() => setGraphModal({ id: item.id, title: item.title })}
                      >
                        🕸️ Graph
                      </Button>
                    </Tooltip>
                  </Stack>
                </Stack>
                {(item.tags || item.wikiRefs || item.constraints || (item.commitIds && item.commitIds.length > 0)) && (
                  <Stack
                    direction="row"
                    spacing={0.75}
                    useFlexGap
                    sx={{
                      flexWrap: "wrap",
                      mt: 1
                    }}>
                    {item.tags && item.tags.trim() && item.tags.split(',').map((t) => t.trim()).filter(Boolean).map((tag) => (
                      <Chip key={tag} size="small" label={tag} variant="outlined" color="default" sx={{ fontSize: 11 }} />
                    ))}
                    {item.wikiRefs && item.wikiRefs.trim() && (
                      <Chip size="small" label={`wiki: ${item.wikiRefs}`} variant="outlined" sx={{ fontSize: 11, color: '#1565c0' }} />
                    )}
                    {item.constraints && item.constraints.trim() && (
                      <Chip size="small" label={`constraints: ${item.constraints}`} variant="outlined" color="warning" sx={{ fontSize: 11 }} />
                    )}
                    {item.commitIds && item.commitIds.length > 0 && item.commitIds.map((commitId) => (
                      <Chip key={commitId} size="small" label={commitId} variant="outlined" sx={{ fontSize: 11, fontFamily: 'monospace' }} />
                    ))}
                  </Stack>
                )}
                <Divider sx={{ my: 1.2 }} />
                <Typography variant="caption" sx={{
                  color: "text.secondary"
                }}>
                  ID: {item.id}
                </Typography>
              </CardContent>
            </Card>
          ))
        )}
      </Stack>
      <Dialog open={isCreateModalOpen} onClose={() => setCreateModalOpen(false)} fullWidth maxWidth="md">
        <Stack component="form" onSubmit={handleCreateBacklogItem}>
          <DialogTitle>New Story</DialogTitle>
          <DialogContent>
            <Stack spacing={1.5} sx={{ mt: 1 }}>
              <TextField label="Title" value={title} onChange={(event) => setTitle(event.target.value)} required fullWidth />
              <MarkdownField label="Description" value={description} onChange={setDescription} required />
              <Grid container spacing={1.2}>
                <Grid size={{ xs: 12, sm: 6 }}>
                  <TextField
                    type="number"
                    label="Story points"
                    value={storyPoints}
                    onChange={(event) => setStoryPoints(Number(event.target.value))}
                    inputProps={{ min: 1 }}
                    fullWidth
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 6 }}>
                  <TextField
                    type="number"
                    label="Priority"
                    value={priority}
                    onChange={(event) => setPriority(Number(event.target.value))}
                    inputProps={{ min: 1 }}
                    fullWidth
                  />
                </Grid>
              </Grid>
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setCreateModalOpen(false)}>Cancel</Button>
            <Button type="submit" variant="contained">Add item</Button>
          </DialogActions>
        </Stack>
      </Dialog>
      <Dialog open={Boolean(contextItemId)} onClose={() => setContextItemId('')} fullWidth maxWidth="sm">
        <DialogTitle>Edit Context</DialogTitle>
        <DialogContent>
          <Stack spacing={1.5} sx={{ mt: 1 }}>
            <TextField
              size="small"
              label="Tags (comma-separated)"
              fullWidth
              placeholder="ex: auth, backend, performance"
              value={contextTags}
              onChange={(event) => setContextTags(event.target.value)}
            />
            <TextField
              size="small"
              label="Wiki Refs"
              fullWidth
              placeholder="ex: architecture, api-design"
              value={contextWikiRefs}
              onChange={(event) => setContextWikiRefs(event.target.value)}
            />
            <TextField
              size="small"
              label="Constraints"
              fullWidth
              multiline
              minRows={2}
              placeholder="ex: must not break existing API, max 200ms latency"
              value={contextConstraints}
              onChange={(event) => setContextConstraints(event.target.value)}
            />
            <TextField
              size="small"
              label="Commit IDs"
              fullWidth
              multiline
              minRows={2}
              placeholder="abc123, def456"
              value={contextCommitIds}
              onChange={(event) => setContextCommitIds(event.target.value)}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setContextItemId('')}>Cancel</Button>
          <Button variant="contained" onClick={handleSaveContext}>Save</Button>
        </DialogActions>
      </Dialog>
      {graphModal && selectedProjectId && (
        <GraphModal
          projectId={selectedProjectId}
          backlogItemId={graphModal.id}
          title={graphModal.title}
          onClose={() => setGraphModal(null)}
        />
      )}
    </Stack>
  );
}
