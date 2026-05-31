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
import type { KnowledgeWikiPage } from '../types'
import { groupByCategory } from '../types'
import { KnowledgeNav } from './KnowledgeNav'

export function WikiPage() {
  const { selectedProjectId, selectedProject, knowledge, refreshProjectViews } = useProjectContext()
  const [isModalOpen, setModalOpen] = useState(false)
  const [wikiTitle, setWikiTitle] = useState('')
  const [wikiCategory, setWikiCategory] = useState('How-To')
  const [wikiTags, setWikiTags] = useState('')
  const [wikiContent, setWikiContent] = useState('')

  const [selectedWiki, setSelectedWiki] = useState<KnowledgeWikiPage | null>(null)

  const wikiByCategory = useMemo(() => groupByCategory(knowledge?.wikiPages ?? []), [knowledge])

  async function handleCreateWiki(event: React.FormEvent) {
    event.preventDefault()
    if (!selectedProjectId || !wikiTitle.trim() || !wikiContent.trim()) {
      return
    }

    await apiClient.createWikiPage(selectedProjectId, {
      title: wikiTitle,
      contentMarkdown: wikiContent,
      category: wikiCategory,
      tags: wikiTags,
    })

    setWikiTitle('')
    setWikiCategory('How-To')
    setWikiTags('')
    setWikiContent('')
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
              Select a project to register and review wiki pages.
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
              <Typography variant="h6">Project Wiki</Typography>
              <Typography sx={{
                color: "text.secondary"
              }}>
                Living content for standards, how-to and operational agreements.
              </Typography>
            </Stack>
            <Button variant="contained" startIcon={<AddCircleOutlineRoundedIcon />} onClick={() => setModalOpen(true)}>
              New Wiki
            </Button>
          </Stack>
        </CardContent>
      </Card>
      <Grid container spacing={2}>
        {Object.entries(wikiByCategory).map(([category, items]) => (
          <Grid key={category} size={{ xs: 12, md: 6, lg: 4 }}>
            <Card sx={{ height: '100%' }}>
              <CardContent>
                <Typography variant="overline" sx={{
                  color: "primary.main"
                }}>{category}</Typography>
                <Stack spacing={0.8} sx={{ mt: 0.8 }}>
                  {items.map((item) => (
                    <CardActionArea key={item.id} onClick={() => setSelectedWiki(item)} sx={{ borderRadius: 1, p: 0.5 }}>
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
      {Object.keys(wikiByCategory).length === 0 ? (
        <Typography sx={{
          color: "text.secondary"
        }}>No wiki entries in this project.</Typography>
      ) : null}
      <Dialog open={selectedWiki !== null} onClose={() => setSelectedWiki(null)} fullWidth maxWidth="lg">
        <DialogTitle>
          <Stack direction="row" spacing={1} sx={{
            alignItems: "center"
          }}>
            <Typography variant="h6" sx={{ flex: 1 }}>{selectedWiki?.title}</Typography>
            {selectedWiki?.category ? <Chip label={selectedWiki.category} size="small" color="primary" variant="outlined" /> : null}
            {selectedWiki?.tags ? <Chip label={selectedWiki.tags} size="small" variant="outlined" /> : null}
          </Stack>
        </DialogTitle>
        <DialogContent>
          <div data-color-mode="light">
            <MDEditor.Markdown source={selectedWiki?.contentMarkdown ?? ''} style={{ padding: '12px', minHeight: 120 }} />
          </div>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSelectedWiki(null)}>Close</Button>
        </DialogActions>
      </Dialog>
      <Dialog open={isModalOpen} onClose={() => setModalOpen(false)} fullWidth maxWidth="lg">
        <Stack component="form" onSubmit={handleCreateWiki}>
          <DialogTitle>New Wiki</DialogTitle>
          <DialogContent>
            <Stack spacing={1.2} sx={{ mt: 1 }}>
              <TextField label="Title" value={wikiTitle} onChange={(event) => setWikiTitle(event.target.value)} required />
              <TextField label="Category" value={wikiCategory} onChange={(event) => setWikiCategory(event.target.value)} required />
              <TextField label="Tags" value={wikiTags} onChange={(event) => setWikiTags(event.target.value)} />
              <MarkdownField label="Content" value={wikiContent} onChange={setWikiContent} required height={320} />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setModalOpen(false)}>Cancel</Button>
            <Button type="submit" variant="contained">Save wiki</Button>
          </DialogActions>
        </Stack>
      </Dialog>
    </Stack>
  );
}
