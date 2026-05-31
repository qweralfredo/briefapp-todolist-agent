import { useEffect, useRef, useState } from 'react';
import {
  Box,
  Button,
  Card,
  CircularProgress,
  Divider,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import MDEditor from '@uiw/react-md-editor';
import { AutoAwesome, Send, CheckCircle, Loop, Token } from '@mui/icons-material';
import { useProjectContext } from '../context/useProjectContext';
import { apiClient } from '../api/client';
import mermaid from 'mermaid';

mermaid.initialize({ startOnLoad: false, theme: 'default' });

const MermaidRenderer = ({ code }: { code: string }) => {
  const ref = useRef<HTMLDivElement>(null);
  const [errorObj, setErrorObj] = useState<any>(null);

  useEffect(() => {
    if (ref.current && code) {
      ref.current.innerHTML = '';
      setErrorObj(null);
      mermaid.render(`mermaid-${Math.random().toString(36).substring(2)}`, code).then(({ svg }) => {
        if (ref.current) {
          ref.current.innerHTML = svg;
        }
      }).catch((e: any) => {
        console.error("Mermaid error:", e);
        setErrorObj(e);
      });
    }
  }, [code]);

  return (
    <div style={{ margin: '20px 0' }}>
      {errorObj && (
        <Typography color="error" variant="caption" sx={{ mb: 1, display: 'block' }}>
          Erro ao renderizar diagrama (mostrando código bruto):
        </Typography>
      )}
      {!errorObj && <div ref={ref} style={{ display: 'flex', justifyContent: 'center' }} />}
      {errorObj && (
         <Box sx={{ background: '#f5f5f5', p: 2, borderRadius: 1, overflowX: 'auto' }}>
           <pre style={{ margin: 0 }}>{code}</pre>
         </Box>
      )}
    </div>
  );
};

export function AgentPlannerPage() {
  const { selectedProject } = useProjectContext();
  const [order, setOrder] = useState('');
  const [complexity, setComplexity] = useState('1.0');
  const [selectedTier, setSelectedTier] = useState<'S' | 'M' | 'L' | 'custom'>('M');
  const [tokenBudget, setTokenBudget] = useState('200000');
  const [planMarkdown, setPlanMarkdown] = useState<string>('');
  const [feedbackHistory, setFeedbackHistory] = useState<string[]>([]);
  const [isGenerating, setIsGenerating] = useState(false);
  const [isExecuting, setIsExecuting] = useState(false);
  const [executionResult, setExecutionResult] = useState<string | null>(null);
  const [loopMeta, setLoopMeta] = useState<{
    iteration: number;
    tokens_used: number;
    budget: number;
    status: string;
  } | null>(null);

  const handleTierChange = (tier: 'S' | 'M' | 'L') => {
    setSelectedTier(tier);
    if (tier === 'S') setComplexity('0.2');
    else if (tier === 'M') setComplexity('1.0');
    else if (tier === 'L') setComplexity('3.0');
  };

  const handleComplexityChange = (val: string) => {
    setComplexity(val);
    const num = parseFloat(val);
    if (num === 0.2) setSelectedTier('S');
    else if (num === 1.0) setSelectedTier('M');
    else if (num === 3.0) setSelectedTier('L');
    else setSelectedTier('custom');
  };

  const handleGeneratePlan = async () => {
    if (!selectedProject?.id || !order.trim()) return;

    setIsGenerating(true);
    setExecutionResult(null);
    setLoopMeta(null);

    // Initial markdown placeholder
    setPlanMarkdown('Reunindo dados do projeto e raciocinando...\n');

    // We use a custom fetch + SSE parsing since EventSource might be tricky with POST body
    const body = {
      project_id: selectedProject.id,
      order: order,
      complexity_multiplier: parseFloat(complexity) || 1.0,
      feedback_history: feedbackHistory,
      max_tokens_budget: parseInt(tokenBudget) || 200000,
    };

    try {
      const response = await fetch(`http://${window.location.hostname}:8483/api/agent/plan/stream`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
      });
      
      const reader = response.body?.getReader();
      const decoder = new TextDecoder('utf-8');
      
      if (!reader) throw new Error('No readable stream');
      setPlanMarkdown('');
      let fullText = '';
      let buffer = '';

      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        
        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        
        // O último item não termina com \n, pode estar incompleto. Guardamos no buffer
        buffer = lines.pop() || '';

        for (const line of lines) {
          const trimmed = line.trim();
          if (trimmed.startsWith('data: ')) {
            const dataStr = trimmed.substring(6);
            if (dataStr === '[DONE]') continue;
            try {
              const data = JSON.parse(dataStr);
              if (data.error) {
                setPlanMarkdown(prev => prev + '\n\n**Erro:** ' + data.error);
                break;
              }
              if (data.meta) {
                setLoopMeta(data.meta);
              }
              if (data.text) {
                fullText += data.text;
                setPlanMarkdown(fullText);
              }
              if (data.done) {
                setLoopMeta(prev => prev ? { ...prev, status: 'complete', tokens_used: data.total_tokens || prev.tokens_used } : null);
                break;
              }
            } catch (err) {
              console.error('JSON parse error na linha:', trimmed, err);
            }
          }
        }
      }
    } catch (err) {
      console.error(err);
      setPlanMarkdown(prev => prev + '\n\n**Erro ao comunicar com planner:** ' + err);
    } finally {
      setIsGenerating(false);
    }
  };

  const handleAddFeedback = () => {
    if (!order.trim()) return;
    setFeedbackHistory(prev => [...prev, order]);
    handleGeneratePlan(); // regenerates incorporating the new feedback
  };

  const parseJsonFromMarkdown = (md: string) => {
    let rawJson = null;
    const startStr = "```json_payload";
    const startFallback = "```json";

    let startIndex = md.indexOf(startStr);
    let offset = startStr.length;
    if (startIndex === -1) {
      startIndex = md.indexOf(startFallback);
      offset = startFallback.length;
    }

    if (startIndex !== -1) {
      const contentAfterStart = md.substring(startIndex + offset);
      // Pega o último fechamento, garantindo que não vai quebrar se houver ```mermaid interno
      const endIndex = contentAfterStart.lastIndexOf("```");
      rawJson = endIndex !== -1 ? contentAfterStart.substring(0, endIndex).trim() : contentAfterStart.trim();
    }

    if (rawJson) {
      try {
        return JSON.parse(rawJson);
      } catch (err: any) {
        console.error("JSON parse error na string:\n-----\n", rawJson, "\n-----\nErro:", err);
      }
    }
    return null;
  };

  const handleExecutePlan = async () => {
    if (!selectedProject?.id || !planMarkdown) return;
    const payload = parseJsonFromMarkdown(planMarkdown);
    if (!payload) {
      setExecutionResult('Falha: Nenhum JSON payload detectado no plano.');
      return;
    }

    setIsExecuting(true);
    setExecutionResult('⏳ Inicializando materialização fracionada...');
    try {
      const response = await fetch(`http://${window.location.hostname}:8483/api/agent/execute`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          project_id: selectedProject.id,
          plan_payload: payload,
          complexity_multiplier: parseFloat(complexity) || 1.0
        })
      });

      const reader = response.body?.getReader();
      const decoder = new TextDecoder('utf-8');
      
      if (!reader) throw new Error('Não foi possível obter o stream de execução.');

      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        const chunk = decoder.decode(value, { stream: true });
        
        const events = chunk.split('\n\n');
        for (const ev of events) {
          if (ev.startsWith('data: ')) {
            try {
              const data = JSON.parse(ev.replace('data: ', ''));
              if (data.message) {
                setExecutionResult(data.message);
              }
            } catch (err) {}
          }
        }
      }
    } catch (e: any) {
      setExecutionResult('Erro: ' + e.message);
    } finally {
      setIsExecuting(false);
    }
  };

  if (!selectedProject) return null;

  return (
    <Stack direction="row" spacing={2} sx={{ height: 'calc(100vh - 120px)', p: 2, overflowX: 'auto' }}>
      {/* Left Column: Chat / Prompt Options */}
      <Paper sx={{ width: '400px', flexShrink: 0, p: 3, display: 'flex', flexDirection: 'column', gap: 2, overflowY: 'auto' }}>
        <Typography variant="h5" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <AutoAwesome color="primary" /> Atomic Planner
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Descreva sua feature ou épico. O Agente Gemini 3.1 Pro (Architect) irá analisar seu projeto, regras e gerar um plano atômico detalhado.
        </Typography>

        <Divider />

        <Stack direction="column" gap={1}>
          <Typography variant="caption" fontWeight="bold" color="text.secondary">
            Tier de Complexidade (Atomic Flow)
          </Typography>
          <Stack direction="row" spacing={1}>
            {(['S', 'M', 'L'] as const).map((tier) => {
              const isActive = selectedTier === tier;
              return (
                <Button
                  key={tier}
                  variant={isActive ? 'contained' : 'outlined'}
                  onClick={() => handleTierChange(tier)}
                  color={tier === 'S' ? 'success' : tier === 'M' ? 'primary' : 'warning'}
                  sx={{
                    flex: 1,
                    py: 0.8,
                    fontWeight: 'bold',
                    borderRadius: 2,
                    textTransform: 'none',
                    transition: 'all 0.2s ease',
                    boxShadow: isActive ? '0 3px 8px rgba(0,0,0,0.12)' : 'none',
                    '&:hover': {
                      transform: 'translateY(-1px)',
                    }
                  }}
                >
                  Tier {tier}
                </Button>
              );
            })}
          </Stack>
        </Stack>

        <Paper
          variant="outlined"
          sx={{
            p: 2,
            borderRadius: 2,
            bgcolor: 'background.default',
            border: '1px solid',
            borderColor: selectedTier === 'S' ? 'success.light' : selectedTier === 'M' ? 'primary.light' : selectedTier === 'L' ? 'warning.light' : 'divider',
            background: selectedTier === 'S' ? 'linear-gradient(135deg, rgba(76, 175, 80, 0.05) 0%, rgba(76, 175, 80, 0) 100%)' :
                      selectedTier === 'M' ? 'linear-gradient(135deg, rgba(33, 150, 243, 0.05) 0%, rgba(33, 150, 243, 0) 100%)' :
                      selectedTier === 'L' ? 'linear-gradient(135deg, rgba(255, 152, 0, 0.05) 0%, rgba(255, 152, 0, 0) 100%)' : 'none',
            transition: 'all 0.3s ease',
          }}
        >
          <Typography variant="caption" sx={{ fontWeight: 'bold', display: 'flex', alignItems: 'center', gap: 0.5 }}>
            {selectedTier === 'S' && '🟢 Tier S — Simple'}
            {selectedTier === 'M' && '🔵 Tier M — Medium'}
            {selectedTier === 'L' && '🟠 Tier L — Large'}
            {selectedTier === 'custom' && '⚙️ Custom Tier'}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, fontSize: '0.75rem', lineHeight: 1.3 }}>
            {selectedTier === 'S' && 'Cenário: Bug fix, config, docs, ajuste pontual. Focado em entregas rápidas.'}
            {selectedTier === 'M' && 'Cenário: Feature nova, integração ou módulo. Ideal para entregas de médio porte.'}
            {selectedTier === 'L' && 'Cenário: Refactor estrutural ou grande épico multi-domínio.'}
            {selectedTier === 'custom' && 'Configuração customizada para o multiplicador de complexidade.'}
          </Typography>
          <Divider sx={{ my: 0.8 }} />
          <Typography variant="caption" color="text.secondary" display="block" sx={{ fontSize: '0.7rem' }}>
            {selectedTier === 'S' && 'Alvo: 2 Backlogs | 2 Sprints/BL | 1 Task/Sprint | 1 Subtask/Task'}
            {selectedTier === 'M' && 'Alvo: 10 Backlogs | 7 Sprints/BL | 3 Tasks/Sprint | 4 Subtasks/Task'}
            {selectedTier === 'L' && 'Alvo: 30 Backlogs | 21 Sprints/BL | 9 Tasks/Sprint | 12 Subtasks/Task'}
            {selectedTier === 'custom' && `Alvo calculado com C = ${complexity}`}
          </Typography>
        </Paper>

        <Stack direction="row" spacing={1}>
          <TextField
            label="Complexidade (C)"
            type="number"
            value={complexity}
            onChange={(e) => handleComplexityChange(e.target.value)}
            inputProps={{ step: 0.1, min: 0.1 }}
            size="small"
            helperText="0.2=Bug, 1.0=Módulo, 3.0=Épico"
            sx={{ flex: 1 }}
          />
          <TextField
            label="Token Budget"
            type="number"
            value={tokenBudget}
            onChange={(e) => setTokenBudget(e.target.value)}
            inputProps={{ step: 50000, min: 10000 }}
            size="small"
            helperText="Limite do loop"
            sx={{ flex: 1 }}
          />
        </Stack>

        <TextField
          label="Sua Ordem / Especificação"
          multiline
          minRows={6}
          value={order}
          onChange={(e) => setOrder(e.target.value)}
          placeholder="Ex: Criar módulo de autenticação seguindo JWT com refresh token e login social."
        />

        {feedbackHistory.length > 0 && (
          <Box sx={{ bgcolor: 'rgba(0,0,0,0.03)', p: 1, borderRadius: 1 }}>
            <Typography variant="caption" fontWeight="bold">Histórico de Iterações ({feedbackHistory.length}):</Typography>
            {feedbackHistory.map((h, i) => (
              <Typography key={i} variant="caption" display="block" sx={{ mt: 0.5 }}>- {h.length > 50 ? h.substring(0, 50) + '...' : h}</Typography>
            ))}
          </Box>
        )}

        <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
          <Button
            variant="contained"
            fullWidth
            startIcon={isGenerating ? <CircularProgress size={20} color="inherit" /> : <Send />}
            onClick={planMarkdown ? handleAddFeedback : handleGeneratePlan}
            disabled={isGenerating || !order.trim()}
          >
            {isGenerating ? 'Gerando...' : (planMarkdown ? 'Enviar Feedback' : 'Rascunhar Plano')}
          </Button>
        </Stack>
      </Paper>

      {/* Right Column: Planner Visual Renderer */}
      <Card sx={{ width: '800px', flexShrink: 0, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center', bgcolor: 'primary.main', color: 'primary.contrastText' }}>
          <Stack direction="row" alignItems="center" spacing={1}>
            <Typography variant="h6">Plano Visual</Typography>
            {loopMeta && (
              <Stack direction="row" spacing={1} alignItems="center" sx={{ ml: 2 }}>
                <Box sx={{
                  display: 'flex', alignItems: 'center', gap: 0.5,
                  bgcolor: 'rgba(255,255,255,0.15)', borderRadius: 1, px: 1, py: 0.3
                }}>
                  <Loop sx={{ fontSize: 16, animation: loopMeta.status === 'generating' || loopMeta.status === 'continuing' ? 'spin 1s linear infinite' : 'none' }} />
                  <Typography variant="caption">
                    Loop {loopMeta.iteration}
                  </Typography>
                </Box>
                <Box sx={{
                  display: 'flex', alignItems: 'center', gap: 0.5,
                  bgcolor: 'rgba(255,255,255,0.15)', borderRadius: 1, px: 1, py: 0.3
                }}>
                  <Token sx={{ fontSize: 16 }} />
                  <Typography variant="caption">
                    {(loopMeta.tokens_used / 1000).toFixed(1)}k / {(loopMeta.budget / 1000).toFixed(0)}k tokens
                  </Typography>
                </Box>
                <Box sx={{
                  bgcolor: loopMeta.status === 'complete' ? 'success.dark'
                    : loopMeta.status === 'budget_exhausted' ? 'warning.dark'
                    : loopMeta.status === 'stalled' ? 'error.dark'
                    : 'info.dark',
                  borderRadius: 1, px: 1, py: 0.3
                }}>
                  <Typography variant="caption" sx={{ textTransform: 'uppercase', fontWeight: 'bold', letterSpacing: 0.5 }}>
                    {loopMeta.status === 'generating' ? '⚡ Gerando'
                      : loopMeta.status === 'continuing' ? '🔄 Continuando'
                      : loopMeta.status === 'complete' ? '✅ Completo'
                      : loopMeta.status === 'budget_exhausted' ? '⛔ Budget Esgotado'
                      : loopMeta.status === 'stalled' ? '⚠️ Estagnado'
                      : loopMeta.status}
                  </Typography>
                </Box>
              </Stack>
            )}
          </Stack>
          {planMarkdown && !isGenerating && (
            <Button
              variant="contained"
              color="success"
              startIcon={isExecuting ? <CircularProgress size={20} color="inherit" /> : <CheckCircle />}
              onClick={handleExecutePlan}
              disabled={isExecuting}
            >
              Aprovar e Executar
            </Button>
          )}
        </Box>

        <Box sx={{ flex: 1, p: 3, overflowY: 'auto' }} data-color-mode="light">
          {executionResult && (
            <Paper sx={{ p: 2, mb: 2, bgcolor: executionResult.includes('Erro') ? 'error.light' : 'success.light', color: 'white' }}>
              <Typography>{executionResult}</Typography>
            </Paper>
          )}

          {planMarkdown ? (
            <MDEditor.Markdown 
              source={planMarkdown} 
              style={{ minHeight: '100%', paddingBottom: '40px' }}
              components={{
                code: ({ inline, className, children, ...props }: any) => {
                  const match = /language-(\w+)/.exec(className || '');
                  if (!inline && match && match[1] === 'mermaid') {
                    const extractText = (node: any): string => {
                      if (typeof node === 'string') return node;
                      if (Array.isArray(node)) return node.map(extractText).join('');
                      if (node?.props?.children) return extractText(node.props.children);
                      return '';
                    };
                    const codeText = extractText(children);
                    return <MermaidRenderer code={codeText.replace(/\n$/, '')} />;
                  }
                  return <code className={className} {...props}>{children}</code>;
                }
              }}
            />
          ) : (
            <Box sx={{ display: 'flex', height: '100%', alignItems: 'center', justifyContent: 'center', opacity: 0.5 }}>
              <Typography>Aguardando elaboração do plano...</Typography>
            </Box>
          )}
        </Box>
      </Card>
    </Stack>
  );
}
