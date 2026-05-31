import FilterListIcon from '@mui/icons-material/FilterList'
import RefreshIcon from '@mui/icons-material/Refresh'
import TerminalIcon from '@mui/icons-material/Terminal'
import {
  Alert,
  Box,
  Chip,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material'
import { useCallback, useEffect, useRef, useState } from 'react'
import { apiClient } from '../api/client'
import { useProjectContext } from '../context/useProjectContext'
import type { BoxLog } from '../types'

const levelColors: Record<string, string> = {
  info: '#58a6ff',
  warn: '#d29922',
  error: '#f85149',
  debug: '#8b949e',
}

const levelIcons: Record<string, string> = {
  info: 'ℹ',
  warn: '⚠',
  error: '✖',
  debug: '◆',
}

export function LogPage() {
  const { selectedProjectId } = useProjectContext()
  const [logs, setLogs] = useState<BoxLog[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [levelFilter, setLevelFilter] = useState<string>('')
  const [autoScroll, setAutoScroll] = useState(true)
  const logsEndRef = useRef<HTMLDivElement>(null)

  const fetchLogs = useCallback(async () => {
    if (!selectedProjectId) return
    setLoading(true)
    setError('')
    try {
      const data = await apiClient.listLogs(selectedProjectId, {
        level: levelFilter || undefined,
        limit: 200,
      })
      setLogs(data.reverse()) // newest at bottom (terminal-style)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load logs')
    } finally {
      setLoading(false)
    }
  }, [selectedProjectId, levelFilter])

  useEffect(() => {
    fetchLogs()
  }, [fetchLogs])

  useEffect(() => {
    if (autoScroll && logsEndRef.current) {
      logsEndRef.current.scrollIntoView({ behavior: 'smooth' })
    }
  }, [logs, autoScroll])

  function formatTimestamp(ts: string) {
    const d = new Date(ts)
    return d.toLocaleTimeString('en-US', { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' })
      + '.' + String(d.getMilliseconds()).padStart(3, '0')
  }

  if (!selectedProjectId) {
    return (
      <Paper elevation={0} sx={{ p: 4, textAlign: 'center', border: '1px dashed', borderColor: 'divider', borderRadius: 3 }}>
        <TerminalIcon sx={{ fontSize: 56, color: 'action.disabled', mb: 2 }} />
        <Typography variant="h6" sx={{
          color: "text.secondary"
        }}>Select a Box to view logs</Typography>
      </Paper>
    );
  }

  return (
    <Stack spacing={2}>
      <Stack
        direction="row"
        sx={{
          justifyContent: "space-between",
          alignItems: "center"
        }}>
        <Stack direction="row" spacing={1} sx={{
          alignItems: "center"
        }}>
          <TerminalIcon color="primary" sx={{ fontSize: 28 }} />
          <Typography variant="h5" sx={{
            fontWeight: 600
          }}>Log</Typography>
          <Chip label={`${logs.length} entries`} size="small" color="primary" variant="outlined" />
        </Stack>
        <Stack direction="row" spacing={1} sx={{
          alignItems: "center"
        }}>
          <FormControl size="small" sx={{ minWidth: 120 }}>
            <InputLabel><FilterListIcon sx={{ fontSize: 16, mr: 0.5 }} />Level</InputLabel>
            <Select
              value={levelFilter}
              label="Level"
              onChange={(e) => setLevelFilter(e.target.value)}
            >
              <MenuItem value="">All</MenuItem>
              <MenuItem value="info">Info</MenuItem>
              <MenuItem value="warn">Warn</MenuItem>
              <MenuItem value="error">Error</MenuItem>
              <MenuItem value="debug">Debug</MenuItem>
            </Select>
          </FormControl>
          <Tooltip title="Refresh">
            <IconButton onClick={fetchLogs} disabled={loading}>
              <RefreshIcon />
            </IconButton>
          </Tooltip>
          <Tooltip title={autoScroll ? 'Auto-scroll: ON' : 'Auto-scroll: OFF'}>
            <Chip
              label={autoScroll ? 'AUTO ▼' : 'MANUAL'}
              size="small"
              color={autoScroll ? 'success' : 'default'}
              onClick={() => setAutoScroll((v) => !v)}
              variant="outlined"
              sx={{ fontFamily: 'monospace', fontSize: 11, cursor: 'pointer' }}
            />
          </Tooltip>
        </Stack>
      </Stack>
      {error && <Alert severity="error" variant="outlined" onClose={() => setError('')}>{error}</Alert>}
      <Paper
        elevation={0}
        sx={{
          bgcolor: '#0d1117',
          color: '#c9d1d9',
          borderRadius: 2,
          border: '1px solid #30363d',
          p: 0,
          overflowY: 'auto',
          maxHeight: 'calc(100vh - 240px)',
          minHeight: 300,
          fontFamily: '"JetBrains Mono", "Fira Code", "Consolas", monospace',
          fontSize: 12.5,
          lineHeight: 1.7,
        }}
      >
        {loading && (
          <Box sx={{ p: 2, color: '#8b949e' }}>Loading logs...</Box>
        )}
        {!loading && logs.length === 0 && (
          <Box sx={{ p: 2, color: '#8b949e' }}>
            No log entries. Logs will appear here as events occur in the Box.
          </Box>
        )}
        {logs.map((log) => (
          <Box
            key={log.id}
            sx={{
              px: 2,
              py: 0.4,
              display: 'flex',
              gap: 1.5,
              borderBottom: '1px solid rgba(48, 54, 61, 0.5)',
              '&:hover': { bgcolor: 'rgba(88, 166, 255, 0.04)' },
              alignItems: 'flex-start',
            }}
          >
            <Box component="span" sx={{ color: '#8b949e', flexShrink: 0, width: 95, userSelect: 'none' }}>
              {formatTimestamp(log.timestamp)}
            </Box>
            <Box
              component="span"
              sx={{
                color: levelColors[log.level] ?? '#c9d1d9',
                fontWeight: 600,
                width: 50,
                flexShrink: 0,
                textTransform: 'uppercase',
              }}
            >
              {levelIcons[log.level] ?? '•'} {log.level}
            </Box>
            {log.source && (
              <Box
                component="span"
                sx={{
                  color: '#bc8cff',
                  flexShrink: 0,
                  minWidth: 60,
                  maxWidth: 100,
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                  whiteSpace: 'nowrap',
                }}
              >
                [{log.source}]
              </Box>
            )}
            <Box component="span" sx={{ flex: 1, wordBreak: 'break-word', whiteSpace: 'pre-wrap' }}>
              {log.message}
              {log.details && (
                <Box component="span" sx={{ color: '#8b949e', ml: 1 }}>
                  | {log.details.length > 120 ? log.details.substring(0, 120) + '…' : log.details}
                </Box>
              )}
            </Box>
          </Box>
        ))}
        <div ref={logsEndRef} />
      </Paper>
    </Stack>
  );
}
