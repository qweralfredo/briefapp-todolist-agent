# GitHub Copilot — Instruções Globais de Desenvolvimento

Estas instruções se aplicam a **todo** código gerado ou revisado pelo Copilot neste repositório,
independente da linguagem ou tamanho da tarefa.

---

## Fluxo Obrigatório

Toda solicitação deve seguir o fluxo abaixo JUNTAMENTE COM O Atomic-Agent Flow, sem exceção:


```
Backlog -> Criação dos backlog items no Briefapp → Sprint -> Criação das Sprints no Briefapp → Tasks -> Criação das Tasks no Briefapp → TDD (Red→Green→Refactor) → Cobertura ≥80% → E2E/Validação → Commit -> Referenciar o commit id e fazer Revisão das tasks/sprints/backlog no briefapp → knowledge_checkpoint
```

é necessário  rigorosamente criar um arquivo chamado checkpoint.md contendo todas as definições de backlogs, sprints, tasks, critérios de aceite, decisões técnicas e aprendizados relevantes de acordo com Atomic-Agent Flow. Este arquivo deve ser atualizado a cada backlog item concluído, garantindo um histórico de conhecimento valioso para o projeto. O checkpoint.md deve ser referenciado no `knowledge_checkpoint` do Briefapp para manter a rastreabilidade entre o código e a documentação do projeto. 


> **Ao iniciar qualquer sessão  ou fazer nova requisição** — Atualizar Skills,  Chamar `project_list` (ou `project_config_update`) para obter `mainBranch`, `gitHubUrl`, `localPath` e `techStack`. Usar essas informações em todas as decisões da sessão (branch de trabalho, caminhos, stack).

> **Bases de conhecimento:** Ao iniciar **e** concluir qualquer tarefa,  é necessário atualizar as bases de conhecimento do projeto (wiki, documentação, checkpoint, README).

- Se o projeto não tiver repositório git, crie um repositorio privado remotamente — commits são obrigatórios por task sempre.
- Se o repositório remoto não existir, crie-o via `gh repo create` antes do commit de fechamento do backlog (ver §13).
- Use portas altas (>8000) na criação de apps — confirme com o usuário antes de definir.
- Valide funcionalidades com o Playwright MCP ou Chrome WebDriver sempre que houve alterações. Em projetos sem interface web, use scripts de teste ou CLI para validação funcional. No caso de APIs, use `curl` para chamadas diretas e validação de endpoints.
- Se houver backlog items livres sem tarefa ativa, pergunte ao usuário antes de executar.

---

## Briefapp MCP — Configuração e Uso

### Configuração

Caso o MCP do Briefapp Todo List não esteja configurado, adicione ao VS Code settings:

```json
{
    "servers": {
        "briefapp-todo-list-mcp": {
            "type": "http",
            "url": "http://127.0.0.1:8481/mcp"
        }
    },
    "inputs": []
}
```

### Regras

- **1 projeto por workspace** — confirme o nome com o usuário e salve no `README.md`.
- Se o projeto não existir no Briefapp, crie via `project_create` antes de qualquer outra ação.
- Evite criar itens duplicados — verifique com `backlog_list` e `workitem_list` antes de criar.
- Mantenha o `README.md` com um **howto** claro indicando o que precisa ser feito.
- Use o **wiki** para documentar arquitetura e decisões; o **checkpoint** ao término de cada sprint ou epic. E documentação para guia de uso, setup e informações relevantes do projeto.


### Sincronização obrigatória: checklist → work items

Cada item `[ ]` gerado no planejamento **deve ser materializado no Briefapp via MCP**:

| Evento | Tool MCP | Status |
|---|---|---|
| Task criada no plano | `backlog_add` + `workitem_update` | `"todo"` (0) |
| Iniciando trabalho | `workitem_update` | `"in_progress"` (1) |
| Task concluída `[x]` | `workitem_update` + commit | `"done"` (3) |
| Task em revisão | `workitem_update` | `"review"` (2) |

