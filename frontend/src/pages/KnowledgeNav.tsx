import { Tab, Tabs } from '@mui/material'
import { Link as RouterLink, useLocation } from 'react-router-dom'

const tabs = [
  { label: 'Hub', to: '/knowledge' },
  { label: 'Wiki', to: '/knowledge/wiki' },
  { label: 'Checkpoints', to: '/knowledge/checkpoints' },
  { label: 'Documentation', to: '/knowledge/documentation' },
]

function resolveTabValue(pathname: string): string {
  const direct = tabs.find((tab) => tab.to === pathname)
  if (direct) {
    return direct.to
  }

  if (pathname.startsWith('/knowledge/wiki')) {
    return '/knowledge/wiki'
  }

  if (pathname.startsWith('/knowledge/checkpoints')) {
    return '/knowledge/checkpoints'
  }

  if (pathname.startsWith('/knowledge/documentation')) {
    return '/knowledge/documentation'
  }

  return '/knowledge'
}

export function KnowledgeNav() {
  const location = useLocation()

  return (
    <Tabs
      value={resolveTabValue(location.pathname)}
      variant="scrollable"
      allowScrollButtonsMobile
      sx={{
        borderBottom: '1px solid #dbe5f1',
        mb: 1,
        ['& .MuiTab-root']: {
          textTransform: 'none',
          fontWeight: 700,
        },
      }}
    >
      {tabs.map((tab) => (
        <Tab
          key={tab.to}
          value={tab.to}
          component={RouterLink}
          to={tab.to}
          label={tab.label}
        />
      ))}
    </Tabs>
  )
}
