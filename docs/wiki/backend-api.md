# Backend API (.NET 10)

## Papel

O backend é a **fonte da verdade** para projetos, backlog, sprints, work items, reviews, wiki, checkpoints, logs de execução de agentes e todos os módulos V3 (Box Architecture). Expõe OpenAPI (Swagger) em desenvolvimento e aplica migrações EF Core no startup (exceto ambiente de testes).

## Stack

- **.NET 10** (ASP.NET Core, Minimal APIs)
- **PostgreSQL** via Npgsql + Entity Framework Core
- **Serviços principais:**
  - `ScrumService` — domínio Scrum (projetos, backlog, sprints, work items)
  - `ApiKeyService` — RBAC e autenticação de boxes
  - `MetricsEventService` — métricas em tempo real
  - `SandboxService` — gestão de containers Docker isolados por box
  - `DlqService` — Dead Letter Queue com retry/quarantine
  - `CircuitBreakerService` — FSM de resiliência por box
  - `QueueService` — fila de tasks Tansu com publish/lock/ACK/NACK
  - `OpenClawService` — gateway de mensagens multi-canal
  - `PromptCacheService` — cache de segmentos de prompt por box
  - `McpWhiteLabelService` — lifecycle de instâncias MCP per-box
  - `ContextBoxService` — pipeline RAG (ingestão, chunking, embedding, busca)

## Domínio (resumo)

Entidades centrais incluem: `Project`, `BacklogItem` (tags, wiki refs, constraints), `Sprint`, `WorkItem` (sub-tasks recursivas, branch, feedback, tokens), `Review`, `WikiPage`, `KnowledgeCheckpoint`, `AgentRunLog`, `Box`, `Sandbox`, `DlqEntry`, `CircuitBreakerSnapshot`, `TaskQueueEntry`, `OpenClawUser`, `PromptCacheSegment`, `McpWhiteLabelInstance`.

## Endpoints representativos

- Saúde: `GET /health`
- Projetos e config: `GET/POST/PATCH/DELETE /api/projects`, `PATCH /api/projects/{id}/config`
- Backlog e contexto: `GET/POST` backlog, `PATCH /api/backlog-items/{id}/context`
- Sprints e work items: criação de sprint, status, sub-tasks, reviews
- Conhecimento: wiki, checkpoints, agent runs
- **V3 — Box Modules:**
  - Sandboxes: `POST /api/sandboxes`, `POST /api/sandboxes/{id}/exec`, métricas, workspace
  - DLQ: `GET /api/dlq`, retry, quarantine, drain, stats
  - Circuit Breaker: `GET /api/circuit-breaker/{boxId}`, config, reset, history
  - Queue: `POST /api/queues/publish`, lock/ACK/NACK, dashboard
  - OpenClaw: register user, list users, send message, channel status
  - Prompt Cache: configure, warm, clear, stats, get prefix
  - Context-Box RAG: ingest, query, list, delete, batch ingest
  - MCP White Label: spawn, stop, status, registry

A lista completa está no [README principal](../../README.md) na secção REST API.

## Integrações

- **CORS:** origens configuráveis (`Cors:AllowedOrigins` / `FRONTEND_ORIGINS`), padrão inclui o frontend em `8400`.
- **API keys:** serviço singleton para RBAC quando aplicável (ver código e testes).
- **Context Menu / Browser Extension:** comunicam via MCP server (JSON-RPC)

## Testes

Projeto `AgenticTodoList.Api.Tests`: testes de integração contra PostgreSQL real (sem mocks de persistência no fluxo principal). Comando: `dotnet test` na solução.

## Onde aprofundar

- `Program.cs` — mapeamento de rotas e DI
- `Domain/` e `Data/AppDbContext.cs` — modelo
- `Services/` — regras de negócio e V3 modules
