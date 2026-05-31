import { useState, useEffect, useCallback } from 'react';
import {
  Box, Grid, Card, CardContent, Typography, Chip,
  LinearProgress, Button, Table, TableHead, TableRow,
  TableCell, TableBody, Alert, IconButton, Tooltip
} from '@mui/material';
import {
  Speed, MoneyOff, Storage, History,
  Refresh, LocalFireDepartment, DeleteSweep, Settings
} from '@mui/icons-material';
import ReactECharts from 'echarts-for-react';

interface CacheStats {
  totalHits: number;
  totalMisses: number;
  hitRatePercent: number;
  totalTokensCached: number;
  segmentCount: number;
}

interface BudgetStats {
  id: string;
  scope: number;
  scopeId: string;
  usedTokens: number;
  cachedTokensSaved: number;
}

interface CacheSegment {
  id: string;
  boxId: string;
  segmentType: number;
  contentHash: string;
  tokenCount: number;
  hitCount: number;
  missCount: number;
  lastUsedAt: string;
  ttlMinutes: number;
}

const SEGMENT_TYPES = ['SystemPrompt', 'ProjectContext', 'ToolDefinitions', 'UserPreferences', 'SessionHistory'];

// Assume an average of $3.00 per 1M valid context tokens.
const TOKENS_TO_USD = 0.000003; 

