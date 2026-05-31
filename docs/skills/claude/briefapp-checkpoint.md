# briefapp-checkpoint — Salvar Knowledge Checkpoint

Use ao concluir uma **epic**, **sprint** ou **backlog item completo**. Nunca por task individual.

## Quando Usar

- Ao concluir um sprint completo
- Ao concluir uma epic (conjunto de stories relacionadas)
- Ao concluir todas as tasks de um backlog item (§13 do fluxo obrigatório)
- Antes de uma pausa longa no projeto
- Após decisões arquiteturais relevantes

## Executar

```
mcp__local__knowledge_checkpoint(
  project_id       = "<project_id>",
  name             = "<nome descritivo do checkpoint>",
  context_snapshot = "<estado atual do projeto — o que foi implementado>",
  decisions        = "<decisões técnicas tomadas e justificativas>",
  risks            = "<riscos identificados e mitigações>",
  next_actions     = "<próximos passos concretos>"
)
```

## Estrutura Recomendada

**context_snapshot:**
> Descreva o que foi implementado neste sprint/epic, tecnologias usadas, integrações realizadas.

**decisions:**
> Liste decisões arquiteturais com justificativa. Ex: "Escolhido DuckDB por zero-copy com Arrow — alternativa SQLite descartada por não suportar columnar."

**risks:**
> Riscos técnicos e de negócio identificados. Ex: "Versão DuckDB 0.x não suporta UPSERT nativo — workaround com INSERT + ON CONFLICT."

**next_actions:**
> Próximos passos concretos com prioridade. Ex: "1. Adicionar testes de integração; 2. Benchmark com 1M rows; 3. Configurar CI."

## Após o Checkpoint

- Pergunte ao usuário se wiki e documentação precisam ser atualizadas
- Use `mcp__local__wiki_add` para decisões arquiteturais relevantes
- Use `mcp__local__documentation_add` para guias de uso e ADRs
- Se último backlog do sprint: atualizar `backlog_context_update` com o `checkpoint_id` gerado em `wikiRefs`
