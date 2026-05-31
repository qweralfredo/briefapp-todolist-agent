import { useEffect, useRef, useState } from 'react';
import { Box, Card, CardContent, Typography, Chip, Button, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Paper, CircularProgress, Alert, Divider, Stack,
  IconButton, Tooltip } from '@mui/material';
import RefreshIcon    from '@mui/icons-material/Refresh';
import PlayArrowIcon  from '@mui/icons-material/PlayArrow';
import BlockIcon      from '@mui/icons-material/Block';
import DeleteIcon     from '@mui/icons-material/Delete';
import LocalFireDepartmentIcon from '@mui/icons-material/LocalFireDepartment';
import ReactECharts   from 'echarts-for-react';

// ── Types ──────────────────────────────────────────────────────────────────────

interface QueueStats {
  pendingCount:     number;
  processingCount:  number;
  completedToday:   number;
  failedToday:      number;
  dlqSize:          number;
  avgProcessingMs:  number;
  throughputPerMin: number;
  activeLocks:      number;
  capturedAt:       string;
}

interface DlqEntry {
  id:            string;
  boxId:         string;
  originalTopic: string;
  failureReason: string;
  retryCount:    number;
  status:        'Pending' | 'Retrying' | 'Resolved' | 'Quarantined';
  firstFailedAt: string;
  lastFailedAt:  string;
}

interface DlqPage {
  page:       number;
  pageSize:   number;
  totalCount: number;
  items:      DlqEntry[];
}

// ── Helpers ────────────────────────────────────────────────────────────────────

const API = import.meta.env.VITE_API_URL ?? 'http://localhost:5181';

const statusColor: Record<string, 'default' | 'warning' | 'success' | 'error'> = {
  Pending:     'warning',
  Retrying:    'default',
  Resolved:    'success',
  Quarantined: 'error',
};

function MetricCard({ label, value, sub, color }: {
  label: string; value: string | number; sub?: string; color?: string;
}) {
  return (
    <Card sx={{
      flex: 1, minWidth: 140,
      background: 'linear-gradient(135deg, #1a1a2e 0%, #16213e 100%)',
      border: '1px solid rgba(255,255,255,0.08)',
      borderRadius: 3,
    }}>
      <CardContent>
        <Typography variant="caption" sx={{ color: '#aaa', textTransform: 'uppercase', letterSpacing: 1 }}>
          {label}
        </Typography>
        <Typography
          variant="h4"
          sx={{
            fontWeight: 700,
            color: color ?? '#fff',
            mt: 0.5
          }}>
          {value}
        </Typography>
        {sub && <Typography variant="caption" sx={{ color: '#666' }}>{sub}</Typography>}
      </CardContent>
    </Card>
  );
}

// ── QueueDashboardPage ──────────────────────────────────────────────────────────

interface Props { boxId?: string; }

