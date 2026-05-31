# Briefapp Todo List - Contexto da Sprint

> [!NOTE]
> Este arquivo `GEMINI.md` é atualizado dinamicamente pelo backend do Briefapp sempre que uma Sprint inicia ou tarefas mudam de status.
> O Gemini CLI lê este arquivo para obter contexto do que precisa ser feito em cada projeto.

## Active Sprint
**Sprint ID:** Sprint 4 - Integração de Agentes
**Status:** In Progress
**Goal:** Implementar as integrações do Gemini CLI (Checkpointing, Telemetria, Headless).

## Tarefas Pendentes (Subagents Tasks)
- **#101**: `[frontend]` Criar Dashboard de Telemetria no React para consumir dados do Jaeger OTLP.
- **#102**: `[backend]` Expor endpoint `/api/agent-runs` para receber notificações do webhook do Gemini CLI.
- **#103**: `[devops]` Configurar `docker-compose.yml` final com a stack completa incluindo MinIO e Postgres e Jaeger.

## Guidelines para o Gemini CLI
- Use `gemini headless` para a tarefa de CI/CD.
- Para editar UI, delegue para `@frontend`.
- Todo commit deve conter a tag da tarefa, ex: `feat(#101): adicionado painel`.
- Cuidado: Não delete o banco de dados de produção (Porta 5432).

## Conhecimento (Knowledge Base)
- Backend: C# .NET 10, Entity Framework
- Frontend: React 19, TypeScript, Vite
- MCP Server: Python FastMCP