Ao concluir uma epic ou sprint, chamar `knowledge_checkpoint` para salvar contexto, decisões e próximos passos.

É importante que a contagem de tokens usados na sessão seja atualizada em cada `workitem_update` para rastreabilidade de custo e eficiência.

### Campos obrigatórios

| Tool | Campos |
|---|---|
| `backlog_add` | `project_id`, `title`, `description`, `priority` (int), `story_points` |
| `backlog_context_update` | **NEW:** `backlog_item_id`, opcionais: `tags`, `wikiRefs`, `constraints` |
| `sprint_create` | `project_id`, `name`, `goal`, `start_date` (YYYY-MM-DD), `end_date`, `backlog_item_ids` |
| `workitem_update` | `work_item_id`, `status` (**string label** preferido: `"done"`, `"review"`, `"todo"`, `"in_progress"`, `"blocked"`), `assignee`, `branch`, `agent_name`, `model_used`, `ide_used`, `tokens_used`, `feedback` |
| `workitem_add_subtask` | **NEW:** `parent_work_item_id`, `title`, `description`, `assignee`, opcionais: `branch`, `tags` |
| `knowledge_checkpoint` | `project_id`, `name`, `context_snapshot`, `decisions`, `risks`, `next_actions` |

### Valores fixos para workitem_update (contexto do agente)

Sempre preencha os campos de contexto do agente em **toda** chamada `workitem_update`:

| Campo | Valor |
|---|---|
| `agent_name` | nome do agente |
| `model_used` | nome do modelo usado |
| `ide_used` | nome da IDE usada |
| `branch` | **NEW:** branch de trabalho (ex: `develop`, `feat/xyz`) |
| `tokens_used` | tokens usados na sessão por task |
| `feedback` | resumo do que foi feito nesta task |
| `metadata_json` | JSON opcional com detalhes extras (pode ser `{}`) |

### Enums

- `BacklogItemPriority`: Low=0, Medium=1, High=2, Critical=3
- `WorkItemStatus`: Todo=0, InProgress=1, Review=2, Done=3, Blocked=4
  - **Prefira string labels** (`"done"`, `"review"`, `"in_progress"`, `"todo"`, `"blocked"`) em vez de inteiros para evitar erros de mismatch

---

## Context-First Execution Flow (Briefapp v2.1+)

**Obrigatório para qualquer work item complexo ou desconhecido:**

Antes de implementar qualquer sprint, use o prompt `briefapp_context_first_execute` para estruturar a execução em 5 etapas:

### 1. **Discovery** — Scan do contexto do projeto
- Ler dashboard: `GET /api/projects/{projectId}/dashboard`
- Listar work items ativos por status
- Confirmar estado atual sem pressuposições

### 2. **Knowledge Warm-up** — Aquecer contexto técnico
- Ler wiki pages relevantes do projeto
- Revisar checkpoints recentes (decisões anteriores)
- Identificar padrões de constraints ou riscos

### 3. **Context Injection** — Carregar metadados da tarefa
- Ler backlog item com `tags`, `wikiRefs`, `constraints`
- Confirmar branch de trabalho (mainBranch ou branch específica)
- Se houver dados faltantes: chamar `backlog_context_update` para enriquecer
- Para tarefas complexas: planejar sub-tasks com `workitem_add_subtask`

### 4. **Execution** — Implementar com manutenção cognitiva
- `workitem_update(status='in_progress', branch='...')`
- Criar sub-tasks conforme necessário
- Atualizar constraints/tags ao descobrir novos aprendizados
- Ao concluir: `workitem_update(status='done', feedback='...')`

### 5. **Validation Review** — Verificar estado final
- Todos os sub-tasks estão Done? (pai auto-completa)
- Dashboard reflete mudanças?
- Checkpoints/wiki foram atualizados com decisões?
- Nenhum item bloqueado orfão?

