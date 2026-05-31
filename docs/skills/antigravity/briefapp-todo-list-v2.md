---
name: briefapp-todo-list-v2
description: >
  Use esta skill para TUDO que envolve gerenciamento de projetos e tarefas via briefapp Todo List.
  Cobre o MVP completo: projetos, backlog, sprints, work items, sub-tasks, wiki, documentação,
  knowledge checkpoints e leitura de recursos via MCP. Ative sempre que o usuário mencionar:
  criar projeto, listar backlog, planejar sprint, atualizar tarefa, registrar checkpoint,
  documentar decisão, adicionar wiki, arquivar projeto, sub-tasks, triagem de tasks ou
  qualquer operação de gestão de desenvolvimento de software. Se o usuário perguntar sobre
  status de tarefas, andamento de sprint ou histórico de conhecimento, use esta skill imediatamente.
---

# briefapp Todo List v2 — Skill Completa do MVP

Esta skill cobre **100% do MVP** do servidor MCP `briefapp-todo-list-mcp`, mapeando todos os
tools de escrita e todos os recursos de leitura disponíveis. Use-a como referência definitiva
para qualquer operação no briefapp.

---

## 0. Regras Fundamentais (Ler Primeiro)

### 0.1 Protocolo RVW — Read-Verify-Write (Anti-Alucinação)

> 🧠 **REGRA MAIS IMPORTANTE:** Toda operação de escrita DEVE ser seguida por uma leitura de verificação.

O protocolo RVW elimina alucinações garantindo que o agente **nunca assuma** que uma operação funcionou — ele **confirma**.

```
PADRÃO RVW:
1. WRITE  → Executar a operação (ex: backlog_add)
2. READ   → Ler o estado resultante (ex: backlog_list)
3. VERIFY → Confirmar que o item existe e está correto
4. SE FALHA → Retry 1x → Se falhar novamente → PARAR e reportar erro
```

**Exemplos concretos:**

| Operação | Escrita | Leitura de Verificação | O que verificar |
|----------|---------|------------------------|-----------------|
| Criar projeto | `project_create` | `project_list` | Projeto na lista |
| Criar backlog | `backlog_add` | `backlog_list` | Item na lista com título correto |
| Criar sprint | `sprint_create` | `briefapp://projects/{id}/sprints` | Sprint com backlog items vinculados |
| Atualizar status | `workitem_update` | `workitem_list` | Status correto |
| Criar subtask | `workitem_add_subtask` | `workitem_list` | Subtask vinculada ao pai |

**Nunca prosseguir se a verificação falhar.** Reportar o erro e aguardar resolução.

### 0.2 Quality Gates — Mínimos de Qualidade Obrigatórios

Toda entidade criada no briefapp DEVE atender a barra mínima de qualidade:

| Entidade | Campo | Mínimo | Exemplo Ruim ❌ | Exemplo Bom ✅ |
|----------|-------|--------|-----------------|----------------|
| Task | `title` | ≥10 chars, verbo+objeto | "Auth" | "Implementar autenticação JWT" |
| Task | `description` | ≥50 chars, O QUE + POR QUÊ | "Fazer login" | "Criar endpoint POST /auth/login que valida credenciais contra o banco e retorna JWT com expiração de 24h. Necessário para permitir acesso autenticado ao dashboard." |
| Task | Acceptance Criteria | ≥2 critérios testáveis | (nenhum) | "DADO credenciais válidas QUANDO POST /auth/login ENTÃO retorna 200 + token JWT válido" |
| Work Item | `feedback` | ≥30 chars, técnico | "Feito" | "Implementado endpoint JWT com bcrypt para hash, middleware de validação e refresh token rotation" |
| Backlog | `description` | ≥30 chars | "Login" | "Sistema de autenticação com JWT, refresh tokens e controle de sessão" |

**Se o agente não conseguir criar uma descrição com ≥50 chars, significa que a task não está bem definida — decompor ou clarificar antes de criar.**

### 0.3 Matriz de Decisão Autônoma

O agente deve ser autônomo onde seguro e consultar o usuário apenas quando necessário:

