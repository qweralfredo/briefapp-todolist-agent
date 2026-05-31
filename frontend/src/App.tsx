import { Box, CircularProgress, Typography } from '@mui/material'
import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { ProjectProvider } from './context/ProjectContext'
import { AuthProvider } from './context/AuthContext'
import { ProtectedRoute } from './components/ProtectedRoute'
import { AppLayout } from './layout/AppLayout'
import LoginPage from './pages/LoginPage'

// Manager Flow pages (existing)
const DashboardPage = lazy(() => import('./pages/DashboardPage').then((module) => ({ default: module.DashboardPage })))
const TokenInsightsPage = lazy(() => import('./pages/TokenInsightsPage').then((module) => ({ default: module.TokenInsightsPage })))
const BacklogPage = lazy(() => import('./pages/BacklogPage').then((module) => ({ default: module.BacklogPage })))
const SprintsPage = lazy(() => import('./pages/SprintsPage').then((module) => ({ default: module.SprintsPage })))
const KnowledgePage = lazy(() => import('./pages/KnowledgePage').then((module) => ({ default: module.KnowledgePage })))
const WikiPage = lazy(() => import('./pages/WikiPage').then((module) => ({ default: module.WikiPage })))
const CheckpointsPage = lazy(() => import('./pages/CheckpointsPage').then((module) => ({ default: module.CheckpointsPage })))
const DocumentationPage = lazy(() => import('./pages/DocumentationPage').then((module) => ({ default: module.DocumentationPage })))
const SettingsPage = lazy(() => import('./pages/SettingsPage').then((module) => ({ default: module.SettingsPage })))

// Box module pages (new)
const UsersPage = lazy(() => import('./pages/UsersPage').then((module) => ({ default: module.UsersPage })))
const ContextBoxPage = lazy(() => import('./pages/ContextBoxPage').then((module) => ({ default: module.ContextBoxPage })))
const MemoryBoxPage = lazy(() => import('./pages/MemoryBoxPage').then((module) => ({ default: module.MemoryBoxPage })))
const LogPage = lazy(() => import('./pages/LogPage').then((module) => ({ default: module.LogPage })))
const UsagePage = lazy(() => import('./pages/UsagePage').then((module) => ({ default: module.UsagePage })))
const ApiKeysPage = lazy(() => import('./pages/ApiKeysPage').then((module) => ({ default: module.ApiKeysPage })))
const AllowListPage = lazy(() => import('./pages/AllowListPage').then((module) => ({ default: module.AllowListPage })))
const CircuitBreakerPage = lazy(() => import('./pages/CircuitBreakerPage').then((module) => ({ default: module.CircuitBreakerPage })))
const CostDashboardPage   = lazy(() => import('./pages/CostDashboardPage'))
const SandboxMonitorPage  = lazy(() => import('./pages/SandboxMonitorPage'))
const QueueDashboardPage  = lazy(() => import('./pages/QueueDashboardPage'))
const PromptCachePage     = lazy(() => import('./pages/PromptCachePage'))
const AgentPlannerPage    = lazy(() => import('./pages/AgentPlannerPage').then((module) => ({ default: module.AgentPlannerPage })))
const AgentDashboardPage  = lazy(() => import('./pages/AgentDashboardPage').then((module) => ({ default: module.AgentDashboardPage })))

function PageFallback() {
  return (
    <Box sx={{ minHeight: 200, display: 'grid', placeItems: 'center', gap: 1 }}>
      <CircularProgress size={30} />
      <Typography sx={{
        color: "text.secondary"
      }}>Loading...</Typography>
    </Box>
  );
}

function Lazy({ children }: { children: React.ReactNode }) {
  return <Suspense fallback={<PageFallback />}>{children}</Suspense>
}

const isDevMode = import.meta.env.VITE_MODE === 'dev'

function App() {
  return (
    <AuthProvider>
      <ProjectProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
            {/* Manager Flow routes (existing — same paths for backward compatibility) */}
            <Route index element={<Lazy><DashboardPage /></Lazy>} />
            <Route path="backlog" element={<Lazy><BacklogPage /></Lazy>} />
            {!isDevMode ? (
              <Route path="dashboard/tokens" element={<Lazy><TokenInsightsPage /></Lazy>} />
            ) : null}
            <Route path="sprints" element={<Lazy><SprintsPage /></Lazy>} />
            <Route path="knowledge" element={<Lazy><KnowledgePage /></Lazy>} />
            <Route path="knowledge/wiki" element={<Lazy><WikiPage /></Lazy>} />
            <Route path="knowledge/checkpoints" element={<Lazy><CheckpointsPage /></Lazy>} />
            <Route path="knowledge/documentation" element={<Lazy><DocumentationPage /></Lazy>} />
            <Route path="settings" element={<Lazy><SettingsPage /></Lazy>} />

            {/* Box module routes (new) */}
            {!isDevMode ? (
              <>
                <Route path="users" element={<Lazy><UsersPage /></Lazy>} />
                <Route path="context-box" element={<Lazy><ContextBoxPage /></Lazy>} />
                <Route path="memory-box" element={<Lazy><MemoryBoxPage /></Lazy>} />
                <Route path="log" element={<Lazy><LogPage /></Lazy>} />
                <Route path="agent-runs" element={<Lazy><AgentDashboardPage /></Lazy>} />
                <Route path="usage" element={<Lazy><UsagePage /></Lazy>} />
                <Route path="api-keys" element={<Lazy><ApiKeysPage /></Lazy>} />
                <Route path="allow-list" element={<Lazy><AllowListPage /></Lazy>} />
                <Route path="circuit-breaker" element={<Lazy><CircuitBreakerPage /></Lazy>} />
                <Route path="cost-guard" element={<Lazy><CostDashboardPage /></Lazy>} />
                <Route path="sandbox-monitor" element={<Lazy><SandboxMonitorPage /></Lazy>} />
                <Route path="queue-dashboard" element={<Lazy><QueueDashboardPage /></Lazy>} />
                <Route path="prompt-cache" element={<Lazy><PromptCachePage /></Lazy>} />
              </>
            ) : null}
            <Route path="planner" element={<Lazy><AgentPlannerPage /></Lazy>} />

            {/* Fallback */}
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Routes>
      </ProjectProvider>
    </AuthProvider>
  )
}

export default App
