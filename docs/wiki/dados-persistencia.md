# Dados e persistência — V3

## PostgreSQL (núcleo)

- **Base:** `briefapp_todo_list`
- **Utilizador/senha** no compose: configurados em `docker-compose.yml` (ambiente local; mudar em produção).
- **Volume:** `./ops/postgres/data` — dados duráveis no host.

## Migrações

A API aplica migrações EF Core automaticamente em ambientes não-`Testing`. O modelo inclui tabelas para Scrum, conhecimento, feedback de work items, métricas e módulos V3 (ver `Migrations/`).

## Backups

- **Container `postgres-backup`:** `pg_dump` periódico para `./ops/postgres/backups`, com retenção por dias (`BACKUP_KEEP_DAYS`).
- **Scripts PowerShell:** `ops/scripts/backup-postgres.ps1`, `restore-postgres.ps1`.

Documentação: [../BACKUP-RESTORE.md](../BACKUP-RESTORE.md).

## LanceDB (V3 — Context-Box RAG)

- **Tipo:** vector store embeddado para busca semântica
- **Uso:** armazena embeddings dos chunks gerados pelo pipeline RAG (extract → split → embed)
- **Acesso:** via `context_box_query` no MCP ou API REST `/api/context-box/query`

## Tansu (V3 — Event Queue)

- **Tipo:** fila de eventos por box (topic: `briefapp.{boxId}.tasks`)
- **Uso:** publish/subscribe de tasks assíncronas com lock distribuído e retry
- **DLQ:** entradas que falham N vezes são encaminhadas para Dead Letter Queue
- **Persistência:** fila em PostgreSQL com status tracking

## Modelo mental para agentes

- **Projeto** agrupa backlog, sprints e artefatos de conhecimento.
- **Box** (V3) é a unidade de isolamento: cada box tem seu próprio RAG, sandbox, queue, circuit breaker e prompt cache.
- **Work items** podem formar árvores (sub-tasks) e carregar metadados de agente (tokens, branch, commits).
- **Wiki e checkpoints** são dados de primeira classe — devem ser atualizados nos marcos do processo.
- **Context-Box RAG** fornece busca semântica sobre documentos ingeridos, complementando wiki/checkpoints.
- **Sandboxes** são efêmeros — criados para execução e destruídos após uso.
