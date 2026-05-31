import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline'
import CloudUploadOutlinedIcon from '@mui/icons-material/CloudUploadOutlined'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline'
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline'
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty'
import InsertDriveFileOutlinedIcon from '@mui/icons-material/InsertDriveFileOutlined'
import SearchOutlinedIcon from '@mui/icons-material/SearchOutlined'
import StorageOutlinedIcon from '@mui/icons-material/StorageOutlined'
import SyncIcon from '@mui/icons-material/Sync'
import TuneIcon from '@mui/icons-material/Tune'
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
  Grid,
  IconButton,
  LinearProgress,
  Paper,
  Stack,
  Tab,
  Tabs,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import { useCallback, useEffect, useRef, useState } from 'react'
import { apiClient } from '../api/client'
import type { BatchIngestJob, BatchStats, ContextChunkFile, ContextSearchResult } from '../types'

// ── Supported Extensions ───────────────────────────────────────────
const SUPPORTED_EXTS = [
  '.pdf', '.docx', '.xlsx', '.csv', '.txt', '.md', '.json', '.yaml', '.yml',
  '.html', '.xml', '.py', '.js', '.ts', '.tsx', '.jsx', '.css', '.sql', '.go',
  '.rs', '.cpp', '.c', '.h', '.java', '.kt', '.rb', '.php', '.sh', '.bat', '.ps1', '.log',
]

// ── Status colour helper ────────────────────────────────────────────
function jobStatusColor(status: BatchIngestJob['status']) {
  switch (status) {
    case 'done': return 'success'
    case 'failed': return 'error'
    case 'processing': return 'info'
    default: return 'default'
  }
}

function jobStatusIcon(status: BatchIngestJob['status']) {
  switch (status) {
    case 'done': return <CheckCircleOutlineIcon fontSize="small" />
    case 'failed': return <ErrorOutlineIcon fontSize="small" />
    case 'processing': return <SyncIcon fontSize="small" sx={{ animation: 'spin 1.2s linear infinite', '@keyframes spin': { to: { transform: 'rotate(360deg)' } } }} />
    default: return <HourglassEmptyIcon fontSize="small" />
  }
}

// ── File Size Formatter ─────────────────────────────────────────────
function formatBytes(b: number) {
  if (b < 1024) return `${b} B`
  if (b < 1024 ** 2) return `${(b / 1024).toFixed(1)} KB`
  return `${(b / 1024 ** 2).toFixed(2)} MB`
}

// ── Dropzone Component ─────────────────────────────────────────────
function FileDropzone({ onFiles }: { onFiles: (files: File[]) => void }) {
  const [dragging, setDragging] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  function handleDrop(e: React.DragEvent) {
    e.preventDefault()
    setDragging(false)
    const dropped = Array.from(e.dataTransfer.files).filter((f) =>
      SUPPORTED_EXTS.some((ext) => f.name.toLowerCase().endsWith(ext)),
    )
    if (dropped.length) onFiles(dropped)
  }

  return (
    <Box
      onDragOver={(e) => { e.preventDefault(); setDragging(true) }}
      onDragLeave={() => setDragging(false)}
      onDrop={handleDrop}
      onClick={() => inputRef.current?.click()}
      sx={{
        border: '2px dashed',
        borderColor: dragging ? 'primary.main' : 'divider',
        borderRadius: 3,
        p: { xs: 3, md: 5 },
        textAlign: 'center',
        cursor: 'pointer',
        transition: 'all 0.2s',
        bgcolor: dragging ? 'primary.50' : 'transparent',
        '&:hover': { borderColor: 'primary.main', bgcolor: 'rgba(15,76,129,0.03)' },
      }}
    >
      <CloudUploadOutlinedIcon sx={{ fontSize: 52, color: dragging ? 'primary.main' : 'action.disabled', mb: 1.5 }} />
      <Typography variant="h6" gutterBottom>
        Drop files here or <Box component="span" sx={{ color: 'primary.main', textDecoration: 'underline' }}>browse</Box>
      </Typography>
      <Typography variant="body2" sx={{
        color: "text.secondary"
      }}>
        Supports: {SUPPORTED_EXTS.slice(0, 10).join(', ')} and {SUPPORTED_EXTS.length - 10} more
      </Typography>
      <input
        ref={inputRef}
        type="file"
        multiple
        hidden
        accept={SUPPORTED_EXTS.join(',')}
        onChange={(e) => {
          const picked = Array.from(e.target.files ?? [])
          if (picked.length) onFiles(picked)
          e.target.value = ''
        }}
      />
    </Box>
  );
}

