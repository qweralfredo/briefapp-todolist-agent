import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline'
import EditOutlinedIcon from '@mui/icons-material/EditOutlined'
import GroupOutlinedIcon from '@mui/icons-material/GroupOutlined'
import PersonAddOutlinedIcon from '@mui/icons-material/PersonAddOutlined'
import {
  Alert,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Select,
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
import type { BoxUser } from '../types'

const roles = ['viewer', 'editor', 'admin', 'owner'] as const

const roleColors: Record<string, 'default' | 'primary' | 'secondary' | 'error' | 'warning' | 'info' | 'success'> = {
  owner: 'error',
  admin: 'warning',
  editor: 'primary',
  viewer: 'default',
}

export function UsersPage() {
  const { selectedProjectId } = useProjectContext()
  const [users, setUsers] = useState<BoxUser[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editUser, setEditUser] = useState<BoxUser | null>(null)
  const [deleteUser, setDeleteUser] = useState<BoxUser | null>(null)
  const [form, setForm] = useState({ email: '', role: 'viewer', groups: '' })

  const fetchUsers = useCallback(async () => {
    if (!selectedProjectId) return
    setLoading(true)
    setError('')
    try {
      const data = await apiClient.listBoxUsers(selectedProjectId)
      setUsers(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load users')
    } finally {
      setLoading(false)
    }
  }, [selectedProjectId])

  useEffect(() => {
    fetchUsers()
  }, [fetchUsers])

  function openAdd() {
    setEditUser(null)
    setForm({ email: '', role: 'viewer', groups: '' })
    setDialogOpen(true)
  }

  function openEdit(user: BoxUser) {
    setEditUser(user)
    setForm({ email: user.email, role: user.role, groups: user.groups })
    setDialogOpen(true)
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedProjectId) return
    setError('')
    try {
      if (editUser) {
        await apiClient.updateBoxUser(selectedProjectId, editUser.id, {
          role: form.role,
          groups: form.groups,
        })
      } else {
        await apiClient.addBoxUser(selectedProjectId, {
          email: form.email,
          role: form.role,
          groups: form.groups || undefined,
        })
      }
      setDialogOpen(false)
      fetchUsers()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Operation failed')
    }
  }

  async function handleDelete() {
    if (!selectedProjectId || !deleteUser) return
    setError('')
    try {
      await apiClient.deleteBoxUser(selectedProjectId, deleteUser.id)
      setDeleteUser(null)
      fetchUsers()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Delete failed')
    }
  }

  if (!selectedProjectId) {
    return (
      <Paper elevation={0} sx={{ p: 4, textAlign: 'center', border: '1px dashed', borderColor: 'divider', borderRadius: 3 }}>
        <GroupOutlinedIcon sx={{ fontSize: 56, color: 'action.disabled', mb: 2 }} />
        <Typography variant="h6" sx={{
          color: "text.secondary"
        }}>Select a Box to manage users</Typography>
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
          <GroupOutlinedIcon color="primary" sx={{ fontSize: 28 }} />
          <Typography variant="h5" sx={{
            fontWeight: 600
          }}>Users</Typography>
          <Chip label={`${users.length}`} size="small" color="primary" variant="outlined" />
        </Stack>
        <Button variant="contained" startIcon={<PersonAddOutlinedIcon />} onClick={openAdd} sx={{ borderRadius: 2 }}>
          Invite User
        </Button>
      </Stack>
      {error && <Alert severity="error" variant="outlined" onClose={() => setError('')}>{error}</Alert>}
      <TableContainer component={Paper} elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2 }}>
        <Table size="small">
          <TableHead>
            <TableRow sx={{ bgcolor: 'rgba(15, 76, 129, 0.04)' }}>
              <TableCell sx={{ fontWeight: 600 }}>Email</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Role</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Groups</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Added</TableCell>
              <TableCell align="right" sx={{ fontWeight: 600 }}>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading && (
              <TableRow>
                <TableCell colSpan={5} sx={{ textAlign: 'center', py: 4, color: 'text.secondary' }}>
                  Loading users...
                </TableCell>
              </TableRow>
            )}
            {!loading && users.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} sx={{ textAlign: 'center', py: 4, color: 'text.secondary' }}>
                  No users yet. Click "Invite User" to add team members.
                </TableCell>
              </TableRow>
            )}
            {users.map((user) => (
              <TableRow key={user.id} hover sx={{ '&:last-child td': { borderBottom: 0 } }}>
                <TableCell>
                  <Typography variant="body2" sx={{
                    fontWeight: 500
                  }}>{user.email}</Typography>
                </TableCell>
                <TableCell>
                  <Chip
                    label={user.role}
                    size="small"
                    color={roleColors[user.role] ?? 'default'}
                    variant="outlined"
                    sx={{ textTransform: 'capitalize', fontWeight: 600 }}
                  />
                </TableCell>
                <TableCell>
                  {user.groups ? (
                    <Stack direction="row" spacing={0.5} sx={{
                      flexWrap: "wrap"
                    }}>
                      {user.groups.split(',').map((g) => (
                        <Chip key={g} label={g.trim()} size="small" variant="outlined" sx={{ fontSize: 11 }} />
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
                    {new Date(user.createdAt).toLocaleDateString()}
                  </Typography>
                </TableCell>
                <TableCell align="right">
                  <Tooltip title="Edit role/groups">
                    <IconButton size="small" onClick={() => openEdit(user)}>
                      <EditOutlinedIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Remove user">
                    <IconButton size="small" color="error" onClick={() => setDeleteUser(user)}>
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
          <DialogTitle>{editUser ? 'Edit User' : 'Invite User'}</DialogTitle>
          <DialogContent>
            <Stack spacing={2} sx={{ mt: 1 }}>
              <TextField
                label="Email"
                type="email"
                value={form.email}
                onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                required
                fullWidth
                disabled={!!editUser}
                autoFocus={!editUser}
              />
              <FormControl fullWidth>
                <InputLabel>Role</InputLabel>
                <Select
                  value={form.role}
                  label="Role"
                  onChange={(e) => setForm((f) => ({ ...f, role: e.target.value }))}
                >
                  {roles.map((r) => (
                    <MenuItem key={r} value={r} sx={{ textTransform: 'capitalize' }}>
                      {r}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              <TextField
                label="Groups (comma-separated)"
                value={form.groups}
                onChange={(e) => setForm((f) => ({ ...f, groups: e.target.value }))}
                fullWidth
                placeholder="e.g. developers, qa"
              />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setDialogOpen(false)}>Cancel</Button>
            <Button type="submit" variant="contained">{editUser ? 'Save' : 'Invite'}</Button>
          </DialogActions>
        </form>
      </Dialog>
      {/* Delete Confirmation */}
      <Dialog open={!!deleteUser} onClose={() => setDeleteUser(null)}>
        <DialogTitle>Remove User</DialogTitle>
        <DialogContent>
          <Typography>
            Are you sure you want to remove <strong>{deleteUser?.email}</strong> from this box?
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteUser(null)}>Cancel</Button>
          <Button onClick={handleDelete} variant="contained" color="error">Remove</Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
}
