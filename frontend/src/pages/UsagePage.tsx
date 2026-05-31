import QueryStatsIcon from '@mui/icons-material/QueryStats'
import AttachMoneyIcon from '@mui/icons-material/AttachMoney'
import MemoryIcon from '@mui/icons-material/Memory'
import BoltIcon from '@mui/icons-material/Bolt'
import NetworkCheckIcon from '@mui/icons-material/NetworkCheck'
import AnalyticsIcon from '@mui/icons-material/Analytics'
import {
  Alert,
  Box,
  Card,
  CardContent,
  CircularProgress,
  Divider,
  Grid,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import { useCallback, useEffect, useState } from 'react'
import { apiClient } from '../api/client'
import { useProjectContext } from '../context/useProjectContext'
import type { BoxUsageSummary } from '../types'

function StatCard({ title, value, icon, color = 'primary.main', subtitle }: { title: string; value: string | number; icon: React.ReactNode; color?: string; subtitle?: string }) {
  return (
    <Card elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 3, height: '100%' }}>
      <CardContent>
        <Stack direction="row" spacing={2} sx={{
          alignItems: "flex-start"
        }}>
          <Box sx={{ p: 1.5, borderRadius: 2, bgcolor: `${color}15`, color }}>
            {icon}
          </Box>
          <Box sx={{
            flex: 1
          }}>
            <Typography
              variant="body2"
              sx={{
                color: "text.secondary",
                fontWeight: 600,
                mb: 0.5
              }}>
              {title}
            </Typography>
            <Typography variant="h4" sx={{
              fontWeight: 700
            }}>
              {value}
            </Typography>
            {subtitle && (
              <Typography
                variant="caption"
                sx={{
                  color: "text.secondary",
                  display: 'block',
                  mt: 0.5
                }}>
                {subtitle}
              </Typography>
            )}
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}

export function UsagePage() {
  const { selectedProjectId } = useProjectContext()
  const [data, setData] = useState<BoxUsageSummary | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const fetchUsage = useCallback(async () => {
    if (!selectedProjectId) return
    setLoading(true)
    setError('')
    try {
      const summary = await apiClient.getUsageSummary(selectedProjectId)
      setData(summary)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load usage data')
    } finally {
      setLoading(false)
    }
  }, [selectedProjectId])

  useEffect(() => {
    fetchUsage()
  }, [fetchUsage])

  if (!selectedProjectId) {
    return (
      <Paper elevation={0} sx={{ p: 4, textAlign: 'center', border: '1px dashed', borderColor: 'divider', borderRadius: 3 }}>
        <QueryStatsIcon sx={{ fontSize: 56, color: 'action.disabled', mb: 2 }} />
        <Typography variant="h6" sx={{
          color: "text.secondary"
        }}>Select a Box to view usage</Typography>
      </Paper>
    );
  }

  return (
    <Stack spacing={3}>
      <Stack direction="row" spacing={1} sx={{
        alignItems: "center"
      }}>
        <QueryStatsIcon color="primary" sx={{ fontSize: 28 }} />
        <Typography variant="h5" sx={{
          fontWeight: 600
        }}>Usage & Metrics</Typography>
      </Stack>
      {error && <Alert severity="error" variant="outlined" onClose={() => setError('')}>{error}</Alert>}
      {loading && !data && (
        <Stack
          sx={{
            alignItems: "center",
            py: 4
          }}>
          <CircularProgress />
        </Stack>
      )}
      {data && (
        <>
          <Grid container spacing={3}>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <StatCard
                title="Total Cost"
                value={`$${data.totalCostUsd.toFixed(4)}`}
                icon={<AttachMoneyIcon fontSize="medium" />}
                color="#0f5132"
                subtitle="Estimated USD"
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <StatCard
                title="Total Tokens"
                value={(data.totalTokensInput + data.totalTokensOutput).toLocaleString()}
                icon={<MemoryIcon fontSize="medium" />}
                color="#084298"
                subtitle={`${data.totalTokensInput.toLocaleString()} IN / ${data.totalTokensOutput.toLocaleString()} OUT`}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <StatCard
                title="Agent Runs"
                value={data.totalRuns.toLocaleString()}
                icon={<BoltIcon fontSize="medium" />}
                color="#8540f5"
                subtitle="Total executions"
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <StatCard
                title="Success Rate"
                value={`${data.successRatePct.toFixed(1)}%`}
                icon={<NetworkCheckIcon fontSize="medium" />}
                color={data.successRatePct >= 90 ? '#0f5132' : data.successRatePct >= 70 ? '#664d03' : '#842029'}
              />
            </Grid>
          </Grid>

          <Card elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 3 }}>
            <CardContent>
              <Stack
                direction="row"
                spacing={1}
                sx={{
                  alignItems: "center",
                  mb: 3
                }}>
                <AnalyticsIcon color="action" />
                <Typography variant="h6" sx={{
                  fontWeight: 600
                }}>Model Breakdown</Typography>
              </Stack>
              
              {Object.keys(data.runsByModel).length === 0 ? (
                <Typography
                  sx={{
                    color: "text.secondary",
                    textAlign: "center",
                    py: 4
                  }}>
                  No model usage data available yet.
                </Typography>
              ) : (
                <Stack spacing={2} divider={<Divider />}>
                  {Object.entries(data.runsByModel)
                    .sort(([, a], [, b]) => b - a)
                    .map(([model, count]) => (
                      <Stack
                        key={model}
                        direction="row"
                        sx={{
                          justifyContent: "space-between",
                          alignItems: "center"
                        }}>
                        <Typography variant="body1" sx={{
                          fontWeight: 500
                        }}>{model}</Typography>
                        <Stack direction="row" spacing={2} sx={{
                          alignItems: "center"
                        }}>
                          <Typography variant="body2" sx={{
                            color: "text.secondary"
                          }}>{count} runs</Typography>
                          <Box sx={{ height: 6, width: 100, bgcolor: 'action.hover', borderRadius: 3, overflow: 'hidden' }}>
                            <Box sx={{ height: '100%', width: `${(count / data.totalRuns) * 100}%`, bgcolor: 'primary.main' }} />
                          </Box>
                        </Stack>
                      </Stack>
                  ))}
                </Stack>
              )}
            </CardContent>
          </Card>
        </>
      )}
    </Stack>
  );
}