**Resumo:** Invoque `briefapp_context_first_execute` para obter um workflow estruturado antes de codificar. Este fluxo previne rework e garante alinhamento com projeto. O checkpoint deve ser utilizado para documentar aprendizados e decisões ao final de cada backlog item ou sprint, criando um histórico de conhecimento valioso para o projeto.

### Sub-Tasks (Recursivas) — NEW

Para tarefas complexas, decomponha usando `workitem_add_subtask`:

- Parent trabalha item pode ter múltiplos filhos (até n-níveis de profundidade)
- Quando **todos** os sub-tasks estão Done → parent **auto-completa** automaticamente
- Use para: features grandes, bugs de raiz complexa, refatorações multifacetadas
- Frontend mostra badge "↳ sub-task" em kanban

**Exemplo:**
```
Feature: Integração de Auth
├─ Sub-task: JWT Backend
├─ Sub-task: React Provider
└─ Sub-task: E2E Tests
   └ [quando todos Done → Feature auto-marca Done]
```

### Branch Tracking — NEW

Cada `workitem_update` aceita um campo `branch` para rastreabilidade:

```json
{
  "work_item_id": "xyz",
  "status": "in_progress",
  "branch": "feat/auth-backend",
  ...
}
```
 
- Exibido no kanban para contexto de implementação
 

---
*OBRIGATÓRIO LER TODAS AS SEÇÕES ABAIXO PARA GARANTIR QUALIDADE, RASTREABILIDADE E EFICIÊNCIA NO DESENVOLVIMENTO*
## Atomic-Agent Flow — Orquestração Escalável

**Metodologia de orquestração de engenharia de software orientada a agentes**, projetada para converter intenções de alto nível em milhares de unidades de trabalho rastreáveis, seguras e executáveis — diferente de sistemas lineares, este fluxo utiliza uma arquitetura fractal onde a complexidade define a densidade do planejamento.

Use o **Atomic-Agent Flow** via `/briefapp-atomic-flow` para qualquer tarefa que requeira planejamento proporcional à complexidade.

### Quando usar (Multiplicador de Complexidade C)

| Cenário | C | Skill recomendada |
|---|---|---|
| Correção pontual / task isolada | 0.2 | `/briefapp-plan` + `/briefapp-execute` |
| Feature pequena / melhoria simples | 0.5 | `/briefapp-atomic-flow` com C=0.5 |
| Iniciativa de médio porte / novo módulo | 1 | `/briefapp-atomic-flow` com C=1 |
| Feature complexa / múltiplos domínios | 2 | `/briefapp-atomic-flow` com C=2 |
| Refactor estrutural / grande épico | 3 | `/briefapp-atomic-flow` com C=3 |

O proprio usuario poderá definir o multiplicador de complexidade (C) ao iniciar o fluxo, ou o agente pode sugerir um valor baseado na análise do backlog item e contexto do projeto.

### Expansão Hierárquica Matemática

O sistema opera através de um **motor de expansão recursiva** com **multiplicador de complexidade (C ∈ {0.2, 0.5, 1, 2, 3})**.

**Estrutura de Densidade Mínima por Projeto:**

| Nível             | C=0.2 | C=0.5 | C=1 | C=2 | C=3  | Fórmula |
|-------------------|-------|-------|-----|-----|------|---------|
| Backlogs          | 2     | 5     | 10  | 20  | 30   | 10 × C |
| Sprints / Backlog | 1     | 4     | 7   | 14  | 21   | 7 × C |
| Tasks / Sprint    | 1     | 2     | 3   | 6   | 9    | 3 × C |
| Subtasks / Task   | 1     | 2     | 4   | 8   | 12   | 4 × C |

**No nível máximo de complexidade (C=3)**, o sistema orquestra até **22.680 subtasks atômicas**, garantindo que nenhum detalhe técnico seja negligenciado.

### Execução Atômica (Branches Efêmeras)

