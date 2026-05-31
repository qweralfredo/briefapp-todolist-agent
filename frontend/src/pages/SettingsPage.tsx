import SaveOutlinedIcon from '@mui/icons-material/SaveOutlined'
import PlayCircleOutlineIcon from '@mui/icons-material/PlayCircleOutline'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Snackbar,
  Stack,
  TextField,
  Typography,
  Switch,
  FormControlLabel,
  Divider
} from '@mui/material'
import { useEffect, useState } from 'react'
import { useProjectContext } from '../context/useProjectContext'
import { apiClient } from '../api/client'

export function SettingsPage() {
  const { selectedProject, updateProjectConfig } = useProjectContext()

  const [gitHubUrl, setGitHubUrl] = useState('')
  const [localPath, setLocalPath] = useState('')
  const [techStack, setTechStack] = useState('')
  const [mainBranch, setMainBranch] = useState('')
  const [adoEnabled, setAdoEnabled] = useState(false)
  const [adoOrganization, setAdoOrganization] = useState('')
  const [adoProject, setAdoProject] = useState('')
  const [adoPat, setAdoPat] = useState('')
  const [saving, setSaving] = useState(false)
  const [success, setSuccess] = useState(false)
  const [errorMsg, setErrorMsg] = useState('')
  const [testingAdo, setTestingAdo] = useState(false)
  const [testResult, setTestResult] = useState<{ success: boolean; msg: string } | null>(null)

  useEffect(() => {
    if (selectedProject) {
      setGitHubUrl(selectedProject.gitHubUrl ?? '')
      setLocalPath(selectedProject.localPath ?? '')
      setTechStack(selectedProject.techStack ?? '')
      setMainBranch(selectedProject.mainBranch ?? 'main')
      setAdoEnabled(selectedProject.adoEnabled ?? false)
      setAdoOrganization(selectedProject.adoOrganization ?? '')
      setAdoProject(selectedProject.adoProject ?? '')
      setAdoPat(selectedProject.adoPat ?? '')
    }
  }, [selectedProject])

  const handleSave = async () => {
    setSaving(true)
    setErrorMsg('')
    try {
      await updateProjectConfig({ 
        gitHubUrl, 
        localPath, 
        techStack, 
        mainBranch,
        adoEnabled,
        adoOrganization,
        adoProject,
        adoPat
      })
      setSuccess(true)
    } catch {
      setErrorMsg('Erro ao salvar configurações.')
    } finally {
      setSaving(false)
    }
  }

  const handleTestAdo = async () => {
    if (!adoOrganization || !adoPat) {
      setTestResult({ success: false, msg: 'Preencha a Organização e o PAT antes de testar.' })
      return
    }
    setTestingAdo(true)
    setTestResult(null)
    try {
      const result = await apiClient.testAzureDevOpsConnection({
        organization: adoOrganization,
        project: adoProject,
        pat: adoPat
      })
      if (result.success) {
        setTestResult({ success: true, msg: 'Conexão bem-sucedida com o Azure DevOps!' })
      } else {
        setTestResult({ success: false, msg: 'Falha na conexão. Verifique suas credenciais e permissões do PAT.' })
      }
    } catch (err: any) {
      setTestResult({ success: false, msg: err.message || 'Erro ao testar conexão.' })
    } finally {
      setTestingAdo(false)
    }
  }

  if (!selectedProject) {
    return (
      <Box sx={{ p: 3 }}>
        <Typography sx={{
          color: "text.secondary"
        }}>Nenhum projeto selecionado.</Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ p: 3, maxWidth: 720 }}>
      <Typography variant="h5" gutterBottom sx={{
        fontWeight: 700
      }}>
        Configurações do Projeto
      </Typography>
      <Typography
        variant="body2"
        sx={{
          color: "text.secondary",
          mb: 3
        }}>
        {selectedProject.name}
      </Typography>
      <Card variant="outlined">
        <CardContent>
          <Stack spacing={3}>
            <TextField
              label="Branch Atual"
              value={mainBranch}
              onChange={(e) => setMainBranch(e.target.value)}
              placeholder="main"
              helperText="Branch principal do repositório (ex: main, develop)"
              fullWidth
            />
            <TextField
              label="URL do GitHub"
              value={gitHubUrl}
              onChange={(e) => setGitHubUrl(e.target.value)}
              placeholder="https://github.com/org/repo"
              fullWidth
            />
            <TextField
              label="Caminho Local"
              value={localPath}
              onChange={(e) => setLocalPath(e.target.value)}
              placeholder="c:\projetos\meu-repo"
              fullWidth
            />
            <TextField
              label="Tech Stack"
              value={techStack}
              onChange={(e) => setTechStack(e.target.value)}
              placeholder=".NET 10, React 19, PostgreSQL"
              fullWidth
            />

            <Divider />

            <Typography variant="h6" sx={{ fontWeight: 600 }}>Integração Azure DevOps</Typography>
            
            <FormControlLabel
              control={
                <Switch
                  checked={adoEnabled}
                  onChange={(e) => setAdoEnabled(e.target.checked)}
                />
              }
              label="Habilitar sincronização com Azure DevOps"
            />

            {adoEnabled && (
              <Stack spacing={3} sx={{ pl: 2, borderLeft: '4px solid', borderColor: 'divider' }}>
                <TextField
                  label="Organização"
                  value={adoOrganization}
                  onChange={(e) => setAdoOrganization(e.target.value)}
                  placeholder="MinhaOrganizacao"
                  fullWidth
                />
                <TextField
                  label="Projeto"
                  value={adoProject}
                  onChange={(e) => setAdoProject(e.target.value)}
                  placeholder="MeuProjeto"
                  fullWidth
                />
                <TextField
                  label="Personal Access Token (PAT)"
                  value={adoPat}
                  onChange={(e) => setAdoPat(e.target.value)}
                  placeholder="Seu token PAT..."
                  type="password"
                  fullWidth
                />

                {testResult && (
                  <Alert severity={testResult.success ? "success" : "error"}>
                    {testResult.msg}
                  </Alert>
                )}
                
                <Box>
                  <Button
                    variant="outlined"
                    startIcon={<PlayCircleOutlineIcon />}
                    onClick={handleTestAdo}
                    disabled={testingAdo || !adoOrganization || !adoPat}
                  >
                    {testingAdo ? 'Testando...' : 'Testar Conexão'}
                  </Button>
                </Box>
              </Stack>
            )}

            {errorMsg && <Alert severity="error">{errorMsg}</Alert>}

            <Box>
              <Button
                variant="contained"
                startIcon={<SaveOutlinedIcon />}
                onClick={handleSave}
                disabled={saving}
              >
                {saving ? 'Salvando...' : 'Salvar'}
              </Button>
            </Box>
          </Stack>
        </CardContent>
      </Card>
      <Snackbar
        open={success}
        autoHideDuration={3000}
        onClose={() => setSuccess(false)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity="success" onClose={() => setSuccess(false)}>
          Configurações salvas com sucesso!
        </Alert>
      </Snackbar>
    </Box>
  );
}
