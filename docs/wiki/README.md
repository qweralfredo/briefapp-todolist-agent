# Wikis Briefapp — Briefapp Todo List V3

Este diretório contém **páginas de wiki** alinhadas ao hub de conhecimento do Briefapp (wiki por projeto, decisões técnicas e onboarding de agentes). Use o conteúdo como base para criar ou sincronizar páginas via MCP (`wiki_add`) no projeto correspondente no Briefapp.

## Índice das páginas

| Página | Tópico |
|--------|--------|
| [visao-geral-sistemas.md](visao-geral-sistemas.md) | Mapa dos sistemas, portas e dependências (V3 Box Architecture) |
| [backend-api.md](backend-api.md) | API .NET 10, domínio, serviços e V3 modules |
| [frontend-dashboard.md](frontend-dashboard.md) | React, rotas e UX operacional |
| [mcp-python.md](mcp-python.md) | Servidor MCP FastMCP, ferramentas e recursos |
| [code-agent.md](code-agent.md) | Agente de código e V3 Sandbox system |
| [dados-persistencia.md](dados-persistencia.md) | PostgreSQL, LanceDB, Tansu e modelo mental de dados |
| [integracao-agentes.md](integracao-agentes.md) | Fluxo humano+IA, context-first e rastreabilidade |

## Documentação relacionada no repositório

- [../ARCHITECTURE.md](../ARCHITECTURE.md) — arquitetura e entidades (V3)
- [../GOVERNANCE.md](../GOVERNANCE.md) — governança e processos
- [../mcps/briefapp-mcp.md](../mcps/briefapp-mcp.md) — referência MCP (V3 tools)
- [../../README.md](../../README.md) — visão geral e quick start

## Sugestão de títulos no Briefapp

Ao criar wikis no Briefapp, use títulos curtos e estáveis, por exemplo:

- `Sistemas — visão geral (V3)`
- `Backend API (.NET 10)`
- `Frontend dashboard`
- `MCP Python`
- `Code Agent & Sandboxes`
- `Dados e persistência`
- `Integração com agentes`

Referencie essas páginas em `backlog_context_update` (`wikiRefs`) quando o backlog tratar do respectivo sistema.
