# Documentação — Atualizações v2.1 (Sub-tasks, Branch, Context-First)

Data: 21/03/2026

## ✅ Arquivos Atualizados

### 1. **README.md** — Features e API
- ✅ Adicionado "Advanced Task Management" com sub-tasks recursivas, branch tracking e context-first enrichment
- ✅ Atualizada tabela "Available Tools" com novas ferramentas
- ✅ Atualizado "Available Prompts" com `briefapp_context_first_execute`
- ✅ Adicionados novos endpoints REST: `/api/work-items/{id}/sub-tasks` e `/api/backlog-items/{id}/context`

### 2. **docs/ARCHITECTURE.md** — Arquitetura e Fluxo
- ✅ Adicionada seção "Recursos Avançados" com Recursive Sub-Tasks, Branch Tracking e Context-First Enrichment
- ✅ Explicado fluxo atualizado: Scan → Warm-up → Inject → Execute → Review
- ✅ Detalhes sobre auto-completamento de parent e constraints de FK

### 3. **docs/mcps/briefapp-mcp.md** — Documentação MCP
- ✅ Atualizada tabela de Tools com 3 novas ferramentas:
  - `backlog_context_update` — Atualizar metadados de backlog
  - `workitem_add_subtask` — Criar sub-tasks recursivas
  - `workitem_update` — Agora inclui campo `branch`
- ✅ Adicionada seção "Available Prompts" com documentação do prompt `briefapp_context_first_execute`
- ✅ Exemplos de JSON para cada nova ferramenta

### 4. **.github/copilot-instructions.md** — Instruções do Workspace
- ✅ Adicionada seção "Context-First Execution Flow" com 5 passos estruturados
- ✅ Atualizada tabela de "Campos obrigatórios" com novas ferramentas e campo `branch`
- ✅ Atualizada tabela de "Valores fixos para workitem_update" com `branch`
- ✅ Exemplos de uso de sub-tasks

### 5. **docs/skills/copilot/context-first-execution.md** — NOVO
- ✅ Criada nova skill com documentação completa
- ✅ Explicação dos 5 passos com exemplos práticos
- ✅ Padrões comuns: Spike, Performance, Blocked
- ✅ Exemplos Python e integração com kanban frontend

### 6. **mcp-server-python/README.md** — Documentação do Server MCP
- ✅ Adicionada seção "Novidades (v2.1)" no início
- ✅ Referência para a nova skill de context-first execution

### 7. **vscode-userdata:**[geral.instructions.md] — Skill Global VS Code
- ✅ Atualizada tabela de "Campos obrigatórios" (6 ferramentas agora)
- ✅ Adicionada nova seção "Context-First Execution Flow" completa
- ✅ Adicionada documentação de "Sub-Tasks (Recursivas)" e "Branch Tracking"
- ✅ Atualizada priority table de Skills (adicionado `context-first-execution` em Prioridade 1)

---

## 📊 Estatísticas

| Arquivo | Mudanças |
|---------|----------|
| README.md | +30 linhas |
| ARCHITECTURE.md | +80 linhas |
| briefapp-mcp.md | +130 linhas |
| copilot-instructions.md | +120 linhas |
| context-first-execution.md | +450 linhas (NEW) |
| mcp-server-python/README.md | +12 linhas |
| geral.instructions.md (VS Code global) | +180 linhas |
| **TOTAL** | **+1002 linhas** |

---

## 🎯 Cobertura de Features

### Sub-Tasks Recursivas
- ✅ Descrição técnica em ARCHITECTURE.md
- ✅ MCP tool (`workitem_add_subtask`) em briefapp-mcp.md
- ✅ Exemplos práticos em context-first-execution.md
- ✅ Fluxo no copilot-instructions.md
- ✅ Skill global VS Code atualizada

### Branch Tracking
- ✅ Campo `branch` em `workitem_update` (MCP, REST, README)
- ✅ Exemplos no copilot-instructions.md
- ✅ Integração frontend (badges em kanban)

