# Briefapp Todo List — Sistema de Tarefas Orientado a Agentes

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![MCP Python Server](https://img.shields.io/badge/MCP-Python%20FastMCP-3776AB?style=for-the-badge&logo=python&logoColor=white)](mcp-server-python/README.md)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](backend/AgenticTodoList.Api)
[![React](https://img.shields.io/badge/React-19-61DAFB?style=for-the-badge&logo=react&logoColor=black)](frontend)

> **brief.app — Design com inteligência**
> 
> Uma plataforma de design com IA idealizada por **Ana Rovina** e **Alfredo Rosa**. 
> Focada em entregar **seu produto digital com excelência e resultado real**, projetada para founders e empreendedores que têm a ideia mas precisam do produto com rapidez e segurança, sem meses de desenvolvimento ou custos imprevisíveis.
> 

> - **Website:** [brief.app.br](https://brief.app.br/)

Um **Sistema de Tarefas Orientado a Agentes** de código aberto e full-stack, desenvolvido para a **colaboração entre humanos e IA**. Construído em torno do [Model Context Protocol (MCP)](https://modelcontextprotocol.io), integra-se nativamente a ambientes de agentes como o VS Code Copilot, permitindo que agentes de IA criem sprints, gerenciem o backlog e acompanhem itens de trabalho — tudo em tempo real.

- **Backend:** .NET 10 Web API com PostgreSQL (EF Core)
- **Frontend:** React 19 + TypeScript (Vite + MUI)
- **Protocolo de agentes:** Servidor MCP em Python (FastMCP SDK oficial) para integração com aplicativos de IA e VS Code
- **Metodologia:** Estrutura Scrum completa — projetos, backlog, sprints, tarefas e revisões
- **Hub de Conhecimento:** páginas de wiki, checkpoints de contexto e histórico de execução de agentes
- **Operações:** Docker Compose com persistência em disco local e scripts de backup

---

## Início Rápido

### Instalar o MCP no VS Code (um comando — Windows)

```powershell
powershell -ExecutionPolicy Bypass -File .\ops\scripts\install-briefapp-mcp-vscode.ps1
```

Para abrir o link de instalação profunda do VS Code automaticamente:

```powershell
powershell -ExecutionPolicy Bypass -File .\ops\scripts\install-briefapp-mcp-vscode.ps1 -OpenInstallLink
```

### Executar com Docker

```bash
docker compose up -d --build
```

Portas do host:

| Serviço    | Porta |
|------------|-------|
| Frontend   | 8400  |
| API        | 8480  |
| Servidor MCP| 8481  |
| PostgreSQL | 8432  |

### Executar sem Docker

1. Inicie uma instância local do PostgreSQL na porta `5432`:
   - database: `briefapp_todo_list`
   - user: `Briefapp`
   - password: `Briefapp`

2. Backend:
   ```bash
   cd backend/AgenticTodoList.Api
   dotnet run
   ```

3. Frontend:
   ```bash
   cd frontend
   npm install
   npm run dev
   ```

---

## Configuração de Ambiente e Chaves de API

O Briefapp opera em dois modos distintos configurados via variáveis de ambiente: **Modo de Desenvolvimento** (permite testes locais rápidos e sem barreiras) e **Modo de Produção** (habilita controle de acesso completo baseado em funções e segurança).

### 1. Modo de Desenvolvimento (Autenticação Ignorada)
No Modo de Desenvolvimento, a tela de login é ignorada, nenhuma credencial ou token é necessário e as validações de chaves de API para o backend, frontend e servidor MCP Python são desativadas.

Para ativar o Modo de Desenvolvimento:
1. No diretório raiz, crie um arquivo `.env` contendo:
   ```env
   MODE=dev
   VITE_MODE=dev
   GEMINI_API_KEY=sua-chave-gemini-opcional
   ```
2. Execute a stack com:
   ```bash
   docker compose up -d --build
   ```
3. Abra `http://localhost:8400` no seu navegador. Você será logado automaticamente como Desenvolvedor e todos os sistemas (incluindo o planejador e o MCP) se conectarão sem a necessidade de uma chave de API mestra.

---

### 2. Modo de Produção (Autenticação Segura)
No Modo de Produção, todas as ações são protegidas pelo Firebase Google Auth no frontend e pela validação de tokens (`pbx_...`) na REST API e no servidor MCP em Python.

#### Passo 1: Configurar variáveis no `.env`
Copie o arquivo `.env.release.example` para `.env` na pasta raiz:
```bash
cp .env.release.example .env
```
Abra o `.env` e configure os seguintes parâmetros:
- **`GEMINI_API_KEY`**: Seu token da API do Google Gemini, necessário pelo Agent Planner para sugerir planos e estruturar a decomposição do backlog.
- **`PANDORA_API_KEY`**: Defina uma chave aleatória segura começando com `pbx_` (ex: `pbx_suachavealeatoriasegura`). Este é o token mestre usado para autenticar chamadas entre o servidor proxy MCP e a API backend.
- **`DB_PASSWORD` / `REDIS_PASSWORD` / `MINIO_PASSWORD`**: Defina senhas seguras para o banco de dados Postgres, cache Redis e armazenamento de objetos Minio.

#### Passo 2: Configurar `.gemini/settings.json` para Agentes de IA
Para permitir que agentes de IA (como VS Code Copilot, Gemini CLI ou subagentes personalizados) interajam com o seu workspace, você deve registrar o servidor MCP do Briefapp.

Crie um arquivo `.gemini/settings.json` na raiz do seu workspace (ex: `./.gemini/settings.json`) com o seguinte conteúdo JSON:

```json
{
  "mcpServers": {
    "briefapp-todo-list-mcp": {
      "command": "node",
      "args": ["./install/proxy/pandora-mcp-proxy.mjs"],
      "env": {
        "BRIEFAPP_API_KEY": "pbx_sua-chave-de-api-segura-de-producao",
        "MCP_ENDPOINT": "http://localhost:8481/mcp"
      }
    }
  }
}
```

> [!IMPORTANT]
> - Certifique-se de que o caminho em `args` aponte exatamente para o local do script `pandora-mcp-proxy.mjs` no seu disco.
> - O valor `BRIEFAPP_API_KEY` em `env` DEVE corresponder à `PANDORA_API_KEY` definida no seu arquivo `.env` da raiz.

#### Passo 3: Executar a Stack
Execute o docker compose para iniciar o banco de dados, API backend, motor de contexto RAG e o servidor frontend:
```bash
docker compose up -d --build
```

---

### 3. Melhores Práticas de Segurança
- **Nunca commite o `.env` ou o `.gemini/settings.json`**: Estes arquivos estão incluídos no [.gitignore](.gitignore) e contêm chaves privadas sensíveis.
- **Rotacione as chaves regularmente**: Se as chaves forem empurradas acidentalmente para um repositório remoto público, rotacione a `PANDORA_API_KEY` no `.env` e a `BRIEFAPP_API_KEY` nos arquivos de configuração imediatamente.

---

## Arquitetura

```
backend/AgenticTodoList.Api/         # .NET 10 REST API — domínio, serviços, EF Core
backend/AgenticTodoList.Api.Tests/   # Testes de integração xUnit (sem mocks, PostgreSQL real)
frontend/src/                        # Dashboard React 19 + TypeScript
mcp-server-python/server.py          # Servidor MCP em Python (FastMCP) — proxy sobre a API REST
ops/postgres/data/                   # Dados do PostgreSQL persistidos no host
ops/postgres/backups/                # Arquivos de backup gerados no host
ops/scripts/                         # Scripts de backup/restauração do PowerShell
docker-compose.yml                   # Definição completa da stack
```

---

## Funcionalidades

### Gestão Scrum
- CRUD completo para projetos
- Backlog por projeto com prioridades e story points
- Criação de sprints com itens selecionados do backlog
- Conversão automática de itens do backlog para itens de trabalho ao iniciar a sprint
- Atualização do status dos itens de trabalho
- Revisões de sprint (Sprint reviews)

### Gestão Avançada de Tarefas (NOVO)
- **Sub-Tarefas Recursivas** — hierarquia ilimitada de tarefas com preenchimento automático do pai quando todos os filhos estão "Concluídos" (Done)
- **Rastreamento de Branches** — associe branches do git a itens de trabalho individuais para rastreabilidade
- **Rastreamento de Commits** — itens de backlog, sprints e itens de trabalho podem armazenar vários IDs de commit (`commitIds`)
- **Context-First Backlog** — enriqueça os itens do backlog com tags, referências wiki e restrições
- Visibilidade de sub-tarefas e emblemas de status no quadro Kanban
- Preservação do relacionamento pai-filho através das sprints

### Melhorias de UX no Frontend
- **Shell de Aplicativo Responsivo** — o layout principal agora se adapta a celular/tablet/desktop
- Em telas pequenas, a barra lateral muda para uma gaveta temporária com um botão de menu na barra superior
- No desktop, a barra lateral suporta modo oculto (retrátil)
- Controles de cabeçalho (seletor de projeto ativo e ação de novo projeto) empilham-se de forma segura em larguras estreitas
- A área de conteúdo principal recalcula o espaçamento e deslocamentos por ponto de quebra para evitar sobreposição, cortes e espaços vazios
- **Modo Race nas Sprints** — `/sprints` agora inclui a visualização `Race (all cards live)` que lista cartões de todas as sprints do projeto juntos, com atualização em tempo real, filtros de responsável/prioridade, chips de contexto da sprint/backlog, data/hora da atividade e ordenação decrescente pela última atividade
- **Modal de Edição de Tarefas** — o modal pré-preenche as informações atuais do item e os dados de feedback mais recentes (agente/modelo/ide/tokens/feedback/metadados/branch/commitIds)

### Hub de Conhecimento Briefapp
- Páginas de wiki por projeto
- Checkpoints de conhecimento (foto do contexto, decisões, riscos, próximos passos)
- Registro de log das execuções do agente com rastreamento de tokens
- Dashboard de métricas operacionais

---

## Servidor MCP — Integração de Agentes

O servidor MCP roda em Python usando o SDK oficial FastMCP, exposto via HTTP pelo Docker Compose em `http://127.0.0.1:8481/mcp`.

### Configuração local (sem Docker)

```bash
cd mcp-server-python
python -m venv .venv
.venv/Scripts/activate   # Windows
pip install -r requirements.txt
python server.py
```

### Ferramentas Disponíveis

| Ferramenta | Descrição |
|---|---|
| `project_list` | Lista todos os projetos |
| `project_create` | Cria um novo projeto |
| `project_delete` | Realiza exclusão lógica de um projeto |
| `backlog_add` | Adiciona um item ao backlog |
| `backlog_list` | Lista os itens do backlog |
| `backlog_context_update` | **NOVO:** Atualiza o contexto do item do backlog (tags, referências wiki, restrições) |
| `sprint_create` | Cria uma sprint |
| `workitem_list` | Lista os itens de trabalho |
| `workitem_update` | Atualiza o status do item de trabalho |
| `workitem_add_subtask` | **NOVO:** Cria uma sub-tarefa (recursiva) |
| `knowledge_checkpoint` | Salva um checkpoint de conhecimento |
| `wiki_add` | Adiciona uma página na wiki |
| `wiki_list` | Lista páginas da wiki |
| `documentation_add` | Adiciona uma página de documentação |
| `documentation_list` | Lista páginas de documentação |

### Prompts Disponíveis

- `briefapp_project_create` — criação guiada de projeto
- `briefapp_sprint_create` — criação guiada de sprint
- `briefapp_resources_guide` — mapa de recursos completo da UI e MCP/API
- `briefapp_context_first_execute` — **NOVO:** fluxo de execução guiada por contexto em 5 passos para agentes

### Recursos MCP (contexto somente leitura para agentes)

**Diretos:**
- `briefapp://about`
- `briefapp://projects/active`
- `briefapp://projects/all`

**Modelos (Templates):**
- `briefapp://projects/{project_id}/context`
- `briefapp://projects/{project_id}/dashboard`
- `briefapp://projects/{project_id}/backlog`
- `briefapp://projects/{project_id}/sprints`
- `briefapp://projects/{project_id}/workitems`
- `briefapp://projects/{project_id}/workitems/status/{status}`
- `briefapp://projects/{project_id}/sprints/{sprint_id}/workitems`
- `briefapp://projects/{project_id}/tasks/overview`
- `briefapp://projects/{project_id}/tasks/triage`
- `briefapp://projects/{project_id}/knowledge`

---

## Referência da API REST

| Método | Endpoint |
|--------|----------|
| GET | `/health` |
| GET | `/api/projects` |
| POST | `/api/projects` |
| DELETE | `/api/projects/{projectId}` |
| PATCH | `/api/projects/{projectId}/config` |
| GET | `/api/projects/{projectId}/dashboard` |
| GET | `/api/projects/{projectId}/backlog` |
| POST | `/api/projects/{projectId}/backlog` |
| PATCH | `/api/backlog-items/{backlogItemId}/context` | **NOVO:** Atualizar tags, wikis, restrições |
| GET | `/api/projects/{projectId}/sprints` |
| POST | `/api/projects/{projectId}/sprints` |
| PATCH | `/api/sprints/{sprintId}/commits` | **NOVO:** Anexar IDs de commits na sprint |
| POST | `/api/work-items/{workItemId}/status` |
| POST | `/api/work-items/{workItemId}/sub-tasks` | **NOVO:** Criar sub-tarefa |
| POST | `/api/sprints/{sprintId}/reviews` |
| GET | `/api/projects/{projectId}/knowledge` |
| POST | `/api/projects/{projectId}/wiki` |
| POST | `/api/projects/{projectId}/checkpoints` |
| POST | `/api/projects/{projectId}/agent-runs` |

---

## Testes

```bash
dotnet test AgenticTodoList.slnx
dotnet test AgenticTodoList.slnx --collect:"XPlat Code Coverage"
```

- Testes passando: **24/24**
- Cobertura de código (Line coverage): **97.66%**

> Todos os testes rodam contra uma instância real do PostgreSQL — sem mocks ou bancos fakes em memória.

---

## Backup e Restauração

Com a stack rodando:

```powershell
# Fazer backup
powershell -ExecutionPolicy Bypass -File .\ops\scripts\backup-postgres.ps1

# Restaurar
powershell -ExecutionPolicy Bypass -File .\ops\scripts\restore-postgres.ps1 -FilePath .\ops\postgres\backups\<nome_do_arquivo>.sql
```

> No Windows, o Docker Desktop precisa estar aberto para que a stack do compose se conecte ao motor.

---

## Como Contribuir

Contribuições são bem-vindas! Por favor, abra uma _issue_ ou envie um _pull request_. Para grandes mudanças, abra uma _issue_ primeiro para discutir o que você gostaria de mudar.

1. Faça um Fork do repositório
2. Crie a branch da sua feature (`git checkout -b feat/sua-feature`)
3. Faça o commit das alterações (`git commit -m 'feat: adicionar nova feature'`)
4. Dê push para a branch (`git push origin feat/sua-feature`)
5. Abra um Pull Request

---

## Licença

Este projeto está licenciado sob a **Apache License 2.0**. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

```
Copyright 2026 Contribuidores do Briefapp Todo List

Licenciado sob a Licença Apache, Versão 2.0 (a "Licença");
você não pode usar este arquivo exceto em conformidade com a Licença.
Você pode obter uma cópia da Licença em

    http://www.apache.org/licenses/LICENSE-2.0

A menos que exigido pela lei aplicável ou acordado por escrito, o software
distribuído sob a Licença é distribuído "COMO ESTÁ",
SEM GARANTIAS OU CONDIÇÕES DE QUALQUER TIPO, sejam expressas ou implícitas.
Consulte a Licença para o idioma específico que rege as permissões e
limitações sob a Licença.
```

---

Feito por [Alfredo Rosa](https://www.linkedin.com/in/alfredo-rosa/)
