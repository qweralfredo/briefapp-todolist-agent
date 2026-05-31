import { Box, Typography, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Chip, IconButton, Collapse } from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import { apiClient } from '../api/client'
import { useProjectContext } from '../context/useProjectContext'
import RefreshIcon from '@mui/icons-material/Refresh'
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown'
import KeyboardArrowUpIcon from '@mui/icons-material/KeyboardArrowUp'
import { useState } from 'react'

function AgentRunRow({ run }: { run: any }) {
  const [open, setOpen] = useState(false)

  return (
    <>
      <TableRow
        sx={{ cursor: 'pointer', '&:hover': { bgcolor: 'action.hover' } }}
        onClick={() => setOpen(!open)}
      >
        <TableCell padding="checkbox">
          <IconButton size="small">
            {open ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
          </IconButton>
        </TableCell>
        <TableCell>{new Date(run.startedAt).toLocaleString()}</TableCell>
        <TableCell>{run.agentName}</TableCell>
        <TableCell>{run.entryPoint}</TableCell>
        <TableCell>
          <Chip
            label={run.status}
            size="small"
            color={
              run.status?.toLowerCase() === 'completed' || run.status?.toLowerCase() === 'success' ? 'success' :
              run.status?.toLowerCase() === 'failed' ? 'error' :
              run.status?.toLowerCase() === 'running' ? 'warning' : 'default'
            }
          />
        </TableCell>
        <TableCell>{run.modelName}</TableCell>
        <TableCell align="right">{run.costUsd?.toFixed(4)}</TableCell>
        <TableCell align="right">{run.latencyMs}</TableCell>
      </TableRow>
      <TableRow>
        <TableCell style={{ paddingBottom: 0, paddingTop: 0 }} colSpan={8}>
          <Collapse in={open} timeout="auto" unmountOnExit>
            <Box sx={{ m: 2 }}>
              {run.inputSummary && (
                <Box sx={{ mb: 1 }}>
                  <Typography variant="subtitle2" color="text.secondary" gutterBottom>
                    Input Summary
                  </Typography>
                  <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', bgcolor: 'grey.50', p: 1, borderRadius: 1 }}>
                    {run.inputSummary}
                  </Typography>
                </Box>
              )}
              {run.outputSummary && (
                <Box sx={{ mb: 1 }}>
                  <Typography variant="subtitle2" color="text.secondary" gutterBottom>
                    Output / Feedback
                  </Typography>
                  <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', bgcolor: 'grey.50', p: 1, borderRadius: 1 }}>
                    {run.outputSummary}
                  </Typography>
                </Box>
              )}
              {run.errorMessage && (
                <Box sx={{ mb: 1 }}>
                  <Typography variant="subtitle2" color="error" gutterBottom>
                    Error
                  </Typography>
                  <Typography variant="body2" color="error" sx={{ whiteSpace: 'pre-wrap', bgcolor: 'error.50', p: 1, borderRadius: 1 }}>
                    {run.errorMessage}
                  </Typography>
                </Box>
              )}
              {!run.inputSummary && !run.outputSummary && !run.errorMessage && (
                <Typography variant="body2" color="text.secondary" sx={{ fontStyle: 'italic' }}>
                  No feedback details available for this run.
                </Typography>
              )}
              <Box sx={{ display: 'flex', gap: 2, mt: 1 }}>
                {run.tokensInput > 0 && (
                  <Typography variant="caption" color="text.secondary">
                    Tokens In: {run.tokensInput?.toLocaleString()}
                  </Typography>
                )}
                {run.tokensOutput > 0 && (
                  <Typography variant="caption" color="text.secondary">
                    Tokens Out: {run.tokensOutput?.toLocaleString()}
                  </Typography>
                )}
                {run.finishedAt && (
                  <Typography variant="caption" color="text.secondary">
                    Finished: {new Date(run.finishedAt).toLocaleString()}
                  </Typography>
                )}
              </Box>
            </Box>
          </Collapse>
        </TableCell>
      </TableRow>
    </>
  )
}

export function AgentDashboardPage() {
  const { selectedProjectId } = useProjectContext()

  const { data: knowledge, isLoading, refetch } = useQuery({
    queryKey: ['knowledge', selectedProjectId],
    queryFn: () => apiClient.getKnowledge(selectedProjectId!),
    enabled: !!selectedProjectId,
    refetchInterval: 10000 // auto-refresh every 10s
  })

  const agentRuns = knowledge?.agentRuns || []

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3, p: 3, height: '100%' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography variant="h4" fontWeight="bold">
          Agent Executions
        </Typography>
        <IconButton onClick={() => refetch()} color="primary">
          <RefreshIcon />
        </IconButton>
      </Box>

      {/* Tabela de Execuções */}
      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" sx={{ mb: 2 }}>Run History</Typography>
        {!selectedProjectId && (
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2, fontStyle: 'italic' }}>
            Select a project from the top-right dropdown to see agent runs.
          </Typography>
        )}
        <TableContainer sx={{ maxHeight: 500 }}>
          <Table stickyHeader size="small">
            <TableHead>
              <TableRow>
                <TableCell padding="checkbox" />
                <TableCell>Started At</TableCell>
                <TableCell>Agent</TableCell>
                <TableCell>Entry Point</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Model</TableCell>
                <TableCell align="right">Cost ($)</TableCell>
                <TableCell align="right">Latency (ms)</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <TableRow><TableCell colSpan={8} align="center">Loading...</TableCell></TableRow>
              ) : agentRuns.length === 0 ? (
                <TableRow><TableCell colSpan={8} align="center">No agent runs found.</TableCell></TableRow>
              ) : (
                agentRuns.map((run: any) => (
                  <AgentRunRow key={run.id} run={run} />
                ))
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </Paper>

      {/* Jaeger Iframe */}
      <Paper sx={{ p: 2, flexGrow: 1, display: 'flex', flexDirection: 'column', minHeight: 600 }}>
        <Typography variant="h6" sx={{ mb: 2 }}>Jaeger Traces</Typography>
        <Box sx={{ flexGrow: 1, display: 'flex', border: '1px solid #ccc', borderRadius: 1, overflow: 'hidden' }}>
          <iframe
            src="http://localhost:16686/search"
            title="Jaeger UI"
            width="100%"
            style={{ border: 'none', flexGrow: 1 }}
          />
        </Box>
      </Paper>
    </Box>
  )
}

