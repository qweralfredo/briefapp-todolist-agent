# briefapp-execute — Context-First Execution (Briefapp v2.1+)

Fluxo obrigatório para qualquer work item complexo. Execute as 5 etapas antes de codificar.

## Etapa 1 — Discovery

- Listar work items ativos: `mcp__local__workitem_list(project_id)`
- Confirmar estado atual sem pressuposições
- Identificar o item a ser trabalhado

## Etapa 2 — Knowledge Warm-up

- Ler wiki relevante: `mcp__local__wiki_list(project_id)`
- Revisar checkpoints recentes: `mcp__local__checkpoint_list(project_id)`
- Identificar constraints e riscos conhecidos

## Etapa 3 — Context Injection

- Ler backlog item com `tags`, `wikiRefs`, `constraints`
- Confirmar branch de trabalho (`mainBranch` ou branch específica)
- Se dados faltarem: `mcp__local__backlog_context_update(backlog_item_id, tags, wikiRefs, constraints)` para enriquecer
- Para tarefas complexas: planejar sub-tasks com `mcp__local__workitem_add_subtask`

## Etapa 4 — Execution (TDD obrigatório)

### Iniciar task:
```
mcp__local__workitem_update(
  work_item_id, status="in_progress",
  branch="<branch>", agent_name="Claude Code",
  model_used="Claude Sonnet 4.6", ide_used="VS Code"
)
```

### Ciclo TDD (Red → Green → Refactor):

1. **RED** — escrever teste primeiro (deve falhar)
   ```
   git commit -m "test: add failing test for <feature>

   Refs: backlog/<backlog-id> | sprint/<sprint-id> | task/<task-id>"
   ```

2. **GREEN** — implementação mínima para passar
   ```
   git commit -m "feat: implement <feature> to pass tests

   Refs: backlog/<backlog-id> | sprint/<sprint-id> | task/<task-id>"
   ```

3. **REFACTOR** — limpar sem quebrar testes
   ```
   git commit -m "refactor: clean up <feature>

   Refs: backlog/<backlog-id> | sprint/<sprint-id> | task/<task-id>
   Wiki: <wiki-id>         ← se houver
   Checkpoint: <cp-id>     ← se sprint/epic concluído"
   ```

> **Todo commit**, sem exceção, deve conter bloco `Refs:` com IDs reais do Briefapp.

### Cobertura obrigatória (≥ 80%):

| Linguagem | Comando |
|---|---|
| Python | `pytest --cov=src --cov-report=term-missing --cov-fail-under=80` |
| TypeScript/JS | `vitest run --coverage` ou `jest --coverage` |
| C# | `dotnet test --collect:"XPlat Code Coverage"` |
| Rust | `cargo llvm-cov --fail-under-lines 80` |
| Go | `go test -coverprofile=coverage.out && go tool cover -func=coverage.out` |
| Java | `mvn test jacoco:report` |
| PHP | `phpunit --coverage-text --coverage-clover=coverage.xml` |
| Ruby | `rspec --format documentation` + SimpleCov |

Cobertura abaixo de 80% = **task não está concluída**.

### Ao concluir:
```
mcp__local__workitem_update(
  work_item_id, status="done",
  feedback="<resumo do que foi feito>",
  tokens_used=<estimativa int>, branch="<branch>",
  agent_name="Claude Code", model_used="Claude Sonnet 4.6", ide_used="VS Code"
)
```

## Etapa 5 — Validation Review

- [ ] Todos os sub-tasks estão Done? (pai auto-completa)
- [ ] Cobertura ≥ 80% confirmada?
- [ ] Validação E2E ou CLI executada?
- [ ] Wiki/checkpoint precisam ser atualizados?
- [ ] Dashboard reflete mudanças?
- [ ] Nenhum item bloqueado orfão?

## Sub-Tasks (recursivas)

Para features grandes, decomponha com `workitem_add_subtask`:

```
mcp__local__workitem_add_subtask(
  parent_work_item_id, title, description,
  assignee, branch, tags
)
```

Quando **todos** os sub-tasks estão Done → parent auto-completa.

```
Feature: Integração de Auth
├─ Sub-task: JWT Backend
├─ Sub-task: React Provider
└─ Sub-task: E2E Tests
   └ [quando todos Done → Feature auto-marca Done]
```

---

**Valores fixos para workitem_update:**

| Campo | Valor |
|---|---|
| `agent_name` | `Claude Code` |
| `model_used` | `Claude Sonnet 4.6` |
| `ide_used` | `VS Code` |
| `branch` | branch de trabalho atual |
| `tokens_used` | estimativa int da sessão |
| `feedback` | resumo do que foi feito |
