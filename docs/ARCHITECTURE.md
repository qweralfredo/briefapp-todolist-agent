# Arquitetura do Briefapp Todo List — V3 (Box Architecture)

## Objetivo

Disponibilizar uma plataforma onde humanos e agentes de IA compartilham contexto operacional do ciclo de software (Scrum), com dados versionáveis, rastreáveis e persistentes — agora com isolamento per-Box, RAG semântico, sandbox execution e orquestração de filas.

## Contexto para IA (Knowledge-first)

Cada projeto possui:
- Wiki pages: conhecimento acumulado estruturado
- Knowledge checkpoints: snapshots de contexto em marcos importantes
- Agent runs: histórico de execuções para auditoria e replay de aprendizado
- **Context-Box RAG** (V3): busca semântica sobre documentos ingeridos via LanceDB

Esses artefatos permitem:
- onboarding acelerado de agentes
- retomada de contexto sem perda de informação
- checkpoints para comparar decisões técnicas e riscos
- busca semântica para contexto relevante sem poluir o prompt

## Entidades principais

### Core Scrum
- Project
- BacklogItem (com Tags, WikiRefs, Constraints para context-first)
- Sprint
- WorkItem (com Branch tracking e ParentWorkItemId para sub-tasks)
- Review
- WikiPage
- KnowledgeCheckpoint
- AgentRunLog

### V3 — Box Architecture
- **Box** — unidade de isolamento (módulo/projeto/agente)
- **Sandbox** — container Docker isolado por box (CPU/RAM/rede/TTL)
- **DlqEntry** — Dead Letter Queue entry com retry/quarantine
- **CircuitBreakerSnapshot** — FSM de resiliência (Closed/Open/Half-Open)
- **TaskQueueEntry** — task na fila Tansu com lock distribuído
- **OpenClawUser** — usuário de canal externo (WhatsApp/Slack/Telegram)
- **PromptCacheSegment** — segmento cacheável (SystemPrompt/ToolDefinitions/ProjectContext)
- **McpWhiteLabelInstance** — servidor MCP isolado per-box

## Recursos Avançados

### Recursive Sub-Tasks
- WorkItem pode ter ParentWorkItemId para criar hierarquia recursiva
- Auto-completamento: quando todos os sub-tasks estão Done, parent é auto-marcado como Done
- Usado para decomposição de tarefas complexas e rastreamento fino de progresso
- Sem limite de profundidade (n-níveis de nesting)

### Branch Tracking
- Cada WorkItem pode ter um campo Branch associado
- Permite rastreabilidade entre tarefas de código e branches git
- Preenchido durante execução do `workitem_update`
- Exibido no kanban para contexto de implementação

### Context-First Backlog Enrichment
- BacklogItem enriquecida com Tags, WikiRefs, Constraints
- Tags: métricas/características da tarefa (ex: "auth", "performance")
- WikiRefs: referências para páginas de conhecimento relevantes
- Constraints: limitações não-funcionais e restrições de design

### Context-Box RAG (V3)
- Pipeline: Extract → Split → Embed → LanceDB
- Ingestão: individual (`context_box_ingest`) ou batch (`context_box_ingest_batch`)
- Busca: `context_box_query` retorna chunks com score de similaridade
- Integração: context menu (Windows/macOS) e browser extension para ingestão

### Sandbox Execution (V3)
- Containers Docker isolados por box com imagens pré-configuradas (node, python, dotnet)
- Controle de recursos: CPU (0.5–4 cores), RAM (128–2048 MB), TTL (1–120 min)
- Políticas de rede: Offline, Restricted (whitelist), Full
- Workspace: clone git com OverlayFS copy-on-write
- Métricas em tempo real: CPU%, memory, network I/O, disk I/O

### Task Queue & DLQ (V3)
- Fila Tansu por box: `briefapp.{boxId}.tasks`
- Lock distribuído com heartbeat e timeout
- ACK/NACK com retry automático
- DLQ para entries que excedem max retries
- Circuit breaker FSM por box para resiliência

### Prompt Cache (V3)
- Segmentos cacheáveis: SystemPrompt, ToolDefinitions, ProjectContext
- Hit/miss tracking com warmup proativo
- Discount rates: Anthropic 90%, OpenAI 50%, Gemini 75%

## Fluxo Scrum operacional

1. Criar projeto
2. Alimentar backlog com story points/prioridade
3. Enriquecer backlog items com tags, wiki refs e constraints
4. Criar sprint com backlog selecionado
5. Atualizar status de tarefas durante execução
6. Criar sub-tasks se tarefas forem complexas
7. Rastrear branch de trabalho em workitem_update
8. Usar Context-Box RAG para contexto semântico (V3)
9. Usar sandboxes para execução isolada (V3)
10. Registrar review
11. Atualizar wiki/checkpoints com aprendizados

## Fluxo do Briefapp Todo List via MCP (Python SDK oficial)

1. Agente conecta no servidor `mcp-server-python/server.py` (HTTP streamable)
2. Solicita `tools/list` para descobrir funcionalidades
3. Executa workflow context-first em 5 etapas:
   - **Scan:** lê dashboard e work items ativos via recursos MCP
   - **Warm-up:** carrega wiki pages e checkpoints relevantes + busca RAG
   - **Inject:** enriquece com tags/wiki_refs/constraints do backlog
   - **Execute:** implementa com sub-tasks se necessário, rastreando branch; usa sandbox se isolamento necessário
   - **Review:** valida completamento e registra checkpoint
4. Servidor MCP chama a API REST do backend (`/api/...`)
5. Salva checkpoints para preservar contexto entre sessões

## Atomic-Agent Flow

Metodologia de orquestração hierárquica fractal para converter intenções de alto nível em unidades atômicas de trabalho. Invocada via `/briefapp-atomic-flow`.

### Motor de Expansão (Multiplicador C)

| Nível             | Fórmula           | C=1 | C=2 | C=3  |
|-------------------|-------------------|-----|-----|------|
| Backlogs          | 10 × C            | 10  | 20  | 30   |
| Sprints / Backlog | 7 × C             | 7   | 14  | 21   |
| Tasks / Sprint    | 3 × C             | 3   | 6   | 9    |
| Subtasks / Task   | 4 × C             | 4   | 8   | 12   |

No nível C=3: até **22.680 subtasks atômicas**.

### Protocolo de Branches em Cascata

Hierarquia completa: `develop` → `backlog/{id}` → `sprint/{id}` → `task/{id}` → `subtask/{id}`

- Cada nível é ramificado a partir do nível imediatamente superior
- Merge em cascata (bubble-up) ao concluir cada nível
- Branch deletada após merge

Ver referência completa: [docs/skills/briefapp-atomic-flow.md](skills/briefapp-atomic-flow.md)

## Segurança e confiabilidade

- Persistência real via PostgreSQL
- Sem fallback e sem mock de runtime
- Backup em disco local (dumps diários)
- Auto-completamento de pais previne orfanatos de tarefas
- Constraints de FK com OnDelete(Restrict) evita cascata acidental
- Sandboxes isolados com controle de recursos e rede (V3)
- Circuit breaker previne cascata de falhas entre boxes (V3)
- DLQ captura tasks falhadas para análise e retry manual (V3)