| Situação | Ação | Justificativa |
|----------|------|---------------|
| Task clara e scope limitado | ✅ PROSSEGUIR | Baixo risco, reversível |
| Nomenclatura, estilo, formatação | ✅ PROSSEGUIR | Preferência estética, não-bloqueante |
| Escolha de lib/framework com equivalentes | ✅ PROSSEGUIR (justificar no commit) | Decisão técnica de baixo impacto |
| Ordem de execução de tasks independentes | ✅ PROSSEGUIR | Otimização interna |
| Decisão arquitetural (DB schema, API design) | ❓ PERGUNTAR | Alto impacto, difícil reverter |
| Dependência externa nova (serviço, API, custo) | ❓ PERGUNTAR | Implicações financeiras/operacionais |
| Requisito ambíguo ou contraditório | ❓ PERGUNTAR | Risco de implementar errado |
| Mudança que afeta segurança/dados de produção | 🛑 PARAR e PERGUNTAR | Risco crítico |

### 0.4 Recuperação de Erros

| Tipo de Erro | Ação | Limite |
|--------------|------|--------|
| MCP timeout | Retry com backoff: 2s → 4s → 8s | Max 3 tentativas |
| MCP erro de validação | Corrigir payload, retry 1x | Se persistir → skip + log |
| Falha parcial em batch | Continuar batch, reportar sumário no final | Não reiniciar do zero |
| Branch conflict | `git stash` → `git pull` → `git stash pop` → resolver | Se complexo → PERGUNTAR |
| Teste falhando | Analisar erro, corrigir, re-executar | Max 3 ciclos → PERGUNTAR |

---

## 1. Arquitetura do MVP

O briefapp é organizado em dois planos:

| Plano | Mecanismo | Uso |
|-------|-----------|-----|
| **Leitura** | Resources (`briefapp://...`) | Contexto, dashboards, listagens |
| **Escrita** | Tools MCP | Criar, atualizar, deletar entidades |

> 🚨 **ATENÇÃO: INVOCANDO TOOLS E RESOURCES** 🚨
> As ferramentas (Tools) listadas abaixo são expostas nativamente para você via MCP (Model Context Protocol).
> Você DEVE chamá-las através do seu mecanismo de **function calling nativo** (tool calls do modelo).
> **NUNCA** tente executar essas ferramentas no terminal ou shell usando `npx`, `npm` ou linha de comando. Chame as funções nativamente!

---

## 2. Resources de Leitura (Read-Only Context)

Leia resources sempre **antes de escrever** para ter contexto atualizado.

### Resources Diretos

| URI | Descrição |
|-----|-----------|
| `briefapp://about` | Mapa completo do servidor (resources + templates) |
| `briefapp://projects/active` | Lista projetos ativos |
| `briefapp://projects/all` | Lista todos os projetos, incluindo arquivados |

### Resource Templates (substituir `{project_id}` pelo ID real)

| URI Template | Descrição |
|--------------|-----------|
| `briefapp://projects/{id}/context` | Contexto completo do projeto (config + dashboard + knowledge) |
| `briefapp://projects/{id}/config` | Configuração: gitHubUrl, localPath, techStack, mainBranch |
| `briefapp://projects/{id}/dashboard` | Visão geral: sprints ativos, work items pendentes, métricas |
| `briefapp://projects/{id}/backlog` | Todos os backlog items do projeto |
| `briefapp://projects/{id}/sprints` | Todos os sprints do projeto |
| `briefapp://projects/{id}/workitems` | Todos os work items do projeto |
| `briefapp://projects/{id}/workitems/status/{status}` | Work items filtrados por status (0-4) |
| `briefapp://projects/{id}/sprints/{sprint_id}/workitems` | Work items de um sprint específico |
| `briefapp://projects/{id}/tasks/overview` | Visão geral de tasks: contagens por status |
| `briefapp://projects/{id}/tasks/triage` | Tasks bloqueadas ou sem sprint (para triagem) |
| `briefapp://projects/{id}/knowledge` | Wiki, docs, checkpoints e agent runs |

> **Padrão de uso:** Ao iniciar qualquer sessão, leia `briefapp://projects/active` e depois
> `briefapp://projects/{id}/context` do projeto relevante antes de qualquer operação de escrita.

---

## 3. Tools de Projetos

### `project_create`
Cria um novo projeto no briefapp.

```
Campos: name (str), description (str)
Opcionais: github_url, local_path, tech_stack, main_branch
```

**Quando usar:** Primeira vez que o usuário menciona um projeto que não existe ainda.

