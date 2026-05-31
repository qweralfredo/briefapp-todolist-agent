# briefapp-done — Checklist de Conclusão de Task

Execute este checklist **antes** de marcar qualquer task como concluída.

## Checklist Obrigatório (por task)

- [ ] Teste escrito **antes** da implementação (TDD)
- [ ] RED confirmado — teste falhou primeiro
- [ ] GREEN confirmado — implementação mínima passando
- [ ] REFACTOR feito — código limpo
- [ ] Cobertura ≥ 80% confirmada
- [ ] Validação E2E ou CLI executada
- [ ] Commit realizado com sequência: `test:` → `feat:` → `refactor:` — cada um com bloco `Refs:`
- [ ] Briefapp atualizado: `workitem_update(status="done", feedback="...")`
- [ ] Perguntado ao usuário se é necessário atualizar wiki, docs, checkpoint ou README

## Convenções de Commit

```
test:      testes adicionados ou corrigidos
feat:      implementação adicionada
refactor:  melhorias sem mudança de comportamento
fix:       correção de bug
chore:     configuração, deps, CI
docs:      documentação
```

Sequência obrigatória por task: `test:` → `feat:` → `refactor:`

### Footer de Rastreabilidade — Obrigatório em TODO commit

```
<tipo>: <descrição curta>

Refs: backlog/<backlog-id> | sprint/<sprint-id> | task/<task-id>
Wiki: <wiki-id>            ← incluir apenas se houver wiki page relacionada
Checkpoint: <cp-id>        ← incluir apenas se houver checkpoint relacionado
```

- `backlog/<id>`, `sprint/<id>` e `task/<id>` são **sempre obrigatórios**
- Use os IDs reais retornados pelo Briefapp MCP (não rótulos humanos)
- Se múltiplas tasks: `task/wk-115, task/wk-116`
- Commit sem `Refs:` é considerado inválido

## Atualizar Briefapp (por task)

```
mcp__local__workitem_update(
  work_item_id  = "<id>",
  status        = "done",
  feedback      = "<resumo do que foi implementado>",
  branch        = "<branch utilizada>",
  agent_name    = "Claude Code",
  model_used    = "Claude Sonnet 4.6",
  ide_used      = "VS Code",
  tokens_used   = <estimativa int>
)
```

---

## Finalização de Backlog Item (ao concluir TODAS as tasks)

Quando todas as tasks de um backlog item estiverem concluídas, execute a sequência §13 completa:

### 1. Documentar e Ajustar Contextos

```
mcp__local__backlog_context_update(
  backlog_item_id,
  tags        = [...],   # tags finais refletindo o estado entregue
  wikiRefs    = [...],   # wiki pages criadas ou atualizadas
  constraints = "..."    # constraints descobertas durante a implementação
)
```

```
mcp__local__wiki_add(...)   # para decisões arquiteturais e padrões relevantes
```

```
mcp__local__knowledge_checkpoint(
  project_id, name,
  context_snapshot = "...",  # estado atual do código e integrações
  decisions        = "...",  # decisões técnicas e justificativas
  risks            = "...",  # riscos identificados ou mitigados
  next_actions     = "..."   # próximos passos para backlogs futuros
)
```

### 2. Verificar Repositório Remoto

```bash
git remote -v   # verificar se remote existe
gh auth status  # verificar GitHub CLI
```

Se não existir remote:
```bash
gh repo create <nome> --private --source=. --remote=origin --push
# Após criar: registrar gitHubUrl no Briefapp via project_config_update
```

### 3. Commit de Fechamento do Backlog

```
feat: complete backlog/<backlog-id> — <título do backlog>

<resumo do que foi implementado>
<decisões técnicas relevantes>
Cobertura: ≥80%

Refs: backlog/<id> | sprint/<id> | task/<id-1>, task/<id-2>, ...
Wiki: <wiki-id-1>, <wiki-id-2>
Checkpoint: <checkpoint-id>
```

Após o commit:
```bash
git push
```

### 4. Atribuir Commit às Entidades Briefapp

```
mcp__local__workitem_update(
  work_item_id  = "<task-id>",
  status        = "done",
  branch        = "<branch-name>",
  feedback      = "Commit <hash> — <titulo> | Sprint: <sprint-name>",
  agent_name    = "Claude Code",
  model_used    = "Claude Sonnet 4.6",
  ide_used      = "VS Code",
  tokens_used   = <estimativa>
)
```

Repetir para **cada task** do backlog finalizado.

### 5. Checklist de Finalização de Backlog

- [ ] Todos os work items do backlog estão `done` no Briefapp
- [ ] `backlog_context_update` executado com tags, wikiRefs e constraints finais
- [ ] `wiki_add` para decisões técnicas e padrões relevantes
- [ ] `knowledge_checkpoint` salvo com context_snapshot, decisions, risks e next_actions
- [ ] Repositório remoto verificado — criado via `gh repo create` se não existia
- [ ] Commit de fechamento com bloco `Refs:` completo
- [ ] `git push` executado com sucesso
- [ ] `workitem_update` em todas as tasks com `branch`, `feedback` e hash do commit
- [ ] Se último backlog do sprint: `knowledge_checkpoint` de sprint e atualização do sprint

---

## Regras Inegociáveis

1. Nunca marque done sem o commit correspondente com `Refs:`
2. Cobertura abaixo de 80% = task **não** está concluída
3. Nunca inicie a próxima task sem commitar a atual
4. Sem fallback/mock em runtime — integrações reais são obrigatórias
5. Ao concluir backlog completo → executar sequência §13 acima antes de iniciar o próximo
