# briefapp-plan — Backlog + Sprint Planning

Antes de qualquer código, decomponha a solicitação e registre no Briefapp.

## 1. Estrutura do Plano (apresentar ao usuário)

```markdown
## Backlog

### Epic: <nome da funcionalidade principal>

#### Story 1: <descrição>
- [ ] Task 1.1 — <atômica, máx 2h>
- [ ] Task 1.2 — <atômica, máx 2h>

#### Story 2: <descrição>
- [ ] Task 2.1 — <descrição>

---

## Sprint 1 — Escopo

- [ ] Task 1.1
- [ ] Task 1.2
- [ ] Task 2.1

**Critérios de aceite:**
- [ ] Cobertura de testes ≥ 80%
- [ ] Todos os testes passando
- [ ] Validação E2E executada
```

## 2. Registrar no Briefapp via MCP

Para cada backlog item:
```
mcp__local__backlog_add(
  project_id, title, description,
  priority (0=Low, 1=Medium, 2=High, 3=Critical),
  story_points
)
```

Enriquecer contexto do backlog:
```
mcp__local__backlog_context_update(
  backlog_item_id,
  tags        = [...],       # tags relevantes
  wikiRefs    = [...],       # wiki pages relacionadas
  constraints = "..."        # constraints ou dependências conhecidas
)
```

Criar sprint:
```
mcp__local__sprint_create(
  project_id, name, goal,
  start_date (YYYY-MM-DD), end_date,
  backlog_item_ids: [...]
)
```

Marcar work items como todo:
```
mcp__local__workitem_update(
  work_item_id, status="todo",
  agent_name="Claude Code", model_used="Claude Sonnet 4.6",
  ide_used="VS Code", branch="<mainBranch>"
)
```

## 3. Campos obrigatórios

| Tool | Campos |
|---|---|
| `backlog_add` | `project_id`, `title`, `description`, `priority` (int), `story_points` |
| `backlog_context_update` | `backlog_item_id`, opcionais: `tags`, `wikiRefs`, `constraints` |
| `sprint_create` | `project_id`, `name`, `goal`, `start_date`, `end_date`, `backlog_item_ids` |
| `workitem_update` | `work_item_id`, `status`, `assignee`, `branch`, `agent_name`, `model_used`, `ide_used`, `tokens_used`, `feedback` |
| `workitem_add_subtask` | `parent_work_item_id`, `title`, `description`, `assignee`, opcionais: `branch`, `tags` |

## 4. Enums

- `BacklogItemPriority`: Low=0, Medium=1, High=2, Critical=3
- `WorkItemStatus`: Todo=0, InProgress=1, Review=2, Done=3, Blocked=4
  - **Prefira string labels** (`"done"`, `"review"`, `"in_progress"`, `"todo"`, `"blocked"`)

## 5. Regras

- Tasks devem ser atômicas (máximo 2h de trabalho)
- Apresente o backlog **antes** de começar a codificar — aguarde aprovação do usuário
- Nunca pule o backlog, mesmo para mudanças "simples"
- Ao gerar o plano, sincronize imediatamente com Briefapp via MCP
- Evite duplicatas — verifique com `backlog_list` e `workitem_list` antes de criar