### `project_list`
Lista projetos. Aceita `include_archived=true` para incluir arquivados.

### `project_config_update`
Atualiza configuração de ambiente do projeto (apenas campos fornecidos são alterados).

```
Campos: project_id (obrigatório)
Opcionais: github_url, local_path, tech_stack, main_branch
```

### `project_delete`
Arquiva (soft delete) um projeto por ID. O projeto não é apagado permanentemente.

---

## 4. Tools de Backlog

### `backlog_add`
Adiciona um backlog item ao projeto.

```
Campos obrigatórios:
  project_id    — ID do projeto
  title         — Título do backlog item
  description   — Descrição detalhada (≥30 chars — Quality Gate)
  story_points  — Pontos de esforço (int)
  priority      — 0=Low, 1=Medium, 2=High, 3=Critical
```

> **RVW:** Após `backlog_add`, execute `backlog_list` e confirme que o item aparece.

### `backlog_list`
Lista todos os backlog items de um projeto (`project_id` obrigatório).

### `backlog_context_update`
Atualiza metadados de contexto em um backlog item (apenas campos fornecidos são alterados).

```
Campos: backlog_item_id (obrigatório)
Opcionais:
  tags        — labels separadas por vírgula (ex: 'auth,security,mvp')
  wiki_refs   — referências a wiki pages (ex: 'wiki:Authentication,wiki:JWT-Design')
  constraints — pré-condições ou dependências (ex: 'Must be done before Sprint 3')
```

**Quando usar:** Ao concluir um backlog item, enriqueça seus metadados com tags, refs e constraints.

---

## 5. Tools de Sprint

### `sprint_create`
Cria um sprint a partir de backlog items existentes.

```
Campos obrigatórios:
  project_id       — ID do projeto
  name             — Nome do sprint (ex: 'Sprint 1 — Auth')
  goal             — Objetivo do sprint
  start_date       — Data início (YYYY-MM-DD)
  end_date         — Data fim (YYYY-MM-DD)
  backlog_item_ids — Array de IDs de backlog items a incluir (DEVE conter ao menos 1 ID)
```

> Backlog items devem existir antes de criar o sprint. Use `backlog_add` primeiro.
> ⚠️ **Validação**: `backlog_item_ids` NÃO pode ser vazio. O MCP retorna erro se nenhum ID for fornecido.
> **RVW:** Após `sprint_create`, leia `briefapp://projects/{id}/sprints` e confirme.

---

## 6. Tools de Work Items

Work items são as tarefas concretas dentro de um sprint, derivadas dos backlog items.

### `workitem_list`
Lista work items de um projeto filtrados por sprint.

```
Campos obrigatórios:
  project_id — ID do projeto
  sprint_id  — ID do sprint (OBRIGATÓRIO — use briefapp://projects/{id}/sprints para obter IDs válidos)
```

> ⚠️ **Validação**: `sprint_id` é OBRIGATÓRIO. O MCP retorna erro se não for fornecido.

### `workitem_update`
Atualiza status e rastreia metadados de execução de um work item.

```
Campos obrigatórios:
  work_item_id — ID do work item
  status       — Ver tabela de status abaixo
  assignee     — Responsável (nome ou identificador)

Campos opcionais (rastreamento de execução):
  branch       — Branch git sendo trabalhada (ex: 'feature/login')
  agent_name   — Nome do agente (ex: 'Antigravity')
  model_used   — Modelo LLM usado (ex: 'gemini-2.5-pro')
  ide_used     — IDE utilizada (ex: 'vscode')
  tokens_used  — Contagem REAL de tokens (nunca estimada — omitir se não disponível)
  feedback     — Observações sobre o trabalho (≥30 chars — Quality Gate. NUNCA use "unknown", "unknow" ou strings vazias. Descreva concretamente a implementação real)
```

**Tabela de status (use string label, não int):**

| String | Int | Significado |
|--------|-----|-------------|
| `"todo"` | 0 | Não iniciado |
| `"in_progress"` | 1 | Em progresso |
| `"review"` | 2 | Em revisão |
| `"done"` | 3 | Concluído |
| `"blocked"` | 4 | Bloqueado |

### `workitem_add_subtask`
Cria uma sub-task sob um work item existente. Sub-tasks herdam sprint e backlog do pai.
Quando **todos os filhos** chegam a Done, o pai é automaticamente completado.

