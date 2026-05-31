import {
  Card,
  CardContent,
  Grid,
  Link,
  Stack,
  Typography,
} from '@mui/material'
import { NavLink } from 'react-router-dom'
import { useProjectContext } from '../context/useProjectContext'
import { KnowledgeNav } from './KnowledgeNav'

export function KnowledgePage() {
  const { selectedProject, knowledge } = useProjectContext()

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
            <Typography variant="h6">No active project</Typography>
            <Typography
              sx={{
                color: "text.secondary",
                textAlign: 'center',
                maxWidth: 540
              }}>
              Select a project to register wiki, checkpoints and execution documentation.
            </Typography>
          </Stack>
        </CardContent>
      </Card>
    );
  }

  return (
    <Stack spacing={2}>
      <KnowledgeNav />
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, lg: 4 }}>
          <Card>
            <CardContent>
              <Typography variant="h6">Wiki</Typography>
              <Typography
                sx={{
                  color: "text.secondary",
                  mt: 0.8
                }}>
                Centralize guides, how-tos and team technical agreements.
              </Typography>
              <Link component={NavLink} to="/knowledge/wiki" underline="hover" sx={{ mt: 1.2, display: 'inline-flex' }}>
                Open Wiki
              </Link>
            </CardContent>
          </Card>
        </Grid>

        <Grid size={{ xs: 12, lg: 4 }}>
          <Card>
            <CardContent>
              <Typography variant="h6">Checkpoint</Typography>
              <Typography
                sx={{
                  color: "text.secondary",
                  mt: 0.8
                }}>
                Register context, decisions, risks and next steps per milestone.
              </Typography>
              <Link component={NavLink} to="/knowledge/checkpoints" underline="hover" sx={{ mt: 1.2, display: 'inline-flex' }}>
                Open Checkpoints
              </Link>
            </CardContent>
          </Card>
        </Grid>

        <Grid size={{ xs: 12, lg: 4 }}>
          <Card>
            <CardContent>
              <Typography variant="h6">Documentation</Typography>
              <Typography
                sx={{
                  color: "text.secondary",
                  mt: 0.8
                }}>
                Keep architecture, operations and technical references organized.
              </Typography>
              <Link component={NavLink} to="/knowledge/documentation" underline="hover" sx={{ mt: 1.2, display: 'inline-flex' }}>
                Open Documentation
              </Link>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 4 }}>
          <CategoryCountCard title="Wiki" total={knowledge?.wikiPages.length ?? 0} />
        </Grid>
        <Grid size={{ xs: 12, md: 4 }}>
          <CategoryCountCard title="Checkpoints" total={knowledge?.checkpoints.length ?? 0} />
        </Grid>
        <Grid size={{ xs: 12, md: 4 }}>
          <CategoryCountCard title="Documentation" total={knowledge?.documentationPages.length ?? 0} />
        </Grid>
      </Grid>
    </Stack>
  );
}

function CategoryCountCard({ title, total }: { title: string; total: number }) {
  return (
    <Card sx={{ height: '100%' }}>
      <CardContent>
        <Typography variant="h6">{title}</Typography>
        <Typography variant="h3" sx={{ mt: 1.2 }}>{total}</Typography>
        <Typography
          variant="body2"
          sx={{
            color: "text.secondary",
            mt: 0.8
          }}>
          registered items
        </Typography>
      </CardContent>
    </Card>
  );
}
