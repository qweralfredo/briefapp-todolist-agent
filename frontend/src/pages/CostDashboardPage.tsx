import { useState, useEffect, useCallback } from 'react';
import {
  Box, Grid, Card, CardContent, Typography, Chip,
  LinearProgress, Button, Dialog, DialogTitle, DialogContent,
  DialogActions, TextField, Table, TableHead, TableRow,
  TableCell, TableBody, Alert, Tooltip, IconButton,
} from '@mui/material';
import {
  MonetizationOn, Warning, Block, CheckCircle,
  DeleteForever, Refresh, TrendingUp,
} from '@mui/icons-material';
import ReactECharts from 'echarts-for-react';

// ── Types ─────────────────────────────────────────────────────────────────────

interface BudgetStats {
  id: string;
  scope: number;
  scopeId: string;
  budgetTokens: number;
  usedTokens: number;
  utilizationPercent: number;
  remainingTokens: number;
  alertThresholdPercent: number;
  hardStopPercent: number;
  frozen: boolean;
  alertTriggered: boolean;
  hardStopTriggered: boolean;
  updatedAt: string;
}

interface RateLimitStatus {
  provider: string;
  maxRpm: number;
  currentRpm: number;
  utilizationPercent: number;
}

const SCOPE_LABELS = ['Platform', 'Box', 'Sprint', 'Task'];

// ── Helpers ───────────────────────────────────────────────────────────────────

const fmtTokens = (n: number) =>
  n >= 1e9 ? `${(n / 1e9).toFixed(1)}B` :
  n >= 1e6 ? `${(n / 1e6).toFixed(1)}M` :
  n >= 1e3 ? `${(n / 1e3).toFixed(1)}K` : String(n);

const utilizationColor = (pct: number) =>
  pct >= 100 ? '#f44336' : pct >= 80 ? '#ff9800' : '#4caf50';

// ── BudgetCard ────────────────────────────────────────────────────────────────

function BudgetCard({ budget, onKillSwitch }: { budget: BudgetStats; onKillSwitch: (b: BudgetStats) => void }) {
  const color = utilizationColor(budget.utilizationPercent);

  return (
    <Card sx={{
      background: 'linear-gradient(145deg,#1a1d2e,#0f1120)',
      border: `1px solid ${budget.frozen ? '#f44336' : budget.alertTriggered ? '#ff9800' : '#2a2d3e'}`,
      borderRadius: 3, position: 'relative', overflow: 'visible',
    }}>
      {budget.frozen && (
        <Chip icon={<Block fontSize="small" />} label="FROZEN" color="error" size="small"
          sx={{ position: 'absolute', top: -10, right: 12, fontWeight: 700 }} />
      )}
      {budget.alertTriggered && !budget.frozen && (
        <Chip icon={<Warning fontSize="small" />} label="ALERT" color="warning" size="small"
          sx={{ position: 'absolute', top: -10, right: 12, fontWeight: 700 }} />
      )}
      <CardContent>
        <Box
          sx={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-start",
            mb: 1
          }}>
          <Box>
            <Chip label={SCOPE_LABELS[budget.scope]} size="small"
              sx={{ background: '#6c63ff22', color: '#6c63ff', mb: 0.5 }} />
            <Typography
              variant="body2"
              noWrap
              sx={{
                color: "text.secondary",
                maxWidth: 180
              }}>
              {budget.scopeId}
            </Typography>
          </Box>
          <Tooltip title="Emergency Kill Switch">
            <IconButton size="small" onClick={() => onKillSwitch(budget)}
              sx={{ color: '#f44336', '&:hover': { background: '#f4433622' } }}>
              <DeleteForever fontSize="small" />
            </IconButton>
          </Tooltip>
        </Box>

        {/* Utilization gauge */}
        <Box sx={{
          mb: 1.5
        }}>
          <Box
            sx={{
              display: "flex",
              justifyContent: "space-between",
              mb: 0.5
            }}>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>Utilization</Typography>
            <Typography variant="caption" sx={{ color, fontWeight: 700 }}>
              {budget.utilizationPercent.toFixed(1)}%
            </Typography>
          </Box>
          <LinearProgress variant="determinate" value={Math.min(100, budget.utilizationPercent)}
            sx={{
              height: 8, borderRadius: 4,
              backgroundColor: '#2a2d3e',
              '& .MuiLinearProgress-bar': { backgroundColor: color, borderRadius: 4 },
            }} />
        </Box>

        {/* Token counters */}
        <Grid container spacing={1}>
          <Grid size={{xs: 4}}>
            <Typography
              variant="caption"
              sx={{
                color: "text.secondary",
                display: "block"
              }}>Used</Typography>
            <Typography variant="body2" sx={{
              fontWeight: 700
            }}>{fmtTokens(budget.usedTokens)}</Typography>
          </Grid>
          <Grid size={{xs: 4}}>
            <Typography
              variant="caption"
              sx={{
                color: "text.secondary",
                display: "block"
              }}>Budget</Typography>
            <Typography variant="body2" sx={{
              fontWeight: 700
            }}>{fmtTokens(budget.budgetTokens)}</Typography>
          </Grid>
          <Grid size={{xs: 4}}>
            <Typography
              variant="caption"
              sx={{
                color: "text.secondary",
                display: "block"
              }}>Left</Typography>
            <Typography
              variant="body2"
              sx={{
                fontWeight: 700,
                color: budget.remainingTokens < budget.budgetTokens * 0.2 ? '#f44336' : '#4caf50'
              }}>
              {fmtTokens(budget.remainingTokens)}
            </Typography>
          </Grid>
        </Grid>
      </CardContent>
    </Card>
  );
}

