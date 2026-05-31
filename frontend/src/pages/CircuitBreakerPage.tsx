import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Collapse,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Grid,
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
import RefreshIcon from '@mui/icons-material/Refresh'
import RestartAltIcon from '@mui/icons-material/RestartAlt'
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined'
import HistoryOutlinedIcon from '@mui/icons-material/HistoryOutlined'
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline'
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline'
import PauseCircleOutlineIcon from '@mui/icons-material/PauseCircleOutline'
import { useCallback, useEffect, useState } from 'react'
import { useProjectContext } from '../context/useProjectContext'

const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:8480'

// ── Types ────────────────────────────────────────────────────────────────────

interface BreakerDto {
  boxId: string
  state: number           // 0=Closed, 1=Open, 2=HalfOpen
  stateLabel: string
  failureCount: number
  failureThreshold: number
  cooldownSeconds: number
  halfOpenMaxCalls: number
  halfOpenCallCount: number
  trippedAt: string | null
  lastFailureAt: string | null
  lastTransitionAt: string
  cooldownExpired: boolean
}

interface TransitionDto {
  id: string
  boxId: string
  fromState: number
  toState: number
  category: number | null
  reason: string
  triggeredAt: string
}

// ── Helpers ──────────────────────────────────────────────────────────────────

const STATE_META: Record<number, { label: string; color: 'success' | 'error' | 'warning'; icon: React.ReactNode }> = {
  0: { label: 'Closed', color: 'success', icon: <CheckCircleOutlineIcon fontSize="small" /> },
  1: { label: 'Open',   color: 'error',   icon: <ErrorOutlineIcon fontSize="small" /> },
  2: { label: 'Half-Open', color: 'warning', icon: <PauseCircleOutlineIcon fontSize="small" /> },
}

function stateMeta(state: number) {
  return STATE_META[state] ?? { label: 'Unknown', color: 'default', icon: null }
}

function StateChip({ state }: { state: number }) {
  const meta = stateMeta(state)
  return (
    <Chip
      icon={meta.icon as React.ReactElement}
      label={meta.label}
      color={meta.color as 'success' | 'error' | 'warning'}
      size="small"
      sx={{ fontWeight: 700, fontSize: 12 }}
    />
  )
}

function fmtDate(iso: string | null): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleString()
}

// ── API helpers ───────────────────────────────────────────────────────────────

async function apiFetch<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

// ── BreakerCard ───────────────────────────────────────────────────────────────

interface BreakerCardProps {
  breaker: BreakerDto
  onReset: (boxId: string) => void
  onConfig: (breaker: BreakerDto) => void
  onHistory: (boxId: string) => void
  resetting: string | null
}

function BreakerCard({ breaker, onReset, onConfig, onHistory, resetting }: BreakerCardProps) {
  const isResetting = resetting === breaker.boxId
  const meta = stateMeta(breaker.state)

  return (
    <Paper
      elevation={0}
      sx={{
        border: '1.5px solid',
        borderColor: breaker.state === 1 ? 'error.light' : breaker.state === 2 ? 'warning.light' : 'success.light',
        borderRadius: 3,
        p: 2.5,
        transition: 'all 0.25s',
        '&:hover': { boxShadow: 4 },
        background: breaker.state === 1
          ? 'linear-gradient(135deg, #fff5f5 0%, #fff 80%)'
          : breaker.state === 2
          ? 'linear-gradient(135deg, #fffde7 0%, #fff 80%)'
          : 'linear-gradient(135deg, #f0fff4 0%, #fff 80%)',
      }}
    >
      <Stack
        direction="row"
        sx={{
          justifyContent: "space-between",
          alignItems: "flex-start",
          mb: 1.5
        }}>
        <Box>
          <Stack
            direction="row"
            spacing={1}
            sx={{
              alignItems: "center",
              mb: 0.5
            }}>
            <StateChip state={breaker.state} />
            {breaker.state === 1 && breaker.cooldownExpired && (
              <Chip label="Cooldown Expired" size="small" color="warning" variant="outlined" />
            )}
          </Stack>
          <Typography
            variant="caption"
            sx={{
              color: "text.secondary",
              fontFamily: 'monospace',
              fontSize: 11
            }}>
            {breaker.boxId}
          </Typography>
        </Box>
        <Stack direction="row" spacing={0.5}>
          <Tooltip title="View history">
            <IconButton size="small" onClick={() => onHistory(breaker.boxId)}>
              <HistoryOutlinedIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title="Configure">
            <IconButton size="small" onClick={() => onConfig(breaker)}>
              <SettingsOutlinedIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title="Force reset to Closed">
            <IconButton
              size="small"
              color="primary"
              onClick={() => onReset(breaker.boxId)}
              disabled={isResetting || breaker.state === 0}
            >
              {isResetting ? <CircularProgress size={16} /> : <RestartAltIcon fontSize="small" />}
            </IconButton>
          </Tooltip>
        </Stack>
      </Stack>
      <Grid container spacing={1.5} sx={{ mt: 0.5 }}>
        {[
          { label: 'Failures', value: `${breaker.failureCount} / ${breaker.failureThreshold}` },
          { label: 'Cooldown', value: `${breaker.cooldownSeconds}s` },
          { label: 'Half-Open calls', value: `${breaker.halfOpenCallCount} / ${breaker.halfOpenMaxCalls}` },
          { label: 'Last failure', value: fmtDate(breaker.lastFailureAt) },
          { label: 'Tripped at', value: fmtDate(breaker.trippedAt) },
          { label: 'Last transition', value: fmtDate(breaker.lastTransitionAt) },
        ].map(({ label, value }) => (
          <Grid key={label} size={{xs: 6}}>
            <Typography
              variant="caption"
              sx={{
                color: "text.secondary",
                display: "block"
              }}>{label}</Typography>
            <Typography variant="body2" sx={{
              fontWeight: 600
            }}>{value}</Typography>
          </Grid>
        ))}
      </Grid>
    </Paper>
  );
}

