# Integração com agentes — V3

## Objetivo

Permitir que **agentes de IA** (via MCP) e **humanos** (via UI) partilhem o mesmo estado: backlog, sprints, tarefas, wiki, checkpoints e Context-Box RAG, com **rastreabilidade** para git e decisões.

## Fluxo recomendado (resumo)

1. **Discovery:** `project_list`, dashboard, work items ativos.
2. **Warm-up:** ler wikis e checkpoints do projeto (recursos MCP ou UI).
3. **Context injection:** enriquecer backlog com `tags`, `wikiRefs`, `constraints` (`backlog_context_update`). Opcionalmente, alimentar Context-Box RAG com `context_box_ingest`.
4. **Execução:** `workitem_update` com status, branch, feedback e tokens; sub-tasks para trabalho grande. Usar `sandbox_create` + `sandbox_exec` para execução isolada de código.
5. **Validação:** sub-tasks concluídas, parent atualizado; documentar no wiki e `knowledge_checkpoint` em marcos.

## Context-Box RAG (V3)

Cada box/projeto pode ter uma base de conhecimento vetorial:

- **Ingestão:** `context_box_ingest(file_path)` — extract → split → embed → LanceDB
- **Busca:** `context_box_query(query, limit)` — retorna chunks relevantes com score
- **Batch:** `context_box_ingest_batch(file_paths)` — ingestão assíncrona em lote
- **Extensões:** context menu (Windows/macOS) permite enviar arquivos diretamente do Explorer/Finder

## Sandboxes (V3)

Containers Docker isolados para execução segura:

- `sandbox_create(box_id, image, cpu, mem, timeout, network_mode)`
- `sandbox_exec(sandbox_id, command)` — executa comandos dentro do container
- `sandbox_workspace_prepare(sandbox_id, git_repo_url)` — clona repo com OverlayFS
- Políticas de rede: `Offline`, `Restricted`, `Full`

## Task Queue (V3)

Fila de tarefas assíncrona por box:

- `task_publish` → `task_lock` → `task_ack` / `task_nack`
- DLQ automática após N falhas
- Circuit breaker por box para resiliência

## Rastreabilidade

- Commits devem referenciar IDs Briefapp quando a equipa adota essa política (ver skill Briefapp / README).
- Work items suportam **branch** e **commit IDs** para ligar código a tarefas.

## Atomic-Agent Flow

Planeamento hierárquico opcional (complexidade C) para quebrar épicos em tarefas atômicas — ver `docs/skills/briefapp-atomic-flow.md` e skills por IDE em `docs/skills/`.

## Ferramentas auxiliares no repo

- Playwright MCP (documentação em `docs/mcps/playwright-mcp.md`) para E2E quando há UI web.
- Scripts em `ops/scripts` para MCP VS Code, validação pós-deploy, importação de histórico git.
- **Instaladores cross-platform** em `installers/` — registro de protocolo `briefapp://`, context menus e extensões.

## IDEs suportadas

| IDE | Skill Path | Config MCP |
|-----|-----------|------------|
| VS Code (Copilot) | `docs/skills/claude/` | `.vscode/mcp.json` |
| Antigravity (Gemini) | `docs/skills/antigravity/` | `~/.gemini/antigravity/skills/` |
| Cursor | `docs/skills/cursor/` | `.cursor/mcp.json` |
| Windsurf | `docs/skills/windsurf/` | `.windsurf/mcp.json` |

## Governança

Para regras de processo e qualidade, ver [../GOVERNANCE.md](../GOVERNANCE.md).