// ── CostDashboardPage ─────────────────────────────────────────────────────────

export default function CostDashboardPage() {
  const [budgets,    setBudgets]    = useState<BudgetStats[]>([]);
  const [rateLimits, setRateLimits] = useState<RateLimitStatus[]>([]);
  const [loading,    setLoading]    = useState(true);
  const [error,      setError]      = useState<string | null>(null);

  // Kill switch dialog
  const [killTarget,    setKillTarget]    = useState<BudgetStats | null>(null);
  const [killConfirm,   setKillConfirm]   = useState('');
  const [killSubmitting, setKillSubmitting] = useState(false);

  // Budget config dialog
  const [configTarget,  setConfigTarget]  = useState<BudgetStats | null>(null);
  const [budgetInput,   setBudgetInput]   = useState('');
  const [alertInput,    setAlertInput]    = useState('80');

  const load = useCallback(async () => {
    try {
      const [bRes, rRes] = await Promise.all([
        fetch('/api/budget'),
        fetch('/api/rate-limit'),
      ]);
      if (bRes.ok) setBudgets(await bRes.json());
      if (rRes.ok) setRateLimits(await rRes.json());
      setError(null);
    } catch (e: any) {
      setError(e.message ?? 'Failed to load data');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); const t = setInterval(load, 10_000); return () => clearInterval(t); }, [load]);

  const handleKillSwitch = async () => {
    if (!killTarget || killConfirm !== 'KILL') return;
    setKillSubmitting(true);
    await fetch(`/api/budget/${killTarget.scope}/${killTarget.scopeId}/kill`, { method: 'POST' });
    setKillTarget(null); setKillConfirm(''); setKillSubmitting(false); load();
  };

  const handleConfigSave = async () => {
    if (!configTarget) return;
    await fetch(`/api/budget/${configTarget.scope}/${configTarget.scopeId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ budgetTokens: Number(budgetInput), alertThresholdPercent: Number(alertInput) }),
    });
    setConfigTarget(null); load();
  };

  // ECharts: utilization pie by scope
  const pieOption = {
    backgroundColor: 'transparent',
    tooltip: { trigger: 'item', formatter: '{b}: {d}%' },
    legend: { textStyle: { color: '#aaa' }, bottom: 0 },
    series: [{
      type: 'pie', radius: ['40%','70%'],
      label: { color: '#ccc' },
      data: budgets.map(b => ({
        name: `${SCOPE_LABELS[b.scope]} (${b.scopeId.slice(0,8)})`,
        value: b.usedTokens,
      })),
      itemStyle: { borderRadius: 6 },
    }],
  };

  const totalUsed   = budgets.reduce((s, b) => s + b.usedTokens, 0);
  const totalBudget = budgets.reduce((s, b) => s + (b.budgetTokens < 9e18 ? b.budgetTokens : 0), 0);
  const frozen      = budgets.filter(b => b.frozen).length;
  const alerts      = budgets.filter(b => b.alertTriggered && !b.frozen).length;

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
          <MonetizationOn sx={{ color: '#6c63ff', fontSize: 32 }} />
          <Box>
            <Typography variant="h5" sx={{
              fontWeight: 700
            }}>Cost Guard & Token Budget</Typography>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>Hierarchical budget monitoring — Platform → Box → Sprint → Task</Typography>
          </Box>
        </Box>
        <IconButton onClick={load} sx={{ color: '#6c63ff' }}><Refresh /></IconButton>
      </Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {/* Summary chips */}
      <Box
        sx={{
          display: "flex",
          gap: 1.5,
          mb: 3,
          flexWrap: "wrap"
        }}>
        <Chip icon={<TrendingUp />}    label={`${fmtTokens(totalUsed)} / ${fmtTokens(totalBudget)} tokens used`} sx={{ background: '#6c63ff22', color: '#6c63ff' }} />
        <Chip icon={<Warning />}       label={`${alerts} alert(s)`} color={alerts > 0 ? 'warning' : 'default'} />
        <Chip icon={<Block />}         label={`${frozen} frozen`}    color={frozen > 0 ? 'error' : 'default'} />
        <Chip icon={<CheckCircle />}   label={`${budgets.length} scopes tracked`} sx={{ background: '#4caf5022', color: '#4caf50' }} />
      </Box>
      <Grid container spacing={3}>
        {/* Budget cards */}
        <Grid size={{xs: 12, md: 7}}>
          <Typography
            variant="subtitle2"
            sx={{
              color: "text.secondary",
              mb: 1.5
            }}>Token Budgets</Typography>
          {loading ? <LinearProgress /> : (
            <Grid container spacing={2}>
              {budgets.length === 0 ? (
                <Grid size={{xs: 12}}>
                  <Alert severity="info">No budgets configured yet. Use the MCP tool <code>budget_status</code> or POST /api/budget to create one.</Alert>
                </Grid>
              ) : budgets.map(b => (
                <Grid key={b.id} size={{xs: 12, sm: 6}}>
                  <BudgetCard budget={b} onKillSwitch={setKillTarget} />
                </Grid>
              ))}
            </Grid>
          )}
        </Grid>

        {/* Pie chart + Rate limits */}
        <Grid size={{xs: 12, md: 5}}>
          {budgets.length > 0 && (
            <Card sx={{ background: 'linear-gradient(145deg,#1a1d2e,#0f1120)', border: '1px solid #2a2d3e', borderRadius: 3, mb: 3 }}>
              <CardContent>
                <Typography
                  variant="subtitle2"
                  sx={{
                    color: "text.secondary",
                    mb: 1
                  }}>Token Usage Distribution</Typography>
                <ReactECharts option={pieOption} style={{ height: 220 }} />
              </CardContent>
            </Card>
          )}

          {/* Rate limits table */}
          <Card sx={{ background: 'linear-gradient(145deg,#1a1d2e,#0f1120)', border: '1px solid #2a2d3e', borderRadius: 3 }}>
            <CardContent>
              <Typography
                variant="subtitle2"
                sx={{
                  color: "text.secondary",
                  mb: 1.5
                }}>LLM Rate Limits (RPM)</Typography>
              {rateLimits.length === 0 ? (
                <Typography variant="body2" sx={{
                  color: "text.secondary"
                }}>No provider data</Typography>
              ) : (
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell sx={{ color: '#aaa', border: 0 }}>Provider</TableCell>
                      <TableCell sx={{ color: '#aaa', border: 0 }}>Util %</TableCell>
                      <TableCell sx={{ color: '#aaa', border: 0 }}>RPM</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {rateLimits.map(r => (
                      <TableRow key={r.provider}>
                        <TableCell sx={{ border: 0, color: '#ddd', textTransform: 'capitalize' }}>{r.provider}</TableCell>
                        <TableCell sx={{ border: 0 }}>
                          <LinearProgress variant="determinate" value={Math.min(100, r.utilizationPercent)}
                            sx={{ height: 6, borderRadius: 3, background: '#2a2d3e',
                              '& .MuiLinearProgress-bar': { background: utilizationColor(r.utilizationPercent) } }} />
                        </TableCell>
                        <TableCell sx={{ border: 0, color: '#ddd' }}>{r.currentRpm}/{r.maxRpm}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>
      {/* Kill Switch Dialog */}
      <Dialog open={!!killTarget} onClose={() => { setKillTarget(null); setKillConfirm(''); }}>
        <DialogTitle sx={{ color: '#f44336' }}>⚠ Emergency Kill Switch</DialogTitle>
        <DialogContent>
          <Alert severity="error" sx={{ mb: 2 }}>
            This will immediately freeze <strong>{killTarget ? `${SCOPE_LABELS[killTarget.scope]} / ${killTarget.scopeId}` : ''}</strong>.
            All new task submissions will be blocked until manually unfrozen.
          </Alert>
          <TextField fullWidth label='Type "KILL" to confirm' value={killConfirm}
            onChange={e => setKillConfirm(e.target.value)} size="small" />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { setKillTarget(null); setKillConfirm(''); }}>Cancel</Button>
          <Button color="error" variant="contained" disabled={killConfirm !== 'KILL' || killSubmitting}
            onClick={handleKillSwitch}>Activate Kill Switch</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
