# MCP Python (FastMCP) — V3

## Papel

O servidor em `mcp-server-python` implementa o **Model Context Protocol** sobre HTTP (streamable), expondo **ferramentas** que chamam a API REST interna (`PANDORA_API_BASE_URL` no Docker: `http://api:8080`).

## Transporte e URL

- Compose mapeia **8481 → 8000** no container MCP.
- VS Code / Cursor / Windsurf: configurar servidor MCP HTTP para `http://127.0.0.1:8481/mcp`.

## Ferramentas — Scrum Core

| Tool | Descrição |
|------|-----------|
| `project_list` / `project_create` / `project_delete` | Gestão de projetos |
| `project_config_update` | Config ambiente (GitHub URL, path, stack, branch) |
| `backlog_add` / `backlog_list` | Backlog items |
| `backlog_context_update` | Tags, wikiRefs, constraints |
| `sprint_create` | Criar sprint de backlog items |
| `workitem_list` / `workitem_update` | Work items com status, branch, tokens |
| `workitem_add_subtask` | Sub-tasks recursivas |
| `wiki_add` / `wiki_list` | Wiki pages |
| `documentation_add` / `documentation_list` | Docs formais |
| `knowledge_checkpoint` / `knowledge_list` / `checkpoint_list` | Knowledge checkpoints |
| `get_modification_impact` | Acoplamento temporal de arquivos |
| `search` | Busca global cross-entity |

## Ferramentas — V3 Box Modules

| Grupo | Tools |
|-------|-------|
| **Context-Box RAG** | `context_box_ingest`, `context_box_ingest_batch`, `context_box_query`, `context_box_list`, `context_box_delete`, `context_box_batch_status`, `context_box_batch_stats` |
| **Sandbox** | `sandbox_create`, `sandbox_start`, `sandbox_stop`, `sandbox_destroy`, `sandbox_exec`, `sandbox_status`, `sandbox_list`, `sandbox_stats`, `sandbox_metrics`, `sandbox_network_info`, `sandbox_workspace_prepare`, `sandbox_workspace_cleanup` |
| **Task Queue** | `task_publish`, `task_lock`, `task_unlock`, `task_heartbeat`, `task_ack`, `task_nack`, `lock_status`, `queue_status`, `queue_dashboard` |
| **DLQ** | `dlq_list`, `dlq_stats`, `dlq_retry`, `dlq_quarantine`, `dlq_drain` |
| **Circuit Breaker** | `circuit_breaker_status`, `circuit_breaker_all`, `circuit_breaker_config`, `circuit_breaker_reset`, `circuit_breaker_history` |
| **OpenClaw** | `openclaw_register_user`, `openclaw_list_users`, `openclaw_inbound_stats`, `channel_send`, `channel_status` |
| **Prompt Cache** | `prompt_cache_stats`, `prompt_cache_warm`, `prompt_cache_clear`, `prompt_cache_configure`, `prompt_cache_get_prefix` |
| **MCP White Label** | `mcp_wl_spawn`, `mcp_wl_stop`, `mcp_wl_status`, `mcp_wl_registry`, `mcp_wl_registry_stats` |

## Recursos (read-only)

URIs `briefapp://...` fornecem contexto para agentes:

| URI | Descrição |
|-----|-----------|
| `briefapp://about` | Mapa completo do servidor |
| `briefapp://projects/active` | Projetos ativos |
| `briefapp://projects/all` | Todos os projetos |
| `briefapp://projects/{id}/context` | Contexto completo (config + dashboard + knowledge) |
| `briefapp://projects/{id}/config` | Configuração do projeto |
| `briefapp://projects/{id}/dashboard` | Métricas e sprints ativos |
| `briefapp://projects/{id}/backlog` | Backlog items |
| `briefapp://projects/{id}/sprints` | Sprints |
| `briefapp://projects/{id}/workitems` | Work items |
| `briefapp://projects/{id}/workitems/status/{status}` | Work items filtrados |
| `briefapp://projects/{id}/sprints/{sprint_id}/workitems` | Work items de um sprint |
| `briefapp://projects/{id}/tasks/overview` | Task overview |
| `briefapp://projects/{id}/tasks/triage` | Tasks para triagem |
| `briefapp://projects/{id}/knowledge` | Wiki + docs + checkpoints |
| `briefapp://boxes/{id}/context-rag` | Estado RAG do box |

## Boas práticas

- Obter `project_id` via `project_list` antes de criar itens.
- Evitar duplicatas: conferir `backlog_list` / `workitem_list`.
- Após mudanças relevantes, atualizar wiki/checkpoint conforme metodologia do projeto.
- Usar `context_box_ingest` para alimentar a base RAG do box com arquivos de contexto.
- Usar `sandbox_create` + `sandbox_exec` para executar código em ambiente isolado.
