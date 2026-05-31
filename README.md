# Briefapp Todo List — Agentic Task System

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![MCP Python Server](https://img.shields.io/badge/MCP-Python%20FastMCP-3776AB?style=for-the-badge&logo=python&logoColor=white)](mcp-server-python/README.md)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](backend/AgenticTodoList.Api)
[![React](https://img.shields.io/badge/React-19-61DAFB?style=for-the-badge&logo=react&logoColor=black)](frontend)

An open-source, full-stack **Agentic Task System** designed for **human + AI collaboration**. Built around the [Model Context Protocol (MCP)](https://modelcontextprotocol.io), it integrates natively with agentic environments such as VS Code Copilot, enabling AI agents to create sprints, manage backlogs, and track work items — all in real time.

- **Backend:** .NET 10 Web API with PostgreSQL (EF Core)
- **Frontend:** React 19 + TypeScript (Vite + MUI)
- **Agentic protocol:** Python MCP server (official FastMCP SDK) for integration with agentic apps and VS Code
- **Methodology:** Full Scrum structure — projects, backlog, sprints, tasks, reviews
- **Knowledge hub:** wiki pages, context checkpoints, agentic run history
- **Operations:** Docker Compose with local disk persistence and backup scripts

---

## Quick Start

### Install MCP in VS Code (one command — Windows)

```powershell
powershell -ExecutionPolicy Bypass -File .\ops\scripts\install-briefapp-mcp-vscode.ps1
```

To automatically open the VS Code install deep link:

```powershell
powershell -ExecutionPolicy Bypass -File .\ops\scripts\install-briefapp-mcp-vscode.ps1 -OpenInstallLink
```

### Run with Docker

```bash
docker compose up -d --build
```

Host ports:

| Service    | Port  |
|------------|-------|
| Frontend   | 8400  |
| API        | 8480  |
| MCP Server | 8481  |
| PostgreSQL | 8432  |

### Run without Docker

1. Start a local PostgreSQL instance on port `5432`:
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

## Environment Setup & API Keys Configuration

Briefapp operates in two distinct modes configured via environment variables: **Development Mode** (enables fast, bypassed local testing) and **Production Mode** (enables full role-based access control and security).

### 1. Development Mode (Bypassed Authentication)
In Development Mode, the login screen is bypassed, no credentials or tokens are required, and the API key checks for the backend, frontend, and Python MCP server are deactivated.

To activate Development Mode:
1. In the root directory, create a `.env` file containing:
   ```env
   MODE=dev
   VITE_MODE=dev
   GEMINI_API_KEY=your-optional-gemini-key
   ```
2. Run the stack with:
   ```bash
   docker compose up -d --build
   ```
3. Open `http://localhost:8400` in your browser. You will be automatically logged in as a Developer, and all systems (including the planner and MCP) will connect without requiring a master API key.

---

### 2. Production Mode (Secure Authentication)
In Production Mode, all actions are protected by Firebase Google Auth on the frontend, and API key token validation (`pbx_...` prefix) on the REST API and Python MCP server.

#### Step 1: Configure `.env` variables
Copy the `.env.release.example` file to `.env` in the root folder:
```bash
cp .env.release.example .env
```
Open `.env` and configure the following parameters:
- **`GEMINI_API_KEY`**: Your Google Gemini API token, required by the Agent Planner to suggest plans and structure backlog decomposition.
- **`PANDORA_API_KEY`**: Set a secure random key beginning with `pbx_` (e.g. `pbx_yourrandomalphanumerickey`). This is the master token used to authenticate calls between the MCP proxy server and the backend API.
- **`DB_PASSWORD` / `REDIS_PASSWORD` / `MINIO_PASSWORD`**: Set secure passwords for the Postgres database, Redis cache, and Minio object store.

#### Step 2: Configure `.gemini/settings.json` for AI Agents
To allow AI agents (such as VS Code Copilot, Gemini CLI, or custom subagents) to interact with your workspace, you must register the Briefapp MCP server.

Create a `.gemini/settings.json` file in your workspace root (e.g., `C:/projetos/briefapp/.gemini/settings.json`) with the following JSON content:

```json
{
  "mcpServers": {
    "briefapp-todo-list-mcp": {
      "command": "node",
      "args": ["C:/projetos/briefapp/briefapp-todolist/install/proxy/pandora-mcp-proxy.mjs"],
      "env": {
        "BRIEFAPP_API_KEY": "pbx_your-secure-production-api-key",
        "MCP_ENDPOINT": "http://localhost:8481/mcp"
      }
    }
  }
}
```

> [!IMPORTANT]
> - Ensure the path in `args` points exactly to the location of the `pandora-mcp-proxy.mjs` script on your disk.
> - The `BRIEFAPP_API_KEY` value in `env` MUST match the `PANDORA_API_KEY` you defined in your root `.env` file.

#### Step 3: Run the Stack
Run docker compose to start the database, backend api, context RAG engine, and frontend server:
```bash
docker compose up -d --build
```

---

### 3. Security Best Practices
- **Never commit `.env` or `.gemini/settings.json`**: These files are included in the [.gitignore](.gitignore) file and contain sensitive private keys.
- **Rotate keys regularly**: If keys are accidentally pushed to a public remote repository, rotate the `PANDORA_API_KEY` in `.env` and `BRIEFAPP_API_KEY` in your settings files immediately.

---

## Architecture

```
backend/AgenticTodoList.Api/         # .NET 10 REST API — domain, services, EF Core
backend/AgenticTodoList.Api.Tests/   # xUnit integration tests (no mocks, real PostgreSQL)
frontend/src/                        # React 19 + TypeScript dashboard
mcp-server-python/server.py          # Python MCP server (FastMCP) — proxy over the REST API
ops/postgres/data/                   # PostgreSQL data persisted on the host
ops/postgres/backups/                # Backup files generated on the host
ops/scripts/                         # PowerShell backup/restore scripts
docker-compose.yml                   # Full stack definition
```

---

## Features

### Scrum Management
- Full CRUD for projects
- Per-project backlog with priorities and story points
- Sprint creation with selected backlog items
- Automatic conversion of backlog items to work items on sprint start
- Work item status updates
- Sprint reviews

### Advanced Task Management (NEW)
- **Recursive Sub-Tasks** — unlimited task hierarchy with parent auto-completion when all children are Done
- **Branch Tracking** — associate git branches with individual work items for traceability
- **Commit IDs Tracking** — backlog items, sprints, and work items can store multiple commit IDs (`commitIds`)
- **Context-First Backlog Enrichment** — annotate backlog items with tags, wiki references, and constraints
- Sub-task visibility and status badges on kanban board
- Parent-child relationship preservation across sprints

### Frontend UX Improvements
- **Responsive App Shell** — main layout now adapts for mobile/tablet/desktop
- On small screens, the sidebar switches to a temporary drawer with a menu button in the top bar
- On desktop, the sidebar supports collapsed mode (retratil)
- Header controls (active project selector and new project action) stack safely on narrow widths
- Main content area recalculates spacing and offsets per breakpoint to avoid overlap, clipping, and side gaps
- **Sprints Race Mode** — `/sprints` now includes a `Race (all cards live)` view that lists cards from all project sprints together, with live refresh, assignee/priority filters, sprint/backlog context chips, activity date+time, and descending ordering by latest activity
- **Task Edit Modal (current data)** — modal pre-fills current work item and latest feedback data (agent/model/ide/tokens/feedback/metadata/branch/commitIds)

### Briefapp Knowledge Hub
- Per-project wiki pages
- Knowledge checkpoints (context snapshot, decisions, risks, next actions)
- Agentic run log with token tracking
- Operational metrics dashboard

---

## MCP Server — Agentic Integration

The MCP server runs in Python using the official FastMCP SDK, exposed over HTTP via Docker Compose at `http://127.0.0.1:8481/mcp`.

### Local setup (without Docker)

```bash
cd mcp-server-python
python -m venv .venv
.venv/Scripts/activate   # Windows
pip install -r requirements.txt
python server.py
```

### Available Tools

| Tool | Description |
|---|---|
| `project_list` | List all projects |
| `project_create` | Create a new project |
| `project_delete` | Soft-delete a project |
| `backlog_add` | Add a backlog item |
| `backlog_list` | List backlog items |
| `backlog_context_update` | **NEW:** Update backlog item context (tags, wiki refs, constraints) |
| `sprint_create` | Create a sprint |
| `workitem_list` | List work items |
| `workitem_update` | Update work item status |
| `workitem_add_subtask` | **NEW:** Create a sub-task (recursive) |
| `knowledge_checkpoint` | Save a knowledge checkpoint |
| `wiki_add` | Add wiki page |
| `wiki_list` | List wiki pages |
| `documentation_add` | Add documentation page |
| `documentation_list` | List documentation pages |

### Available Prompts

- `briefapp_project_create` — guided project creation
- `briefapp_sprint_create` — guided sprint creation
- `briefapp_resources_guide` — full UI and MCP/API resource map
- `briefapp_context_first_execute` — **NEW:** 5-step context-first execution flow for agents

### MCP Resources (read-only context for agents)

**Direct:**
- `briefapp://about`
- `briefapp://projects/active`
- `briefapp://projects/all`

**Templates:**
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

## REST API Reference

| Method | Endpoint |
|--------|----------|
| GET | `/health` |
| GET | `/api/projects` |
| POST | `/api/projects` |
| DELETE | `/api/projects/{projectId}` |
| PATCH | `/api/projects/{projectId}/config` |
| GET | `/api/projects/{projectId}/dashboard` |
| GET | `/api/projects/{projectId}/backlog` |
| POST | `/api/projects/{projectId}/backlog` |
| PATCH | `/api/backlog-items/{backlogItemId}/context` | **NEW:** Update tags, wiki refs, constraints |
| GET | `/api/projects/{projectId}/sprints` |
| POST | `/api/projects/{projectId}/sprints` |
| PATCH | `/api/sprints/{sprintId}/commits` | **NEW:** Append sprint commit IDs |
| POST | `/api/work-items/{workItemId}/status` |
| POST | `/api/work-items/{workItemId}/sub-tasks` | **NEW:** Create sub-task |
| POST | `/api/sprints/{sprintId}/reviews` |
| GET | `/api/projects/{projectId}/knowledge` |
| POST | `/api/projects/{projectId}/wiki` |
| POST | `/api/projects/{projectId}/checkpoints` |
| POST | `/api/projects/{projectId}/agent-runs` |

---

## Testing

```bash
dotnet test AgenticTodoList.slnx
dotnet test AgenticTodoList.slnx --collect:"XPlat Code Coverage"
```

- Tests passing: **24/24**
- Line coverage: **97.66%**

> All tests run against a real PostgreSQL instance — no mocks, no in-memory fakes.

---

## Backup & Restore

With the stack running:

```powershell
# Backup
powershell -ExecutionPolicy Bypass -File .\ops\scripts\backup-postgres.ps1

# Restore
powershell -ExecutionPolicy Bypass -File .\ops\scripts\restore-postgres.ps1 -FilePath .\ops\postgres\backups\<filename>.sql
```

> On Windows, Docker Desktop must be running for the compose stack to connect to the engine.

---

## Contributing

Contributions are welcome! Please open an issue or submit a pull request. For major changes, open an issue first to discuss what you would like to change.

1. Fork the repository
2. Create a feature branch (`git checkout -b feat/your-feature`)
3. Commit your changes (`git commit -m 'feat: add your feature'`)
4. Push to the branch (`git push origin feat/your-feature`)
5. Open a Pull Request

---

## License

This project is licensed under the **Apache License 2.0**. See the [LICENSE](LICENSE) file for details.

```
Copyright 2026 Briefapp Todo List Contributors

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```

---

Made by [Alfredo Rosa](https://www.linkedin.com/in/alfredo-rosa/)

