# MCP: Briefapp Todo List — V3

> **Type:** Model Context Protocol (MCP) Server  
> **Protocol:** HTTP Streamable (FastMCP)  
> **Local URL:** `http://127.0.0.1:8481/mcp`  
> **When to use:** Manage backlog, sprints, tasks, knowledge bases, RAG, sandboxes and queues via AI agent

---

## What It Is

The **Briefapp MCP Server** is a Python MCP server (FastMCP) that exposes the Briefapp Todo List API as tools for AI agents. It enables any MCP-compatible agent (VS Code Copilot, Cursor, Windsurf, Claude Desktop, Antigravity) to:

- Create and manage projects, backlog items, sprints, and work items
- Update task status in real time during development
- Track agentic context (tokens, model, IDE, feedback)
- Maintain knowledge bases: wiki, documentation, checkpoints
- **V3:** Ingest documents into Context-Box RAG for semantic search
- **V3:** Create and manage sandboxes for isolated code execution
- **V3:** Publish tasks to queues with DLQ and circuit breaker

---

## How to Install — Cross-Platform Installer (Recommended)

### Windows

```powershell
# From project root — installs MCP, protocol handler, context menus, extensions
powershell -ExecutionPolicy Bypass -File .\installers\install-briefapp.ps1
```

### macOS

```bash
# From project root
chmod +x ./installers/install-briefapp.sh
sudo ./installers/install-briefapp.sh
```

The installer registers the `briefapp://` protocol, creates `C:\briefapp` (or `/briefapp`), installs context menus, and places the browser extension in the extensions folder.

---

## How to Install MCP — Per IDE

### VS Code (Copilot)

**Option 1: Automated script**
```powershell
powershell -ExecutionPolicy Bypass -File .\ops\scripts\install-briefapp-mcp-vscode.ps1
```

**Option 2: Manual** — Add to `.vscode/mcp.json`:
```json
{
  "servers": {
    "briefapp-todo-list-mcp": {
      "type": "http",
      "url": "http://127.0.0.1:8481/mcp"
    }
  }
}
```

### Cursor

Add to `.cursor/mcp.json`:
```json
{
  "mcpServers": {
    "briefapp-todo-list-mcp": {
      "url": "http://127.0.0.1:8481/mcp"
    }
  }
}
```

Install skills: copy `docs/skills/cursor/*.mdc` to `.cursor/rules/`.

### Windsurf

Add to `.windsurf/mcp.json`:
```json
{
  "mcpServers": {
    "briefapp-todo-list-mcp": {
      "serverUrl": "http://127.0.0.1:8481/mcp"
    }
  }
}
```

Install skills: copy `docs/skills/windsurf/*.md` to `.windsurf/rules/`.

### Claude Desktop

Add to `claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "briefapp-todo-list-mcp": {
      "command": "npx",
      "args": ["-y", "mcp-remote", "http://127.0.0.1:8481/mcp"]
    }
  }
}
```

### Antigravity (Gemini)

Skills are installed globally at `~/.gemini/antigravity/skills/`. Copy from `docs/skills/antigravity/`.

---

## How to Start the Server

```bash
# From project root — starts all services including briefapp-mcp
docker compose up -d

# Check if it's running
docker compose ps briefapp-mcp

# View logs
docker compose logs briefapp-mcp -f

# Health check
curl http://127.0.0.1:8481/health
```

---

## Available Tools — Scrum Core

| Tool | Description |
|---|---|
| `project_list` | List all projects |
| `project_create` | Create a new project |
| `project_config_update` | Update project settings |
| `project_delete` | Archive (soft delete) a project |
| `backlog_list` | List backlog items |
| `backlog_add` | Add item to backlog |
| `backlog_context_update` | Update backlog item context (tags, wiki refs, constraints) |
| `sprint_create` | Create a new sprint |
| `workitem_list` | List work items |
| `workitem_update` | Update work item status/context |
| `workitem_add_subtask` | Create a sub-task (child work item) |
| `wiki_add` / `wiki_list` | Wiki pages |
| `documentation_add` / `documentation_list` | Documentation pages |
| `knowledge_checkpoint` | Create a knowledge checkpoint |
| `knowledge_list` / `checkpoint_list` | List knowledge data |
| `get_modification_impact` | Temporal coupling analysis for a file |
| `search` | Global search across all entities |

## Available Tools — V3 Box Modules