export default function PromptCachePage() {
  const [stats, setStats] = useState<CacheStats | null>(null);
  const [budgets, setBudgets] = useState<BudgetStats[]>([]);
  const [segments, setSegments] = useState<CacheSegment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    try {
      const [statsRes, budgetsRes, segmentsRes] = await Promise.all([
        fetch('/api/prompt-cache/stats'),
        fetch('/api/budget'),
        fetch('/api/prompt-cache/segments/all')
      ]);

      if (statsRes.ok) setStats(await statsRes.json());
      if (budgetsRes.ok) setBudgets(await budgetsRes.json());
      if (segmentsRes.ok) setSegments(await segmentsRes.json());
      setError(null);
    } catch (e: any) {
      setError(e.message || 'Failed to fetch cache dashboard data');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
    const t = setInterval(loadData, 10000);
    return () => clearInterval(t);
  }, [loadData]);

  const handleWarmCache = async () => {
    // Ideally this would take a box_id from a state, 0000..0 is a stub for global/default box warmup
    const defaultBoxId = "00000000-0000-0000-0000-000000000000";
    await fetch(`/api/prompt-cache/${defaultBoxId}/warm`, { method: 'POST' });
    loadData();
  };

  const handleClearCache = async () => {
    const defaultBoxId = "00000000-0000-0000-0000-000000000000";
    await fetch(`/api/prompt-cache/${defaultBoxId}/invalidate`, { method: 'POST' });
    loadData();
  };

  if (!stats && loading) return <LinearProgress />;

  // Gauge Options
  const gaugeOption = {
    series: [
      {
        type: 'gauge',
        startAngle: 180,
        endAngle: 0,
        min: 0,
        max: 100,
        splitNumber: 4,
        itemStyle: {
          color: stats && stats.hitRatePercent > 70 ? '#4caf50' : stats && stats.hitRatePercent > 40 ? '#ff9800' : '#f44336'
        },
        progress: { show: true, width: 24 },
        pointer: { show: false },
        axisLine: { lineStyle: { width: 24 } },
        axisTick: { show: false },
        splitLine: { show: false },
        axisLabel: { show: false },
        title: { show: false },
        detail: {
          valueAnimation: true,
          offsetCenter: [0, '-10%'],
          fontSize: 32,
          color: '#fff',
          formatter: '{value}%'
        },
        data: [{ value: stats ? stats.hitRatePercent.toFixed(1) : 0 }]
      }
    ]
  };

  // Pie options (Savings by Scope/Box)
  const pieData = budgets
    .filter(b => b.cachedTokensSaved > 0)
    .map(b => ({
      name: b.scopeId.slice(0, 8),
      value: b.cachedTokensSaved
    }));

  const pieOption = {
    backgroundColor: 'transparent',
    tooltip: { trigger: 'item', formatter: '{b}: {c} tokens' },
    series: [{
      type: 'pie',
      radius: ['40%', '70%'],
      label: { color: '#ccc' },
      data: pieData.length > 0 ? pieData : [{ name: 'No Savings', value: 0 }],
      itemStyle: { borderRadius: 6 }
    }]
  };

  const totalTokensSaved = budgets.reduce((acc, b) => acc + b.cachedTokensSaved, 0);
  const estimatedCostSaved = (totalTokensSaved * TOKENS_TO_USD).toFixed(2);

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
          <Speed sx={{ color: '#00e676', fontSize: 32 }} />
          <Box>
            <Typography variant="h5" sx={{
              fontWeight: 700
            }}>Prompt Caching</Typography>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>Optimize LLM performance & reduce bandwidth</Typography>
          </Box>
        </Box>
        <Box>
          <Tooltip title="Refresh">
            <IconButton onClick={loadData} sx={{ color: '#6c63ff', mr: 1 }}><Refresh /></IconButton>
          </Tooltip>
          <Button variant="outlined" startIcon={<LocalFireDepartment />} size="small" onClick={handleWarmCache} sx={{ mr: 1, borderColor: '#ff9800', color: '#ff9800' }}>Warm Cache</Button>
          <Button variant="outlined" startIcon={<DeleteSweep />} size="small" color="error" onClick={handleClearCache}>Clear Cache</Button>
        </Box>
      </Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Grid container spacing={3} sx={{
        mb: 3
      }}>
        {/* Hit Rate Gauge */}
        <Grid size={{xs: 12, md: 4}}>
          <Card sx={{ background: 'linear-gradient(145deg,#1a1d2e,#0f1120)', border: '1px solid #2a2d3e', borderRadius: 3, height: '100%' }}>
            <CardContent>
              <Typography
                variant="subtitle2"
                sx={{
                  color: "text.secondary",
                  mb: 1,
                  display: "flex",
                  alignItems: "center",
                  gap: 1
                }}>
                <History fontSize="small" /> Cache Hit Rate
              </Typography>
              <ReactECharts option={gaugeOption} style={{ height: 200 }} />
              <Box
                sx={{
                  display: "flex",
                  justifyContent: "space-around",
                  mt: -2
                }}>
                <Box sx={{
                  textAlign: "center"
                }}>
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>Hits</Typography>
                  <Typography variant="h6" sx={{
                    color: "#4caf50"
                  }}>{stats?.totalHits || 0}</Typography>
                </Box>
                <Box sx={{
                  textAlign: "center"
                }}>
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>Misses</Typography>
                  <Typography variant="h6" sx={{
                    color: "#f44336"
                  }}>{stats?.totalMisses || 0}</Typography>
                </Box>
              </Box>
            </CardContent>
          </Card>
        </Grid>

        {/* Total Tokens / Savings */}
        <Grid size={{xs: 12, md: 4}}>
          <Card sx={{ background: 'linear-gradient(145deg,#1a1d2e,#0f1120)', border: '1px solid #2a2d3e', borderRadius: 3, height: '100%' }}>
            <CardContent>
              <Typography
                variant="subtitle2"
                sx={{
                  color: "text.secondary",
                  mb: 3,
                  display: "flex",
                  alignItems: "center",
                  gap: 1
                }}>
                <MoneyOff fontSize="small" /> Token & Cost Savings
              </Typography>
              
              <Box sx={{
                mb: 3
              }}>
                <Typography
                  variant="h3"
                  sx={{
                    fontWeight: 700,
                    color: "#00e676"
                  }}>
                  {totalTokensSaved.toLocaleString()}
                </Typography>
                <Typography variant="body2" sx={{
                  color: "text.secondary"
                }}>Total Cached Tokens Recovered</Typography>
              </Box>

              <Box>
                <Typography
                  variant="h4"
                  sx={{
                    fontWeight: 400,
                    color: "#6c63ff"
                  }}>
                  ~US$ {estimatedCostSaved}
                </Typography>
                <Typography variant="body2" sx={{
                  color: "text.secondary"
                }}>Estimated Amount Saved ($3.00/1M input avg)</Typography>
              </Box>
            </CardContent>
          </Card>
        </Grid>

        {/* Pie By Box */}
        <Grid size={{xs: 12, md: 4}}>
          <Card sx={{ background: 'linear-gradient(145deg,#1a1d2e,#0f1120)', border: '1px solid #2a2d3e', borderRadius: 3, height: '100%' }}>
            <CardContent>
              <Typography
                variant="subtitle2"
                sx={{
                  color: "text.secondary",
                  mb: 1,
                  display: "flex",
                  alignItems: "center",
                  gap: 1
                }}>
                <Storage fontSize="small" /> Savings Distribution
              </Typography>
              <ReactECharts option={pieOption} style={{ height: 240 }} />
            </CardContent>
          </Card>
        </Grid>
      </Grid>
      {/* Segments Table */}
      <Card sx={{ background: 'linear-gradient(145deg,#1a1d2e,#0f1120)', border: '1px solid #2a2d3e', borderRadius: 3 }}>
        <CardContent>
          <Box
            sx={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              mb: 2
            }}>
            <Typography variant="subtitle2" sx={{
              color: "text.secondary"
            }}>Global Cache Segments Explorer</Typography>
            <Chip label={`${stats?.segmentCount || 0} Segment(s) Stored`} size="small" />
          </Box>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ color: '#aaa', border: 0 }}>Type</TableCell>
                <TableCell sx={{ color: '#aaa', border: 0 }}>Hash (Trunc.)</TableCell>
                <TableCell sx={{ color: '#aaa', border: 0 }}>Box ID</TableCell>
                <TableCell sx={{ color: '#aaa', border: 0 }}>Tokens</TableCell>
                <TableCell sx={{ color: '#aaa', border: 0 }}>Hits/Miss</TableCell>
                <TableCell sx={{ color: '#aaa', border: 0 }}>Last Used</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {segments.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} sx={{ border: 0, textAlign: 'center', py: 4, color: '#666' }}>No active segments in cache</TableCell>
                </TableRow>
              ) : (
                segments.map(s => (
                  <TableRow key={s.id}>
                    <TableCell sx={{ border: 0 }}>
                      <Chip label={SEGMENT_TYPES[s.segmentType] || `Type ${s.segmentType}`} size="small" sx={{ background: '#6c63ff22', color: '#6c63ff' }} />
                    </TableCell>
                    <TableCell sx={{ border: 0, color: '#ccc', fontFamily: 'monospace' }}>
                      {s.contentHash.slice(0, 8)}...
                    </TableCell>
                    <TableCell sx={{ border: 0, color: '#ccc', fontSize: '0.8rem' }}>
                      {s.boxId.slice(0, 8)} 
                    </TableCell>
                    <TableCell sx={{ border: 0, color: '#fff', fontWeight: 500 }}>
                      {s.tokenCount.toLocaleString()}
                    </TableCell>
                    <TableCell sx={{ border: 0 }}>
                      <span style={{ color: '#4caf50' }}>{s.hitCount}</span> / <span style={{ color: '#f44336' }}>{s.missCount}</span>
                    </TableCell>
                    <TableCell sx={{ border: 0, color: '#888', fontSize: '0.85rem' }}>
                      {new Date(s.lastUsedAt).toLocaleString()}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </Box>
  );
}