Para cada subtask, seguir o protocolo de **Branches Efêmeras**:

1. **Isolation** — Criar branch de vida curta `task/{work_item_id}`
2. **Atomic Development** — Implementação focada **exclusivamente** no escopo da subtask
3. **Self-Healing & Lint** — Validação automatizada de sintaxe e testes unitários
4. **Conventional Commits** — Registro de mudanças seguindo padrões semânticos (feat, fix, test, refactor) para automação de changelogs
5. **Squash & Merge** — Integração limpa na branch de funcionalidade, mantendo histórico impecável

**Exemplo (Git Flow Atômico):**

```bash
# 1. Isolation
git checkout -b task/{work_item_id}

# 2. Atomic Development
# Implementação focada no escopo

# 3. Self-Healing & Lint
# Testes e validações

# 4. Conventional Commits
git commit -m "feat: descrição atômica da mudança"

# 5. Squash & Merge (via PR ou manual)
git checkout develop
git merge task/{work_item_id} --no-ff --squash
git branch -d task/{work_item_id}
```

---

## 1. Backlog & Sprint

Antes de qualquer código, decomponha a solicitação em markdown:

```markdown
## 📋 Backlog

### Epic: <nome da funcionalidade principal>

#### 🟦 Story 1: <descrição da história>
  - [] Sprint 1 — <descrição do objetivo da sprint>
    - [ ] Task 1.1 — <atômica, máx 2h>
    - [ ] Task 1.2 — <atômica, máx 2h>

#### 🟦 Story 2: <descrição da história>
  - [] Sprint 2 — <descrição do objetivo da sprint>
    - [ ] Task 2.1 — <atômica, máx 2h>

---

## 🏃 Sprint 1 — Escopo
- [ ] Task 1.1
- [ ] Task 1.2
- [ ] Task 2.1

**Critérios de aceite:**
- [ ] Cobertura de testes ≥ 80%
- [ ] Todos os testes passando
- [ ] Validação E2E executada
```

**Regras:**
- Tasks devem ser atômicas (máximo 10 min de trabalho)
- Sempre apresente o backlog completo antes de começar a codificar
- Marque `[x]` nas tasks conforme avança
- Ao gerar este plano, chame `backlog_add` + `sprint_create` + `workitem_update` (status `"todo"`) via MCP para cada task

---

## 2. TDD — Sempre Red → Green → Refactor

**Nunca gere implementação sem o teste correspondente.**

### Passo 1: RED — Escreva o teste primeiro (deve falhar)
- Escreva o teste antes de qualquer implementação
- Execute e confirme que ele **falha** (se passar, o teste está errado)
- Commit:
  ```
  test: add failing test for <feature>

  Refs: backlog/<backlog-id> | sprint/<sprint-id> | task/<task-id>
  ```

### Passo 2: GREEN — Implementação mínima
- Escreva o código mínimo para o teste passar
- Sem over-engineering neste passo
- Commit:
  ```
  feat: implement <feature> to pass tests

  Refs: backlog/<backlog-id> | sprint/<sprint-id> | task/<task-id>
  Wiki: <wiki-id>   ← se houver
  ```

### Passo 3: REFACTOR — Limpe o código
- Melhore legibilidade, remova duplicações
- Rode os testes novamente — todos devem continuar passando
- Commit:
  ```
  refactor: clean up <feature>

  Refs: backlog/<backlog-id> | sprint/<sprint-id> | task/<task-id>
  Wiki: <wiki-id>         ← se houver
  Checkpoint: <cp-id>     ← se sprint/epic for concluído neste passo
  ```

---

## 3. Cobertura por Linguagem