// ── Upload Tab ─────────────────────────────────────────────────────
function UploadTab() {
  const [staged, setStaged] = useState<File[]>([])
  const [jobs, setJobs] = useState<BatchIngestJob[]>([])
  const [stats, setStats] = useState<BatchStats | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [pollingIds, setPollingIds] = useState<Set<string>>(new Set())

  const fetchStats = useCallback(async () => {
    try { setStats(await apiClient.contextBatchStats()) } catch { /* silent */ }
  }, [])

  useEffect(() => { fetchStats() }, [fetchStats])

  // Poll in-progress jobs
  useEffect(() => {
    if (pollingIds.size === 0) return
    const interval = setInterval(async () => {
      const updated = await Promise.all(
        Array.from(pollingIds).map((id) => apiClient.contextGetBatchJob(id).catch(() => null)),
      )
      setJobs((prev) =>
        prev.map((j) => {
          const u = updated.find((x) => x?.id === j.id)
          return u ?? j
        }),
      )
      const stillRunning = updated.filter((j) => j && (j.status === 'pending' || j.status === 'processing'))
      if (stillRunning.length === 0) {
        setPollingIds(new Set())
        fetchStats()
      } else {
        setPollingIds(new Set(stillRunning.map((j) => j!.id)))
      }
    }, 2000)
    return () => clearInterval(interval)
  }, [pollingIds, fetchStats])

  async function handleIngest() {
    if (!staged.length) return
    setLoading(true)
    setError('')
    try {
      const { jobs: newJobs } = await apiClient.contextBatchIngest(staged)
      setJobs((prev) => [...newJobs, ...prev])
      setPollingIds(new Set(newJobs.map((j) => j.id)))
      setStaged([])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Batch ingest failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <Stack spacing={3}>
      {/* Stats bar */}
      {stats && (
        <Grid container spacing={2}>
          {[
            { label: 'Workers', value: stats.workers_active, color: '#1976d2' },
            { label: 'Queue', value: stats.queue_depth, color: '#9c27b0' },
            { label: 'Done', value: stats.done, color: '#2e7d32' },
            { label: 'Failed', value: stats.failed, color: '#d32f2f' },
            { label: 'Chunks', value: stats.total_chunks_processed.toLocaleString(), color: '#ed6c02' },
          ].map(({ label, value, color }) => (
            <Grid key={label} size={{ xs: 6, sm: 4, md: 2.4 }}>
              <Paper elevation={0} sx={{ p: 1.5, border: '1px solid', borderColor: 'divider', borderRadius: 2, textAlign: 'center' }}>
                <Typography
                  variant="h5"
                  sx={{
                    fontWeight: 700,
                    color
                  }}>{value}</Typography>
                <Typography variant="caption" sx={{
                  color: "text.secondary"
                }}>{label}</Typography>
              </Paper>
            </Grid>
          ))}
        </Grid>
      )}
      <FileDropzone onFiles={(f) => setStaged((prev) => [...prev, ...f.filter((x) => !prev.find((p) => p.name === x.name))])} />
      {/* Staged files */}
      {staged.length > 0 && (
        <Paper elevation={0} sx={{ p: 2, border: '1px solid', borderColor: 'divider', borderRadius: 2 }}>
          <Stack
            direction="row"
            sx={{
              justifyContent: "space-between",
              alignItems: "center",
              mb: 1.5
            }}>
            <Typography variant="subtitle2" sx={{
              fontWeight: 600
            }}>{staged.length} file(s) staged</Typography>
            <Button variant="outlined" size="small" color="error" onClick={() => setStaged([])}>Clear</Button>
          </Stack>
          <Stack spacing={0.8}>
            {staged.map((f) => (
              <Stack
                key={f.name}
                direction="row"
                sx={{
                  justifyContent: "space-between",
                  alignItems: "center"
                }}>
                <Stack direction="row" spacing={1} sx={{
                  alignItems: "center"
                }}>
                  <InsertDriveFileOutlinedIcon fontSize="small" color="action" />
                  <Typography variant="body2" noWrap sx={{ maxWidth: 300 }}>{f.name}</Typography>
                </Stack>
                <Stack direction="row" spacing={1} sx={{
                  alignItems: "center"
                }}>
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>{formatBytes(f.size)}</Typography>
                  <IconButton size="small" onClick={() => setStaged((prev) => prev.filter((x) => x.name !== f.name))}>
                    <DeleteOutlineIcon fontSize="small" />
                  </IconButton>
                </Stack>
              </Stack>
            ))}
          </Stack>
          {error && <Alert severity="error" sx={{ mt: 1.5, py: 0 }}>{error}</Alert>}
          <Button
            variant="contained"
            fullWidth
            sx={{ mt: 2 }}
            onClick={handleIngest}
            disabled={loading}
            startIcon={loading ? <CircularProgress size={16} /> : <CloudUploadOutlinedIcon />}
          >
            {loading ? 'Queuing...' : `Ingest ${staged.length} file(s) in batch`}
          </Button>
        </Paper>
      )}
      {/* Job queue table */}
      {jobs.length > 0 && (
        <Paper elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2, overflow: 'hidden' }}>
          <Stack
            direction="row"
            spacing={1}
            sx={{
              alignItems: "center",
              px: 2,
              py: 1.5,
              bgcolor: 'rgba(0,0,0,0.02)',
              borderBottom: '1px solid',
              borderColor: 'divider'
            }}>
            <SyncIcon fontSize="small" color="action" />
            <Typography variant="subtitle2" sx={{
              fontWeight: 600
            }}>Batch Jobs</Typography>
          </Stack>
          <Stack divider={<Box sx={{ borderBottom: '1px solid', borderColor: 'divider' }} />}>
            {jobs.map((job) => (
              <Stack
                key={job.id}
                direction="row"
                spacing={2}
                sx={{
                  alignItems: "center",
                  px: 2,
                  py: 1.2
                }}>
                {jobStatusIcon(job.status)}
                <Box
                  sx={{
                    flex: 1,
                    minWidth: 0
                  }}>
                  <Typography variant="body2" noWrap sx={{
                    fontWeight: 500
                  }}>{job.file_name}</Typography>
                  {job.status === 'processing' && (
                    <LinearProgress variant="determinate" value={job.progress_pct} sx={{ mt: 0.5, height: 4, borderRadius: 2 }} />
                  )}
                  {job.error && <Typography variant="caption" color="error">{job.error}</Typography>}
                </Box>
                <Chip label={job.status} size="small" color={jobStatusColor(job.status)} />
                <Typography
                  variant="caption"
                  sx={{
                    color: "text.secondary",
                    minWidth: 60,
                    textAlign: 'right'
                  }}>
                  {job.chunks_processed}/{job.chunks_total || '?'} chunks
                </Typography>
                {job.processing_time_ms != null && (
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>{job.processing_time_ms}ms</Typography>
                )}
              </Stack>
            ))}
          </Stack>
        </Paper>
      )}
    </Stack>
  );
}

