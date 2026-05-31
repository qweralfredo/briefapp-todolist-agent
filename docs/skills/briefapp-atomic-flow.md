---
name: briefapp-atomic-flow
description: >
  Metodologia de orquestração de engenharia de software orientada a agentes que converte intenções
  de alto nível em unidades de trabalho rastreáveis, seguras e executáveis via briefapp MCP.
  Use esta skill SEMPRE que o usuário mencionar: briefapp-atomic-flow, atomic flow, fluxo atômico,
  planejamento proporcional à complexidade, branches efêmeras, tiers de complexidade,
  épico grande, refactor estrutural, feature complexa com múltiplos domínios, decomposição
  hierárquica de tarefas, ou quando precisar escalar o planejamento além de um sprint simples.
  Ative também quando o usuário disser "tier S", "tier M", "tier L", "use atomic flow" ou qualquer variante.
  Esta skill complementa a skill `briefapp-todo-list-v2` com a camada de orquestração — não a substitui.
---

# briefapp Atomic Flow — Orquestração Anti-Alucinação

## Visão Geral

O **Atomic-Agent Flow** é uma metodologia de planejamento fractal onde o **Tier de Complexidade** define a densidade de decomposição do trabalho. A complexidade do planejamento é **proporcional à complexidade da entrega** — um fluxo enxuto para tarefas simples; um grafo denso para iniciativas de grande porte.

**Princípio central:** Cada task criada deve ser **verificável, executável em uma sessão focada, e rastreável**. Se uma task não atende esses 3 critérios, ela precisa ser decomposta ou refinada.

---

## Passo 0 — Classificação do Tier de Complexidade

Antes de qualquer planejamento, classifique o tier com base no escopo:

| Tier | Cenário típico | Backlogs | Sprints | Tasks/Sprint | Subtasks/Task | Max total |
|------|---------------|----------|---------|--------------|---------------|-----------|
| **S** (Simple) | Bug fix, config, docs, ajuste pontual | 1 | 1 | 1-3 | 0-2 | ~6 |
| **M** (Medium) | Feature nova, módulo, integração | 2-5 | 1-3 | 3-7 | 2-5 | ~105 |
| **L** (Large) | Refactor estrutural, épico multi-domínio | 5-15 | 3-7 | 5-10 | 3-8 | ~840 |

**Heurísticas para classificação:**
- **S:** "Consigo descrever a mudança em 1-2 frases" → corrigir um bug, adicionar campo, ajustar config
- **M:** "Preciso de um plano com múltiplas etapas" → novo endpoint com validação e testes, tela com CRUD completo
- **L:** "Envolve múltiplos domínios ou sistemas" → migração de banco, nova arquitetura, módulo com 5+ componentes

Se o usuário não especificar tier, classifique com base no escopo e confirme: *"Classifico como Tier M (Feature média). Correto?"*

> **Para migrações legadas (C=2, C=3):** Se o usuário usar a notação C antiga, mapeie: C≤0.5→S, C=1→M, C≥2→L

---

## Passo 1 — Inicialização do Projeto briefapp

Antes de criar qualquer entidade, carregar contexto completo:

```
1. briefapp://projects/active           → listar projetos existentes
2. briefapp://projects/{id}/context     → contexto completo (se projeto existe)
3. project_create (se necessário)       → RVW: verificar com project_list
```

> Consultar a skill `briefapp-todo-list-v2` (Seção 0.1 - Protocolo RVW) para o padrão de verificação.

---

## Passo 2 — Decomposição Hierárquica com Verificação

### 2.1 Heurística de Decomposição de Tasks

**O método para decompor requisitos em tasks sem alucinar:**

```
1. RELER o requisito original do usuário (literalmente — não parafrasear da memória)
2. LISTAR todos os comportamentos verificáveis:
   - O que o sistema deve FAZER?
   - O que o sistema deve REJEITAR/PREVENIR?
   - Que DADOS devem persistir?
   - Que INTERFACES mudam?
3. CADA comportamento verificável = 1 task
4. VALIDAR cada task:
   ✅ Completável em 1 sessão focada (15-60 min)?
   ✅ Testável com critério de aceite claro?
   ✅ Independente o suficiente para um commit isolado?
5. Se task > 60 min → decompor em subtasks
6. TESTE DE SUFICIÊNCIA: "Se eu deletar esta task, algo não será entregue?"
   - Se SIM → manter
   - Se NÃO → remover (é redundante)
7. TESTE DE COBERTURA: "Existe algum aspecto do requisito sem task correspondente?"
   - Se SIM → criar task faltante
   - Se NÃO → decomposição completa
```

### 2.2 Plano de Execução em CSV

Antes de chamar qualquer MCP para criação em massa, esboce o plano em CSV:

```csv
Type,ID,Title,Description,ParentID,Status,AcceptanceCriteria
Epic,E1,"Nova Iniciativa","Descrição do Epic",,todo,""
Backlog,B1,"Área Funcional 1","Descrição Backlog E1",E1,todo,""
Sprint,S1,"Sprint 1 - Objetivo","",B1,todo,""
Task,T1,"Implementar endpoint POST /auth/login","Criar endpoint de autenticação que valida credenciais e retorna JWT",S1,todo,"DADO credenciais válidas QUANDO POST /login ENTÃO retorna 200+JWT"
Subtask,ST1,"Criar schema de validação","Schema Zod/Pydantic para request body de login",T1,todo,""
```

