import AddOutlinedIcon from '@mui/icons-material/AddOutlined'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline'
import EditOutlinedIcon from '@mui/icons-material/EditOutlined'
import MemoryOutlinedIcon from '@mui/icons-material/MemoryOutlined'
import SearchIcon from '@mui/icons-material/Search'
import {
  Alert,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  InputAdornment,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import { useCallback, useEffect, useState } from 'react'
import { apiClient } from '../api/client'
import { useProjectContext } from '../context/useProjectContext'
import type { MemoryItem } from '../types'

export function MemoryBoxPage() {
  const { selectedProjectId } = useProjectContext()
  const [items, setItems] = useState<MemoryItem[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editItem, setEditItem] = useState<MemoryItem | null>(null)
  const [deleteItem, setDeleteItem] = useState<MemoryItem | null>(null)
  const [form, setForm] = useState({ key: '', value: '', tags: '' })

  const fetchItems = useCallback(async () => {
    if (!selectedProjectId) return
    setLoading(true)
    setError('')
    try {
      const data = await apiClient.listMemory(selectedProjectId)
      setItems(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load memory items')
    } finally {
      setLoading(false)
    }
  }, [selectedProjectId])

  useEffect(() => {
    fetchItems()
  }, [fetchItems])

  const filtered = items.filter((item) => {
    if (!search) return true
    const s = search.toLowerCase()
    return item.key.toLowerCase().includes(s) || item.value.toLowerCase().includes(s) || item.tags.toLowerCase().includes(s)
  })

  function openAdd() {
    setEditItem(null)
    setForm({ key: '', value: '', tags: '' })
    setDialogOpen(true)
  }

  function openEdit(item: MemoryItem) {
    setEditItem(item)
    setForm({ key: item.key, value: item.value, tags: item.tags })
    setDialogOpen(true)
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedProjectId) return
    setError('')
    try {
      await apiClient.upsertMemory(selectedProjectId, {
        key: form.key.trim(),
        value: form.value,
        tags: form.tags,
      })
      setDialogOpen(false)
      fetchItems()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Operation failed')
    }
  }

  async function handleDelete() {
    if (!selectedProjectId || !deleteItem) return
    setError('')
    try {
      await apiClient.deleteMemory(selectedProjectId, deleteItem.key)
      setDeleteItem(null)
      fetchItems()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Delete failed')
    }
  }

  function truncate(text: string, max: number) {
    return text.length > max ? text.substring(0, max) + '…' : text
  }

  if (!selectedProjectId) {
    return (
      <Paper elevation={0} sx={{ p: 4, textAlign: 'center', border: '1px dashed', borderColor: 'divider', borderRadius: 3 }}>
        <MemoryOutlinedIcon sx={{ fontSize: 56, color: 'action.disabled', mb: 2 }} />
        <Typography variant="h6" sx={{
          color: "text.secondary"
        }}>Select a Box to manage memory</Typography>
      </Paper>
    );
  }

  return (
    <Stack spacing={2.5}>
      <Stack
        direction="row"
        sx={{
          justifyContent: "space-between",
          alignItems: "center"
        }}>
        <Stack direction="row" spacing={1} sx={{
          alignItems: "center"
        }}>
          <MemoryOutlinedIcon color="primary" sx={{ fontSize: 28 }} />
          <Typography variant="h5" sx={{
            fontWeight: 600
          }}>Memory-Box</Typography>
          <Chip label={`${items.length} keys`} size="small" color="primary" variant="outlined" />
        </Stack>
        <Button variant="contained" startIcon={<AddOutlinedIcon />} onClick={openAdd} sx={{ borderRadius: 2 }}>
          Add Entry
        </Button>
      </Stack>
      <TextField
        size="small"
        placeholder="Search keys, values, tags…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        InputProps={{
          startAdornment: (
            <InputAdornment position="start">
              <SearchIcon fontSize="small" color="action" />
            </InputAdornment>
          ),
        }}
        sx={{ maxWidth: 400 }}
      />
      {error && <Alert severity="error" variant="outlined" onClose={() => setError('')}>{error}</Alert>}
      <TableContainer component={Paper} elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2 }}>
        <Table size="small">
          <TableHead>
            <TableRow sx={{ bgcolor: 'rgba(15, 76, 129, 0.04)' }}>
              <TableCell sx={{ fontWeight: 600, width: '25%' }}>Key</TableCell>
              <TableCell sx={{ fontWeight: 600, width: '40%' }}>Value</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Tags</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Updated</TableCell>
              <TableCell align="right" sx={{ fontWeight: 600 }}>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading && (
              <TableRow>
                <TableCell colSpan={5} sx={{ textAlign: 'center', py: 4, color: 'text.secondary' }}>
                  Loading…
                </TableCell>
              </TableRow>
            )}
            {!loading && filtered.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} sx={{ textAlign: 'center', py: 4, color: 'text.secondary' }}>
                  {items.length === 0 ? 'No memory entries yet. Click "Add Entry" to create one.' : 'No results match your search.'}
                </TableCell>
              </TableRow>
            )}
            {filtered.map((item) => (
              <TableRow key={item.id} hover sx={{ '&:last-child td': { borderBottom: 0 } }}>
                <TableCell>
                  <Typography
                    variant="body2"
                    sx={{
                      fontWeight: 600,
                      fontFamily: "monospace",
                      fontSize: 13
                    }}>
                    {item.key}
                  </Typography>
                </TableCell>
                <TableCell>
                  <Tooltip title={item.value.length > 80 ? item.value : ''} arrow>
                    <Typography variant="body2" sx={{ fontSize: 13, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
                      {truncate(item.value, 80)}
                    </Typography>
                  </Tooltip>
                </TableCell>
                <TableCell>
                  {item.tags ? (
                    <Stack direction="row" spacing={0.5} sx={{
                      flexWrap: "wrap"
                    }}>
                      {item.tags.split(',').map((t) => (
                        <Chip key={t} label={t.trim()} size="small" variant="outlined" sx={{ fontSize: 11 }} />
                      ))}
                    </Stack>
                  ) : (
                    <Typography variant="body2" sx={{
                      color: "text.disabled"
                    }}>—</Typography>
                  )}
                </TableCell>
                <TableCell>
                  <Typography
                    variant="body2"
                    sx={{
                      color: "text.secondary",
                      fontSize: 12
                    }}>
                    {new Date(item.updatedAt).toLocaleDateString()}
                  </Typography>
                </TableCell>
                <TableCell align="right">
                  <Tooltip title="Edit">
                    <IconButton size="small" onClick={() => openEdit(item)}>
                      <EditOutlinedIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Delete">
                    <IconButton size="small" color="error" onClick={() => setDeleteItem(item)}>
                      <DeleteOutlineIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      {/* Add / Edit Dialog */}
      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} fullWidth maxWidth="sm">
        <form onSubmit={handleSubmit}>
          <DialogTitle>{editItem ? `Edit: ${editItem.key}` : 'Add Memory Entry'}</DialogTitle>
          <DialogContent>
            <Stack spacing={2} sx={{ mt: 1 }}>
              <TextField
                label="Key"
                value={form.key}
                onChange={(e) => setForm((f) => ({ ...f, key: e.target.value }))}
                required
                fullWidth
                disabled={!!editItem}
                autoFocus={!editItem}
                placeholder="e.g. user.preferences.theme"
                InputProps={{ sx: { fontFamily: 'monospace' } }}
              />
              <TextField
                label="Value"
                value={form.value}
                onChange={(e) => setForm((f) => ({ ...f, value: e.target.value }))}
                required
                fullWidth
                multiline
                minRows={3}
                maxRows={10}
                placeholder="Any text, JSON, or data…"
              />
              <TextField
                label="Tags (comma-separated)"
                value={form.tags}
                onChange={(e) => setForm((f) => ({ ...f, tags: e.target.value }))}
                fullWidth
                placeholder="e.g. chatbot, preferences"
              />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setDialogOpen(false)}>Cancel</Button>
            <Button type="submit" variant="contained">{editItem ? 'Save' : 'Create'}</Button>
          </DialogActions>
        </form>
      </Dialog>
      {/* Delete Confirmation */}
      <Dialog open={!!deleteItem} onClose={() => setDeleteItem(null)}>
        <DialogTitle>Delete Memory Entry</DialogTitle>
        <DialogContent>
          <Typography>
            Are you sure you want to delete <strong>{deleteItem?.key}</strong>?
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteItem(null)}>Cancel</Button>
          <Button onClick={handleDelete} variant="contained" color="error">Delete</Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
}