```
Campos obrigatórios:
  parent_work_item_id — ID do work item pai
  title               — Título da sub-task (≥10 chars — Quality Gate)
  description         — Descrição detalhada (≥50 chars — Quality Gate)

Campos opcionais:
  assignee — Responsável
  branch   — Branch git
  tags     — Tags separadas por vírgula
```

**Quando usar sub-tasks:**
- Work item com múltiplas etapas independentes
- Feature grande que pode ser paralelizada entre agentes
- Qualquer work item que levaria mais de 30 minutos sem decomposição

---

## 7. Tools de Conhecimento

### `wiki_add`
Cria uma página wiki para o projeto.

```
Campos obrigatórios:
  project_id       — ID do projeto
  title            — Título da página
  content_markdown — Conteúdo em Markdown
  tags             — Tags separadas por vírgula

Opcional:
  category — Categoria (default: 'General')
```

**Quando usar:** Ao concluir um backlog item, documente decisões técnicas, padrões adotados
e lições aprendidas em uma página wiki.

### `wiki_list`
Lista todas as páginas wiki de um projeto.

### `documentation_add`
Cria uma página de documentação formal (diferente de wiki — é voltada a usuários/equipe).

```
Campos obrigatórios:
  project_id       — ID do projeto
  title            — Título
  content_markdown — Conteúdo em Markdown
  category         — Categoria (ex: 'API', 'Setup', 'Architecture')
  tags             — Tags separadas por vírgula
```

### `documentation_list`
Lista todas as páginas de documentação de um projeto.

### `knowledge_checkpoint`
Salva um checkpoint de conhecimento do projeto — snapshot do estado atual para uso futuro.

```
Campos obrigatórios:
  project_id       — ID do projeto
  name             — Nome do checkpoint (ex: 'Sprint 1 — Auth Concluído')
  context_snapshot — Resumo do estado atual do projeto
  decisions        — Decisões técnicas tomadas
  risks            — Riscos identificados
  next_actions     — Próximas ações planejadas
```

**Quando criar checkpoints:**
- Ao concluir um sprint
- Ao finalizar um backlog item importante
- Antes de uma mudança arquitetural grande
- Ao final de qualquer sessão de trabalho longa

### Critérios de Aceite (Obrigatório)

Ao iniciar o planejamento de um Sprint ou criar Backlog Items/Tasks, é **OBRIGATÓRIO** criar Critérios de Aceite. Use `documentation_add` (com `category='AcceptanceCriteria'`) ou `wiki_add` com a tag `criterios-de-aceite`.

Formato obrigatório para cada critério:
```
DADO [contexto/pré-condição]
QUANDO [ação do usuário/sistema]
ENTÃO [resultado esperado verificável]
```

Todas as Sprints e Tasks devem ter Critérios de Aceite registrados. No fim de toda sprint, o software DEVE ser testado e conferido contra estes critérios.

### `knowledge_list`
Lista o payload completo de conhecimento (wiki + docs + checkpoints + agent runs).

### `checkpoint_list`
Lista apenas os knowledge checkpoints de um projeto.

### `get_modification_impact`
Avalia o acoplamento temporal de um arquivo através de mineração no Git e correlação histórica com WorkItems prévios.

```
Campos obrigatórios:
  project_id — ID do projeto
  file_path  — Caminho relativo do arquivo alvo
```

**Quando usar:** Antes de realizar um refactor profundo ou ao investigar bugs (ex: "Quando este arquivo muda, o que mais quebra junto?").

---

## 8. Fluxo de Sessão (3 Fases)

### Fase LOAD — Carregar Contexto (Início de sessão)

```
1. briefapp://projects/active                   → identificar projeto
2. briefapp://projects/{id}/context             → contexto completo (config + dashboard + knowledge)
3. briefapp://projects/{id}/tasks/triage        → tasks bloqueadas/sem sprint
4. Ler Critérios de Aceite da task/sprint alvo  → wiki/docs com tag criterios-de-aceite
5. todo.md (se existir na raiz)                 → checklist do Protocol Handler
```

### Fase WORK — Executar com Verificação Contínua

```
6. workitem_update(status='in_progress')  → marcar início (RVW: verificar status)
7. IMPLEMENTAR no workspace isolado:
   - Branch da task
   - TDD: Red → Green → Refactor (para lógica de negócio)
   - Commits convencionais com bloco Refs:
8. workitem_update(status='review')       → marcar para revisão
9. VERIFICAR contra Critérios de Aceite   → testes + execução manual
10. git add, commit, push                 → persistir com bloco Refs:
```