| Linguagem | Comando |
|-----------|---------|
| Python | `pytest --cov=src --cov-report=term-missing --cov-fail-under=80` |
| TypeScript/JS | `vitest run --coverage` ou `jest --coverage` |
| C# | `dotnet test --collect:"XPlat Code Coverage"` |
| Rust | `cargo llvm-cov --fail-under-lines 80` |
| Go | `go test -coverprofile=coverage.out && go tool cover -func=coverage.out` |
| Java | `mvn test jacoco:report` e verificar relatório HTML |
| PHP | `phpunit --coverage-text --coverage-clover=coverage.xml` |
| Ruby | `rspec --format documentation --format RspecJunitFormatter --out rspec.xml` + SimpleCov |

Cobertura abaixo de 80% = **task não está concluída**.

---

## 4. Estrutura de Testes por Linguagem

```
Python:   tests/test_<module>.py
          tests/test_integration_<module>.py

TS/JS:    src/__tests__/<module>.test.ts

C#:       MyProject.Tests/<Module>Tests.cs

Rust:     src/lib.rs  ← #[cfg(test)] mod tests { ... }
          tests/integration_test.rs
```

---

## 5. Validação E2E

Após TDD completo e cobertura ≥ 80%, valide com:

- **Playwright MCP** — para fluxos web e APIs via browser
- **curl / CLI** — para endpoints REST e serviços
- **Scripts de smoke test** — para integrações críticas

```bash
# Verificar status
curl -s -o /dev/null -w "%{http_code}" http://localhost:3000/health

# API REST
curl -X POST http://localhost:3000/endpoint \
  -H "Content-Type: application/json" \
  -d '{"key": "value"}' | jq .
```

---

## 6. Checklist de Conclusão de Task

Antes de marcar uma task como `[x]`, confirme:

- [ ] Teste escrito **antes** da implementação (TDD)
- [ ] RED confirmado — teste falhou primeiro
- [ ] GREEN confirmado — implementação mínima passando
- [ ] REFACTOR feito — código limpo
- [ ] Cobertura ≥ 80% confirmada
- [ ] Validação E2E ou CLI executada
- [ ] Briefapp atualizado — `workitem_update` com status `"done"` (Done=3)
- [ ] Commit realizado — obrigatório antes de iniciar a próxima task, **com bloco `Refs:` contendo backlog-id, sprint-id e task-id**
- [ ] `knowledge_checkpoint` via MCP — **somente** ao concluir epic ou sprint (salva contexto, decisões, próximos passos)
- [ ] Documentação atualizada no briefapp
- [ ] **Ao concluir o backlog item completo** → executar sequência completa de §13 (documentar contexto, commit de fechamento com referências, push, atribuir commit no Briefapp)
- [ ]  é necessário atualizar bases de conhecimento (wiki, docs, README, checkpoint) — obrigatório em **toda** task (início e conclusão)

---

## 7. Convenções de Commit

```
test:      testes adicionados ou corrigidos
feat:      implementação adicionada
refactor:  melhorias sem mudança de comportamento
fix:       correção de bug
chore:     configuração, deps, CI
docs:      documentação
```

Sequência obrigatória por task: `test:` → `feat:` → `refactor:`

### 7.1 Footer de Rastreabilidade — Obrigatório em TODO commit

**Todo commit**, sem exceção (incluindo `test:`, `feat:`, `refactor:`, `fix:`, `chore:`, `docs:`), deve conter no corpo um bloco `Refs:` com os IDs das entidades Briefapp relacionadas. Isso garante indexação bidirecional entre o histórico git e o sistema de gestão.

**Formato obrigatório:**

```
<tipo>: <descrição curta>

Refs: backlog/<backlog-id> | sprint/<sprint-id> | task/<task-id>
Wiki: <wiki-id>            ← incluir apenas se houver wiki page relacionada
Checkpoint: <checkpoint-id> ← incluir apenas se houver checkpoint relacionado
```

**Exemplos:**

```
test: add failing test for JWT validation

Refs: backlog/bl-42 | sprint/sp-07 | task/wk-115
```

```
feat: implement JWT validation to pass tests

Refs: backlog/bl-42 | sprint/sp-07 | task/wk-115
Wiki: wiki/auth-decisions
```

