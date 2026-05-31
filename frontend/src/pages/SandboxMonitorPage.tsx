import { useState, useEffect, useCallback } from 'react';
import {
  Box, Grid, Card, CardContent, Typography, Chip,
  LinearProgress, Table, TableHead, TableRow, TableCell,
  TableBody, Alert, IconButton, Tooltip, Tab, Tabs, Badge,
} from '@mui/material';
import {
  Memory, Speed, Storage, Wifi, PlayArrow,
  Stop, Refresh, Terminal, Circle,
} from '@mui/icons-material';
import ReactECharts from 'echarts-for-react';

// ── Types ─────────────────────────────────────────────────────────────────────

interface SandboxInfo {
  id: string;
  taskId: string;
  image: string;
  status: string; // Created / Running / Stopped / Destroyed
  startedAt?: string;
  stoppedAt?: string;
  containerId?: string;
  cpuLimit: number;
  memoryLimitMb: number;
  createdAt: string;
}

interface SandboxMetrics {
  cpuPercent: number;
  memoryMb: number;
  memoryPercent: number;
  networkRxBytes: number;
  networkTxBytes: number;
  diskReadBytes: number;
  diskWriteBytes: number;
  uptimeSeconds: number;
  capturedAt: string;
}

interface NetworkPolicy {
  sandboxId: string;
  allowInternet: boolean;
  allowedHosts: string[];
  dnsProvider: string;
  bandwidthLimitKbps: number;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

const statusColor = (s: string) => ({
  Running: '#4caf50', Created: '#2196f3',
  Stopped: '#ff9800', Destroyed: '#f44336',
}[s] ?? '#aaa');

const fmtBytes = (b: number) => b >= 1e9 ? `${(b/1e9).toFixed(1)}GB`
  : b >= 1e6 ? `${(b/1e6).toFixed(1)}MB`
  : b >= 1e3 ? `${(b/1e3).toFixed(1)}KB`
  : `${b}B`;

const fmtUptime = (s: number) => {
  const h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60), ss = s % 60;
  return h > 0 ? `${h}h ${m}m` : m > 0 ? `${m}m ${ss}s` : `${ss}s`;
};

// ── MetricGauge ───────────────────────────────────────────────────────────────

function MetricGauge({ label, value, max, unit, icon }: {
  label: string; value: number; max: number; unit: string; icon: React.ReactNode;
}) {
  const pct   = Math.min(100, max > 0 ? (value / max) * 100 : 0);
  const color = pct >= 90 ? '#f44336' : pct >= 70 ? '#ff9800' : '#4caf50';

  return (
    <Box sx={{
      p: 1.5, border: `1px solid #2a2d3e`, borderRadius: 2,
      background: 'linear-gradient(135deg,#13152200,#1a1d2e)',
    }}>
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          gap: 0.5,
          mb: 0.5
        }}>
        <Box sx={{ color, fontSize: 16 }}>{icon}</Box>
        <Typography variant="caption" sx={{
          color: "text.secondary"
        }}>{label}</Typography>
      </Box>
      <Typography
        variant="h6"
        sx={{
          fontWeight: 700,
          color
        }}>
        {value.toFixed(1)}{unit}
      </Typography>
      <LinearProgress variant="determinate" value={pct}
        sx={{ mt: 0.5, height: 4, borderRadius: 2, background: '#2a2d3e',
          '& .MuiLinearProgress-bar': { background: color } }} />
    </Box>
  );
}

// ── SandboxCard ───────────────────────────────────────────────────────────────