**Atualização do CSV (Checkpoint):**
Conforme o trabalho avança, marcar itens como `done`. O CSV atua como fonte da verdade local.

### 2.3 Materialização no briefapp via MCP

Após aprovação do plano:

```
1. backlog_add          → para cada backlog item → RVW: backlog_list
2. sprint_create        → vinculando backlog_item_ids → RVW: briefapp://projects/{id}/sprints
3. workitem_list        → verificar work items gerados automaticamente
4. workitem_add_subtask → para decomposição fina → RVW: workitem_list
5. documentation_add    → Critérios de Aceite obrigatórios
6. backlog_context_update → tags, wikiRefs, constraints iniciais
```

**Anti-alucinação:** Após criar TODAS as entidades, fazer um read completo e comparar com o CSV planejado.

---

## Passo 3 — Checkpoints Anti-Alucinação

Em cada transição de fase, o agente DEVE executar uma verificação:

| Momento | O que fazer | Como verificar |
|---------|-------------|----------------|
| **Antes de criar tasks** | Re-ler requisito original do usuário | Comparar intenção vs. plano CSV |
| **Após criar tasks** | Listar TODAS as tasks criadas | `workitem_list` → comparar com CSV |
| **Antes de implementar** | Ler critérios de aceite | `briefapp://projects/{id}/knowledge` |
| **Antes de marcar done** | Executar critérios de aceite | Rodar testes + verificação manual |
| **Após sprint completo** | Dashboard review | `briefapp://projects/{id}/dashboard` → todas done? |
| **Fim de sessão longa** | Knowledge checkpoint | `knowledge_checkpoint` com snapshot completo |

**Regra:** Se uma verificação revelar inconsistência → PARAR → corrigir antes de prosseguir.

---

## Passo 4 — Context-First Execution (Por Task)

Para cada task, executar as 4 etapas antes de escrever código:

1. **Discovery** — `workitem_list` + ler critérios de aceite; confirmar estado atual
2. **Context Injection** — ler backlog item com `tags`/`wikiRefs`/`constraints`; criar subtasks se necessário
3. **Execution** — `workitem_update(status='in_progress')` → implementar → testar
4. **Validation** — critérios de aceite passam? → `workitem_update(status='done', feedback='[DETALHE TÉCNICO]')`

> Subtasks com auto-completar: quando **todos** os filhos estão `done`, o pai é marcado automaticamente.

---

## Passo 5 — Arquitetura de Branches (3 Níveis)

A estrutura de branches reflete a hierarquia do Atomic Flow com **3 níveis** (não 5):

```
develop → feature/{backlog-id} → task/{task-id}
```

### 5.1 Criação da Estrutura

```bash
git checkout develop
git pull origin develop
git checkout -b feature/{backlog_id}
```

### 5.2 Execução por Task

Cada task cria sua branch a partir da feature:

```bash
git checkout feature/{backlog_id}
git checkout -b task/{task_id}

# ... Implementação, TDD, Commits ...

git commit -m "feat: <descrição atômica>

Refs: backlog/<b_id> | sprint/<s_id> | task/<t_id>"
```

**Subtasks NÃO criam branches próprias** — trabalham direto na branch da task com commits separados. Isso elimina overhead de merge sem perder rastreabilidade (o commit message referencia a subtask).

### 5.3 Merge Simplificado (2 Níveis)

```bash
# Task concluída → merge na feature:
git checkout feature/{backlog_id}
git merge task/{task_id} --no-ff
git branch -d task/{task_id}

# Feature/Backlog concluído → merge em develop:
git checkout develop
git merge feature/{backlog_id} --no-ff
git branch -d feature/{backlog_id}
git push origin develop
```

> **Próxima feature** sempre criada a partir de `develop` atualizada.

---

## Passo 6 — TDD Proporcional ao Risco

O nível de testes é proporcional ao risco da mudança:

| Tipo de mudança | Abordagem | Commits | Cobertura |
|-----------------|-----------|---------|-----------|
| **Lógica de negócio** (validação, cálculos, regras) | TDD completo: 🔴 Red → 🟢 Green → 🔵 Refactor | 3 | ≥ 80% |
| **Integração/Infraestrutura** (API, DB, messaging) | Implementar → Teste de integração | 2 | ≥ 60% |
| **Config/Docs/Estilo** (README, CSS, env vars) | Sem testes (justificar no commit) | 1 | N/A |

**Formato de commits TDD:**

| Passo | Formato do commit |
|-------|-------------------|
| 🔴 RED | `test: add failing test for <feature>` |
| 🟢 GREEN | `feat: implement <feature> to pass tests` |
| 🔵 REFACTOR | `refactor: clean up <feature>` |

**Comandos de cobertura por linguagem:**