```
refactor: clean up JWT validation logic

Refs: backlog/bl-42 | sprint/sp-07 | task/wk-115
Wiki: wiki/auth-decisions
Checkpoint: cp-18
```

**Regras:**
- `backlog/<id>`, `sprint/<id>` e `task/<id>` são **sempre obrigatórios** — sem exceção
- `Wiki:` e `Checkpoint:` são **condicionais** — incluir quando a task ou sprint tiver wiki/checkpoint associado
- Os IDs devem ser os IDs reais retornados pelo Briefapp MCP (não rótulos humanos)
- Se múltiplas tasks forem afetadas por um único commit, liste todas: `task/wk-115, task/wk-116`
- Se múltiplos backlogs forem afetados (raro), liste todos: `backlog/bl-42, backlog/bl-43`
- Nunca omitir o bloco `Refs:` — commit sem rastreabilidade é considerado inválido

---

## 8. Regras Inegociáveis

1. **Nunca gere código sem o teste prévio** — TDD é inegociável
2. **Nunca pule o backlog** — mesmo para mudanças "simples"
3. **Cobertura abaixo de 80% = task não concluída**
4. **Sempre valide com E2E ou CLI** antes de fechar o sprint
5. **Tasks grandes devem ser quebradas** antes de começar
6. **Commit obrigatório ao concluir cada task** — nunca inicie a próxima sem commitar
7. **Sem fallback e sem mock em runtime** — integrações reais são obrigatórias
8. **Aplicação imediata das regras** — ao receber "faça", execute os itens pendentes sem pedir confirmação desnecessária
9. **Bases de conhecimento** — ao iniciar **ou** ao concluir qualquer tarefa, pergunte ao usuário se é necessário atualizar wiki, documentação, checkpoint ou README do projeto
10. **Rastreabilidade total** — todo código deve ser referenciado a um backlog item, sprint e task no Briefapp via commit message e MCP updates


---

## 9. Skills e Agentes VS Code

Use as skills e agentes disponíveis para maximizar qualidade e eficiência. A ordem de prioridade abaixo é obrigatória — **sempre verifique da linha 1 para a linha N** antes de agir manualmente:

| Prioridade | Situação | Skill / Agente |
|:---:|---|---|
| 1 | Implementação contextual de work items Briefapp com sub-tasks | Skill `geral.instructions.md` |
| 2 | Pesquisa de codebase antes de planejar qualquer implementação | Agente `Explore` |   
| 6 | Exibir resultados de busca do GitHub em tabela | Skill `show-github-search-result` |
| 7 | Exploração de repositório GitHub (pull requests, branches) | `github-pull-request_*` tools |
| 8 | Validação E2E de fluxos web com browser | Playwright MCP (`mcp_playwright_*`) |  
| 9 | Redesign/re-arquitetura de módulo | Agente `modernize-rearchitecture` |
| 10 | Fase de fundação (constitution + knowledge graph) | Agente `modernize-foundation` |
| 11 | Fase de design (specification document) | Agente `modernize-design` |
| 12 | Geração de plano de implementação | Agente `modernize-plan` |
| 13 | Geração de tasks de uma fase do plano | Agente `modernize-task` |
| 14 | Implementação em lotes (batch) | Agente `modernize-implementation` / `modernize-batch-impl` |
| 15 | Cross-check e validação de specs/planos/tasks | Agente `modernize-gatekeep` |

### Regras de Skills (em ordem de aplicação)
1. **Ao receber um backlog item** → usar `geral.instructions.md` para decompor em sub-tasks e criar no Briefapp MCP.
2. **Planejamento em codebase desconhecido** → usar `Explore` para coleta de contexto **antes** de escrever qualquer código. 
4. **Testes E2E** → usar Playwright MCP quando browser estiver disponível — não substituir por curl apenas.
5. **Modernização** → escolher o agente correto pela stack (`modernize-dotnet`, `modernize-java`, `modernize-rearchitecture` etc.) — não codificar manualmente sem o agente especializado.

