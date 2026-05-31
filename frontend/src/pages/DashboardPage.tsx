import {
  Box,
  Card,
  CardContent,
  Chip,
  Grid,
  Skeleton,
  Stack,
  Typography,
} from '@mui/material'
import { useProjectContext } from '../context/useProjectContext'

const metricCards: Array<{ key: string; label: string; color: 'primary' | 'secondary' | 'success' | 'warning' }> = [
  { key: 'backlogTotal', label: 'Total backlog', color: 'primary' },
  { key: 'backlogDone', label: 'Completed backlog', color: 'success' },
  { key: 'activeSprints', label: 'Active sprints', color: 'secondary' },
  { key: 'workItemsInProgress', label: 'Tasks in progress', color: 'warning' },
  { key: 'workItemsReview', label: 'Tasks in review', color: 'warning' },
  { key: 'workItemsDone', label: 'Tasks done', color: 'success' },
  { key: 'knowledgeCheckpoints', label: 'Checkpoints', color: 'primary' },
  { key: 'wikiPages', label: 'Wiki pages', color: 'secondary' },
  { key: 'agentRuns', label: 'Agentic runs', color: 'primary' },
]

export function DashboardPage() {
  const { selectedProject, dashboard } = useProjectContext()

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
            <Typography variant="h6">No project selected</Typography>
            <Typography
              sx={{
                color: "text.secondary",
                textAlign: 'center',
                maxWidth: 480
              }}>
              Select a project at the top to view sprint capacity, task progress and knowledge indicators.
            </Typography>
          </Stack>
        </CardContent>
      </Card>
    );
  }

  if (!dashboard) {
    return (
      <Box>
        <Skeleton variant="rounded" height={110} sx={{ mb: 2 }} />
        <Grid container spacing={2}>
          {Array.from({ length: 6 }).map((_, index) => (
            <Grid key={index} size={{ xs: 12, sm: 6, md: 4 }}>
              <Skeleton variant="rounded" height={110} />
            </Grid>
          ))}
        </Grid>
      </Box>
    )
  }

  return (
    <Stack spacing={2}>
      <Card>
        <CardContent>
          <Stack
            direction={{ xs: 'column', md: 'row' }}
            spacing={2}
            sx={{
              justifyContent: "space-between",
              alignItems: "center"
            }}>
            <Stack>
              <Typography variant="h5">{dashboard.projectName}</Typography>
              <Typography sx={{
                color: "text.secondary"
              }}>Capacity and progress panel in Jira style.</Typography>
            </Stack>
            <Stack direction="row" spacing={1} useFlexGap sx={{
              flexWrap: "wrap"
            }}>
              <Chip color="primary" label={`To Do: ${dashboard.workItemsTodo}`} />
              <Chip color="warning" label={`In Progress: ${dashboard.workItemsInProgress}`} />
              <Chip color="secondary" label={`Review: ${dashboard.workItemsReview}`} />
              <Chip color="success" label={`Done: ${dashboard.workItemsDone}`} />
            </Stack>
          </Stack>
        </CardContent>
      </Card>
      <Grid container spacing={2}>
        {metricCards.map((metric) => (
          <Grid key={metric.key} size={{ xs: 12, sm: 6, md: 4 }}>
            <Card sx={{ borderTop: '4px solid', borderColor: `${metric.color}.main` }}>
              <CardContent>
                <Typography variant="overline" sx={{
                  color: "text.secondary"
                }}>
                  {metric.label}
                </Typography>
                <Typography variant="h4" sx={{ mt: 0.8 }}>
                  {dashboard[metric.key as keyof typeof dashboard]}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
    </Stack>
  );
}