export default function QueueDashboardPage({ boxId }: Props) {
  const [stats,   setStats]   = useState<QueueStats | null>(null);
  const [history, setHistory] = useState<{ t: string; throughput: number; pending: number }[]>([]);
  const [dlq,     setDlq]     = useState<DlqPage | null>(null);
  const [dlqPage, setDlqPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState<string | null>(null);

  // ── SSE live stats ──────────────────────────────────────────────────────────
  useEffect(() => {
    const url = `${API}/api/queue/stats/live${boxId ? `?boxId=${boxId}` : ''}`;
    const es  = new EventSource(url);

    es.onmessage = (e) => {
      try {
        const data: QueueStats = JSON.parse(e.data);
        setStats(data);
        setHistory(prev => {
          const next = [...prev, {
            t:          new Date(data.capturedAt).toLocaleTimeString(),
            throughput: data.throughputPerMin,
            pending:    data.pendingCount,
          }];
          return next.slice(-60);
        });
      } catch { /* ignore */ }
    };

    es.onerror = () => setError('SSE disconnected. Trying to reconnect…');

    return () => es.close();
  }, [boxId]);

  // ── DLQ fetcher ─────────────────────────────────────────────────────────────
  const fetchDlq = async (page = 1) => {
    setLoading(true);
    try {
      const params = new URLSearchParams({ page: String(page), size: '10' });
      if (boxId) params.set('boxId', boxId);
      const res  = await fetch(`${API}/api/queue/dlq?${params}`);
      setDlq(await res.json());
      setDlqPage(page);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void fetchDlq(1); }, [boxId]);

  const actionDlq = async (id: string, action: 'retry' | 'quarantine') => {
    await fetch(`${API}/api/queue/dlq/${id}/${action}`, { method: 'POST' });
    void fetchDlq(dlqPage);
  };

  const deleteDlq = async (id: string) => {
    await fetch(`${API}/api/queue/dlq/${id}`, { method: 'DELETE' });
    void fetchDlq(dlqPage);
  };

  const drain = async () => {
    const qs = boxId ? `?boxId=${boxId}` : '';
    await fetch(`${API}/api/queue/dlq/drain${qs}`, { method: 'POST' });
    void fetchDlq(1);
  };

  // ── ECharts option ─────────────────────────────────────────────────────────
  const chartOption = {
    backgroundColor: 'transparent',
    grid: { top: 20, bottom: 30, left: 40, right: 20 },
    xAxis: { type: 'category', data: history.map(h => h.t), axisLabel: { color: '#666', fontSize: 10 } },
    yAxis: { axisLabel: { color: '#666', fontSize: 10 }, splitLine: { lineStyle: { color: '#222' } } },
    tooltip: {
      trigger: 'axis',
      backgroundColor: '#1a1a2e',
      borderColor: '#333',
      textStyle: { color: '#fff' },
    },
    legend: { data: ['Throughput/min', 'Pending'], textStyle: { color: '#aaa' }, bottom: 0 },
    series: [
      { name: 'Throughput/min', type: 'line', data: history.map(h => h.throughput),
        smooth: true, lineStyle: { color: '#f97316' }, itemStyle: { color: '#f97316' }, showSymbol: false },
      { name: 'Pending', type: 'line', data: history.map(h => h.pending),
        smooth: true, lineStyle: { color: '#3b82f6' }, itemStyle: { color: '#3b82f6' }, showSymbol: false,
        areaStyle: { color: 'rgba(59,130,246,0.15)' } },
    ],
  };

  return (
    <Box sx={{ p: 3, minHeight: '100vh', background: '#0d1117', color: '#fff' }}>
      {/* Header */}
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 3, gap: 2 }}>
        <LocalFireDepartmentIcon sx={{ fontSize: 32, color: '#f97316' }} />
        <Box>
          <Typography variant="h5" sx={{
            fontWeight: 700
          }}>Transactional Queue</Typography>
          <Typography variant="caption" sx={{ color: '#aaa' }}>Live Dashboard — updates every 5s via SSE</Typography>
        </Box>
        <Box sx={{
          flex: 1
        }} />
        <Tooltip title="Refresh DLQ">
          <IconButton onClick={() => void fetchDlq(dlqPage)} size="small" sx={{ color: '#aaa' }}>
            <RefreshIcon />
          </IconButton>
        </Tooltip>
      </Box>
      {error && <Alert severity="warning" sx={{ mb: 2 }} onClose={() => setError(null)}>{error}</Alert>}
      {/* Metric Cards */}
      <Stack
        direction="row"
        spacing={2}
        useFlexGap
        sx={{
          flexWrap: "wrap",
          mb: 3
        }}>
        <MetricCard label="Pending"         value={stats?.pendingCount    ?? '—'} color="#f97316" />
        <MetricCard label="Processing"      value={stats?.processingCount ?? '—'} color="#3b82f6" />
        <MetricCard label="Completed Today" value={stats?.completedToday  ?? '—'} color="#22c55e" />
        <MetricCard label="Failed Today"    value={stats?.failedToday     ?? '—'} color="#ef4444" />
        <MetricCard label="DLQ Size"        value={stats?.dlqSize         ?? '—'} color="#a855f7" />
        <MetricCard label="Active Locks"    value={stats?.activeLocks     ?? '—'} />
        <MetricCard
          label="Throughput"
          value={`${(stats?.throughputPerMin ?? 0).toFixed(1)} t/min`}
          sub={`avg ${(stats?.avgProcessingMs ?? 0).toFixed(0)} ms`}
        />
      </Stack>
      {/* Throughput Chart */}
      {history.length > 1 && (
        <Card sx={{ mb: 3, background: '#161b22', border: '1px solid rgba(255,255,255,0.08)', borderRadius: 3, p: 2 }}>
          <Typography variant="subtitle2" sx={{ color: '#aaa', mb: 1 }}>
            Live Throughput &amp; Pending — last {history.length} snapshots
          </Typography>
          <ReactECharts option={chartOption} style={{ height: 220 }} />
        </Card>
      )}
      <Divider sx={{ my: 3, borderColor: 'rgba(255,255,255,0.08)' }} />
      {/* DLQ Section */}
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 2, gap: 2 }}>
        <Typography variant="h6" sx={{
          fontWeight: 600
        }}>Dead Letter Queue</Typography>
        {dlq && <Chip label={`${dlq.totalCount} total`} size="small" color="error" />}
        <Box sx={{
          flex: 1
        }} />
        <Button
          variant="outlined" size="small" onClick={drain}
          startIcon={<PlayArrowIcon />}
          sx={{ borderColor: '#f97316', color: '#f97316', '&:hover': { borderColor: '#fb923c' } }}
        >
          Drain All Pending
        </Button>
      </Box>
      {loading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
          <CircularProgress size={32} sx={{ color: '#f97316' }} />
        </Box>
      ) : (
        <TableContainer component={Paper} sx={{ background: '#161b22', borderRadius: 2 }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                {['Topic', 'Reason', 'Retries', 'Status', 'Last Failed', 'Actions'].map(h => (
                  <TableCell key={h} sx={{ color: '#aaa', borderColor: 'rgba(255,255,255,0.06)', fontWeight: 600 }}>
                    {h}
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {(!dlq || dlq.items.length === 0) && (
                <TableRow>
                  <TableCell colSpan={6} sx={{ textAlign: 'center', color: '#555', py: 4 }}>
                    No DLQ entries 🎉
                  </TableCell>
                </TableRow>
              )}
              {dlq?.items.map(entry => (
                <TableRow key={entry.id} hover sx={{ '&:hover': { background: 'rgba(255,255,255,0.03)' } }}>
                  <TableCell sx={{ color: '#ccc', borderColor: 'rgba(255,255,255,0.04)', maxWidth: 160, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {entry.originalTopic.split('.').pop()}
                  </TableCell>
                  <TableCell sx={{ color: '#ef4444', borderColor: 'rgba(255,255,255,0.04)', maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {entry.failureReason.slice(0, 60)}
                  </TableCell>
                  <TableCell sx={{ color: '#aaa', borderColor: 'rgba(255,255,255,0.04)' }}>
                    {entry.retryCount}
                  </TableCell>
                  <TableCell sx={{ borderColor: 'rgba(255,255,255,0.04)' }}>
                    <Chip label={entry.status} size="small" color={statusColor[entry.status]} />
                  </TableCell>
                  <TableCell sx={{ color: '#666', borderColor: 'rgba(255,255,255,0.04)', fontSize: 11 }}>
                    {new Date(entry.lastFailedAt).toLocaleString()}
                  </TableCell>
                  <TableCell sx={{ borderColor: 'rgba(255,255,255,0.04)' }}>
                    <Stack direction="row" spacing={0.5}>
                      {entry.status === 'Pending' && (
                        <Tooltip title="Retry"><IconButton size="small" sx={{ color: '#22c55e' }}
                          onClick={() => void actionDlq(entry.id, 'retry')}>
                          <PlayArrowIcon fontSize="small" />
                        </IconButton></Tooltip>
                      )}
                      {entry.status !== 'Quarantined' && (
                        <Tooltip title="Quarantine"><IconButton size="small" sx={{ color: '#a855f7' }}
                          onClick={() => void actionDlq(entry.id, 'quarantine')}>
                          <BlockIcon fontSize="small" />
                        </IconButton></Tooltip>
                      )}
                      <Tooltip title="Delete"><IconButton size="small" sx={{ color: '#ef4444' }}
                        onClick={() => void deleteDlq(entry.id)}>
                        <DeleteIcon fontSize="small" />
                      </IconButton></Tooltip>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      {dlq && dlq.totalCount > dlq.pageSize && (
        <Stack
          direction="row"
          spacing={1}
          sx={{
            justifyContent: "flex-end",
            mt: 2
          }}>
          <Button size="small" disabled={dlqPage <= 1} onClick={() => void fetchDlq(dlqPage - 1)}
            sx={{ color: '#aaa' }}>← Prev</Button>
          <Typography variant="body2" sx={{ color: '#aaa', lineHeight: '30px' }}>
            {dlqPage} / {Math.ceil(dlq.totalCount / dlq.pageSize)}
          </Typography>
          <Button size="small" disabled={dlqPage >= Math.ceil(dlq.totalCount / dlq.pageSize)}
            onClick={() => void fetchDlq(dlqPage + 1)} sx={{ color: '#aaa' }}>Next →</Button>
        </Stack>
      )}
    </Box>
  );
}