### Fase CLOSE — Documentar e Finalizar

```
11. workitem_update(status='done')         → SOMENTE após push e validação QA
12. wiki_add / documentation_add           → documentar decisões (por backlog)
13. backlog_context_update                 → tags, wiki_refs, constraints
14. knowledge_checkpoint                   → snapshot do estado (por sprint/backlog)
```

> **Regra de Ouro:** Nunca conclua uma task (passo 11) se os testes quebrarem ou não houver commit estruturado.

---

## 9. Fluxo de Criação de Novo Projeto e Feature

> **REGRAS OBRIGATÓRIAS PARA NOVOS PROJETOS:**
> 1. **Diretório Padrão:** Todo novo projeto DEVE ser criado em `C:\briefapp\projetos\{nome-projeto}\master` e esse deve ser o `local_path` no briefapp.
> 2. **Repositório Privado:** O projeto DEVE ser inicializado no Git e um repositório remoto privado no GitHub deve ser criado (`gh repo create {nome} --private ...`).
> 3. **Estrutura de Agentes:** Cada subagente opera em sua própria pasta separada (`C:\briefapp\projetos\{nome-projeto}\{papel-do-agente}`) clonando o repositório e criando sua branch.
> 4. **Settings MCP:** Cada pasta DEVE ter `.gemini/settings.json` com o MCP configurado (ver Seção 12).

```
0. Criar .gemini → mkdir C:\briefapp\projetos\{nome-projeto}\.gemini
               → criar settings.json (Seção 12) nessa pasta
1. Preparar pastas  → mkdir C:\briefapp\projetos\{nome-projeto}\master\.gemini
               → copiar settings.json
2. Inicializar Git  → git init, commit inicial e gh repo create --private
3. project_create   → RVW: verificar com project_list
4. backlog_add × N  → RVW: verificar cada um com backlog_list
5. sprint_create    → RVW: verificar com briefapp://projects/{id}/sprints
6. workitem_list    → verificar work items gerados automaticamente
7. documentation_add→ OBRIGATÓRIO: Critérios de Aceite para TODAS as Tasks
8. wiki_add         → página de arquitetura inicial
9. knowledge_checkpoint → snapshot inicial
```

---

## 10. Tools do Ecossistema briefapp-Box (Módulos Base)

A versão v3 ("Box Architecture") injetou novas tools e resources para uso com os Boxes de projeto e RAG.

### Tools de Context-Box RAG
`context_box_ingest(file_path: str)`: Envia arquivo local ao sistema RAG (Extract, Split, Embed, LanceDB).
`context_box_query(query: str, limit: int, file_type: str)`: Busca semântica mapeada para similaridade (retorna content, score, metadata).
`context_box_list()`: Lista arquivos ingeridos atualmente.
`context_box_delete(file_path: str)`: Deleta de forma síncrona um arquivo e seus chunks RAG.

### Futuros Tools Restantes (Users, Memory-Box, Usage, Logs, API Keys, Allow-List)
*Endpoints da API já suportam*, porém tools do MCP específicos em desenvolvimento:
- **Memory-Box:** para chatbots persistentes (key-value storage)
- **Log / Usage:** métricas para agentes autonômos e requests processados.
- **Box Users / Security:** Permissões explícitas.

### Novos Resources de Leitura Box
| URI Template | Descrição |
|--------------|-----------|
| `briefapp://boxes/{id}/context-rag` | (Disponível agora) Retorna estado estático e chunks processados com o file count (RAG). |
| `briefapp://boxes/{id}/users` | (Coming soon) Box users autorizados |
| `briefapp://boxes/{id}/usage` | (Coming soon) Visões e logs |

---

## 11. Prompt Cache (Otimização de Tokens)

### `prompt_cache_stats`
Retorna estatísticas de cache hit/miss. Se `box_id` for fornecido, retorna stats daquele box específico.

### `prompt_cache_warm`
Faz warmup de todos os segmentos cacheáveis para um box.
```
Campos obrigatórios: box_id — ID do box alvo
```

