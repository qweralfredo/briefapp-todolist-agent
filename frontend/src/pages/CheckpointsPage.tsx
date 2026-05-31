import AddCircleOutlineRoundedIcon from '@mui/icons-material/AddCircleOutlineRounded'
import {
  Button,
  Card,
  CardActionArea,
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
  Typography,
} from '@mui/material'
import MDEditor from '@uiw/react-md-editor'
import { MarkdownField } from '../components/MarkdownField'
import { useMemo, useState } from 'react'
import { apiClient } from '../api/client'
import { useProjectContext } from '../context/useProjectContext'
import type { KnowledgeCheckpoint } from '../types'
import { groupByCategory } from '../types'
import { KnowledgeNav } from './KnowledgeNav'

export function CheckpointsPage() {
  const { selectedProjectId, selectedProject, knowledge, refreshProjectViews } = useProjectContext()
  const [isModalOpen, setModalOpen] = useState(false)
  const [checkpointName, setCheckpointName] = useState('')
  const [checkpointCategory, setCheckpointCategory] = useState('Release')
  const [checkpointContext, setCheckpointContext] = useState('')
  const [checkpointDecisions, setCheckpointDecisions] = useState('')
  const [checkpointRisks, setCheckpointRisks] = useState('')
  const [checkpointNextActions, setCheckpointNextActions] = useState('')

  const [selectedCheckpoint, setSelectedCheckpoint] = useState<KnowledgeCheckpoint | null>(null)

  const checkpointByCategory = useMemo(() => groupByCategory(knowledge?.checkpoints ?? []), [knowledge])

  async function handleCreateCheckpoint(event: React.FormEvent) {
    event.preventDefault()
    if (!selectedProjectId || !checkpointName.trim()) {
      return
    }

    await apiClient.createCheckpoint(selectedProjectId, {
      name: checkpointName,
      category: checkpointCategory,
      contextSnapshot: checkpointContext,
      decisions: checkpointDecisions,
      risks: checkpointRisks,
      nextActions: checkpointNextActions,
    })

    setCheckpointName('')
    setCheckpointCategory('Release')
    setCheckpointContext('')
    setCheckpointDecisions('')
    setCheckpointRisks('')
    setCheckpointNextActions('')
    setModalOpen(false)
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
                maxWidth: 540
              }}>
              Select a project to register progress checkpoints.
            </Typography>
          </Stack>
        </CardContent>
      </Card>
    );
  }

  return (
    <Stack spacing={2}>
      <KnowledgeNav />
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
              <Typography variant="h6">Project Checkpoints</Typography>
              <Typography sx={{
                color: "text.secondary"
              }}>
                Milestones with context, decisions, risks and next actions.
              </Typography>
            </Stack>
            <Button variant="contained" startIcon={<AddCircleOutlineRoundedIcon />} onClick={() => setModalOpen(true)}>
              New Checkpoint
            </Button>
          </Stack>
        </CardContent>
      </Card>
      <Grid container spacing={2}>
        {Object.entries(checkpointByCategory).map(([category, items]) => (
          <Grid key={category} size={{ xs: 12, md: 6, lg: 4 }}>
            <Card sx={{ height: '100%' }}>
              <CardContent>
                <Typography variant="overline" sx={{
                  color: "primary.main"
                }}>{category}</Typography>
                <Stack spacing={0.7} sx={{ mt: 0.8 }}>
                  {items.map((item) => (
                    <CardActionArea key={item.id} onClick={() => setSelectedCheckpoint(item)} sx={{ borderRadius: 1, px: 0.5, py: 0.3 }}>
                      <Typography variant="body2" sx={{
                        fontWeight: 700
                      }}>
                        {item.name}
                      </Typography>
                      <Divider sx={{ mt: 0.5 }} />
                    </CardActionArea>
                  ))}
                </Stack>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
      {Object.keys(checkpointByCategory).length === 0 ? (
        <Typography sx={{
          color: "text.secondary"
        }}>No checkpoints in this project.</Typography>
      ) : null}
      <Dialog open={selectedCheckpoint !== null} onClose={() => setSelectedCheckpoint(null)} fullWidth maxWidth="xl">
        <DialogTitle>
          <Stack direction="row" spacing={1} sx={{
            alignItems: "center"
          }}>
            <Typography variant="h6" sx={{ flex: 1 }}>{selectedCheckpoint?.name}</Typography>
            {selectedCheckpoint?.category ? <Chip label={selectedCheckpoint.category} size="small" color="primary" variant="outlined" /> : null}
          </Stack>
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            {selectedCheckpoint?.contextSnapshot ? (
              <Stack spacing={0.5}>
                <Typography variant="overline" sx={{
                  color: "text.secondary"
                }}>Context</Typography>
                <div data-color-mode="light">
                  <MDEditor.Markdown source={selectedCheckpoint.contextSnapshot} style={{ padding: '10px', minHeight: 60 }} />
                </div>
              </Stack>
            ) : null}
            {selectedCheckpoint?.decisions ? (
              <Stack spacing={0.5}>
                <Divider />
                <Typography variant="overline" sx={{
                  color: "text.secondary"
                }}>Decisions</Typography>
                <div data-color-mode="light">
                  <MDEditor.Markdown source={selectedCheckpoint.decisions} style={{ padding: '10px', minHeight: 60 }} />
                </div>
              </Stack>
            ) : null}
            {selectedCheckpoint?.risks ? (
              <Stack spacing={0.5}>
                <Divider />
                <Typography variant="overline" sx={{
                  color: "text.secondary"
                }}>Risks</Typography>
                <div data-color-mode="light">
                  <MDEditor.Markdown source={selectedCheckpoint.risks} style={{ padding: '10px', minHeight: 60 }} />
                </div>
              </Stack>
            ) : null}
            {selectedCheckpoint?.nextActions ? (
              <Stack spacing={0.5}>
                <Divider />
                <Typography variant="overline" sx={{
                  color: "text.secondary"
                }}>Next Actions</Typography>
                <div data-color-mode="light">
                  <MDEditor.Markdown source={selectedCheckpoint.nextActions} style={{ padding: '10px', minHeight: 60 }} />
                </div>
              </Stack>
            ) : null}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSelectedCheckpoint(null)}>Close</Button>
        </DialogActions>
      </Dialog>
      <Dialog open={isModalOpen} onClose={() => setModalOpen(false)} fullWidth maxWidth="xl">
        <Stack component="form" onSubmit={handleCreateCheckpoint}>
          <DialogTitle>New Checkpoint</DialogTitle>
          <DialogContent>
            <Stack spacing={1.2} sx={{ mt: 1 }}>
              <TextField label="Name" value={checkpointName} onChange={(event) => setCheckpointName(event.target.value)} required />
              <TextField label="Category" value={checkpointCategory} onChange={(event) => setCheckpointCategory(event.target.value)} required />
              <MarkdownField label="Context" value={checkpointContext} onChange={setCheckpointContext} height={160} />
              <MarkdownField label="Decisions" value={checkpointDecisions} onChange={setCheckpointDecisions} height={160} />
              <MarkdownField label="Risks" value={checkpointRisks} onChange={setCheckpointRisks} height={160} />
              <MarkdownField label="Next actions" value={checkpointNextActions} onChange={setCheckpointNextActions} height={160} />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setModalOpen(false)}>Cancel</Button>
            <Button type="submit" variant="contained">Save checkpoint</Button>
          </DialogActions>
        </Stack>
      </Dialog>
    </Stack>
  );
}
