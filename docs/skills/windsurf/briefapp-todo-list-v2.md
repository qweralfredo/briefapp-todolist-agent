# Briefapp Todo List V3 — Windsurf Skill

> Use this skill for ALL project management and task operations via Briefapp Todo List MCP.
> Covers the full MVP: projects, backlog, sprints, work items, sub-tasks, wiki, documentation,
> knowledge checkpoints, Context-Box RAG, sandboxes, and queues.

## Session Flow

Execute in this order when starting or resuming work:

```
1. [READ]  briefapp://projects/active               → identify project
2. [READ]  briefapp://projects/{id}/context         → full context
3. [READ]  briefapp://projects/{id}/tasks/triage    → blocked/unassigned tasks
4. [WRITE] workitem_update(status='in_progress')   → start work
5. [READ]  todo.md (if exists at root)             → follow checklist
6. [WORK]  ... implementation ...
7. [WRITE] workitem_update(status='review')        → mark for review
8. [GIT]   add, commit, push                       → persist code
9. [WRITE] workitem_update(status='done')          → complete task AFTER push
10.[WRITE] wiki_add / documentation_add            → document (per backlog)
11.[WRITE] knowledge_checkpoint                    → save state snapshot
```

## Key Tools — Scrum Core

- `project_list` / `project_create` / `project_config_update` / `project_delete`
- `backlog_add` / `backlog_list` / `backlog_context_update`
- `sprint_create`
- `workitem_list` / `workitem_update` / `workitem_add_subtask`
- `wiki_add` / `wiki_list` / `documentation_add` / `documentation_list`
- `knowledge_checkpoint` / `knowledge_list` / `checkpoint_list`
- `search` — global search across entities

## Key Tools — V3 Box Modules

- **Context-Box RAG:** `context_box_ingest`, `context_box_query`, `context_box_list`, `context_box_delete`, `context_box_ingest_batch`
- **Sandbox:** `sandbox_create`, `sandbox_start`, `sandbox_stop`, `sandbox_exec`, `sandbox_destroy`, `sandbox_metrics`, `sandbox_workspace_prepare`
- **Task Queue:** `task_publish`, `task_lock`, `task_unlock`, `task_heartbeat`, `task_ack`, `task_nack`, `queue_dashboard`
- **DLQ:** `dlq_list`, `dlq_stats`, `dlq_retry`, `dlq_quarantine`, `dlq_drain`
- **Circuit Breaker:** `circuit_breaker_status`, `circuit_breaker_config`, `circuit_breaker_reset`
- **Prompt Cache:** `prompt_cache_stats`, `prompt_cache_warm`, `prompt_cache_configure`

## Resources (Read-Only)

| URI | Description |
|-----|-------------|
| `briefapp://projects/active` | Active projects |
| `briefapp://projects/{id}/context` | Full project context |
| `briefapp://projects/{id}/dashboard` | Sprint metrics |
| `briefapp://projects/{id}/backlog` | Backlog items |
| `briefapp://projects/{id}/workitems` | Work items |
| `briefapp://projects/{id}/tasks/triage` | Tasks needing triage |
| `briefapp://projects/{id}/knowledge` | Wiki + checkpoints |

## Status Values

Use string labels: `"todo"`, `"in_progress"`, `"review"`, `"done"`, `"blocked"`

## workitem_update — Required Context

| Field | Value |
|-------|-------|
| `agent_name` | Name of the active agent |
| `model_used` | LLM model of the session |
| `ide_used` | `Windsurf` |
| `branch` | Current working branch |
| `tokens_used` | Real token count (omit if unavailable) |
| `feedback` | Concrete technical summary (NEVER use "unknown") |

## MCP Connection

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
