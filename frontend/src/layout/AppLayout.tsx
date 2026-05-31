import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined'
import AnalyticsOutlinedIcon from '@mui/icons-material/AnalyticsOutlined'
import AutoFixHighOutlinedIcon from '@mui/icons-material/AutoFixHighOutlined'
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import { MarkdownField } from '../components/MarkdownField'
import DescriptionOutlinedIcon from '@mui/icons-material/DescriptionOutlined'
import MenuIcon from '@mui/icons-material/Menu'
import SprintOutlinedIcon from '@mui/icons-material/OnlinePredictionOutlined'
import ViewKanbanOutlinedIcon from '@mui/icons-material/ViewKanbanOutlined'
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined'
import GroupOutlinedIcon from '@mui/icons-material/GroupOutlined'
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined'
import StorageOutlinedIcon from '@mui/icons-material/StorageOutlined'
import SdStorageOutlinedIcon from '@mui/icons-material/SdStorageOutlined'
import TerminalOutlinedIcon from '@mui/icons-material/TerminalOutlined'
import BarChartOutlinedIcon from '@mui/icons-material/BarChartOutlined'
import VpnKeyOutlinedIcon from '@mui/icons-material/VpnKeyOutlined'
import SecurityOutlinedIcon from '@mui/icons-material/SecurityOutlined'
import LogoutOutlinedIcon from '@mui/icons-material/LogoutOutlined'
import ExpandLess from '@mui/icons-material/ExpandLess'
import ExpandMore from '@mui/icons-material/ExpandMore'
import Inventory2OutlinedIcon from '@mui/icons-material/Inventory2Outlined'
import OfflineBoltOutlinedIcon from '@mui/icons-material/OfflineBoltOutlined'
import SpeedOutlinedIcon from '@mui/icons-material/SpeedOutlined'
import {
  Alert,
  AppBar,
  Box,
  Button,
  Collapse,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Drawer,
  FormControl,
  IconButton,
  InputLabel,
  LinearProgress,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  MenuItem,
  Paper,
  Select,
  Stack,
  TextField,
  Toolbar,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material'
import { useMemo, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useProjectContext } from '../context/useProjectContext'
import { useAuth } from '../context/AuthContext'

const drawerWidthExpanded = 260
const drawerWidthCollapsed = 76

type MenuItem = {
  label: string
  to: string
  icon: React.ReactNode
  children?: MenuItem[]
}

const isDevMode = import.meta.env.VITE_MODE === 'dev'

const boxMenu: MenuItem[] = [
  ...(!isDevMode ? [
    { label: 'Users', to: '/users', icon: <GroupOutlinedIcon /> },
  ] : []),
  {
    label: 'Manager Flow',
    to: '/',
    icon: <AccountTreeOutlinedIcon />,
    children: [
      { label: 'Dashboard', to: '/', icon: <DashboardOutlinedIcon /> },
      ...(!isDevMode ? [
        { label: 'Token Insights', to: '/dashboard/tokens', icon: <AnalyticsOutlinedIcon /> },
      ] : []),
      { label: 'Backlog', to: '/backlog', icon: <ViewKanbanOutlinedIcon /> },
      { label: 'Sprints', to: '/sprints', icon: <SprintOutlinedIcon /> },
      { label: 'Knowledge', to: '/knowledge', icon: <DescriptionOutlinedIcon /> },
      { label: 'Agent Planner', to: '/planner', icon: <AutoFixHighOutlinedIcon /> },
      { label: 'Settings', to: '/settings', icon: <SettingsOutlinedIcon /> },
    ],
  },
  ...(!isDevMode ? [
    { label: 'Context-Box', to: '/context-box', icon: <StorageOutlinedIcon /> },
    { label: 'Memory-Box', to: '/memory-box', icon: <SdStorageOutlinedIcon /> },
    { label: 'Agent Runs', to: '/agent-runs', icon: <TerminalOutlinedIcon /> },
    { label: 'Log', to: '/log', icon: <TerminalOutlinedIcon /> },
    { label: 'Usage', to: '/usage', icon: <BarChartOutlinedIcon /> },
    { label: 'API Keys', to: '/api-keys', icon: <VpnKeyOutlinedIcon /> },
    { label: 'Allow-List', to: '/allow-list', icon: <SecurityOutlinedIcon /> },
    { label: 'Circuit Breaker', to: '/circuit-breaker', icon: <OfflineBoltOutlinedIcon /> },
    { label: 'Prompt Cache', to: '/prompt-cache', icon: <SpeedOutlinedIcon /> },
  ] : []),
]

/** All flat routes for title resolution */
const allRoutes = boxMenu.flatMap((item) =>
  item.children ? item.children : [item],
)

export function AppLayout() {
  const theme = useTheme()
  const isMobile = useMediaQuery(theme.breakpoints.down('md'))
  const location = useLocation()
  const navigate = useNavigate()
  const {
    projects,
    selectedProjectId,
    selectedProject,
    loading,
    error,
    setSelectedProjectId,
    createProject,
  } = useProjectContext()

  const { logout } = useAuth()

  const [dialogOpen, setDialogOpen] = useState(false)
  const [mobileDrawerOpen, setMobileDrawerOpen] = useState(false)
  const [desktopMenuCollapsed, setDesktopMenuCollapsed] = useState(false)
  const [managerFlowOpen, setManagerFlowOpen] = useState(true)
  const [projectName, setProjectName] = useState('')
  const [projectDescription, setProjectDescription] = useState('')
  const showErrorBanner = Boolean(error && selectedProjectId)
  const desktopDrawerWidth = desktopMenuCollapsed ? drawerWidthCollapsed : drawerWidthExpanded
  const showMenuLabels = isMobile || !desktopMenuCollapsed

  const pageTitle = useMemo(() => {
    const match = allRoutes.find(
      (item) =>
        location.pathname === item.to ||
        (item.to !== '/' && location.pathname.startsWith(`${item.to}/`)),
    )
    return match?.label ?? 'Box Space'
  }, [location.pathname])

  async function handleLogout() {
    await logout()
    setSelectedProjectId('')
    navigate('/login')
  }

  function isRouteActive(to: string) {
    if (to === '/') return location.pathname === '/'
    return location.pathname === to || location.pathname.startsWith(`${to}/`)
  }

  function isManagerFlowActive() {
    const managerRoutes = boxMenu.find((m) => m.label === 'Manager Flow')?.children ?? []
    return managerRoutes.some((child) => isRouteActive(child.to))
  }

  const renderMenuItem = (item: MenuItem, nested = false) => {
    const isActive = isRouteActive(item.to)
    return (
      <Tooltip
        key={item.to + item.label}
        title={!showMenuLabels ? item.label : ''}
        placement="right"
        arrow
      >
        <ListItemButton
          component={NavLink}
          to={item.to}
          onClick={() => setMobileDrawerOpen(false)}
          sx={{
            borderRadius: 2,
            mb: 0.4,
            pl: nested ? 4.5 : 2,
            py: 0.8,
            bgcolor: isActive ? 'rgba(233, 80, 110, 0.08)' : 'transparent',
            color: isActive ? 'primary.main' : 'text.primary',
            ['&:hover']: {
              bgcolor: 'rgba(233, 80, 110, 0.06)',
            },
          }}
        >
          <ListItemIcon
            sx={{
              color: isActive ? 'primary.main' : 'text.secondary',
              minWidth: 34,
              '& .MuiSvgIcon-root': { fontSize: nested ? 18 : 22 },
            }}
          >
            {item.icon}
          </ListItemIcon>
          {showMenuLabels && (
            <ListItemText
              primary={item.label}
              primaryTypographyProps={{
                fontSize: nested ? 13 : 14,
                fontWeight: isActive ? 600 : 400,
              }}
            />
          )}
        </ListItemButton>
      </Tooltip>
    )
  }

  const drawerContent = (
    <>
      <Toolbar />
      <Box sx={{ p: 2.2 }}>
        {showMenuLabels && (
          <>
            <Stack
              direction="row"
              spacing={0.8}
              sx={{
                alignItems: "center",
                mb: 0.5
              }}>
              <Inventory2OutlinedIcon sx={{ fontSize: 18, color: 'primary.main' }} />
              <Typography
                variant="overline"
                sx={{
                  color: "primary.main",
                  fontWeight: 700,
                  letterSpacing: 1.5
                }}>
                Current Box
              </Typography>
            </Stack>
            <Typography variant="h6" sx={{ mt: 0.2, lineHeight: 1.3 }}>
              {selectedProject?.name ?? 'No box selected'}
            </Typography>
            <Typography
              variant="body2"
              sx={{
                color: "text.secondary",
                mt: 0.5,
                display: '-webkit-box',
                WebkitLineClamp: 2,
                WebkitBoxOrient: 'vertical',
                overflow: 'hidden'
              }}>
              {selectedProject?.description ?? 'Create or select a box to get started.'}
            </Typography>
          </>
        )}
        <Divider sx={{ mt: 1.5 }} />
      </Box>

      <List sx={{ px: 1.2, flexGrow: 1 }}>
        {boxMenu.map((item) => {
          if (item.children) {
            // Manager Flow with sub-items
            const mfActive = isManagerFlowActive()
            return (
              <Box key={item.label}>
                <Tooltip
                  title={!showMenuLabels ? item.label : ''}
                  placement="right"
                  arrow
                >
                  <ListItemButton
                    onClick={() => {
                      if (desktopMenuCollapsed && !isMobile) {
                        // collapsed: navigate to manager flow index
                        navigate('/')
                      } else {
                        setManagerFlowOpen(!managerFlowOpen)
                      }
                    }}
                    sx={{
                      borderRadius: 2,
                      mb: 0.4,
                      py: 0.8,
                      bgcolor: mfActive ? 'rgba(233, 80, 110, 0.06)' : 'transparent',
                      color: mfActive ? 'primary.dark' : 'text.primary',
                      ['&:hover']: { bgcolor: 'rgba(233, 80, 110, 0.06)' },
                    }}
                  >
                    <ListItemIcon
                      sx={{
                        color: mfActive ? 'primary.main' : 'text.secondary',
                        minWidth: 34,
                      }}
                    >
                      {item.icon}
                    </ListItemIcon>
                    {showMenuLabels && (
                      <>
                        <ListItemText
                          primary={item.label}
                          primaryTypographyProps={{
                            fontSize: 14,
                            fontWeight: mfActive ? 600 : 500,
                          }}
                        />
                        {managerFlowOpen ? <ExpandLess /> : <ExpandMore />}
                      </>
                    )}
                  </ListItemButton>
                </Tooltip>
                {showMenuLabels && (
                  <Collapse in={managerFlowOpen} timeout="auto" unmountOnExit>
                    <List component="div" disablePadding sx={{ pl: 0 }}>
                      {item.children.map((child) => renderMenuItem(child, true))}
                    </List>
                  </Collapse>
                )}
              </Box>
            )
          }
          return renderMenuItem(item)
        })}
      </List>

      {/* Logout at bottom */}
      <Box sx={{ px: 1.2, pb: 2 }}>
        <Divider sx={{ mb: 1 }} />
        <Tooltip title={!showMenuLabels ? 'Logout' : ''} placement="right" arrow>
          <ListItemButton
            onClick={handleLogout}
            sx={{
              borderRadius: 2,
              py: 0.8,
              color: 'error.main',
              ['&:hover']: { bgcolor: 'rgba(211, 47, 47, 0.08)' },
            }}
          >
            <ListItemIcon sx={{ color: 'error.main', minWidth: 34 }}>
              <LogoutOutlinedIcon />
            </ListItemIcon>
            {showMenuLabels && (
              <ListItemText
                primary="Logout"
                primaryTypographyProps={{ fontSize: 14, fontWeight: 500 }}
              />
            )}
          </ListItemButton>
        </Tooltip>
      </Box>
    </>
  )

  async function handleCreateProject(event: React.FormEvent) {
    event.preventDefault()
    if (!projectName.trim() || !projectDescription.trim()) {
      return
    }

    await createProject({
      name: projectName,
      description: projectDescription,
    })

    setProjectName('')
    setProjectDescription('')
    setDialogOpen(false)
  }

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: 'background.default' }}>
      <AppBar position="fixed" sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}>
        <Toolbar sx={{ display: 'flex', justifyContent: 'space-between', gap: 1.5, minHeight: 68, flexWrap: { xs: 'wrap', md: 'nowrap' }, py: { xs: 1, md: 0 } }}>
          <Stack
            direction="row"
            spacing={1}
            sx={{
              alignItems: "center",
              minWidth: 0
            }}>
            {isMobile ? (
              <IconButton
                color="inherit"
                aria-label="open navigation"
                onClick={() => setMobileDrawerOpen(true)}
                edge="start"
              >
                <MenuIcon />
              </IconButton>
            ) : (
              <IconButton
                color="inherit"
                aria-label="toggle navigation"
                onClick={() => setDesktopMenuCollapsed((prev) => !prev)}
                edge="start"
              >
                {desktopMenuCollapsed ? <ChevronRightIcon /> : <ChevronLeftIcon />}
              </IconButton>
            )}
            <Inventory2OutlinedIcon sx={{ fontSize: 24 }} />
            <Typography variant="h6">Briefapp Todo List</Typography>
            <Typography variant="caption" sx={{ opacity: 0.88, display: { xs: 'none', sm: 'inline' } }}>
              Box &gt; Manager Flow &gt; Backlog &gt; Sprint &gt; Tasks
            </Typography>
          </Stack>

          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            spacing={1}
            sx={{
              alignItems: "stretch",
              width: { xs: '100%', md: 'auto' }
            }}>
            <FormControl size="small" sx={{ minWidth: { xs: '100%', sm: 220, md: 260 }, bgcolor: 'rgba(255,255,255,0.9)', borderRadius: 1 }}>
              <InputLabel id="box-select-label">Active box</InputLabel>
              <Select
                labelId="box-select-label"
                value={selectedProjectId}
                label="Active box"
                onChange={(event) => setSelectedProjectId(event.target.value)}
              >
                {projects.map((project) => (
                  <MenuItem value={project.id} key={project.id}>
                    {project.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <Button variant="contained" color="secondary" onClick={() => setDialogOpen(true)} sx={{ px: 2.2, whiteSpace: 'nowrap' }}>
              New box
            </Button>
          </Stack>
        </Toolbar>
      </AppBar>
      <Drawer
        variant={isMobile ? 'temporary' : 'permanent'}
        open={isMobile ? mobileDrawerOpen : true}
        onClose={() => setMobileDrawerOpen(false)}
        ModalProps={{ keepMounted: true }}
        sx={{
          width: { md: desktopDrawerWidth },
          flexShrink: 0,
          ['& .MuiDrawer-paper']: {
            width: { xs: drawerWidthExpanded, md: desktopDrawerWidth },
            boxSizing: 'border-box',
            borderRight: '1px solid',
            borderColor: 'divider',
            backgroundColor: '#fdfcfd',
            display: 'flex',
            flexDirection: 'column',
          },
          display: { xs: 'block', md: 'block' },
        }}
      >
        {drawerContent}
      </Drawer>
      <Box
        component="main"
        sx={{
          flexGrow: 1,
          mt: { xs: 15, sm: 12, md: 8 },
          pb: 3,
        }}
      >
        <Container maxWidth="xl" sx={{ pt: { xs: 2, md: 3 }, px: { xs: 1.25, sm: 2, md: 3 } }}>
          <Paper
            elevation={0}
            sx={{
              border: '1px solid',
              borderColor: 'divider',
              borderRadius: 3,
              p: { xs: 2, md: 2.5 },
              backgroundColor: 'rgba(255, 255, 255, 0.82)',
              backdropFilter: 'blur(3px)',
            }}
          >
            <Stack spacing={1.4}>
              <Typography variant="h4">{pageTitle}</Typography>
              {loading ? <LinearProgress sx={{ borderRadius: 999, height: 6 }} /> : null}
              {showErrorBanner ? (
                <Alert severity="error" variant="outlined" sx={{ py: 0.25 }}>
                  {error}
                </Alert>
              ) : null}
              <Outlet />
            </Stack>
          </Paper>
        </Container>
      </Box>
      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} fullWidth maxWidth="md">
        <Box component="form" onSubmit={handleCreateProject}>
          <DialogTitle>New Box</DialogTitle>
          <DialogContent>
            <Stack spacing={2} sx={{ mt: 1 }}>
              <TextField
                value={projectName}
                onChange={(event) => setProjectName(event.target.value)}
                label="Name"
                required
                fullWidth
              />
              <MarkdownField label="Description" value={projectDescription} onChange={setProjectDescription} required />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setDialogOpen(false)}>Cancel</Button>
            <Button type="submit" variant="contained">Create</Button>
          </DialogActions>
        </Box>
      </Dialog>
    </Box>
  );
}