| Linguagem | Comando |
|-----------|---------|
| Python | `pytest --cov=src --cov-report=term-missing --cov-fail-under=80` |
| TypeScript/JS | `vitest run --coverage` ou `jest --coverage` |
| C# | `dotnet test --collect:"XPlat Code Coverage"` |
| Go | `go test -coverprofile=coverage.out && go tool cover -func=coverage.out` |

---

## Passo 7 — Rastreabilidade em Todo Commit

**Todo commit** deve conter o bloco `Refs:`:

```
<tipo>: <descrição curta>

Refs: backlog/<backlog-id> | sprint/<sprint-id> | task/<task-id>
Wiki: <wiki-id>            ← incluir se houver wiki page relacionada
Checkpoint: <cp-id>        ← incluir ao concluir sprint ou epic
```

Commit sem `Refs:` é inválido e não encerra nenhuma task.

---

## Passo 8 — Sincronização de Status no Kanban

Mantenha o briefapp sempre atualizado:

| Evento | Tool MCP | Status |
|--------|----------|--------|
| Task criada no plano | `workitem_update` | `"todo"` |
| Iniciando implementação | `workitem_update` | `"in_progress"` |
| Subtask/Task concluída | `workitem_update` + commit | `"done"` |
| Bloqueio identificado | `workitem_update` | `"blocked"` |
| Aguardando revisão | `workitem_update` | `"review"` |

**Campos obrigatórios em toda chamada `workitem_update`:**

| Campo | Valor |
|-------|-------|
| `agent_name` | Nome do agente/assistente ativo |
| `model_used` | Modelo LLM da sessão |
| `ide_used` | IDE em uso (ex: `VS Code`, `Cursor`) |
| `branch` | Branch da task (ex: `task/wk-42`) |
| `feedback` | ≥30 chars — resumo técnico concreto do que foi implementado (NUNCA use "unknown" ou genéricos) |

---

## Passo 9 — Finalização de Backlog Item

Ao concluir **todas** as tasks de um backlog item, execute em ordem:

1. `backlog_context_update` — tags finais, wikiRefs, constraints descobertas
2. `wiki_add` — documentar decisões técnicas e padrões de arquitetura
3. `knowledge_checkpoint` — salvar `context_snapshot`, `decisions`, `risks`, `next_actions`
4. Verificar remote: `git remote -v` → criar se ausente: `gh repo create <nome> --private --source=. --remote=origin --push`
5. Commit de fechamento com `Refs:` completo + `git push`
6. `workitem_update` em todas as tasks com hash do commit final

**Checklist de fechamento:**

- [ ] Todos os work items do backlog estão `done` no briefapp
- [ ] `backlog_context_update` executado com tags, wikiRefs e constraints finais
- [ ] `wiki_add` para decisões técnicas relevantes
- [ ] `knowledge_checkpoint` salvo
- [ ] Repositório remoto verificado e pushado
- [ ] Commit de fechamento com bloco `Refs:` completo

---

## Exemplo Completo — Feature Média (Tier M)

**Usuário:** "Implementar módulo de notificações em tempo real com WebSocket e persistência"

**Agente:**

```
1. Tier: M → 3 backlogs, 2 sprints, ~5 tasks/sprint, ~3 subtasks/task
2. LOAD: project_list → techStack=FastAPI/Redis/PostgreSQL
3. DECOMPOSIÇÃO (Heurística):
   - Comportamentos: conectar WS, receber notif, persistir, listar histórico, marcar lida
   - 5 tasks identificadas, cada uma 30-45 min
   - TESTE DE COBERTURA: ✅ todos os aspectos cobertos
4. CSV gerado → usuário aprova
5. MCP: backlog_add × 3 → RVW verificado ✅
6. MCP: sprint_create × 2 → RVW verificado ✅
7. documentation_add: Critérios de Aceite
8. Para cada task:
   - git checkout -b task/{id}
   - TDD (lógica de negócio): Red → Green → Refactor
   - workitem_update(status='done', feedback='Implementado WebSocket handler com...')
   - git merge → feature branch
9. Ao concluir feature: merge → develop + wiki_add + knowledge_checkpoint
```

---

## Regras Inegociáveis

1. **Protocolo RVW:** Toda escrita → leitura de verificação → assert. Sem exceção.
2. **Quality Gates:** Tasks com título < 10 chars ou descrição < 50 chars são inválidas.
3. **Critérios de Aceite:** Toda task DEVE ter ao menos 2 critérios testáveis antes do início.
4. **Status sync:** `workitem_update` imediato ao iniciar e ao concluir. Sem delay.
5. **Bloco Refs:** Todo commit DEVE conter `Refs:` — commit sem refs é inválido.
6. **Feedback real:** Campo `feedback` DEVE ser técnico e concreto (≥30 chars). Nunca "unknown".
7. **Cobertura:** Lógica de negócio: ≥80%. Integração: ≥60%. Config/docs: isenta (justificar).
8. **Sem mock/fallback silencioso** em runtime (apenas em testes automatizados).
9. **Fluxo sequencial:** Iniciar próxima task somente após commitar e fechar a atual.
10. **Windows/PowerShell:** Cada comando em chamada separada, sem encadear com `;`.