| Group | Tool | Description |
|---|---|---|
| **Context-Box RAG** | `context_box_ingest` | Ingest file into RAG system |
| | `context_box_ingest_batch` | Batch ingest multiple files |
| | `context_box_query` | Semantic search over ingested docs |
| | `context_box_list` | List indexed files |
| | `context_box_delete` | Remove file from index |
| | `context_box_batch_status` | Check batch job status |
| | `context_box_batch_stats` | Pipeline aggregate stats |
| **Sandbox** | `sandbox_create` | Create Docker container (node/python/dotnet) |
| | `sandbox_start` / `sandbox_stop` | Lifecycle control |
| | `sandbox_destroy` | Force-remove container |
| | `sandbox_exec` | Execute shell command in sandbox |
| | `sandbox_status` | Get sandbox metadata |
| | `sandbox_list` | List sandboxes for a box |
| | `sandbox_stats` | Aggregate sandbox statistics |
| | `sandbox_metrics` | Real-time resource metrics |
| | `sandbox_network_info` | Available network policies |
| | `sandbox_workspace_prepare` | Clone git repo (OverlayFS) |
| | `sandbox_workspace_cleanup` | Remove workspace directory |
| **Task Queue** | `task_publish` | Publish task to box queue |
| | `task_lock` / `task_unlock` | Distributed locking |
| | `task_heartbeat` | Renew lock TTL |
| | `task_ack` / `task_nack` | Success/failure acknowledgment |
| | `lock_status` | Check lock state |
| | `queue_status` | Queue stats per topic |
| | `queue_dashboard` | Live dashboard metrics |
| **DLQ** | `dlq_list` | List dead letter entries |
| | `dlq_stats` | DLQ statistics |
| | `dlq_retry` | Retry single entry |
| | `dlq_quarantine` | Mark as poison message |
| | `dlq_drain` | Retry all pending entries |
| **Circuit Breaker** | `circuit_breaker_status` | Get breaker state for box |
| | `circuit_breaker_all` | All breakers snapshot |
| | `circuit_breaker_config` | Update thresholds |
| | `circuit_breaker_reset` | Manual reset to Closed |
| | `circuit_breaker_history` | FSM transition log |
| **OpenClaw** | `openclaw_register_user` | Register channel user to box |
| | `openclaw_list_users` | List registered users |
| | `openclaw_inbound_stats` | Inbound statistics |
| | `channel_send` | Send message via channel |
| | `channel_status` | Channel connection status |
| **Prompt Cache** | `prompt_cache_stats` | Cache hit/miss stats |
| | `prompt_cache_warm` | Pre-populate cache |
| | `prompt_cache_clear` | Invalidate cache |
| | `prompt_cache_configure` | Upsert cacheable segment |
| | `prompt_cache_get_prefix` | Get all cached segments |
| **MCP White Label** | `mcp_wl_spawn` | Spawn per-box MCP instance |
| | `mcp_wl_stop` | Stop instance |
| | `mcp_wl_status` | Instance status |
| | `mcp_wl_registry` | List all instances |
| | `mcp_wl_registry_stats` | Registry statistics |

---

## Required Fields per Tool

### workitem_update

```json
{
  "work_item_id": "work-item-uuid",
  "status": "in_progress",
  "assignee": "Agent Name",
  "branch": "feature/my-branch",
  "agent_name": "GitHub Copilot",
  "model_used": "claude-sonnet-4-6",
  "ide_used": "VS Code",
  "tokens_used": 5000,
  "feedback": "Detailed summary of what was implemented"
}
```

**Status values:** `todo (0)`, `in_progress (1)`, `review (2)`, `done (3)`, `blocked (4)`

### sandbox_create

```json
{
  "box_id": "box-uuid",
  "image": "python",
  "cpu": 2,
  "mem": 512,
  "timeout": 30,
  "network_mode": "Restricted"
}
```

### context_box_ingest

```json
{
  "file_path": "/absolute/path/to/file.md"
}
```

---

## Post-Installation Validation

```powershell
# Full validation script
powershell -ExecutionPolicy Bypass -File .\ops\scripts\validate-briefapp-mcp-after-deploy.ps1 -SkipBuild
```

---

## References

- [MCP Server README](../../mcp-server-python/README.md)
- [Model Context Protocol](https://modelcontextprotocol.io)
- [FastMCP SDK](https://github.com/jlowin/fastmcp)
- [Skills by IDE](../skills/README.md)
- [Installers](../../installers/README.md)