// ── Index Tab ──────────────────────────────────────────────────────
function IndexTab() {
  const [files, setFiles] = useState<ContextChunkFile[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [deleteTarget, setDeleteTarget] = useState<ContextChunkFile | null>(null)
  const [deleting, setDeleting] = useState(false)

  const refresh = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setFiles(await apiClient.contextListFiles())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load index')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  async function handleDelete() {
    if (!deleteTarget) return
    setDeleting(true)
    try {
      await apiClient.contextDeleteFile(deleteTarget.file_path)
      setDeleteTarget(null)
      refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Delete failed')
    } finally {
      setDeleting(false)
    }
  }

  const totalChunks = files.reduce((s, f) => s + f.chunks, 0)

  return (
    <Stack spacing={2}>
      <Stack
        direction="row"
        sx={{
          justifyContent: "space-between",
          alignItems: "center"
        }}>
        <Stack direction="row" spacing={2}>
          <Chip icon={<StorageOutlinedIcon />} label={`${files.length} files`} size="small" variant="outlined" />
          <Chip icon={<InsertDriveFileOutlinedIcon />} label={`${totalChunks.toLocaleString()} chunks`} size="small" variant="outlined" />
        </Stack>
        <Button size="small" startIcon={<SyncIcon />} onClick={refresh} disabled={loading}>Refresh</Button>
      </Stack>
      {error && <Alert severity="error" onClose={() => setError('')}>{error}</Alert>}
      {loading && <LinearProgress sx={{ borderRadius: 2 }} />}
      {files.length === 0 && !loading ? (
        <Paper elevation={0} sx={{ p: 5, textAlign: 'center', border: '1px dashed', borderColor: 'divider', borderRadius: 3 }}>
          <StorageOutlinedIcon sx={{ fontSize: 48, color: 'action.disabled', mb: 1.5 }} />
          <Typography sx={{
            color: "text.secondary"
          }}>No files indexed yet. Upload some documents in the Upload tab.</Typography>
        </Paper>
      ) : (
        <Paper elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2, overflow: 'hidden' }}>
          <Stack divider={<Box sx={{ borderBottom: '1px solid', borderColor: 'divider' }} />}>
            {/* Header */}
            <Stack
              direction="row"
              sx={{
                alignItems: "center",
                px: 2,
                py: 1,
                bgcolor: 'rgba(0,0,0,0.02)'
              }}>
              <Typography
                variant="caption"
                sx={{
                  fontWeight: 700,
                  flex: 1
                }}>File Name</Typography>
              <Typography
                variant="caption"
                sx={{
                  fontWeight: 700,
                  minWidth: 80,
                  textAlign: 'right'
                }}>Chunks</Typography>
              <Box sx={{ minWidth: 50 }} />
            </Stack>
            {files.map((f) => (
              <Stack
                key={f.file_path}
                direction="row"
                spacing={1}
                sx={{
                  alignItems: "center",
                  px: 2,
                  py: 1.2
                }}>
                <InsertDriveFileOutlinedIcon fontSize="small" color="action" />
                <Box
                  sx={{
                    flex: 1,
                    minWidth: 0
                  }}>
                  <Typography variant="body2" noWrap sx={{
                    fontWeight: 500
                  }}>{f.file_name}</Typography>
                  <Typography variant="caption" noWrap sx={{
                    color: "text.secondary"
                  }}>{f.file_path}</Typography>
                </Box>
                <Chip label={`${f.chunks} chunks`} size="small" variant="outlined" />
                <Tooltip title="Delete from index">
                  <IconButton size="small" color="error" onClick={() => setDeleteTarget(f)}>
                    <DeleteOutlineIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </Stack>
            ))}
          </Stack>
        </Paper>
      )}
      {/* Confirm delete dialog */}
      <Dialog open={!!deleteTarget} onClose={() => setDeleteTarget(null)} maxWidth="sm" fullWidth>
        <DialogTitle>Delete from Index</DialogTitle>
        <DialogContent>
          <Typography>
            Remove <strong>{deleteTarget?.file_name}</strong> and all its{' '}
            <strong>{deleteTarget?.chunks}</strong> chunks from the vector index? This cannot be undone.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteTarget(null)}>Cancel</Button>
          <Button variant="contained" color="error" onClick={handleDelete} disabled={deleting}>
            {deleting ? <CircularProgress size={18} /> : 'Delete'}
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
}

