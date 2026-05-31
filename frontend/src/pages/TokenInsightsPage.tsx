import InsightsOutlinedIcon from '@mui/icons-material/InsightsOutlined'
import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  Grid,
  Stack,
  Typography,
} from '@mui/material'
import ReactECharts from 'echarts-for-react'
import { useMemo } from 'react'
import { useProjectContext } from '../context/useProjectContext'

type TokenFeedback = {
  model: string
  ide: string
  agent: string
  tokens: number
}

function normalizeLabel(value: string | undefined, fallback: string): string {
  const normalized = value?.trim()
  return normalized ? normalized : fallback
}

function sumBy(items: TokenFeedback[], picker: (item: TokenFeedback) => string) {
  const totals = new Map<string, number>()
  for (const item of items) {
    const key = picker(item)
    totals.set(key, (totals.get(key) ?? 0) + item.tokens)
  }
  return Array.from(totals.entries())
    .map(([name, value]) => ({ name, value }))
    .sort((a, b) => b.value - a.value)
}

export function TokenInsightsPage() {
  const { selectedProject, sprints } = useProjectContext()

  const analysis = useMemo(() => {
    const feedbacks: TokenFeedback[] = []
    const sprintTotals = new Map<string, number>()
    const timeline = new Map<string, number>()
    let totalTokens = 0
    let sessions = 0

    for (const sprint of sprints) {
      for (const workItem of sprint.workItems) {
        const directTokens = workItem.totalTokensSpent ?? 0
        sprintTotals.set(sprint.name, (sprintTotals.get(sprint.name) ?? 0) + directTokens)
        totalTokens += directTokens

        for (const feedback of workItem.feedbacks ?? []) {
          const tokens = feedback.tokensUsed ?? 0
          if (tokens <= 0) {
            continue
          }

          sessions += 1
          const day = new Date(feedback.createdAt).toISOString().slice(0, 10)
          timeline.set(day, (timeline.get(day) ?? 0) + tokens)

          feedbacks.push({
            model: normalizeLabel(feedback.modelUsed, 'Modelo não informado'),
            ide: normalizeLabel(feedback.ideUsed, 'IDE não informada'),
            agent: normalizeLabel(feedback.agentName, 'Agente não informado'),
            tokens,
          })
        }
      }
    }

    const byModel = sumBy(feedbacks, (item) => item.model)
    const byIde = sumBy(feedbacks, (item) => item.ide)
    const byAgent = sumBy(feedbacks, (item) => item.agent)

    const bySprint = Array.from(sprintTotals.entries())
      .map(([name, value]) => ({ name, value }))
      .sort((a, b) => b.value - a.value)

    const trend = Array.from(timeline.entries())
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([date, tokens]) => ({ date, tokens }))

    const topModel = byModel[0]
    const topIde = byIde[0]
    const avgTokensPerSession = sessions > 0 ? Math.round(feedbacks.reduce((acc, item) => acc + item.tokens, 0) / sessions) : 0

    return {
      totalTokens,
      sessions,
      avgTokensPerSession,
      topModel,
      topIde,
      byModel,
      byIde,
      byAgent,
      bySprint,
      trend,
    }
  }, [sprints])

  if (!selectedProject) {
    return (
      <Card variant="outlined" sx={{ borderStyle: 'dashed' }}>
        <CardContent>
          <Stack
            spacing={1.2}
            sx={{
              alignItems: "center",
              justifyContent: "center",
              py: 4
            }}>
            <Typography variant="h6">Nenhum projeto selecionado</Typography>
            <Typography
              sx={{
                color: "text.secondary",
                textAlign: 'center',
                maxWidth: 520
              }}>
              Selecione um projeto para visualizar a análise de uso de tokens por modelo, IDE e agente.
            </Typography>
          </Stack>
        </CardContent>
      </Card>
    );
  }

  const hasTokenData = analysis.totalTokens > 0 || analysis.sessions > 0

  return (
    <Stack spacing={2}>
      <Card>
        <CardContent>
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.2} sx={{
            justifyContent: "space-between"
          }}>
            <Stack>
              <Stack direction="row" spacing={1} sx={{
                alignItems: "center"
              }}>
                <InsightsOutlinedIcon color="primary" />
                <Typography variant="h5">Dashboard de Gastos de Tokens</Typography>
              </Stack>
              <Typography sx={{
                color: "text.secondary"
              }}>
                Visão profunda do consumo por modelo, IDE, agente e distribuição por sprint.
              </Typography>
            </Stack>
            <Stack direction="row" spacing={1} useFlexGap sx={{
              flexWrap: "wrap"
            }}>
              <Chip color="primary" label={`Tokens totais: ${analysis.totalTokens.toLocaleString('pt-BR')}`} />
              <Chip color="secondary" label={`Sessões: ${analysis.sessions.toLocaleString('pt-BR')}`} />
              <Chip color="info" label={`Média por sessão: ${analysis.avgTokensPerSession.toLocaleString('pt-BR')}`} />
            </Stack>
          </Stack>
        </CardContent>
      </Card>
      {!hasTokenData ? (
        <Alert severity="info">
          Ainda não há dados de tokens para análise. Atualize tarefas com `tokensUsed`, `modelUsed` e `ideUsed` para alimentar os gráficos.
        </Alert>
      ) : (
        <>
          <Grid container spacing={2}>
            <Grid size={{ xs: 12, md: 6 }}>
              <Card sx={{ height: '100%' }}>
                <CardContent>
                  <Typography variant="overline" sx={{
                    color: "text.secondary"
                  }}>
                    Consumo por modelo
                  </Typography>
                  <ReactECharts
                    style={{ height: 340 }}
                    option={{
                      tooltip: { trigger: 'axis' },
                      xAxis: { type: 'value' },
                      yAxis: { type: 'category', data: analysis.byModel.map((item) => item.name), axisLabel: { width: 220, overflow: 'truncate' } },
                      series: [
                        {
                          type: 'bar',
                          data: analysis.byModel.map((item) => item.value),
                          itemStyle: { color: '#1976d2' },
                          label: { show: true, position: 'right' },
                        },
                      ],
                      grid: { left: 150, right: 28, top: 20, bottom: 25 },
                    }}
                  />
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Card sx={{ height: '100%' }}>
                <CardContent>
                  <Typography variant="overline" sx={{
                    color: "text.secondary"
                  }}>
                    Distribuição por IDE
                  </Typography>
                  <ReactECharts
                    style={{ height: 340 }}
                    option={{
                      tooltip: { trigger: 'item' },
                      legend: { bottom: 0, type: 'scroll' },
                      series: [
                        {
                          type: 'pie',
                          radius: ['36%', '68%'],
                          center: ['50%', '45%'],
                          itemStyle: { borderRadius: 6, borderColor: '#fff', borderWidth: 2 },
                          label: { formatter: '{b}\n{d}%' },
                          data: analysis.byIde,
                        },
                      ],
                    }}
                  />
                </CardContent>
              </Card>
            </Grid>
          </Grid>

          <Grid container spacing={2}>
            <Grid size={{ xs: 12, md: 7 }}>
              <Card sx={{ height: '100%' }}>
                <CardContent>
                  <Typography variant="overline" sx={{
                    color: "text.secondary"
                  }}>
                    Evolução diária de tokens
                  </Typography>
                  <ReactECharts
                    style={{ height: 320 }}
                    option={{
                      tooltip: { trigger: 'axis' },
                      xAxis: { type: 'category', data: analysis.trend.map((item) => item.date) },
                      yAxis: { type: 'value' },
                      series: [
                        {
                          type: 'line',
                          smooth: true,
                          areaStyle: { opacity: 0.2 },
                          symbolSize: 7,
                          itemStyle: { color: '#2e7d32' },
                          data: analysis.trend.map((item) => item.tokens),
                        },
                      ],
                      grid: { left: 40, right: 18, top: 20, bottom: 45 },
                    }}
                  />
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, md: 5 }}>
              <Card sx={{ height: '100%' }}>
                <CardContent>
                  <Typography variant="overline" sx={{
                    color: "text.secondary"
                  }}>
                    Top agentes por tokens
                  </Typography>
                  <ReactECharts
                    style={{ height: 320 }}
                    option={{
                      tooltip: { trigger: 'item' },
                      xAxis: { type: 'value' },
                      yAxis: { type: 'category', data: analysis.byAgent.map((item) => item.name), axisLabel: { width: 180, overflow: 'truncate' } },
                      series: [
                        {
                          type: 'bar',
                          data: analysis.byAgent.map((item) => item.value),
                          itemStyle: { color: '#7b1fa2' },
                          label: { show: true, position: 'right' },
                        },
                      ],
                      grid: { left: 130, right: 25, top: 20, bottom: 25 },
                    }}
                  />
                </CardContent>
              </Card>
            </Grid>
          </Grid>

          <Card>
            <CardContent>
              <Typography variant="overline" sx={{
                color: "text.secondary"
              }}>
                Tokens por sprint
              </Typography>
              <ReactECharts
                style={{ height: 320 }}
                option={{
                  tooltip: { trigger: 'axis' },
                  xAxis: { type: 'category', data: analysis.bySprint.map((item) => item.name), axisLabel: { rotate: 18 } },
                  yAxis: { type: 'value' },
                  series: [
                    {
                      type: 'bar',
                      data: analysis.bySprint.map((item) => item.value),
                      itemStyle: { color: '#ef6c00' },
                      label: { show: true, position: 'top' },
                    },
                  ],
                  grid: { left: 45, right: 20, top: 24, bottom: 68 },
                }}
              />
            </CardContent>
          </Card>

          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            {analysis.topModel ? (
              <Chip variant="outlined" label={`Modelo mais custoso: ${analysis.topModel.name} (${analysis.topModel.value.toLocaleString('pt-BR')} tokens)`} />
            ) : null}
            {analysis.topIde ? (
              <Chip variant="outlined" label={`IDE com maior consumo: ${analysis.topIde.name} (${analysis.topIde.value.toLocaleString('pt-BR')} tokens)`} />
            ) : null}
          </Box>
        </>
      )}
    </Stack>
  );
}
