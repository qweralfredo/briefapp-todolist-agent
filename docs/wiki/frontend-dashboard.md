# Frontend dashboard

## Papel

Interface web em **React** + **TypeScript** (Vite), consumindo a API REST. Oferece seleção de projeto, backlog, sprints (incluindo modos como race view), kanban, conhecimento (wiki/checkpoints), insights de tokens e fluxos de edição alinhados ao domínio (ex.: modal de task com feedback e metadados de agente).

## Configuração

Em Docker, a build recebe `VITE_API_BASE_URL` apontando para a API pública (ex.: `http://localhost:8480`). Em desenvolvimento local, usar `npm run dev` com variáveis equivalentes.

## Contexto de projeto

O estado do projeto ativo é centralizado (ex.: `useProjectContext`, tipos em `types.ts`), permitindo que páginas filtrem dados por `projectId`.

## Páginas e áreas (não exaustivo)

- Navegação principal e shell responsivo (sidebar / drawer em mobile)
- Sprints e visualizações de cards (filtros, chips de contexto, atualização em tempo real onde implementado)
- Knowledge / wiki no sentido de UI do hub de conhecimento
- Token insights e métricas expostas pela API

## Relação com o MCP

O frontend é para **humanos**; agentes usam o **servidor MCP** para as mesmas operações de forma programática. Manter paridade de conceitos (status de work item, wiki refs) entre UI e MCP evita divergência de processo.
