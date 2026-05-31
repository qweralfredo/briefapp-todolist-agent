import AddOutlinedIcon from '@mui/icons-material/AddOutlined'
import BlockIcon from '@mui/icons-material/Block'
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline'
import EditOutlinedIcon from '@mui/icons-material/EditOutlined'
import RuleIcon from '@mui/icons-material/Rule'
import {
  Alert,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
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
import type { AllowListEntry } from '../types'

export function AllowListPage() {
  const { selectedProjectId } = useProjectContext()
  const [entries, setEntries] = useState<AllowListEntry[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editEntry, setEditEntry] = useState<AllowListEntry | null>(null)
  const [deleteEntry, setDeleteEntry] = useState<AllowListEntry | null>(null)
  const [form, setForm] = useState({ appName: '', callbackUrl: '', scopes: 'read' })

  const fetchEntries = useCallback(async () => {
    if (!selectedProjectId) return
    setLoading(true)
    setError('')
    try {
      const data = await apiClient.listAllowList(selectedProjectId)
      setEntries(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load allow-list')
    } finally {
      setLoading(false)
    }
  }, [selectedProjectId])

  useEffect(() => {
    fetchEntries()
  }, [fetchEntries])

  function openAdd() {
    setEditEntry(null)
    setForm({ appName: '', callbackUrl: '', scopes: 'read' })
    setDialogOpen(true)
  }

  function openEdit(entry: AllowListEntry) {
    setEditEntry(entry)
    setForm({ appName: entry.appName, callbackUrl: entry.callbackUrl, scopes: entry.scopes })
    setDialogOpen(true)
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedProjectId) return
    setError('')
    try {
      await apiClient.upsertAllowList(selectedProjectId, {
        appName: form.appName.trim(),
        callbackUrl: form.callbackUrl.trim(),
        scopes: form.scopes.trim() || 'read',
      })
      setDialogOpen(false)
      fetchEntries()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Operation failed')
    }
  }

  async function handleToggle(entry: AllowListEntry) {
    if (!selectedProjectId) return
    setError('')
    try {
      await apiClient.toggleAllowList(selectedProjectId, entry.id)
      fetchEntries()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Toggle status failed')
    }
  }

  async function handleDelete() {
    if (!selectedProjectId || !deleteEntry) return
    setError('')
    try {
      await apiClient.deleteAllowList(selectedProjectId, deleteEntry.id)
      setDeleteEntry(null)
      fetchEntries()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Delete failed')
    }
  }

  if (!selectedProjectId) {
    return (
      <Paper elevation={0} sx={{ p: 4, textAlign: 'center', border: '1px dashed', borderColor: 'divider', borderRadius: 3 }}>
        <RuleIcon sx={{ fontSize: 56, color: 'action.disabled', mb: 2 }} />
        <Typography variant="h6" sx={{
          color: "text.secondary"
        }}>Select a Box to manage allow-list</Typography>
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
          <RuleIcon color="primary" sx={{ fontSize: 28 }} />
          <Typography variant="h5" sx={{
            fontWeight: 600
          }}>Allow-List</Typography>
          <Chip label={`${entries.length} apps`} size="small" color="primary" variant="outlined" />
        </Stack>
        <Button variant="contained" startIcon={<AddOutlinedIcon />} onClick={openAdd} sx={{ borderRadius: 2 }}>
          Whitelist App
        </Button>
      </Stack>
      {error && <Alert severity="error" variant="outlined" onClose={() => setError('')}>{error}</Alert>}
      <TableContainer component={Paper} elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2 }}>
        <Table size="small">
          <TableHead>
            <TableRow sx={{ bgcolor: 'rgba(15, 76, 129, 0.04)' }}>
              <TableCell sx={{ fontWeight: 600 }}>App Name</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Scopes</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Callback URL</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
              <TableCell align="right" sx={{ fontWeight: 600 }}>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading && (
              <TableRow>
                <TableCell colSpan={5} sx={{ textAlign: 'center', py: 4, color: 'text.secondary' }}>Loading…</TableCell>
              </TableRow>
            )}
            {!loading && entries.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} sx={{ textAlign: 'center', py: 4, color: 'text.secondary' }}>No apps whitelisted yet.</TableCell>
              </TableRow>
            )}
            {entries.map((entry) => (
              <TableRow key={entry.id} hover sx={{ opacity: entry.isActive ? 1 : 0.6, '&:last-child td': { borderBottom: 0 } }}>
                <TableCell>
                  <Typography variant="body2" sx={{
                    fontWeight: 600
                  }}>{entry.appName}</Typography>
                </TableCell>
                <TableCell>
                  <Stack direction="row" spacing={0.5} sx={{
                    flexWrap: "wrap"
                  }}>
                    {entry.scopes.split(',').map((s) => (
                      <Chip key={s} label={s.trim()} size="small" variant="outlined" sx={{ fontSize: 11 }} />
                    ))}
                  </Stack>
                </TableCell>
                <TableCell>
                  {entry.callbackUrl ? (
                    <Typography
                      variant="body2"
                      sx={{
                        color: "text.secondary",
                        fontSize: 13,
                        wordBreak: 'break-all'
                      }}>
                      {entry.callbackUrl}
                    </Typography>
                  ) : (
                    <Typography variant="body2" sx={{
                      color: "text.disabled"
                    }}>—</Typography>
                  )}
                </TableCell>
                <TableCell>
                  <Chip
                    label={entry.isActive ? 'Active' : 'Disabled'}
                    size="small"
                    color={entry.isActive ? 'success' : 'default'}
                    variant={entry.isActive ? 'filled' : 'outlined'}
                    sx={{ fontWeight: 600, fontSize: 11 }}
                  />
                </TableCell>
                <TableCell align="right">
                  <Tooltip title={entry.isActive ? 'Disable App' : 'Enable App'}>
                    <IconButton size="small" onClick={() => handleToggle(entry)}>
                      {entry.isActive ? <BlockIcon fontSize="small" /> : <CheckCircleOutlineIcon fontSize="small" color="success" />}
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Edit App">
                    <IconButton size="small" onClick={() => openEdit(entry)}>
                      <EditOutlinedIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Remove App">
                    <IconButton size="small" color="error" onClick={() => setDeleteEntry(entry)}>
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
          <DialogTitle>{editEntry ? `Edit: ${editEntry.appName}` : 'Whitelist App'}</DialogTitle>
          <DialogContent>
            <Stack spacing={2} sx={{ mt: 1 }}>
              <TextField
                label="App Name"
                value={form.appName}
                onChange={(e) => setForm((f) => ({ ...f, appName: e.target.value }))}
                required
                fullWidth
                disabled={!!editEntry}
                autoFocus={!editEntry}
                placeholder="e.g. Acme Billing Dashboard"
              />
              <TextField
                label="Callback URL (Optional)"
                value={form.callbackUrl}
                onChange={(e) => setForm((f) => ({ ...f, callbackUrl: e.target.value }))}
                fullWidth
                placeholder="https://acme.com/oauth/callback"
              />
              <TextField
                label="Scopes (comma-separated)"
                value={form.scopes}
                onChange={(e) => setForm((f) => ({ ...f, scopes: e.target.value }))}
                fullWidth
                placeholder="read,write,admin"
              />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setDialogOpen(false)}>Cancel</Button>
            <Button type="submit" variant="contained">{editEntry ? 'Save' : 'Whitelist'}</Button>
          </DialogActions>
        </form>
      </Dialog>
      {/* Delete Confirmation */}
      <Dialog open={!!deleteEntry} onClose={() => setDeleteEntry(null)}>
        <DialogTitle>Remove App from Allow-List</DialogTitle>
        <DialogContent>
          <Typography>
            Are you sure you want to remove <strong>{deleteEntry?.appName}</strong>?
            This will block the app from connecting immediately.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteEntry(null)}>Cancel</Button>
          <Button onClick={handleDelete} variant="contained" color="error">Remove</Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
}
