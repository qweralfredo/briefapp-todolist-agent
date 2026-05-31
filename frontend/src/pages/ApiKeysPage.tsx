import AddOutlinedIcon from '@mui/icons-material/AddOutlined'
import ContentCopyIcon from '@mui/icons-material/ContentCopy'
import KeyOutlinedIcon from '@mui/icons-material/KeyOutlined'
import BlockIcon from '@mui/icons-material/Block'
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
  Radio,
  RadioGroup,
  FormControlLabel,
  FormControl,
  FormLabel,
  Checkbox,
  FormGroup,
} from '@mui/material'
import { useCallback, useEffect, useState } from 'react'
import { apiClient } from '../api/client'
import { useProjectContext } from '../context/useProjectContext'
import type { BoxApiKey } from '../types'

const PREDEFINED_SCOPES = [
  { id: 'read', label: 'Read (View items, sprints, docs)' },
  { id: 'write', label: 'Write (Create/Edit items)' },
  { id: 'mcp', label: 'MCP (Use Model Context Protocol tools)' },
  { id: 'admin', label: 'Admin (Manage project settings)' },
  { id: 'execute', label: 'Execute (Trigger agents & workflows)' },
]

export function ApiKeysPage() {
  const { selectedProjectId } = useProjectContext()
  const [keys, setKeys] = useState<BoxApiKey[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [dialogOpen, setDialogOpen] = useState(false)
  const [revokeKey, setRevokeKey] = useState<BoxApiKey | null>(null)
  
  const [form, setForm] = useState({ name: '', scopes: 'read' })
  const [scopeMode, setScopeMode] = useState('read')
  
  const [newKey, setNewKey] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  const fetchKeys = useCallback(async () => {
    if (!selectedProjectId) return
    setLoading(true)
    setError('')
    try {
      const data = await apiClient.listApiKeys(selectedProjectId)
      setKeys(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load API keys')
    } finally {
      setLoading(false)
    }
  }, [selectedProjectId])

  useEffect(() => {
    fetchKeys()
  }, [fetchKeys])

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedProjectId) return
    setError('')
    try {
      const result = await apiClient.createApiKey(selectedProjectId, {
        name: form.name,
        scopes: form.scopes || 'read',
      })
      setDialogOpen(false)
      setNewKey(result.key ?? null)
      setCopied(false)
      fetchKeys()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create key')
    }
  }

  async function handleRevoke() {
    if (!selectedProjectId || !revokeKey) return
    setError('')
    try {
      await apiClient.revokeApiKey(selectedProjectId, revokeKey.id)
      setRevokeKey(null)
      fetchKeys()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Revoke failed')
    }
  }

  function handleCopy() {
    if (newKey) {
      navigator.clipboard.writeText(newKey)
      setCopied(true)
    }
  }

  const activeKeys = keys.filter((k) => !k.isRevoked)
  const revokedKeys = keys.filter((k) => k.isRevoked)

  if (!selectedProjectId) {
    return (
      <Paper elevation={0} sx={{ p: 4, textAlign: 'center', border: '1px dashed', borderColor: 'divider', borderRadius: 3 }}>
        <KeyOutlinedIcon sx={{ fontSize: 56, color: 'action.disabled', mb: 2 }} />
        <Typography variant="h6" sx={{
          color: "text.secondary"
        }}>Select a Box to manage API keys</Typography>
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
          <KeyOutlinedIcon color="primary" sx={{ fontSize: 28 }} />
          <Typography variant="h5" sx={{
            fontWeight: 600
          }}>API Keys</Typography>
          <Chip label={`${activeKeys.length} active`} size="small" color="primary" variant="outlined" />
        </Stack>
        <Button variant="contained" startIcon={<AddOutlinedIcon />} onClick={() => { setForm({ name: '', scopes: 'read' }); setScopeMode('read'); setDialogOpen(true); }} sx={{ borderRadius: 2 }}>
          Generate Key
        </Button>
      </Stack>
      {error && <Alert severity="error" variant="outlined" onClose={() => setError('')}>{error}</Alert>}
      {/* Key revealed banner */}
      {newKey && (
        <Alert
          severity="success"
          variant="filled"
          onClose={() => setNewKey(null)}
          action={
            <Tooltip title={copied ? 'Copied!' : 'Copy to clipboard'}>
              <IconButton size="small" color="inherit" onClick={handleCopy}>
                <ContentCopyIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          }
        >
          <Typography
            variant="body2"
            sx={{
              fontWeight: 600,
              mb: 0.5
            }}>
            Your API key (shown only once):
          </Typography>
          <Typography
            variant="body2"
            sx={{
              fontFamily: "monospace",
              wordBreak: 'break-all'
            }}>
            {newKey}
          </Typography>
        </Alert>
      )}
      <TableContainer component={Paper} elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2 }}>
        <Table size="small">
          <TableHead>
            <TableRow sx={{ bgcolor: 'rgba(15, 76, 129, 0.04)' }}>
              <TableCell sx={{ fontWeight: 600 }}>Name</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Key</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Scopes</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Created</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
              <TableCell align="right" sx={{ fontWeight: 600 }}>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading && (
              <TableRow>
                <TableCell colSpan={6} sx={{ textAlign: 'center', py: 4, color: 'text.secondary' }}>Loading…</TableCell>
              </TableRow>
            )}
            {!loading && keys.length === 0 && (
              <TableRow>
                <TableCell colSpan={6} sx={{ textAlign: 'center', py: 4, color: 'text.secondary' }}>No API keys yet.</TableCell>
              </TableRow>
            )}
            {[...activeKeys, ...revokedKeys].map((k) => (
              <TableRow key={k.id} hover sx={{ opacity: k.isRevoked ? 0.5 : 1, '&:last-child td': { borderBottom: 0 } }}>
                <TableCell>
                  <Typography variant="body2" sx={{
                    fontWeight: 500
                  }}>{k.name}</Typography>
                </TableCell>
                <TableCell>
                  <Typography
                    variant="body2"
                    sx={{
                      fontFamily: "monospace",
                      color: "text.secondary",
                      fontSize: 12
                    }}>
                    {k.prefix}••••••••
                  </Typography>
                </TableCell>
                <TableCell>
                  <Stack direction="row" spacing={0.5} sx={{
                    flexWrap: "wrap",
                    gap: 0.5
                  }}>
                    {k.scopes.split(',').map((s) => (
                      <Chip key={s} label={s.trim()} size="small" variant="outlined" sx={{ fontSize: 11 }} />
                    ))}
                  </Stack>
                </TableCell>
                <TableCell>
                  <Typography
                    variant="body2"
                    sx={{
                      color: "text.secondary",
                      fontSize: 12
                    }}>
                    {new Date(k.createdAt).toLocaleDateString()}
                  </Typography>
                </TableCell>
                <TableCell>
                  <Chip
                    label={k.isRevoked ? 'Revoked' : 'Active'}
                    size="small"
                    color={k.isRevoked ? 'error' : 'success'}
                    variant="outlined"
                    sx={{ fontWeight: 600, fontSize: 11 }}
                  />
                </TableCell>
                <TableCell align="right">
                  {!k.isRevoked && (
                    <Tooltip title="Revoke key">
                      <IconButton size="small" color="error" onClick={() => setRevokeKey(k)}>
                        <BlockIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      {/* Create Dialog */}
      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} fullWidth maxWidth="sm">
        <form onSubmit={handleCreate}>
          <DialogTitle>Generate API Key</DialogTitle>
          <DialogContent>
            <Stack spacing={3} sx={{ mt: 1 }}>
              <TextField
                label="Name"
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                required
                fullWidth
                autoFocus
                placeholder="e.g. MCP Connection, CI/CD Pipeline"
              />
              
              <FormControl component="fieldset">
                <FormLabel component="legend" sx={{ mb: 1, fontWeight: 500, color: 'text.primary' }}>Access Level</FormLabel>
                <RadioGroup
                  value={scopeMode}
                  onChange={(e) => {
                    const mode = e.target.value;
                    setScopeMode(mode);
                    if (mode === 'read') setForm(f => ({ ...f, scopes: 'read' }));
                    else if (mode === 'write') setForm(f => ({ ...f, scopes: 'read,write' }));
                    else if (mode === 'full') setForm(f => ({ ...f, scopes: '*' }));
                  }}
                >
                  <FormControlLabel value="read" control={<Radio />} label="Read Only (Safe)" />
                  <FormControlLabel value="write" control={<Radio />} label="Read & Write (Standard)" />
                  <FormControlLabel value="full" control={<Radio />} label="Full Access (*)" />
                  <FormControlLabel value="custom" control={<Radio />} label="Granular / Custom" />
                </RadioGroup>
              </FormControl>

              {scopeMode === 'custom' && (
                <FormGroup sx={{ pl: 2, borderLeft: '2px solid', borderColor: 'divider' }}>
                  {PREDEFINED_SCOPES.map(s => (
                    <FormControlLabel
                      key={s.id}
                      control={
                        <Checkbox 
                          checked={form.scopes.split(',').map(x=>x.trim()).includes(s.id)}
                          onChange={(e) => {
                            const current = form.scopes.split(',').map(x=>x.trim()).filter(Boolean);
                            if (e.target.checked) {
                              if (!current.includes(s.id)) current.push(s.id);
                            } else {
                              const idx = current.indexOf(s.id);
                              if (idx > -1) current.splice(idx, 1);
                            }
                            setForm(f => ({ ...f, scopes: current.join(',') }));
                          }}
                        />
                      }
                      label={s.label}
                    />
                  ))}
                  <TextField 
                    size="small" 
                    label="Raw Scopes (e.g., custom_scope)" 
                    value={form.scopes}
                    onChange={(e) => setForm(f => ({ ...f, scopes: e.target.value }))}
                    sx={{ mt: 2 }}
                    fullWidth
                    helperText="Comma-separated internal scope labels."
                  />
                </FormGroup>
              )}
            </Stack>
          </DialogContent>
          <DialogActions sx={{ px: 3, pb: 3 }}>
            <Button onClick={() => setDialogOpen(false)} color="inherit">Cancel</Button>
            <Button type="submit" variant="contained" disabled={!form.name || !form.scopes}>Generate</Button>
          </DialogActions>
        </form>
      </Dialog>
      {/* Revoke Confirmation */}
      <Dialog open={!!revokeKey} onClose={() => setRevokeKey(null)}>
        <DialogTitle>Revoke API Key</DialogTitle>
        <DialogContent>
          <Typography>
            Are you sure you want to revoke <strong>{revokeKey?.name}</strong>? This action cannot be undone.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRevokeKey(null)}>Cancel</Button>
          <Button onClick={handleRevoke} variant="contained" color="error">Revoke</Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
}