---

## 10. Segurança & OWASP

### 10.1 Checklist de Segurança (OWASP Top 10)

Toda task deve validar:

- [ ] **A01:2021 – Broken Access Control** — Autorização implementada (authz middleware)
- [ ] **A02:2021 – Cryptographic Failures** — Senhas hasheadas (bcrypt), dados sensíveis encriptados
- [ ] **A03:2021 – Injection** — Queries parametrizadas, input sanitizado, sem string concatenation
- [ ] **A04:2021 – Insecure Design** — Validação de entrada, rate limiting, CORS configurado
- [ ] **A05:2021 – Security Misconfiguration** — Variáveis de ambiente, sem secrets no código
- [ ] **A06:2021 – Vulnerable & Outdated Components** — `npm audit`, `cargo audit`, dependências atualizadas
- [ ] **A07:2021 – Authentication Failures** — JWT validado, sessão segura, MFA onde crítico
- [ ] **A08:2021 – Software & Data Integrity Failures** — Assinatura de dependências, CI/CD seguro
- [ ] **A09:2021 – Logging & Monitoring Failures** — Logs estruturados, sem dados sensíveis
- [ ] **A10:2021 – SSRF** — Validação de URLs, whitelist de endpoints permitidos

---

### 10.2 Validação de Entrada
- [ ] **Whitelist de tipos esperados** — tipos explícitos, reject unknown
- [ ] **Tamanho máximo de payload** — validar `Content-Length`
- [ ] **Formato esperado** — regex, schema validation (Zod, Joi, Pydantic)
- [ ] **Caracteres especiais sanitizados** — remover/escapar `<`, `>`, `"`, `'`, `;`
- [ ] **Nenhuma execução de código do input** — `eval()` proibido
- [ ] **Validação server-side obrigatória** — nunca confie apenas no client
 

---

## 11. Ambiente Windows — Boas Práticas

### 11.1 Comandos Separados no CMD/PowerShell

**Regra inegociável:** Cada comando de teste/build em uma janela separada — evite encadear com  `;`

### 11.2 Docker Disponível

Esta máquina possui Docker instalado e configurado.

**Boas práticas:**
- Use containers para isolar ambientes de teste e desenvolvimento
- Compose para orquestração local (`docker-compose.yml`)
- Sempre valide com `docker ps` antes de executar testes
- Logs de container: `docker logs <container_id>`
- Limpeza: `docker system prune -a` (com cuidado)
 
---

## 12. Política de Integração Real (Sem Fallback/Mock)

Estas regras são obrigatórias para produção e validação funcional:

- Não usar fallback silencioso para banco, APIs, filas, cache, autenticação ou MCP
- Não usar dados mockados, stubs ou respostas fake em runtime
- Frontend deve consumir API real do backend
- Backend deve persistir em banco real configurado por ambiente
- Em caso de indisponibilidade de dependência externa, retornar erro explícito e observável

Exceções permitidas (apenas em testes automatizados):
- doubles de teste para isolar unidade quando necessário
- bancos efêmeros de teste (in-memory/testcontainer)

Mesmo em teste, é proibido mascarar falhas de integração com fallback silencioso.

---

## 13. Finalização de Backlog Item

Ao concluir **todas** as tasks de um backlog item, execute obrigatoriamente a seguinte sequência antes de iniciar o próximo backlog:

### 13.1 Documentar e Ajustar Contextos

1. **Atualizar contexto do backlog** — `backlog_context_update` com:
   - `tags`: tags finais refletindo o estado entregue
   - `wikiRefs`: referências às wiki pages criadas ou atualizadas
   - `constraints`: constraints descobertas durante a implementação
   - Toda task concluída deve ter uma wiki page de decisão técnica associada — mesmo que seja para decisões simples, isso cria um histórico valioso para o projeto e evita perda de conhecimento ao longo do tempo. Portanto, é obrigatório criar uma wiki page para cada backlog item concluído, documentando as decisões técnicas tomadas, os padrões adotados e os aprendizados relevantes. Use `wiki_add` para criar essas páginas e referencie-as no `backlog_context_update` com `wikiRefs`.