// ── Search Tab ─────────────────────────────────────────────────────
function SearchTab() {
  const [query, setQuery] = useState('')
  const [fileType, setFileType] = useState('')
  const [limit, setLimit] = useState(10)
  const [results, setResults] = useState<ContextSearchResult[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [showFilters, setShowFilters] = useState(false)
  const [searched, setSearched] = useState(false)

  async function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    if (!query.trim()) return
    setLoading(true)
    setError('')
    setSearched(true)
    try {
      const resp = await apiClient.contextSearch(query, limit, fileType || undefined)
      setResults(resp.results)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Search failed')
    } finally {
      setLoading(false)
    }
  }

  // Highlight query terms in content
  function highlight(text: string): string {
    if (!query.trim()) return text
    const terms = query.trim().split(/\s+/).filter(Boolean)
    const regex = new RegExp(`(${terms.map((t) => t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|')})`, 'gi')
    return text.replace(regex, '<mark style="background:#fff176;border-radius:2px;padding:0 2px">$1</mark>')
  }

  return (
    <Stack spacing={2.5}>
      <Box component="form" onSubmit={handleSearch}>
        <Stack spacing={1.5}>
          <Stack direction="row" spacing={1}>
            <TextField
              fullWidth
              placeholder="Ask anything about your knowledge base..."
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              size="small"
              InputProps={{
                startAdornment: <SearchOutlinedIcon fontSize="small" sx={{ mr: 1, color: 'action.active' }} />,
              }}
            />
            <Button type="submit" variant="contained" disabled={loading || !query.trim()} sx={{ minWidth: 100 }}>
              {loading ? <CircularProgress size={18} /> : 'Search'}
            </Button>
            <Tooltip title="Filters">
              <IconButton onClick={() => setShowFilters((p) => !p)} color={showFilters ? 'primary' : 'default'}>
                <TuneIcon />
              </IconButton>
            </Tooltip>
          </Stack>

          <Collapse in={showFilters}>
            <Paper elevation={0} sx={{ p: 2, border: '1px solid', borderColor: 'divider', borderRadius: 2 }}>
              <Grid container spacing={2}>
                <Grid size={{ xs: 12, sm: 6 }}>
                  <TextField
                    fullWidth
                    size="small"
                    label="File type filter"
                    placeholder=".py, .md, .ts..."
                    value={fileType}
                    onChange={(e) => setFileType(e.target.value)}
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 6 }}>
                  <TextField
                    fullWidth
                    size="small"
                    label="Result limit"
                    type="number"
                    value={limit}
                    onChange={(e) => setLimit(Number(e.target.value))}
                    inputProps={{ min: 1, max: 50 }}
                  />
                </Grid>
              </Grid>
            </Paper>
          </Collapse>
        </Stack>
      </Box>
      {error && <Alert severity="error" onClose={() => setError('')}>{error}</Alert>}
      {searched && results.length === 0 && !loading && (
        <Paper elevation={0} sx={{ p: 4, textAlign: 'center', border: '1px dashed', borderColor: 'divider', borderRadius: 3 }}>
          <SearchOutlinedIcon sx={{ fontSize: 48, color: 'action.disabled', mb: 1 }} />
          <Typography sx={{
            color: "text.secondary"
          }}>No results found for "{query}"</Typography>
        </Paper>
      )}
      {results.length > 0 && (
        <Stack spacing={1.5}>
          <Typography variant="caption" sx={{
            color: "text.secondary"
          }}>
            {results.length} result(s) for "{query}"
          </Typography>
          {results.map((r, i) => (
            <Paper
              key={r.chunk_id}
              elevation={0}
              sx={{ p: 2.5, border: '1px solid', borderColor: 'divider', borderRadius: 2, '&:hover': { borderColor: 'primary.light' }, transition: 'border-color 0.2s' }}
            >
              <Stack
                direction="row"
                sx={{
                  justifyContent: "space-between",
                  alignItems: "flex-start",
                  mb: 1
                }}>
                <Stack direction="row" spacing={1} sx={{
                  alignItems: "center"
                }}>
                  <Chip label={`#${i + 1}`} size="small" color="primary" variant="outlined" />
                  <InsertDriveFileOutlinedIcon fontSize="small" color="action" />
                  <Typography
                    variant="caption"
                    noWrap
                    sx={{
                      color: "text.secondary",
                      maxWidth: 300
                    }}>
                    {r.file_path}
                  </Typography>
                </Stack>
                {r.score != null && (
                  <Chip
                    label={`score: ${r.score.toFixed(4)}`}
                    size="small"
                    variant="outlined"
                    color={r.score < 0.3 ? 'success' : r.score < 0.6 ? 'warning' : 'default'}
                  />
                )}
              </Stack>
              <Typography
                variant="body2"
                sx={{ fontFamily: 'monospace', fontSize: 12, whiteSpace: 'pre-wrap', wordBreak: 'break-word', lineHeight: 1.7, maxHeight: 200, overflow: 'auto' }}
                dangerouslySetInnerHTML={{ __html: highlight(r.content) }}
              />
            </Paper>
          ))}
        </Stack>
      )}
    </Stack>
  );
}

