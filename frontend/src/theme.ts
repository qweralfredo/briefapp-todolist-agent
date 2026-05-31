import { createTheme } from '@mui/material/styles'

export const appTheme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: '#e9506e',
      light: '#ff6b8a',
      dark: '#c73a57',
      contrastText: '#ffffff',
    },
    secondary: {
      main: '#0f172a',
      light: '#1e293b',
      dark: '#0a0f1e',
      contrastText: '#e2e8f0',
    },
    background: {
      default: '#f8f9fb',
      paper: '#ffffff',
    },
    text: {
      primary: '#0f172a',
      secondary: '#475569',
    },
    divider: 'rgba(233, 80, 110, 0.12)',
    error: {
      main: '#ef4444',
    },
  },
  shape: {
    borderRadius: 10,
  },
  typography: {
    fontFamily: '"Inter", "IBM Plex Sans", "Segoe UI", sans-serif',
    h4: {
      fontWeight: 700,
      letterSpacing: '-0.5px',
    },
    h5: {
      fontWeight: 700,
      letterSpacing: '-0.3px',
    },
    h6: {
      fontWeight: 600,
    },
    body1: {
      fontFamily: '"Inter", sans-serif',
    },
    button: {
      fontFamily: '"IBM Plex Sans", "Inter", sans-serif',
      fontWeight: 600,
      textTransform: 'none',
    },
  },
  components: {
    MuiCard: {
      styleOverrides: {
        root: {
          boxShadow: '0 2px 12px rgba(233, 80, 110, 0.06)',
          border: '1px solid rgba(233, 80, 110, 0.08)',
          transition: 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)',
          '&:hover': {
            boxShadow: '0 8px 24px rgba(233, 80, 110, 0.12)',
            transform: 'translateY(-2px)',
          },
        },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: {
          background: 'linear-gradient(135deg, #0f172a 0%, #1a2440 100%)',
          boxShadow: '0 1px 0 rgba(233, 80, 110, 0.15)',
        },
      },
    },
    MuiButton: {
      styleOverrides: {
        root: {
          textTransform: 'none',
          fontWeight: 600,
          fontFamily: '"IBM Plex Sans", "Inter", sans-serif',
          borderRadius: 8,
          transition: 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)',
        },
        containedPrimary: {
          background: 'linear-gradient(135deg, #e9506e, #ff6b8a)',
          boxShadow: '0 4px 14px rgba(233, 80, 110, 0.3)',
          '&:hover': {
            boxShadow: '0 8px 24px rgba(233, 80, 110, 0.4)',
            transform: 'translateY(-1px)',
            background: 'linear-gradient(135deg, #d4405e, #e9506e)',
          },
        },
        containedSecondary: {
          background: 'linear-gradient(135deg, #0f172a, #1e293b)',
          '&:hover': {
            background: 'linear-gradient(135deg, #1e293b, #334155)',
          },
        },
      },
    },
    MuiTextField: {
      styleOverrides: {
        root: {
          '& .MuiOutlinedInput-root': {
            transition: 'all 0.2s ease',
            '&:hover .MuiOutlinedInput-notchedOutline': {
              borderColor: 'rgba(233, 80, 110, 0.4)',
            },
            '&.Mui-focused .MuiOutlinedInput-notchedOutline': {
              borderColor: '#e9506e',
              boxShadow: '0 0 0 3px rgba(233, 80, 110, 0.12)',
            },
          },
        },
      },
    },
    MuiLinearProgress: {
      styleOverrides: {
        root: {
          backgroundColor: 'rgba(233, 80, 110, 0.12)',
        },
        bar: {
          background: 'linear-gradient(90deg, #e9506e, #ff6b8a)',
        },
      },
    },
    MuiDrawer: {
      styleOverrides: {
        paper: {
          borderRight: '1px solid rgba(233, 80, 110, 0.1)',
          backgroundColor: '#fdfcfd',
        },
      },
    },
    MuiListItemButton: {
      styleOverrides: {
        root: {
          borderRadius: 8,
          transition: 'all 0.2s ease',
          '&.Mui-selected': {
            backgroundColor: 'rgba(233, 80, 110, 0.08)',
            color: '#e9506e',
            '&:hover': {
              backgroundColor: 'rgba(233, 80, 110, 0.12)',
            },
          },
          '&:hover': {
            backgroundColor: 'rgba(233, 80, 110, 0.06)',
          },
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: {
          fontFamily: '"Inter", sans-serif',
        },
      },
    },
    MuiDivider: {
      styleOverrides: {
        root: {
          borderColor: 'rgba(233, 80, 110, 0.1)',
        },
      },
    },
  },
})