// ── CircuitBreakerPage ────────────────────────────────────────────────────────

export function CircuitBreakerPage() {
  const { selectedProjectId } = useProjectContext()

  const [breakers, setBreakers] = useState<BreakerDto[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [resetting, setResetting] = useState<string | null>(null)

  // History dialog
  const [historyBoxId, setHistoryBoxId] = useState<string | null>(null)
  const [history, setHistory] = useState<TransitionDto[]>([])
  const [historyLoading, setHistoryLoading] = useState(false)

  // Config dialog
  const [configBreaker, setConfigBreaker] = useState<BreakerDto | null>(null)
  const [cfgThreshold, setCfgThreshold] = useState('')
  const [cfgCooldown, setCfgCooldown] = useState('')
  const [cfgHalfOpen, setCfgHalfOpen] = useState('')
  const [configSaving, setConfigSaving] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const data = await apiFetch<BreakerDto[]>('/api/breaker/all')
      setBreakers(data ?? [])
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to load circuit breakers')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    load()
  }, [selectedProjectId, load])

  async function handleReset(boxId: string) {
    setResetting(boxId)
    try {
      const updated = await apiFetch<BreakerDto>(`/api/breaker/${boxId}/reset`, 'POST')
      setBreakers((prev) => prev.map((b) => (b.boxId === boxId ? updated : b)))
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Reset failed')
    } finally {
      setResetting(null)
    }
  }

  async function handleHistory(boxId: string) {
    setHistoryBoxId(boxId)
    setHistoryLoading(true)
    try {
      const data = await apiFetch<TransitionDto[]>(`/api/breaker/${boxId}/history?limit=50`)
      setHistory(data ?? [])
    } catch {
      setHistory([])
    } finally {
      setHistoryLoading(false)
    }
  }

  function openConfig(breaker: BreakerDto) {
    setConfigBreaker(breaker)
    setCfgThreshold(String(breaker.failureThreshold))
    setCfgCooldown(String(breaker.cooldownSeconds))
    setCfgHalfOpen(String(breaker.halfOpenMaxCalls))
  }

  async function saveConfig() {
    if (!configBreaker) return
    setConfigSaving(true)
    try {
      const updated = await apiFetch<BreakerDto>(`/api/breaker/${configBreaker.boxId}/config`, 'POST', {
        failureThreshold: parseInt(cfgThreshold) || null,
        cooldownSeconds:  parseInt(cfgCooldown)  || null,
        halfOpenMaxCalls: parseInt(cfgHalfOpen)  || null,
      })
      setBreakers((prev) => prev.map((b) => (b.boxId === updated.boxId ? updated : b)))
      setConfigBreaker(null)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Config save failed')
    } finally {
      setConfigSaving(false)
    }
  }

  const stateLabel = (n: number) => stateMeta(n).label

  const closedCount = breakers.filter((b) => b.state === 0).length
  const openCount   = breakers.filter((b) => b.state === 1).length
  const halfCount   = breakers.filter((b) => b.state === 2).length

  return (
    <Stack spacing={3}>
      {/* Header row */}
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={1}
        sx={{
          justifyContent: "space-between",
          alignItems: { sm: 'center' }
        }}>
        <Box>
          <Typography variant="h5" sx={{
            fontWeight: 700
          }}>Circuit Breaker Dashboard</Typography>
          <Typography variant="body2" sx={{
            color: "text.secondary"
          }}>
            BOX3 — Per-box FSM resiliency protection (Closed → Open → Half-Open)
          </Typography>
        </Box>
        <Button
          startIcon={loading ? <CircularProgress size={16} /> : <RefreshIcon />}
          variant="outlined"
          onClick={load}
          disabled={loading}
        >
          Refresh
        </Button>
      </Stack>
      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}
      {/* Summary chips */}
      <Stack direction="row" spacing={1.5} sx={{
        flexWrap: "wrap"
      }}>
        <Chip icon={<CheckCircleOutlineIcon />} label={`${closedCount} Closed`} color="success" />
        <Chip icon={<ErrorOutlineIcon />} label={`${openCount} Open`} color={openCount > 0 ? 'error' : 'default'} />
        <Chip icon={<PauseCircleOutlineIcon />} label={`${halfCount} Half-Open`} color={halfCount > 0 ? 'warning' : 'default'} />
        <Chip label={`${breakers.length} Total boxes`} variant="outlined" />
      </Stack>
      <Divider />
      {/* Breaker cards */}
      {loading && breakers.length === 0 ? (
        <Box sx={{ display: 'grid', placeItems: 'center', py: 8 }}>
          <CircularProgress />
        </Box>
      ) : breakers.length === 0 ? (
        <Alert severity="info">No circuit breakers found. They are created automatically when a Box sends its first task.</Alert>
      ) : (
        <Grid container spacing={2}>
          {breakers.map((b) => (
            <Grid key={b.boxId} size={{xs: 12, sm: 6, lg: 4}}>
              <BreakerCard
                breaker={b}
                onReset={handleReset}
                onConfig={openConfig}
                onHistory={handleHistory}
                resetting={resetting}
              />
            </Grid>
          ))}
        </Grid>
      )}
      {/* History Dialog */}
      <Dialog open={!!historyBoxId} onClose={() => setHistoryBoxId(null)} fullWidth maxWidth="md">
        <DialogTitle>
          <Stack direction="row" spacing={1} sx={{
            alignItems: "center"
          }}>
            <HistoryOutlinedIcon />
            <span>Transition History</span>
          </Stack>
          <Typography
            variant="caption"
            sx={{
              color: "text.secondary",
              fontFamily: 'monospace'
            }}>
            {historyBoxId}
          </Typography>
        </DialogTitle>
        <DialogContent>
          {historyLoading ? (
            <Box sx={{ display: 'grid', placeItems: 'center', py: 4 }}>
              <CircularProgress />
            </Box>
          ) : history.length === 0 ? (
            <Alert severity="info">No transitions recorded yet.</Alert>
          ) : (
            <TableContainer>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>From</TableCell>
                    <TableCell>To</TableCell>
                    <TableCell>Reason</TableCell>
                    <TableCell>Triggered At</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {history.map((t) => (
                    <TableRow key={t.id} hover>
                      <TableCell><StateChip state={t.fromState} /></TableCell>
                      <TableCell><StateChip state={t.toState} /></TableCell>
                      <TableCell sx={{ maxWidth: 300, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        <Tooltip title={t.reason}>
                          <span>{t.reason}</span>
                        </Tooltip>
                      </TableCell>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>{fmtDate(t.triggeredAt)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setHistoryBoxId(null)}>Close</Button>
        </DialogActions>
      </Dialog>
      {/* Config Dialog */}
      <Dialog open={!!configBreaker} onClose={() => setConfigBreaker(null)} fullWidth maxWidth="xs">
        <DialogTitle>
          <Stack direction="row" spacing={1} sx={{
            alignItems: "center"
          }}>
            <SettingsOutlinedIcon />
            <span>Configure Breaker</span>
          </Stack>
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Failure Threshold"
              type="number"
              value={cfgThreshold}
              onChange={(e) => setCfgThreshold(e.target.value)}
              helperText="Consecutive failures before tripping (default: 3)"
              inputProps={{ min: 1 }}
              fullWidth
            />
            <TextField
              label="Cooldown (seconds)"
              type="number"
              value={cfgCooldown}
              onChange={(e) => setCfgCooldown(e.target.value)}
              helperText="Seconds to stay Open before probing (default: 300)"
              inputProps={{ min: 10 }}
              fullWidth
            />
            <TextField
              label="Half-Open Max Calls"
              type="number"
              value={cfgHalfOpen}
              onChange={(e) => setCfgHalfOpen(e.target.value)}
              helperText="Probe requests allowed in Half-Open (default: 1)"
              inputProps={{ min: 1 }}
              fullWidth
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfigBreaker(null)}>Cancel</Button>
          <Button
            variant="contained"
            onClick={saveConfig}
            disabled={configSaving}
            startIcon={configSaving ? <CircularProgress size={16} /> : undefined}
          >
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
}