// ── Main Page ──────────────────────────────────────────────────────
export function ContextBoxPage() {
  const [tab, setTab] = useState(0)

  return (
    <Stack spacing={3}>
      <Stack direction="row" spacing={1.5} sx={{
        alignItems: "center"
      }}>
        <StorageOutlinedIcon color="primary" sx={{ fontSize: 28 }} />
        <Typography variant="h5" sx={{
          fontWeight: 600
        }}>Context-Box</Typography>
        <Chip label="RAG System" size="small" color="secondary" variant="outlined" />
        <Chip label="Batch Processing" size="small" color="success" variant="outlined" />
      </Stack>
      <Paper elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 3, overflow: 'hidden' }}>
        <Tabs
          value={tab}
          onChange={(_, v) => setTab(v)}
          sx={{ borderBottom: '1px solid', borderColor: 'divider', px: 2, pt: 1 }}
        >
          <Tab icon={<CloudUploadOutlinedIcon fontSize="small" />} iconPosition="start" label="Upload" id="ctx-tab-0" />
          <Tab icon={<StorageOutlinedIcon fontSize="small" />} iconPosition="start" label="Index" id="ctx-tab-1" />
          <Tab icon={<SearchOutlinedIcon fontSize="small" />} iconPosition="start" label="Search" id="ctx-tab-2" />
        </Tabs>

        <Box sx={{ p: { xs: 2, md: 3 } }}>
          {tab === 0 && <UploadTab />}
          {tab === 1 && <IndexTab />}
          {tab === 2 && <SearchTab />}
        </Box>
      </Paper>
      {/* Pipeline info footer */}
      <Paper elevation={0} sx={{ p: 2, border: '1px dashed', borderColor: 'divider', borderRadius: 2 }}>
        <Stack
          direction="row"
          spacing={1}
          sx={{
            alignItems: "center",
            flexWrap: "wrap"
          }}>
          {['File Upload', '→', 'Extract', '→', 'Split', '→', 'Batch Embed (Gemini)', '→', 'Store (LanceDB/MinIO)'].map((step, i) => (
            step === '→'
              ? <Typography key={i} variant="body2" sx={{
              color: "text.disabled"
            }}>→</Typography>
              : <Chip key={step} label={step} size="small" variant="outlined" />
          ))}
        </Stack>
      </Paper>
    </Stack>
  );
}