function SandboxCard({ sandbox, selected, onClick }: {
  sandbox: SandboxInfo; selected: boolean; onClick: () => void;
}) {
  return (
    <Card onClick={onClick} sx={{
      cursor: 'pointer',
      background: selected
        ? 'linear-gradient(135deg,#6c63ff22,#1a1d2e)'
        : 'linear-gradient(135deg,#0f1120,#1a1d2e)',
      border: `1px solid ${selected ? '#6c63ff' : '#2a2d3e'}`,
      borderRadius: 2, transition: 'all .2s',
      '&:hover': { borderColor: '#6c63ff88', transform: 'translateY(-1px)' },
    }}>
      <CardContent sx={{ py: 1.5, '&:last-child': { pb: 1.5 } }}>
        <Box
          sx={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-start"
          }}>
          <Box>
            <Typography
              variant="body2"
              noWrap
              sx={{
                fontWeight: 600,
                maxWidth: 180
              }}>
              {sandbox.taskId.slice(0, 20)}…
            </Typography>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>
              {sandbox.image.split(':')[0].split('/').pop()}
            </Typography>
          </Box>
          <Chip
            icon={<Circle sx={{ fontSize: '10px !important', color: `${statusColor(sandbox.status)} !important` }} />}
            label={sandbox.status}
            size="small"
            sx={{
              background: `${statusColor(sandbox.status)}22`,
              color: statusColor(sandbox.status),
              fontWeight: 600, fontSize: 10
            }}
          />
        </Box>
        {sandbox.status === 'Running' && sandbox.startedAt && (
          <Typography
            variant="caption"
            sx={{
              color: "text.secondary",
              display: 'block',
              mt: 0.5
            }}>
            ↑ {new Date(sandbox.startedAt).toLocaleTimeString()}
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}

// ── SandboxMonitorPage ────────────────────────────────────────────────────────

export default function SandboxMonitorPage() {
  const [sandboxes,     setSandboxes]     = useState<SandboxInfo[]>([]);
  const [selected,      setSelected]      = useState<SandboxInfo | null>(null);
  const [metrics,       setMetrics]       = useState<SandboxMetrics[]>([]);
  const [networkPolicy, setNetworkPolicy] = useState<NetworkPolicy | null>(null);
  const [loading,       setLoading]       = useState(true);
  const [tab,           setTab]           = useState(0);
  const [error,         setError]         = useState<string | null>(null);

  const loadSandboxes = useCallback(async () => {
    try {
      const res = await fetch('/api/sandboxes');
      if (res.ok) setSandboxes(await res.json());
      setError(null);
    } catch (e: any) { setError(e.message); }
    finally { setLoading(false); }
  }, []);

  const loadSandboxDetail = useCallback(async (sb: SandboxInfo) => {
    const [mRes, nRes] = await Promise.all([
      fetch(`/api/sandboxes/${sb.id}/metrics?limit=20`),
      fetch(`/api/sandboxes/${sb.id}/network-policy`),
    ]);
    if (mRes.ok) setMetrics(await mRes.json());
    if (nRes.ok) setNetworkPolicy(await nRes.json());
  }, []);

  useEffect(() => {
    loadSandboxes();
    const t = setInterval(loadSandboxes, 15_000);
    return () => clearInterval(t);
  }, [loadSandboxes]);

  useEffect(() => {
    if (!selected) return;
    loadSandboxDetail(selected);
    const t = setInterval(() => loadSandboxDetail(selected), 10_000);
    return () => clearInterval(t);
  }, [selected, loadSandboxDetail]);

  // ── ECharts for CPU/Memory history ────────────────────────────────────────

  const chartData = [...metrics].reverse();
  const timeLabels = chartData.map(m => new Date(m.capturedAt).toLocaleTimeString());

  const metricsChartOption = {
    backgroundColor: 'transparent',
    tooltip: { trigger: 'axis', axisPointer: { type: 'cross' } },
    legend: { data: ['CPU %', 'MEM %'], textStyle: { color: '#aaa' } },
    grid: { top: 40, bottom: 30, left: 40, right: 10 },
    xAxis: { type: 'category', data: timeLabels, axisLabel: { color: '#666', fontSize: 10 } },
    yAxis: { type: 'value', axisLabel: { color: '#666', fontSize: 10 }, min: 0, max: 100 },
    series: [
      {
        name: 'CPU %', type: 'line', smooth: true,
        data: chartData.map(m => m.cpuPercent.toFixed(1)),
        itemStyle: { color: '#6c63ff' },
        areaStyle: { color: { type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
          colorStops: [{ offset: 0, color: '#6c63ff44' }, { offset: 1, color: '#6c63ff00' }] } },
      },
      {
        name: 'MEM %', type: 'line', smooth: true,
        data: chartData.map(m => m.memoryPercent.toFixed(1)),
        itemStyle: { color: '#4caf50' },
        areaStyle: { color: { type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
          colorStops: [{ offset: 0, color: '#4caf5044' }, { offset: 1, color: '#4caf5000' }] } },
      },
    ],
  };

  const running  = sandboxes.filter(s => s.status === 'Running').length;
  const stopped  = sandboxes.filter(s => s.status === 'Stopped' || s.status === 'Created').length;
  const latest   = metrics[0];

  return (
    <Box sx={{ p: 3, background: '#0a0c1a', minHeight: '100vh' }}>
      {/* Header */}
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          mb: 3
        }}>
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            gap: 1.5
          }}>
          <Terminal sx={{ color: '#6c63ff', fontSize: 32 }} />
          <Box>
            <Typography variant="h5" sx={{
              fontWeight: 700
            }}>Sandbox Monitor</Typography>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>
              BOX1: Isolated execution environments — real-time metrics
            </Typography>
          </Box>
        </Box>
        <Box
          sx={{
            display: "flex",
            gap: 1,
            alignItems: "center"
          }}>
          <Chip icon={<PlayArrow />} label={`${running} Running`}
            sx={{ background: '#4caf5022', color: '#4caf50' }} />
          <Chip icon={<Stop />} label={`${stopped} Idle`}
            sx={{ background: '#ff980022', color: '#ff9800' }} />
          <IconButton onClick={loadSandboxes} sx={{ color: '#6c63ff' }}><Refresh /></IconButton>
        </Box>
      </Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Grid container spacing={3}>
        {/* Sandbox list */}
        <Grid size={{xs: 12, md: 3}}>
          <Typography
            variant="subtitle2"
            sx={{
              color: "text.secondary",
              mb: 1.5
            }}>
            Sandboxes ({sandboxes.length})
          </Typography>
          {loading ? <LinearProgress /> : (
            <Box
              sx={{
                display: "flex",
                flexDirection: "column",
                gap: 1
              }}>
              {sandboxes.length === 0 ? (
                <Alert severity="info" sx={{ fontSize: 12 }}>No sandboxes yet.</Alert>
              ) : sandboxes.map(sb => (
                <SandboxCard key={sb.id} sandbox={sb}
                  selected={selected?.id === sb.id}
                  onClick={() => setSelected(sb)} />
              ))}
            </Box>
          )}
        </Grid>

        {/* Detail panel */}
        <Grid size={{xs: 12, md: 9}}>
          {!selected ? (
            <Box
              sx={{
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                height: 300,
                border: '1px dashed #2a2d3e',
                borderRadius: 3,
                color: '#555'
              }}>
              <Typography>Select a sandbox to view metrics</Typography>
            </Box>
          ) : (
            <>
              {/* Current Metrics */}
              {latest && (
                <Grid container spacing={1.5} sx={{
                  mb: 2
                }}>
                  <Grid size={{xs: 6, sm: 3}}>
                    <MetricGauge label="CPU" value={latest.cpuPercent} max={selected.cpuLimit * 100} unit="%" icon={<Speed fontSize="inherit" />} />
                  </Grid>
                  <Grid size={{xs: 6, sm: 3}}>
                    <MetricGauge label="Memory" value={latest.memoryMb} max={selected.memoryLimitMb} unit="MB" icon={<Memory fontSize="inherit" />} />
                  </Grid>
                  <Grid size={{xs: 6, sm: 3}}>
                    <MetricGauge label="Disk Read" value={latest.diskReadBytes / 1e6} max={100} unit="MB" icon={<Storage fontSize="inherit" />} />
                  </Grid>
                  <Grid size={{xs: 6, sm: 3}}>
                    <MetricGauge label="Net RX" value={latest.networkRxBytes / 1e3} max={10_000} unit="KB" icon={<Wifi fontSize="inherit" />} />
                  </Grid>
                </Grid>
              )}

              {/* Tabs: Chart / Network / Info */}
              <Tabs value={tab} onChange={(_, v) => setTab(v)}
                sx={{ mb: 2, '& .MuiTab-root': { color: '#666' }, '& .Mui-selected': { color: '#6c63ff' } }}>
                <Tab label="Metrics Chart" />
                <Tab label="Network Policy" />
                <Tab label="Info" />
              </Tabs>

              {tab === 0 && (
                <Card sx={{ background: 'linear-gradient(145deg,#1a1d2e,#0f1120)', border: '1px solid #2a2d3e', borderRadius: 3 }}>
                  <CardContent>
                    <Box
                      sx={{
                        display: "flex",
                        justifyContent: "space-between",
                        mb: 1
                      }}>
                      <Typography variant="subtitle2" sx={{
                        color: "text.secondary"
                      }}>CPU & Memory History</Typography>
                      {latest && (
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>
                          Uptime: {fmtUptime(latest.uptimeSeconds)}
                        </Typography>
                      )}
                    </Box>
                    {metrics.length > 0
                      ? <ReactECharts option={metricsChartOption} style={{ height: 260 }} />
                      : <Box
                      sx={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        height: 200
                      }}>
                          <Typography sx={{
                            color: "text.secondary"
                          }}>No metrics yet. Collecting every 30s…</Typography>
                        </Box>
                    }
                  </CardContent>
                </Card>
              )}

              {tab === 1 && (
                <Card sx={{ background: 'linear-gradient(145deg,#1a1d2e,#0f1120)', border: '1px solid #2a2d3e', borderRadius: 3 }}>
                  <CardContent>
                    {!networkPolicy ? (
                      <Alert severity="info">No network policy configured for this sandbox.</Alert>
                    ) : (
                      <Box>
                        <Grid container spacing={2} sx={{
                          mb: 2
                        }}>
                          <Grid size={{xs: 6}}>
                            <Typography variant="caption" sx={{
                              color: "text.secondary"
                            }}>Internet Access</Typography>
                            <Chip label={networkPolicy.allowInternet ? 'Allowed' : 'Blocked'}
                              color={networkPolicy.allowInternet ? 'success' : 'error'} size="small"
                              sx={{ display: 'flex', width: 'fit-content', mt: 0.5 }} />
                          </Grid>
                          <Grid size={{xs: 6}}>
                            <Typography variant="caption" sx={{
                              color: "text.secondary"
                            }}>DNS Provider</Typography>
                            <Typography variant="body2" sx={{
                              fontWeight: 600
                            }}>{networkPolicy.dnsProvider}</Typography>
                          </Grid>
                          <Grid size={{xs: 6}}>
                            <Typography variant="caption" sx={{
                              color: "text.secondary"
                            }}>Bandwidth Limit</Typography>
                            <Typography variant="body2" sx={{
                              fontWeight: 600
                            }}>{networkPolicy.bandwidthLimitKbps} Kbps</Typography>
                          </Grid>
                        </Grid>
                        {networkPolicy.allowedHosts.length > 0 && (
                          <>
                            <Typography
                              variant="caption"
                              sx={{
                                color: "text.secondary",
                                mb: 1,
                                display: "block"
                              }}>
                              Allowlist ({networkPolicy.allowedHosts.length} hosts)
                            </Typography>
                            <Box
                              sx={{
                                display: "flex",
                                gap: 0.5,
                                flexWrap: "wrap"
                              }}>
                              {networkPolicy.allowedHosts.map(h => (
                                <Chip key={h} label={h} size="small"
                                  sx={{ background: '#6c63ff22', color: '#6c63ff' }} />
                              ))}
                            </Box>
                          </>
                        )}
                      </Box>
                    )}
                  </CardContent>
                </Card>
              )}

              {tab === 2 && (
                <Card sx={{ background: 'linear-gradient(145deg,#1a1d2e,#0f1120)', border: '1px solid #2a2d3e', borderRadius: 3 }}>
                  <CardContent>
                    <Table size="small">
                      <TableBody>
                        {[
                          ['Sandbox ID', selected.id],
                          ['Task ID', selected.taskId],
                          ['Container', selected.containerId ?? '—'],
                          ['Image', selected.image],
                          ['CPU Limit', `${selected.cpuLimit} cores`],
                          ['Memory Limit', `${selected.memoryLimitMb} MB`],
                          ['Status', selected.status],
                          ['Started', selected.startedAt ? new Date(selected.startedAt).toLocaleString() : '—'],
                          ['Created', new Date(selected.createdAt).toLocaleString()],
                        ].map(([k, v]) => (
                          <TableRow key={k}>
                            <TableCell sx={{ color: '#888', border: 0, width: 140, py: 0.5 }}>{k}</TableCell>
                            <TableCell sx={{ color: '#ddd', border: 0, fontFamily: 'monospace', py: 0.5, fontSize: 12 }}>{v}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </CardContent>
                </Card>
              )}
            </>
          )}
        </Grid>
      </Grid>
    </Box>
  );
}
