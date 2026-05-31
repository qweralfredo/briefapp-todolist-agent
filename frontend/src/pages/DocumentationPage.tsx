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
import type { KnowledgeDocumentation } from '../types'
import { groupByCategory } from '../types'
import { KnowledgeNav } from './KnowledgeNav'

export function DocumentationPage() {
  const { selectedProjectId, selectedProject, knowledge, refreshProjectViews } = useProjectContext()
  const [isModalOpen, setModalOpen] = useState(false)
  const [docTitle, setDocTitle] = useState('')
  const [docCategory, setDocCategory] = useState('Architecture')
  const [docTags, setDocTags] = useState('')
  const [docContent, setDocContent] = useState('')

  const [selectedDoc, setSelectedDoc] = useState<KnowledgeDocumentation | null>(null)

  const docsByCategory = useMemo(() => groupByCategory(knowledge?.documentationPages ?? []), [knowledge])

  async function handleCreateDocumentation(event: React.FormEvent) {
    event.preventDefault()
    if (!selectedProjectId || !docTitle.trim() || !docContent.trim()) {
      return
    }

    await apiClient.createDocumentation(selectedProjectId, {
      title: docTitle,
      contentMarkdown: docContent,
      category: docCategory,
      tags: docTags,
    })

    setDocTitle('')
    setDocCategory('Architecture')
    setDocTags('')
    setDocContent('')
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
              Select a project to keep the technical documentation organized.
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
              <Typography variant="h6">Project Documentation</Typography>
              <Typography sx={{
                color: "text.secondary"
              }}>
                Official pages for architecture, operations and engineering decisions.
              </Typography>
            </Stack>
            <Button variant="contained" startIcon={<AddCircleOutlineRoundedIcon />} onClick={() => setModalOpen(true)}>
              New Document
            </Button>
          </Stack>
        </CardContent>
      </Card>
      <Grid container spacing={2}>
        {Object.entries(docsByCategory).map(([category, items]) => (
          <Grid key={category} size={{ xs: 12, md: 6, lg: 4 }}>
            <Card sx={{ height: '100%' }}>
              <CardContent>
                <Typography variant="overline" sx={{
                  color: "primary.main"
                }}>{category}</Typography>
                <Stack spacing={0.8} sx={{ mt: 0.8 }}>
                  {items.map((item) => (
                    <CardActionArea key={item.id} onClick={() => setSelectedDoc(item)} sx={{ borderRadius: 1, p: 0.5 }}>
                      <Stack spacing={0.4}>
                        <Typography variant="body2" sx={{
                          fontWeight: 700
                        }}>{item.title}</Typography>
                        {item.tags ? <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{item.tags}</Typography> : null}
                        <Divider />
                      </Stack>
                    </CardActionArea>
                  ))}
                </Stack>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
      {Object.keys(docsByCategory).length === 0 ? (
        <Typography sx={{
          color: "text.secondary"
        }}>No documents in this project.</Typography>
      ) : null}
      <Dialog open={selectedDoc !== null} onClose={() => setSelectedDoc(null)} fullWidth maxWidth="lg">
        <DialogTitle>
          <Stack direction="row" spacing={1} sx={{
            alignItems: "center"
          }}>
            <Typography variant="h6" sx={{ flex: 1 }}>{selectedDoc?.title}</Typography>
            {selectedDoc?.category ? <Chip label={selectedDoc.category} size="small" color="primary" variant="outlined" /> : null}
            {selectedDoc?.tags ? <Chip label={selectedDoc.tags} size="small" variant="outlined" /> : null}
          </Stack>
        </DialogTitle>
        <DialogContent>
          <div data-color-mode="light">
            <MDEditor.Markdown source={selectedDoc?.contentMarkdown ?? ''} style={{ padding: '12px', minHeight: 120 }} />
          </div>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSelectedDoc(null)}>Close</Button>
        </DialogActions>
      </Dialog>
      <Dialog open={isModalOpen} onClose={() => setModalOpen(false)} fullWidth maxWidth="lg">
        <Stack component="form" onSubmit={handleCreateDocumentation}>
          <DialogTitle>New Document</DialogTitle>
          <DialogContent>
            <Stack spacing={1.2} sx={{ mt: 1 }}>
              <TextField label="Title" value={docTitle} onChange={(event) => setDocTitle(event.target.value)} required />
              <TextField label="Category" value={docCategory} onChange={(event) => setDocCategory(event.target.value)} required />
              <TextField label="Tags" value={docTags} onChange={(event) => setDocTags(event.target.value)} />
              <MarkdownField label="Content" value={docContent} onChange={setDocContent} required height={320} />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setModalOpen(false)}>Cancel</Button>
            <Button type="submit" variant="contained">Save document</Button>
          </DialogActions>
        </Stack>
      </Dialog>
    </Stack>
  );
}