2. **Registrar decisões técnicas** — `wiki_add` para padrões de arquitetura, decisões de design ou aprendizados relevantes descobertos durante a implementação
3. **Snapshot de conhecimento** — `knowledge_checkpoint` com:
   - `context_snapshot`: estado atual do código, integrações e configurações
   - `decisions`: decisões técnicas tomadas e justificativas
   - `risks`: riscos identificados ou mitigados
   - `next_actions`: próximos passos ou dependências para backlogs futuros

### 13.2 Verificar e Criar Repositório Remoto

**Antes** do commit de fechamento, verifique se o repositório remoto existe:

```powershell
# 1. Verificar se remote está configurado
git remote -v

# 2. Se não existir remote, verificar se GitHub CLI está disponível
gh auth status
```

Se o repositório remoto **não existir**, crie-o via GitHub CLI:

```powershell
# Criar repositório privado no GitHub e configurar como origin
gh repo create <nome-do-repositorio> --private --source=. --remote=origin --push

# Ou, se o repositório já existir no GitHub mas não estiver configurado localmente:
git remote add origin https://github.com/<usuario>/<repositorio>.git
git push -u origin <mainBranch>
```

**Regras:**
- Usar `gitHubUrl` do projeto Briefapp se disponível (`project_config_update` para obter)
- Repositório privado por padrão — confirmar visibilidade com o usuário se necessário
- Após criar o repo: registrar a URL no Briefapp via `project_config_update` com `gitHubUrl`
 
```

**Regras do commit de fechamento:**
- A mensagem **deve referenciar** IDs do Briefapp (backlog item, sprint, tasks)
- Incluir resumo das decisões técnicas tomadas
- Confirmar cobertura de testes no corpo da mensagem
- Fazer push para o remoto imediatamente após o commit: `git push`

### 13.4 Atribuir Commit às Entidades Briefapp

Após o commit atualize o Briefapp com a rastreabilidade do commit em **cada task** do backlog finalizado:

```json
workitem_update({
  "work_item_id": "<task-id>",
  "status": "done",
  "branch": "<branch-name>",
  "feedback": "Commit <hash> — <titulo-do-backlog> | Sprint: <sprint-name>",
  "agent_name": "<agent-name>",
  "model_used": "<model-name>",
  "ide_used": "<IDE-name>",
  "tokens_used": <tokens-count>,
})
```

Atualizar também o contexto do sprint se o backlog for o último do sprint: (Incremental)
- `knowledge_checkpoint` com resumo do sprint concluído
- `backlog_context_update` marcando wikiRefs com o checkpoint gerado

### 13.5 Checklist de Finalização de Backlog

- [ ] Todos os work items do backlog estão `done` no Briefapp
- [ ] `backlog_context_update` executado com tags, wikiRefs e constraints finais
- [ ] `wiki_add` para decisões técnicas e padrões relevantes
- [ ] `knowledge_checkpoint` salvo com context_snapshot, decisions, risks e next_actions
- [ ] Repositório remoto verificado — criado via `gh repo create` se não existia
- [ ] Commit de fechamento realizado — mensagem com bloco `Refs:` contendo `backlog/<id> | sprint/<id> | task/<id-1>, task/<id-2>, ...` + `Wiki:` e `Checkpoint:` se aplicável
- [ ] `git push` executado com sucesso
- [ ] `workitem_update` atualizado em todas as tasks com `branch`, `feedback` e hash do commit
- [ ] Se último backlog do sprint: `knowledge_checkpoint` de sprint e atualização do sprint no Briefapp
