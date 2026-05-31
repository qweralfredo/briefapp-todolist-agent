import React, { useState } from 'react';
import {
  Box,
  Button,
  Typography,
  Paper,
  TextField,
  Divider,
  InputAdornment,
  IconButton,
  Collapse,
  Alert,
  Chip,
} from '@mui/material';
import GoogleIcon from '@mui/icons-material/Google';
import KeyIcon from '@mui/icons-material/Key';
import VisibilityIcon from '@mui/icons-material/Visibility';
import VisibilityOffIcon from '@mui/icons-material/VisibilityOff';
import ArrowForwardIcon from '@mui/icons-material/ArrowForward';
import PersonOutlineIcon from '@mui/icons-material/PersonOutline';
import { useAuth } from '../context/AuthContext';
import { useNavigate } from 'react-router-dom';

const LoginPage: React.FC = () => {
  const { user, signInWithGoogle, signInAsGuest, signInWithApiKey } = useAuth();
  const navigate = useNavigate();

  const [showApiKeyInput, setShowApiKeyInput] = useState(false);
  const [apiKey, setApiKey] = useState('');
  const [showKey, setShowKey] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  React.useEffect(() => {
    if (user) {
      navigate('/', { replace: true });
    }
  }, [user, navigate]);

  const handleGoogleLogin = async () => {
    setLoading(true);
    try {
      await signInWithGoogle();
      navigate('/');
    } catch (err) {
      setError('Erro ao entrar com Google. Tente novamente.');
    } finally {
      setLoading(false);
    }
  };

  const handleApiKeyLogin = () => {
    if (!apiKey.trim()) {
      setError('Digite uma API key válida.');
      return;
    }
    if (!apiKey.startsWith('pbx_') && !apiKey.startsWith('dev-')) {
      setError('Formato inválido. A chave deve começar com pbx_ ou dev-.');
      return;
    }
    setError('');
    signInWithApiKey(apiKey.trim());
    navigate('/');
  };

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'linear-gradient(135deg, #0f1117 0%, #1a1f2e 50%, #0f1117 100%)',
        position: 'relative',
        overflow: 'hidden',
        '&::before': {
          content: '""',
          position: 'absolute',
          width: 600,
          height: 600,
          borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(99,102,241,0.12) 0%, transparent 70%)',
          top: -200,
          right: -100,
          pointerEvents: 'none',
        },
        '&::after': {
          content: '""',
          position: 'absolute',
          width: 400,
          height: 400,
          borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(139,92,246,0.1) 0%, transparent 70%)',
          bottom: -100,
          left: -50,
          pointerEvents: 'none',
        },
      }}
    >
      <Paper
        elevation={0}
        sx={{
          p: 5,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 2.5,
          maxWidth: 420,
          width: '90%',
          background: 'rgba(255,255,255,0.04)',
          backdropFilter: 'blur(20px)',
          border: '1px solid rgba(255,255,255,0.08)',
          borderRadius: 3,
          position: 'relative',
          zIndex: 1,
        }}
      >
        {/* Logo / Brand */}
        <Box sx={{ textAlign: 'center', mb: 1 }}>
          <Box
            sx={{
              width: 56,
              height: 56,
              borderRadius: 2,
              background: 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              mx: 'auto',
              mb: 2,
              boxShadow: '0 0 32px rgba(99,102,241,0.4)',
            }}
          >
            <KeyIcon sx={{ color: '#fff', fontSize: 28 }} />
          </Box>
          <Typography variant="h5" fontWeight={700} color="white" letterSpacing={-0.5}>
            BriefApp
          </Typography>
          <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.45)', mt: 0.5 }}>
            Agentic Project Management
          </Typography>
        </Box>

        {/* Error */}
        <Collapse in={!!error} sx={{ width: '100%' }}>
          <Alert severity="error" sx={{ fontSize: '0.8rem' }} onClose={() => setError('')}>
            {error}
          </Alert>
        </Collapse>

        {/* Google Login */}
        <Button
          variant="contained"
          fullWidth
          size="large"
          startIcon={<GoogleIcon />}
          onClick={handleGoogleLogin}
          disabled={loading}
          sx={{
            py: 1.5,
            background: '#fff',
            color: '#1a1f2e',
            fontWeight: 600,
            borderRadius: 2,
            textTransform: 'none',
            fontSize: '0.95rem',
            boxShadow: 'none',
            '&:hover': { background: '#f0f0f0', boxShadow: 'none' },
          }}
        >
          Entrar com Google
        </Button>

        {/* API Key toggle */}
        <Button
          variant="outlined"
          fullWidth
          size="large"
          startIcon={<KeyIcon />}
          onClick={() => setShowApiKeyInput((v) => !v)}
          sx={{
            py: 1.5,
            borderRadius: 2,
            textTransform: 'none',
            fontSize: '0.95rem',
            fontWeight: 600,
            borderColor: 'rgba(99,102,241,0.5)',
            color: '#a5b4fc',
            '&:hover': {
              borderColor: '#6366f1',
              background: 'rgba(99,102,241,0.08)',
            },
          }}
        >
          Entrar com API Key
        </Button>

        {/* API Key input (collapsible) */}
        <Collapse in={showApiKeyInput} sx={{ width: '100%' }}>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
            <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.45)', display: 'flex', alignItems: 'center', gap: 0.5 }}>
              <Chip label="pbx_..." size="small" sx={{ fontSize: '0.65rem', height: 18, bgcolor: 'rgba(99,102,241,0.2)', color: '#a5b4fc' }} />
              Cole sua chave de API abaixo
            </Typography>
            <TextField
              fullWidth
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder="pbx_..."
              type={showKey ? 'text' : 'password'}
              autoComplete="off"
              onKeyDown={(e) => e.key === 'Enter' && handleApiKeyLogin()}
              InputProps={{
                endAdornment: (
                  <InputAdornment position="end">
                    <IconButton size="small" onClick={() => setShowKey((v) => !v)} edge="end" sx={{ color: 'rgba(255,255,255,0.4)' }}>
                      {showKey ? <VisibilityOffIcon fontSize="small" /> : <VisibilityIcon fontSize="small" />}
                    </IconButton>
                  </InputAdornment>
                ),
                sx: {
                  background: 'rgba(255,255,255,0.05)',
                  borderRadius: 2,
                  color: '#e2e8f0',
                  fontSize: '0.85rem',
                  fontFamily: 'monospace',
                  '& fieldset': { borderColor: 'rgba(99,102,241,0.3)' },
                  '&:hover fieldset': { borderColor: 'rgba(99,102,241,0.6)' },
                  '&.Mui-focused fieldset': { borderColor: '#6366f1' },
                },
              }}
            />
            <Button
              variant="contained"
              fullWidth
              size="large"
              endIcon={<ArrowForwardIcon />}
              onClick={handleApiKeyLogin}
              sx={{
                py: 1.5,
                borderRadius: 2,
                textTransform: 'none',
                fontWeight: 600,
                fontSize: '0.95rem',
                background: 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)',
                boxShadow: '0 4px 20px rgba(99,102,241,0.35)',
                '&:hover': { boxShadow: '0 4px 28px rgba(99,102,241,0.55)' },
              }}
            >
              Autenticar
            </Button>
          </Box>
        </Collapse>

        {/* Divider */}
        <Divider sx={{ width: '100%', borderColor: 'rgba(255,255,255,0.07)' }}>
          <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.25)', px: 1 }}>
            ou
          </Typography>
        </Divider>

        {/* Guest */}
        <Button
          variant="text"
          fullWidth
          size="medium"
          startIcon={<PersonOutlineIcon />}
          onClick={() => {
            signInAsGuest();
            navigate('/');
          }}
          sx={{
            textTransform: 'none',
            color: 'rgba(255,255,255,0.35)',
            fontSize: '0.85rem',
            '&:hover': { color: 'rgba(255,255,255,0.6)', background: 'transparent' },
          }}
        >
          Continuar como Convidado
        </Button>
      </Paper>
    </Box>
  );
};

export default LoginPage;