### Context-First Execution
- ✅ Prompt `briefapp_context_first_execute` no MCP
- ✅ 5-step workflow documentado em 3 lugares (README, copilot-instructions, context-first-execution skill)
- ✅ Intergração com skill global do VS Code
- ✅ Links cruzados entre documentações

---

## 🔗 Links de Referência

**Para novos desenvolvedores:**
1. Ler [README.md](../README.md) → entender features
2. Ler [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md) → entender fluxo
3. Ler [docs/skills/copilot/context-first-execution.md](../docs/skills/copilot/context-first-execution.md) → aprender workflow
4. Invocar `briefapp_context_first_execute` prompt no VS Code antes de cada task complexa

**Referência rápida:**
- MCP tools: [docs/mcps/briefapp-mcp.md](../docs/mcps/briefapp-mcp.md#available-tools)
- Campos obrigatórios: [.github/copilot-instructions.md](./.github/copilot-instructions.md#campos-obrigatórios)
- Prompts disponíveis: [docs/mcps/briefapp-mcp.md](../docs/mcps/briefapp-mcp.md#available-prompts)

---

## 📝 Commits Relacionados

- `77042a0` — Backend: sub-tasks, branch, context-first fields + migration
- `bfcbd3f` — MCP: branch in workitem_update + 3 new tools + briefapp_context_first_execute prompt
- `ab2d340` — Frontend: sub-tasks badge, branch display, backlog context editor
- `07daba9` — Docs: update skills, architecture, and MCP documentation

---

## ✨ Próximas Melhorias Sugeridas

1. Criar diagrama visual do fluxo Context-First (Mermaid)
2. Adicionar exemplos de sub-tasks em video/screencast
3. Expandir seção de "Padrões Avançados" (monorepo, microservices)
4. Metrics dashboard para visualizar sub-task completion rate

---

**Documentação completa e sincronizada! 🎉**

---

## Atualização Incremental — Frontend Commit IDs + UX de Navegação

Data: 21/03/2026

### Escopo aplicado
- Listagem de `commitIds` nas três entidades visíveis no frontend:
  - Backlog: chips de commit no card e edição no modal de contexto
  - Sprint: exibição de quantidade de commits nos chips de agrupamento e no cabeçalho do kanban filtrado
  - Task (work item): chips de commit no card e edição no modal `Edit task`
- Menu lateral desktop retrátil (expandido/recolhido)
- Correção do vão lateral entre menu e conteúdo principal (remoção de offset duplicado)
- Reestruturação do modal `Edit task` para preencher dados atuais da task:
  - status/assignee/branch/commitIds vindos do work item
  - agent/model/ide/tokens/feedback/metadata vindos do último feedback salvo

### Arquivos frontend alterados
- `frontend/src/types.ts`
- `frontend/src/api/client.ts`
- `frontend/src/layout/AppLayout.tsx`
- `frontend/src/pages/SprintsPage.tsx`
- `frontend/src/pages/BacklogPage.tsx`

### Validação
- Build frontend executado com sucesso: `npm run build`

---

## Atualização UI — Ordenação Alfabética de Backlogs

Data: 24/03/2026

### Feature
- Backlogs combobox na página de sprints (http://localhost:8400/sprints) agora ordena opções em ordem alfabética por título
- Aplicado em dois locais:
  1. **Filter combobox** (cabeçalho): Dropdown de filtro mostra todos os backlogs ordenados alfabeticamente
  2. **Sprint creation modal**: Lista de backlogs selecionáveis (filtrada por status ≤ 2) também ordenada alfabeticamente

### Implementação
- Arquivo: `frontend/src/pages/SprintsPage.tsx`
- Método: `.sort((a, b) => a.title.localeCompare(b.title))`
- Sorting: Case-insensitive, compatível com caracteres acentuados (pt-BR)
- Nota: Grouped view mantém ordenação por prioridade (não alterado)

### Validação
- ✅ Build executado sem erros: `npm run build`
- ✅ Container Docker construído: `docker compose build frontend --no-cache`
- ✅ Deployment realizado: `docker compose up -d frontend`
- ✅ Container rodando em porta 8400:80

### Commit
- `feat/rich-ui-copilot-style` — Sort backlog comboboxes alphabetically on sprints page