### `prompt_cache_clear`
Invalida o cache de um box. Se `segment_type` for fornecido, invalida apenas aquele tipo.
```
Campos obrigatórios: box_id
Opcionais: segment_type — SystemPrompt, ToolDefinitions, ProjectContext
```

### `prompt_cache_configure`
Cria ou atualiza um segmento cacheável para um box (upsert).
```
Campos obrigatórios: box_id, segment_type, content
```

### `prompt_cache_get_prefix`
Retorna todos os segmentos cacheáveis de um box, ordenados por tipo.

---

## 12. Referência Rápida de IDs

Os IDs no briefapp são UUIDs. Sempre obtenha IDs via tools/resources de listagem — **nunca assuma ou invente IDs**.

| Entidade | Como obter o ID |
|----------|-----------------|
| Projeto | `project_list` ou `briefapp://projects/active` |
| Backlog item | `backlog_list` ou `briefapp://projects/{id}/backlog` |
| Sprint | `briefapp://projects/{id}/sprints` |
| Work item | `workitem_list` ou `briefapp://projects/{id}/workitems` |

---

## 13. Conexão MCP e Autenticação (Multi-tenant)

O servidor MCP do briefapp é protegido e requer o envio obrigatório do token de autenticação.
O token possui o prefixo `pbx_...`.

**Arquitetura atual — Dual Transport:**
O servidor briefapp expõe **dois endpoints simultâneos**:
- `POST /mcp` → **Streamable HTTP** (SDK v1.27+, protocolo moderno) ← **padrão atual**
- `GET /sse` + `POST /messages/` → SSE legado (mantido para compatibilidade)

O **proxy Node.js** (`./install/proxy/pandora-mcp-proxy.mjs`) faz a ponte `stdio ↔ Streamable HTTP` e é iniciado automaticamente pelo Gemini CLI via `command`.

> ✅ **Use sempre o proxy (command-based)**. Não use `type: "sse"` + URL direta.

O `settings.json` correto para o Gemini CLI:

```json
{
  "mcpServers": {
    "briefapp-todo-list-mcp": {
      "command": "node",
      "args": ["./install/proxy/pandora-mcp-proxy.mjs"],
      "env": {
        "BRIEFAPP_API_KEY": "pbx_c3bfdcc755b695668000b47524e7de24ae9d46e0d266dadb",
        "MCP_ENDPOINT": "http://localhost:8481/mcp"
      }
    }
  }
}
```

**Onde colocar o `settings.json`:**

```
C:\briefapp\.gemini\settings.json                                     ← raiz do launcher
C:\briefapp\projetos\.gemini\settings.json                            ← todos os projetos agents
C:\briefapp\projetos\{nome-projeto}\.gemini\settings.json             ← por projeto
C:\briefapp\projetos\{nome-projeto}\master\.gemini\settings.json      ← branch master
C:\briefapp\projetos\{nome-projeto}\{agente}\.gemini\settings.json    ← por subagente
C:\projetos\briefapp\.gemini\settings.json                            ← repositório principal
C:\projetos\briefapp\briefapp-super-agent\.gemini\settings.json       ← super-agente
C:\projetos\briefapp\briefapp-todolist\.gemini\settings.json          ← projeto (+ checkpointing)
C:\Users\alfre\.local\bin\.gemini\settings.json                       ← launcher global
```

> **Regra:** Ao criar um novo projeto, worktree ou pasta de subagente, **sempre crie** o `.gemini/settings.json` naquela pasta.

---

## 14. Retomar Trabalho Interrompido

Quando uma sessão é interrompida ou o agente precisa continuar trabalho anterior:

```
1. briefapp://projects/{id}/context               → estado atual do projeto
2. briefapp://projects/{id}/workitems/status/1     → tasks in_progress (possível work anterior)
3. briefapp://projects/{id}/tasks/triage           → tasks bloqueadas/órfãs
4. checkpoint_list(project_id)                     → último checkpoint (decisões, riscos, next_actions)
5. git log -5 --oneline                            → últimos commits (ver onde parou)
6. git status                                      → mudanças não commitadas
```

**Regras ao retomar:**
- Se há task `in_progress` sem commits recentes → verificar se o trabalho existe no código
- Se há mudanças não commitadas → avaliar, commitar ou descartar antes de prosseguir
- Se o último checkpoint tem `next_actions` → usá-las como guia para continuar
- Nunca recriar tasks que já existem — sempre verificar com `workitem_list` antes
