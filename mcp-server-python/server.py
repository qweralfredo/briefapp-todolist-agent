import os
import json
from datetime import date, timedelta
from typing import Any, Union

import httpx
from mcp.server.fastmcp import FastMCP
import git_graph_service
from src.connectors.gdrive.auth import list_gdrive_files
from src.connectors.onedrive.auth import list_onedrive_files

API_BASE_URL = os.getenv("PANDORA_API_BASE_URL", "http://127.0.0.1:8480")
TIMEOUT_SECONDS = float(os.getenv("PANDORA_API_TIMEOUT", "30"))
MCP_TRANSPORT = os.getenv("PANDORA_MCP_TRANSPORT", "stdio")
MCP_HOST = os.getenv("PANDORA_MCP_HOST", "127.0.0.1")
MCP_PORT = int(os.getenv("PANDORA_MCP_PORT", "8000"))
MCP_MOUNT_PATH = os.getenv("PANDORA_MCP_MOUNT_PATH", "/")
AGENT_PLANNER_URL = os.getenv("PANDORA_AGENT_PLANNER_URL", "http://agent-planner:8483")

mcp = FastMCP("briefapp-todo-list-mcp", host=MCP_HOST, port=MCP_PORT)


class ApiError(RuntimeError):
    pass


def _request(method: str, path: str, *, params: dict[str, Any] | None = None, payload: dict[str, Any] | None = None) -> Any:
    url = f"{API_BASE_URL.rstrip('/')}{path}"
    headers = {}
    api_key = os.getenv("PANDORA_API_KEY")
    if api_key:
        headers["X-Briefapp-Api-Key"] = api_key
        
    with httpx.Client(timeout=TIMEOUT_SECONDS) as client:
        response = client.request(method=method, url=url, headers=headers, params=params, json=payload)

    if response.status_code >= 400:
        try:
            data = response.json()
        except ValueError:
            data = {"error": response.text}
        raise ApiError(f"API request failed ({response.status_code}): {data}")

    if not response.content:
        return None

    return response.json()


def _request_sse(method: str, url: str, *, payload: dict[str, Any] | None = None) -> list[dict[str, Any]]:
    # Simple SSE consumer for the planner endpoints
    events = []
    headers = {}
    api_key = os.getenv("PANDORA_API_KEY")
    if api_key:
        headers["X-Briefapp-Api-Key"] = api_key
        
    with httpx.Client(timeout=300.0) as client:
        with client.stream(method, url, headers=headers, json=payload) as response:
            if response.status_code >= 400:
                raise ApiError(f"SSE request failed ({response.status_code}): {response.read()}")
            
            for line in response.iter_lines():
                if line.startswith("data: "):
                    data_str = line[6:]
                    try:
                        events.append(json.loads(data_str))
                    except json.JSONDecodeError:
                        pass
    return events


def _json_resource(payload: Any) -> str:
    return json.dumps(payload, ensure_ascii=True, indent=2)


def _flatten_workitems(sprints: list[dict[str, Any]], sprint_id: str | None = None) -> list[dict[str, Any]]:
    items: list[dict[str, Any]] = []
    for sprint in sprints:
        if sprint_id and sprint.get("id") != sprint_id:
            continue

        for work_item in sprint.get("workItems", []):
            row = dict(work_item)
            row["sprintId"] = sprint.get("id")
            row["sprintName"] = sprint.get("name")
            items.append(row)

    return items


def _normalize_workitem_status(status: str) -> int:
    value = status.strip().lower()
    # ENUM: Todo=0 | InProgress=1 | Review=2 | Done=3 | Blocked=4
    aliases = {
        "0": 0,
        "todo": 0,
        "to-do": 0,
        "new": 0,
        "1": 1,
        "in_progress": 1,
        "in-progress": 1,
        "in progress": 1,
        "inprogress": 1,
        "doing": 1,
        "2": 2,
        "review": 2,
        "qa": 2,
        "3": 3,
        "done": 3,
        "completed": 3,
        "4": 4,
        "blocked": 4,
        "impediment": 4,
    }
    if value not in aliases:
        raise ApiError(
            "Invalid work item status. Valid string labels: todo, in_progress, review, done, blocked. "
            "Valid integers: 0=Todo 1=InProgress 2=Review 3=Done 4=Blocked."
        )
    return aliases[value]


_STATUS_LABELS = {0: "Todo", 1: "InProgress", 2: "Review", 3: "Done", 4: "Blocked"}


@mcp.resource("briefapp://about")
def resource_about() -> str:
    """Describe MCP resources available for the Briefapp todo workflow."""
    return _json_resource(
        {
            "name": "briefapp-todo-list-mcp",
            "purpose": "Read-only context resources for projects, backlog, sprints, work items and knowledge.",
            "notes": [
                "Resources are read-only context for agents.",
                "Use tools for write operations.",
            ],
            "directResources": [
                "briefapp://about",
                "briefapp://projects/active",
                "briefapp://projects/all",
            ],
            "resourceTemplates": [
                "briefapp://projects/{project_id}/context",
                "briefapp://projects/{project_id}/config",
                "briefapp://projects/{project_id}/dashboard",
                "briefapp://projects/{project_id}/backlog",
                "briefapp://projects/{project_id}/sprints",
                "briefapp://projects/{project_id}/workitems",
                "briefapp://projects/{project_id}/workitems/status/{status}",
                "briefapp://projects/{project_id}/sprints/{sprint_id}/workitems",
                "briefapp://projects/{project_id}/tasks/overview",
                "briefapp://projects/{project_id}/tasks/triage",
                "briefapp://projects/{project_id}/knowledge",
            ],
        }
    )


@mcp.resource("briefapp://projects/active")
def resource_projects_active() -> str:
    """Read active projects list."""
    return _json_resource(_request("GET", "/api/projects", params={"includeArchived": False}))


@mcp.resource("briefapp://projects/all")
def resource_projects_all() -> str:
    """Read all projects list, including archived."""
    return _json_resource(_request("GET", "/api/projects", params={"includeArchived": True}))


@mcp.resource("briefapp://projects/{project_id}/config")
def resource_project_config(project_id: str) -> str:
    """Read environment configuration for one project (GitHub URL, local path, tech stack, main branch)."""
    projects = _request("GET", "/api/projects", params={"includeArchived": True})
    project = next((p for p in projects if p.get("id") == project_id), None)
    if project is None:
        raise ApiError(f"Project {project_id} not found.")
    return _json_resource({
        "projectId": project_id,
        "name": project.get("name"),
        "gitHubUrl": project.get("gitHubUrl"),
        "localPath": project.get("localPath"),
        "techStack": project.get("techStack"),
        "mainBranch": project.get("mainBranch", "main"),
    })


@mcp.resource("briefapp://projects/{project_id}/dashboard")
def resource_project_dashboard(project_id: str) -> str:
    """Read dashboard summary for one project."""
    return _json_resource(_request("GET", f"/api/projects/{project_id}/dashboard"))


@mcp.resource("briefapp://projects/{project_id}/backlog")
def resource_project_backlog(project_id: str) -> str:
    """Read backlog items for one project."""
    return _json_resource(_request("GET", f"/api/projects/{project_id}/backlog"))


@mcp.resource("briefapp://projects/{project_id}/sprints")
def resource_project_sprints(project_id: str) -> str:
    """Read sprint list for one project."""
    return _json_resource(_request("GET", f"/api/projects/{project_id}/sprints"))


@mcp.resource("briefapp://projects/{project_id}/workitems")
def resource_project_workitems(project_id: str) -> str:
    """Read flattened work items for all sprints in one project."""
    sprints = _request("GET", f"/api/projects/{project_id}/sprints")
    return _json_resource(_flatten_workitems(sprints))


@mcp.resource("briefapp://projects/{project_id}/workitems/status/{status}")
def resource_project_workitems_by_status(project_id: str, status: str) -> str:
    """Read flattened work items for one project filtered by status."""
    sprints = _request("GET", f"/api/projects/{project_id}/sprints")
    status_code = _normalize_workitem_status(status)
    filtered = [item for item in _flatten_workitems(sprints) if int(item.get("status", -1)) == status_code]
    payload = {
        "projectId": project_id,
        "status": status,
        "statusCode": status_code,
        "count": len(filtered),
        "items": filtered,
    }
    return _json_resource(payload)


@mcp.resource("briefapp://projects/{project_id}/sprints/{sprint_id}/workitems")
def resource_project_sprint_workitems(project_id: str, sprint_id: str) -> str:
    """Read flattened work items for one sprint in one project."""
    sprints = _request("GET", f"/api/projects/{project_id}/sprints")
    return _json_resource(_flatten_workitems(sprints, sprint_id=sprint_id))


@mcp.resource("briefapp://projects/{project_id}/knowledge")
def resource_project_knowledge(project_id: str) -> str:
    """Read knowledge payload (wiki, checkpoints, documentation, agent runs)."""
    return _json_resource(_request("GET", f"/api/projects/{project_id}/knowledge"))


@mcp.resource("briefapp://projects/{project_id}/tasks/overview")
def resource_project_tasks_overview(project_id: str) -> str:
    """Read task-centric summary for agents to track planning and execution state."""
    backlog = _request("GET", f"/api/projects/{project_id}/backlog")
    sprints = _request("GET", f"/api/projects/{project_id}/sprints")
    workitems = _flatten_workitems(sprints)

    backlog_done = [item for item in backlog if int(item.get("status", 0)) == 3]
    backlog_open = [item for item in backlog if int(item.get("status", 0)) != 3]
    workitems_done = [item for item in workitems if int(item.get("status", 0)) == 3]
    workitems_open = [item for item in workitems if int(item.get("status", 0)) != 3]

    payload = {
        "projectId": project_id,
        "summary": {
            "backlogTotal": len(backlog),
            "backlogOpen": len(backlog_open),
            "backlogDone": len(backlog_done),
            "workItemsTotal": len(workitems),
            "workItemsOpen": len(workitems_open),
            "workItemsDone": len(workitems_done),
            "sprintsTotal": len(sprints),
        },
        "backlogOpen": backlog_open,
        "workItemsOpen": workitems_open,
    }

    return _json_resource(payload)


@mcp.resource("briefapp://projects/{project_id}/tasks/triage")
def resource_project_tasks_triage(project_id: str) -> str:
    """Read triage view focused on review and blocked items for agent prioritization."""
    backlog = _request("GET", f"/api/projects/{project_id}/backlog")
    sprints = _request("GET", f"/api/projects/{project_id}/sprints")
    workitems = _flatten_workitems(sprints)

    backlog_blocked = [item for item in backlog if int(item.get("status", -1)) == 4]
    workitems_review = [item for item in workitems if int(item.get("status", -1)) == 2]
    workitems_blocked = [item for item in workitems if int(item.get("status", -1)) == 4]

    payload = {
        "projectId": project_id,
        "summary": {
            "backlogBlocked": len(backlog_blocked),
            "workItemsInReview": len(workitems_review),
            "workItemsBlocked": len(workitems_blocked),
            "triagePriorityCount": len(backlog_blocked) + len(workitems_review) + len(workitems_blocked),
        },
        "backlogBlocked": backlog_blocked,
        "workItemsInReview": workitems_review,
        "workItemsBlocked": workitems_blocked,
    }

    return _json_resource(payload)


@mcp.resource("briefapp://projects/{project_id}/context")
def resource_project_context(project_id: str) -> str:
    """Read full project context snapshot for agent grounding and continuity."""
    dashboard = _request("GET", f"/api/projects/{project_id}/dashboard")
    backlog = _request("GET", f"/api/projects/{project_id}/backlog")
    sprints = _request("GET", f"/api/projects/{project_id}/sprints")
    knowledge = _request("GET", f"/api/projects/{project_id}/knowledge")

    payload = {
        "projectId": project_id,
        "dashboard": dashboard,
        "backlog": backlog,
        "sprints": sprints,
        "workItems": _flatten_workitems(sprints),
        "knowledge": knowledge,
    }

    return _json_resource(payload)


@mcp.tool(name="project_list")
def project_list(include_archived: bool = False) -> list[dict[str, Any]]:
    """List projects from Briefapp."""
    return _request("GET", "/api/projects", params={"includeArchived": include_archived})


@mcp.tool(name="project_create")
def project_create(
    name: str,
    description: str,
    github_url: str | None = None,
    local_path: str | None = None,
    tech_stack: str | None = None,
    main_branch: str | None = None,
) -> dict[str, Any]:
    """Create a new project. Optionally set environment config: github_url, local_path, tech_stack, main_branch."""
    payload: dict[str, Any] = {"name": name, "description": description}
    if github_url is not None:
        payload["gitHubUrl"] = github_url
    if local_path is not None:
        payload["localPath"] = local_path
    if tech_stack is not None:
        payload["techStack"] = tech_stack
    if main_branch is not None:
        payload["mainBranch"] = main_branch
    return _request("POST", "/api/projects", payload=payload)


@mcp.tool(name="project_config_update")
def project_config_update(
    project_id: str,
    github_url: str | None = None,
    local_path: str | None = None,
    tech_stack: str | None = None,
    main_branch: str | None = None,
) -> dict[str, Any]:
    """Update environment configuration for a project (GitHub URL, local path, tech stack, main branch). Only provided fields are changed."""
    payload: dict[str, Any] = {}
    if github_url is not None:
        payload["gitHubUrl"] = github_url
    if local_path is not None:
        payload["localPath"] = local_path
    if tech_stack is not None:
        payload["techStack"] = tech_stack
    if main_branch is not None:
        payload["mainBranch"] = main_branch
    if not payload:
        raise ApiError("At least one config field must be provided (github_url, local_path, tech_stack or main_branch).")
    return _request("PATCH", f"/api/projects/{project_id}/config", payload=payload)


@mcp.tool(name="project_delete")
def project_delete(project_id: str) -> dict[str, Any]:
    """Archive a project (soft delete) by id."""
    return _request("DELETE", f"/api/projects/{project_id}")


@mcp.tool(name="backlog_add")
def backlog_add(project_id: str, title: str, description: str, story_points: int, priority: int) -> dict[str, Any]:
    """Add backlog item to a project."""
    return _request(
        "POST",
        f"/api/projects/{project_id}/backlog",
        payload={
            "title": title,
            "description": description,
            "storyPoints": story_points,
            "priority": priority,
        },
    )


@mcp.tool(name="backlog_list")
def backlog_list(project_id: str) -> list[dict[str, Any]]:
    """List backlog items from a project."""
    return _request("GET", f"/api/projects/{project_id}/backlog")


@mcp.tool(name="sprint_create")
def sprint_create(
    project_id: str,
    name: str,
    goal: str,
    start_date: str,
    end_date: str,
    backlog_item_ids: list[str],
) -> dict[str, Any]:
    """Create sprint from backlog items using YYYY-MM-DD dates.

    backlog_item_ids is REQUIRED and must contain at least one backlog item ID.
    Obtain valid IDs via backlog_list(project_id) before calling this tool.
    """
    if not backlog_item_ids:
        raise ApiError(
            "backlog_item_ids is required and must contain at least one backlog item ID. "
            "Use backlog_list(project_id) to obtain valid IDs before creating a sprint."
        )
    return _request(
        "POST",
        f"/api/projects/{project_id}/sprints",
        payload={
            "name": name,
            "goal": goal,
            "startDate": start_date,
            "endDate": end_date,
            "backlogItemIds": backlog_item_ids,
        },
    )


@mcp.tool(name="workitem_list")
def workitem_list(project_id: str, sprint_id: str = "") -> list[dict[str, Any]]:
    """List work items from a project filtered by sprint.

    sprint_id is REQUIRED — you must provide the ID of the sprint to list work items from.
    Obtain valid sprint IDs via the resource briefapp://projects/{project_id}/sprints
    or by reading the sprint data from the project context first.
    """
    if not sprint_id or not sprint_id.strip():
        raise ApiError(
            "sprint_id is required. You must specify which sprint to list work items from. "
            "Use briefapp://projects/{project_id}/sprints to obtain valid sprint IDs."
        )
    sprints = _request("GET", f"/api/projects/{project_id}/sprints")

    items: list[dict[str, Any]] = []
    for sprint in sprints:
        if sprint.get("id") != sprint_id:
            continue

        for work_item in sprint.get("workItems", []):
            row = dict(work_item)
            row["sprintId"] = sprint.get("id")
            row["sprintName"] = sprint.get("name")
            items.append(row)

    return items


@mcp.tool(name="workitem_update")
def workitem_update(
    work_item_id: str,
    status: Union[str, int],
    assignee: str,
    tokens_used: int | None = None,
    agent_name: str = "",
    model_used: str = "",
    ide_used: str = "",
    feedback: str = "",
    metadata_json: str = "",
    branch: str = "",
    responsavel_id: str = "",
) -> dict[str, Any]:
    """Update work item status and track token/feedback metadata.

    status — use string label (preferred) or integer:
      todo / 0       → Todo
      in_progress / 1 → InProgress
      review / 2     → Review
      done / 3       → Done
      blocked / 4    → Blocked

    branch — git branch being worked on for this task (e.g. 'feature/sub-tasks').

    tokens_used — only pass this when you have the ACTUAL token count from
      observability tooling. Do NOT estimate or fabricate a value.
      Omit (or pass None/0) when the real count is unavailable.

    The response includes:
      - 'statusLabel'   confirming the resolved status
      - 'tokensTracked' True if a positive token count was recorded, False if not
    """
    status_int = _normalize_workitem_status(str(status))
    actual_tokens = tokens_used if tokens_used is not None else 0
    
    if not responsavel_id:
        try:
            import json
            conf_path = os.path.join(os.path.dirname(__file__), "briefapp.conf")
            if os.path.exists(conf_path):
                with open(conf_path, "r", encoding="utf-8") as f:
                    conf = json.load(f)
                    responsavel_id = conf.get("agent_uuid", "")
        except Exception:
            pass

    payload = {
        "status": status_int,
        "assignee": assignee,
        "tokensUsed": actual_tokens,
        "agentName": agent_name,
        "modelUsed": model_used,
        "ideUsed": ide_used,
        "feedback": feedback,
        "metadataJson": metadata_json,
        "branch": branch,
    }
    if responsavel_id:
        payload["responsavelId"] = responsavel_id
        
    result = _request(
        "POST",
        f"/api/work-items/{work_item_id}/status",
        payload=payload,
    )
    # Echo status label back so agents can verify what was actually set
    if isinstance(result, dict):
        result["statusLabel"] = _STATUS_LABELS.get(status_int, str(status_int))
        result["tokensTracked"] = tokens_used is not None and tokens_used > 0
    return result


@mcp.tool(name="workitem_add_subtask")
def workitem_add_subtask(
    parent_work_item_id: str,
    title: str,
    description: str,
    assignee: str = "",
    branch: str = "",
    tags: str = "",
) -> dict[str, Any]:
    """Create a sub-task under an existing work item.

    Sub-tasks inherit sprint and backlog item from the parent.
    They appear in the sprint board with a parent badge.
    When all sub-tasks of a parent reach Done, the parent is auto-completed.
    """
    return _request(
        "POST",
        f"/api/work-items/{parent_work_item_id}/sub-tasks",
        payload={
            "title": title,
            "description": description,
            "assignee": assignee,
            "branch": branch,
            "tags": tags,
        },
    )


@mcp.tool(name="backlog_context_update")
def backlog_context_update(
    backlog_item_id: str,
    tags: str | None = None,
    wiki_refs: str | None = None,
    constraints: str | None = None,
) -> dict[str, Any]:
    """Update context metadata on a backlog item.

    tags       — comma-separated labels (e.g. 'auth,security,mvp')
    wiki_refs  — references to wiki pages (e.g. 'wiki:Authentication,wiki:JWT-Design')
    constraints — free-text preconditions or dependencies (e.g. 'Must be done before Sprint 3 release')

    Only provided fields are changed.
    """
    payload: dict[str, Any] = {}
    if tags is not None:
        payload["tags"] = tags
    if wiki_refs is not None:
        payload["wikiRefs"] = wiki_refs
    if constraints is not None:
        payload["constraints"] = constraints
    if not payload:
        raise ApiError("At least one context field must be provided (tags, wiki_refs or constraints).")
    return _request("PATCH", f"/api/backlog-items/{backlog_item_id}/context", payload=payload)


@mcp.tool(name="knowledge_checkpoint")
def knowledge_checkpoint(
    project_id: str,
    name: str,
    context_snapshot: str,
    decisions: str,
    risks: str,
    next_actions: str,
) -> dict[str, Any]:
    """Create a knowledge checkpoint for a project."""
    return _request(
        "POST",
        f"/api/projects/{project_id}/checkpoints",
        payload={
            "name": name,
            "contextSnapshot": context_snapshot,
            "decisions": decisions,
            "risks": risks,
            "nextActions": next_actions,
        },
    )


@mcp.tool(name="knowledge_list")
def knowledge_list(project_id: str) -> dict[str, Any]:
    """List full knowledge payload (wiki, documentation, checkpoints, agent runs)."""
    return _request("GET", f"/api/projects/{project_id}/knowledge")


@mcp.tool(name="wiki_add")
def wiki_add(
    project_id: str,
    title: str,
    content_markdown: str,
    tags: str,
    category: str = "General",
) -> dict[str, Any]:
    """Create wiki page for a project."""
    return _request(
        "POST",
        f"/api/projects/{project_id}/wiki",
        payload={
            "title": title,
            "contentMarkdown": content_markdown,
            "tags": tags,
            "category": category,
        },
    )


@mcp.tool(name="wiki_list")
def wiki_list(project_id: str) -> list[dict[str, Any]]:
    """List wiki pages from a project."""
    knowledge = _request("GET", f"/api/projects/{project_id}/knowledge")
    return knowledge.get("wikiPages", [])


@mcp.tool(name="documentation_add")
def documentation_add(
    project_id: str,
    title: str,
    content_markdown: str,
    category: str,
    tags: str,
) -> dict[str, Any]:
    """Create documentation page for a project."""
    return _request(
        "POST",
        f"/api/projects/{project_id}/documentation",
        payload={
            "title": title,
            "contentMarkdown": content_markdown,
            "category": category,
            "tags": tags,
        },
    )


@mcp.tool(name="documentation_list")
def documentation_list(project_id: str) -> list[dict[str, Any]]:
    """List documentation pages from a project."""
    knowledge = _request("GET", f"/api/projects/{project_id}/knowledge")
    return knowledge.get("documentationPages", [])


@mcp.tool(name="checkpoint_list")
def checkpoint_list(project_id: str) -> list[dict[str, Any]]:
    """List knowledge checkpoints from a project."""
    knowledge = _request("GET", f"/api/projects/{project_id}/knowledge")
    return knowledge.get("checkpoints", [])


@mcp.tool(name="get_modification_impact")
def get_modification_impact(project_id: str, file_path: str) -> str:
    """Read a context modification graph for a specified file to discover temporal coupling and historical context."""
    projects = _request("GET", "/api/projects", params={"includeArchived": False})
    project = next((p for p in projects if str(p.get("id")) == project_id), None)
    if not project or not project.get("localPath"):
        raise ApiError(f"Project {project_id} not found or localPath not configured.")
        
    impact = git_graph_service.analyze_impact(project.get("localPath"), file_path)
    
    if "error" in impact:
        return impact["error"]
        
    md = [f"## Impact Analysis for `{file_path}`", ""]
    
    md.append("### Temporally Coupled Files (Co-modified)")
    if impact.get("co_modified_files"):
        for f in impact["co_modified_files"][:10]:
            md.append(f"- `{f['file']}` (Modified together {f['frequency']} times)")
    else:
        md.append("- No temporal coupling found.")
        
    md.append("")
    md.append("### Historically Related WorkItems")
    if impact.get("historically_related_workitems"):
        for wi in impact["historically_related_workitems"][:10]:
            md.append(f"- WorkItem `{wi}`")
    else:
        md.append("- No correlated WorkItems found in recent history.")
        
    return "\\n".join(md)


@mcp.tool(name="search")
def search(query: str, project_id: str | None = None) -> dict[str, Any]:
    """Search globally across projects, backlogs, sprints, tasks, wiki, docs, and checkpoints.
    
    query: The term to search for.
    project_id: Optional ID of the project to narrow the search scope.
    """
    params = {"q": query}
    if project_id:
        params["projectId"] = project_id
    return _request("GET", "/api/search", params=params)


@mcp.prompt(name="briefapp_project_config")
def briefapp_project_config(
    project_id: str = "<project-id>",
    github_url: str = "",
    local_path: str = "",
    tech_stack: str = "",
    main_branch: str = "main",
) -> str:
    """Prompt guiado para configurar o ambiente do projeto (GitHub, caminho local, stack, branch)."""
    return (
        "Objetivo: configurar os dados de ambiente para que uma IDE possa recriar o contexto do projeto do zero.\n"
        "Resource MCP de leitura: briefapp://projects/{project_id}/config\n"
        "Tool de escrita: project_config_update\n\n"
        "Campos disponiveis:\n"
        f"- github_url: URL do repositório GitHub (ex: https://github.com/org/repo) — atual: '{github_url or 'nao configurado'}'\n"
        f"- local_path: Caminho da pasta no disco (ex: c:/projetos/meuapp) — atual: '{local_path or 'nao configurado'}'\n"
        f"- tech_stack: Stack tecnologica (ex: .NET 10, React, PostgreSQL) — atual: '{tech_stack or 'nao configurado'}'\n"
        f"- main_branch: Branch principal (ex: main, develop) — atual: '{main_branch}'\n\n"
        "Instrucoes:\n"
        f"1) Ler configuracao atual via resource: briefapp://projects/{project_id}/config\n"
        "2) Solicitar ao usuario os campos ausentes ou incorretos.\n"
        f"3) Executar project_config_update(project_id='{project_id}', ...) apenas com os campos a alterar.\n"
        "4) Confirmar resultado relendo o resource de config.\n\n"
        "Validacao minima obrigatoria:\n"
        "- gitHubUrl preenchido se o projeto tiver repositorio remoto.\n"
        "- localPath preenchido se o projeto tiver pasta local conhecida.\n"
        "- techStack preenchido para orientar tooling e agentes.\n"
        "- mainBranch correto (padrao: main).\n\n"
        "Formato de resposta esperado:\n"
        "- projectId, projectName\n"
        "- gitHubUrl, localPath, techStack, mainBranch\n"
        "- campos ainda ausentes (se houver)."
    )


@mcp.prompt(name="briefapp_project_create")
def briefapp_project_create(name: str = "Novo Projeto", description: str = "Projeto criado via prompt") -> str:
    """Prompt guiado para criacao de projeto com validacoes objetivas."""
    return (
        "Objetivo: criar um projeto no Briefapp e validar o resultado.\n"
        "Recurso UI relacionado: seletor de Projeto (topo), Dashboard, Backlog, Sprints e Knowledge.\n"
        "Acao: execute a tool project_create com os dados abaixo:\n"
        f"- name: '{name}'\n"
        f"- description: '{description}'\n"
        "Validacao minima obrigatoria:\n"
        "1) Confirmar retorno com id nao vazio.\n"
        "2) Executar project_list(include_archived=false) e verificar se o id criado aparece.\n"
        "3) Definir este projeto como contexto ativo para as proximas operacoes.\n"
        "Formato de resposta esperado:\n"
        "- project_id\n"
        "- project_name\n"
        "- created_at\n"
        "- proximo passo recomendado (backlog_add)."
    )


@mcp.prompt(name="briefapp_sprint_create")
def briefapp_sprint_create(
    project_id: str = "<project-id>",
    name: str = "Sprint 1",
    goal: str = "Entregar funcionalidades prioritarias",
    start_date: str = date.today().strftime("%Y-%m-%d"),
    end_date: str = (date.today() + timedelta(days=14)).strftime("%Y-%m-%d"),
) -> str:
    """Prompt guiado para criacao de sprint com backlog e verificacao de work items."""
    return (
        "Objetivo: criar sprint a partir do backlog e validar quadro de tarefas.\n"
        "Recurso UI relacionado: pagina Sprints (planejamento + board por status).\n"
        "Pre-condicoes:\n"
        "1) project_id valido.\n"
        "2) backlog com pelo menos um item (obter com backlog_list).\n"
        "Acao:\n"
        "- Se backlog estiver vazio, criar item com backlog_add antes de continuar.\n"
        "- Executar sprint_create com:\n"
        f"  - project_id: '{project_id}'\n"
        f"  - name: '{name}'\n"
        f"  - goal: '{goal}'\n"
        f"  - start_date: '{start_date}'\n"
        f"  - end_date: '{end_date}'\n"
        "  - backlog_item_ids: ['<backlog-item-id>']\n"
        "Validacao minima obrigatoria:\n"
        "1) Confirmar sprint id no retorno.\n"
        "2) Executar workitem_list(project_id, sprint_id) e garantir lista nao vazia.\n"
        "3) Se necessario, atualizar status/assignee com workitem_update.\n"
        "Formato de resposta esperado:\n"
        "- sprint_id\n"
        "- quantidade de work items gerados\n"
        "- proximo passo recomendado (execucao do board)."
    )


@mcp.prompt(name="briefapp_search")
def briefapp_search(query: str = "<term>", project_id: str = "") -> str:
    """Prompt guiado para realizar buscas globais no Briefapp."""
    return (
        "Objetivo: encontrar informacoes em todo o Briefapp ou em um projeto especifico.\n"
        "Recurso MCP relacionado: tool 'search'.\n"
        "Acao: execute a tool search(query=..., project_id=...) para encontrar itens (tasks, projetos, wiki, etc) que contem o termo buscado.\n"
        f"Busca: '{query}'\n"
        f"Filtro de projeto: '{project_id or 'Global'}'\n"
        "Dica: utilize a busca para recuperar contexto de tarefas antigas, decisoes arquiteturais (wiki), ou para encontrar projetos ou membros do time."
    )


@mcp.prompt(name="briefapp_resources_guide")
def briefapp_resources_guide(project_id: str = "<project-id>") -> str:
    """Guia objetivo com todos os recursos da UI e o mapeamento para MCP/API."""
    return (
        "Mapa de recursos do app em http://localhost:8400 e como operar via MCP/API.\n\n"
        "Resources MCP para contexto completo (read-only):\n"
        "- briefapp://projects/{project_id}/context\n"
        "- briefapp://projects/{project_id}/dashboard\n"
        "- briefapp://projects/{project_id}/backlog\n"
        "- briefapp://projects/{project_id}/sprints\n"
        "- briefapp://projects/{project_id}/workitems\n"
        "- briefapp://projects/{project_id}/workitems/status/{status}\n"
        "- briefapp://projects/{project_id}/sprints/{sprint_id}/workitems\n"
        "- briefapp://projects/{project_id}/tasks/overview\n"
        "- briefapp://projects/{project_id}/tasks/triage\n"
        "- briefapp://projects/{project_id}/knowledge\n\n"
        "1) Dashboard (/):\n"
        "- O que mostra: backlog total/concluido, sprints ativas, distribuicao de work items, checkpoint/wiki/agent runs.\n"
        "- API real: GET /api/projects/{project_id}/dashboard\n"
        "- Resource MCP: briefapp://projects/{project_id}/dashboard\n\n"
        "2) Backlog (/backlog):\n"
        "- Operacoes: listar e criar backlog item (title, description, story points, priority).\n"
        "- MCP: backlog_list, backlog_add\n"
        "- API real: GET/POST /api/projects/{project_id}/backlog\n\n"
        "3) Sprints (/sprints):\n"
        "- Operacoes: criar sprint com backlog_item_ids, listar work items por sprint, mover status e atribuir responsavel.\n"
        "- MCP: sprint_create, workitem_list, workitem_update\n"
        "- API real: POST /api/projects/{project_id}/sprints, POST /api/work-items/{work_item_id}/status\n\n"
        "4) Knowledge (/knowledge):\n"
        "- Operacoes na UI: wiki, checkpoints, documentacao, visao por categoria.\n"
        "- MCP Tools: knowledge_list, wiki_add, wiki_list, documentation_add, documentation_list, knowledge_checkpoint, checkpoint_list.\n"
        "- MCP Resource de leitura: briefapp://projects/{project_id}/knowledge\n"
        "- API real complementar:\n"
        "  - GET /api/projects/{project_id}/knowledge\n"
        "  - POST /api/projects/{project_id}/wiki\n"
        "  - POST /api/projects/{project_id}/documentation\n"
        "  - POST /api/projects/{project_id}/checkpoints\n\n"
        "5) Projetos (seletor global no layout):\n"
        "- Operacoes: criar, listar ativos, arquivar, configurar ambiente (GitHub, local path, stack).\n"
    )


@mcp.prompt(name="briefapp_agent_planner")
def briefapp_agent_planner(project_id: str = "<project-id>", user_prompt: str = "<sua-solicitacao-de-planejamento>", complexity: str = "1.0") -> str:
    """Prompt guiado para executar o fluxo Atomic Flow (Agentic Planner) nativo idêntico à UI."""
    return (
        "Objetivo: Gerar e materializar um plano de Backlogs, Sprints e Tarefas usando o Atomic Flow Agentic Planner, reproduzindo a exata experiencia da Interface Web Frontend.\n\n"
        "Fluxo de Execucao Mandatorio em 2 Fases:\n\n"
        "FASE 1: Geracao do Plano (Equivalente ao clique de 'Gerar Plano' na UI)\n"
        f"1. Execute a tool `agent_planner_generate` informando:\n"
        f"   - project_id: {project_id}\n"
        f"   - user_prompt: '{user_prompt}'\n"
        f"   - complexity_multiplier: {float(complexity)}\n"
        "2. A tool ira consumir o streaming `/api/agent/plan/stream` nativamente e retornar o `plan_text` (visão humana do plano em Markdown e Mermaid) e o `plan_payload` (JSON bruto do plano).\n"
        "3. Imprima para o usuario um resumo das Sprints e Backlogs gerados (lendo do plan_text/plan_payload) pedindo sua autorizacao e avaliacao antes de prosseguir para a materializacao.\n\n"
        "FASE 2: Execucao e Materializacao (Equivalente a aprovar o plano na UI)\n"
        "4. Após aprovação do usuário, execute a tool `agent_planner_execute` repassando:\n"
        f"   - project_id: {project_id}\n"
        "   - plan_payload: <o JSON payload obtido na FASE 1>\n"
        "5. Esta tool invocará a rota `/api/agent/execute` em background, que cria o CSV e detona a explosao dos agentes em nested loops para preencher a UI do Board.\n"
        "6. Quando concluir, confirme ao usuario que os itens já estão visiveis na rota `/sprints` e `/backlog`."
    )


@mcp.prompt(name="briefapp_context_first_execute")
def briefapp_context_first_execute(
    project_id: str = "<project-id>",
    work_item_id: str = "<work-item-id>",
    branch: str = "develop",
) -> str:
    """Prompt para execucao Context-First: ciclo de 5 passos para agentes antes de implementar qualquer tarefa."""
    return (
        "## Context-First Execution Flow — 5 Passos Obrigatorios\n\n"
        "Execute TODOS os passos abaixo em ordem antes de escrever qualquer codigo.\n\n"
        "### Passo 1 — Discovery (Scan do Contexto do Projeto)\n"
        "Ler o estado atual do projeto antes de qualquer acao:\n"
        f"- Resource: briefapp://projects/{project_id}/context\n"
        f"- Resource: briefapp://projects/{project_id}/config\n"
        "Extrair: mainBranch, localPath, techStack, sprint(s) ativo(s), work items abertos.\n\n"
        "### Passo 2 — Knowledge Warm-up (Aquecimento de Conhecimento)\n"
        "Ler o knowledge base para evitar repeticao e garantir consistencia:\n"
        f"- Resource: briefapp://projects/{project_id}/knowledge\n"
        "Extrair: checkpoints recentes, wiki pages relevantes, decisoes anteriores.\n"
        "Verificar se ha constraints ou wiki_refs no backlog item relacionado.\n\n"
        "### Passo 3 — Context Injection (Injecao de Contexto)\n"
        f"Ler o work item especifico: briefapp://projects/{project_id}/workitems\n"
        f"Filtrar pelo work_item_id: {work_item_id}\n"
        "Extrair: title, description, tags, branch, parentWorkItemId (se sub-task).\n"
        "Se o item tiver parentWorkItemId, ler o pai para entender o escopo maior.\n"
        "Verificar backlog item associado para ler tags, wikiRefs e constraints.\n\n"
        "### Passo 4 — Execucao (Implementacao com Manutencao Cognitiva)\n"
        "Agora sim, implementar a tarefa:\n"
        f"- workitem_update(work_item_id='{work_item_id}', status='in_progress', branch='{branch}', ...)\n"
        "Durante a implementacao:\n"
        "  - Se criar sub-tarefas: workitem_add_subtask(parent_work_item_id='...', ...)\n"
        "  - Se descobrir constraints novas: backlog_context_update(backlog_item_id='...', constraints='...')\n"
        "  - Se gerar conhecimento novo: wiki_add ou knowledge_checkpoint\n"
        "Ao concluir:\n"
        f"- workitem_update(work_item_id='{work_item_id}', status='done', feedback='...', branch='{branch}')\n\n"
        "### Passo 5 — Validation Review (Revisao de Validacao)\n"
        "Verificar o estado final antes de encerrar a sessao:\n"
        f"- Resource: briefapp://projects/{project_id}/tasks/overview\n"
        f"- Resource: briefapp://projects/{project_id}/tasks/triage\n"
        "Confirmar:\n"
        "  - Work item marcado como Done.\n"
        "  - Sub-tasks (se existirem) todos Done.\n"
        "  - Nenhum item bloqueado sem responsavel.\n"
        "  - Dashboard atualizado (GET /api/projects/{project_id}/dashboard).\n"
        "  - Se encerramento de sprint/epic: executar knowledge_checkpoint.\n\n"
        "### Resumo dos Recursos MCP para este Fluxo\n"
        f"- briefapp://projects/{project_id}/context  (leitura completa)\n"
        f"- briefapp://projects/{project_id}/config   (stack, branch, paths)\n"
        f"- briefapp://projects/{project_id}/knowledge (wiki, checkpoints)\n"
        f"- briefapp://projects/{project_id}/tasks/overview (visao geral)\n"
        f"- briefapp://projects/{project_id}/tasks/triage   (revisao/bloqueios)\n\n"
        "### Tools de Escrita neste Fluxo\n"
        "- workitem_update       (status, branch, tokens, feedback)\n"
        "- workitem_add_subtask  (sub-tarefas recursivas)\n"
        "- backlog_context_update (tags, wiki_refs, constraints)\n"
        "- knowledge_checkpoint  (salvar contexto ao final de epic/sprint)\n"
    )


CONTEXT_API_BASE_URL = os.getenv("PANDORA_CONTEXT_API_BASE_URL", "http://127.0.0.1:8482/api/context")

def _context_request(method: str, path: str, *, params: dict[str, Any] | None = None, payload: dict[str, Any] | None = None) -> Any:
    url = f"{CONTEXT_API_BASE_URL.rstrip('/')}{path}"
    with httpx.Client(timeout=TIMEOUT_SECONDS) as client:
        response = client.request(method=method, url=url, params=params, json=payload)
    if response.status_code >= 400:
        try:
            data = response.json()
        except ValueError:
            data = {"error": response.text}
        raise ApiError(f"Context API request failed ({response.status_code}): {data}")
    if not response.content:
        return None
    return response.json()

@mcp.resource("briefapp://boxes/{box_id}/context-rag")
def resource_context_rag(box_id: str) -> str:
    """Read indexed context files from ContextBox RAG."""
    return _json_resource(_context_request("GET", "/files"))

@mcp.tool(name="context_box_ingest")
def context_box_ingest(file_path: str) -> dict[str, Any]:
    """Ingest local file into Context-Box RAG system. Note: file_path must be an absolute path accessible by the MCP server."""
    if not os.path.exists(file_path):
        raise ApiError(f"File not found: {file_path}")
    
    import mimetypes
    mime_type, _ = mimetypes.guess_type(file_path)
    mime_type = mime_type or 'application/octet-stream'
    
    url = f"{CONTEXT_API_BASE_URL.rstrip('/')}/ingest"
    with open(file_path, 'rb') as f:
        files = {'file': (os.path.basename(file_path), f, mime_type)}
        with httpx.Client(timeout=TIMEOUT_SECONDS * 2) as client:
            response = client.post(url, files=files)
            
    if response.status_code >= 400:
        raise ApiError(f"Context ingest failed ({response.status_code}): {response.text}")
    return response.json()

@mcp.tool(name="context_box_ingest_raw")
def context_box_ingest_raw(content: str, metadata: dict[str, Any], source: str = "windows_context_menu") -> dict[str, Any]:
    """Ingest raw text content into Context-Box RAG system. Use this when the file is not accessible via file system.
    Note: metadata must be a dictionary and can contain fields like file_name, file_type, source, etc.
    """
    payload = {
        "content": content,
        "source": source,
        "metadata": metadata
    }
    url = f"{CONTEXT_API_BASE_URL.rstrip('/')}/ingest/json"
    with httpx.Client(timeout=TIMEOUT_SECONDS * 2) as client:
        response = client.post(url, json=payload)
    if response.status_code >= 400:
        raise ApiError(f"Context raw ingest failed ({response.status_code}): {response.text}")
    return response.json()

@mcp.tool(name="context_box_query")
def context_box_query(query: str, limit: int = 10, file_type: str = "") -> dict[str, Any]:
    """Query the Context-Box RAG system. Returns top-k matching chunks."""
    payload = {"query": query, "limit": limit}
    if file_type:
        payload["file_type"] = file_type
    return _context_request("POST", "/query", payload=payload)

@mcp.tool(name="context_box_list")
def context_box_list() -> list[dict[str, Any]]:
    """List files currently indexed in the Context-Box RAG system."""
    return _context_request("GET", "/files")

@mcp.tool(name="context_box_delete")
def context_box_delete(file_path: str) -> dict[str, Any]:
    """Delete a file from the Context-Box RAG system."""
    import urllib.parse
    return _context_request("DELETE", f"/files/{urllib.parse.quote(file_path, safe='')}")


# ── Batch RAG Processing Tools ─────────────────────────────────────

@mcp.tool(name="context_box_ingest_batch")
def context_box_ingest_batch(file_paths: list[str]) -> dict[str, Any]:
    """Ingest multiple files into Context-Box RAG system in batch mode.
    Files are queued and processed asynchronously by the worker pool.
    Returns immediately with job IDs for tracking progress.
    
    file_paths: List of absolute file paths accessible by the MCP server.
    """
    url = f"{CONTEXT_API_BASE_URL.rstrip('/')}/ingest/batch"
    
    import mimetypes
    files_to_send = []
    for fp in file_paths:
        if not os.path.exists(fp):
            raise ApiError(f"File not found: {fp}")
        mime_type, _ = mimetypes.guess_type(fp)
        mime_type = mime_type or 'application/octet-stream'
        files_to_send.append(
            ('files', (os.path.basename(fp), open(fp, 'rb'), mime_type))
        )
    
    try:
        with httpx.Client(timeout=TIMEOUT_SECONDS * 2) as client:
            response = client.post(url, files=files_to_send)
        
        if response.status_code >= 400:
            raise ApiError(f"Batch ingest failed ({response.status_code}): {response.text}")
        return response.json()
    finally:
        for _, (_, f, _) in files_to_send:
            f.close()


@mcp.tool(name="context_box_batch_status")
def context_box_batch_status(job_id: str = "") -> dict[str, Any]:
    """Get status of batch RAG processing jobs.
    
    If job_id is provided, returns status of that specific job.
    If empty, returns list of all recent jobs.
    """
    if job_id:
        return _context_request("GET", f"/ingest/jobs/{job_id}")
    return _context_request("GET", "/ingest/jobs")


@mcp.tool(name="context_box_batch_stats")
def context_box_batch_stats() -> dict[str, Any]:
    """Get aggregate statistics for the batch RAG processing pipeline.
    Returns: total_jobs, pending, processing, done, failed, total_chunks_processed, avg_processing_time_ms, workers_active, queue_depth.
    """
    return _context_request("GET", "/ingest/stats")


# ── MCP White Label Tools ──────────────────────────────────────────

MCP_WL_API_URL = os.getenv("PANDORA_MCP_WL_API_URL", "http://127.0.0.1:8480")

def _wl_request(method: str, path: str, *, params: dict[str, Any] | None = None, payload: dict[str, Any] | None = None) -> Any:
    """HTTP helper for White Label API calls."""
    url = f"{MCP_WL_API_URL.rstrip('/')}{path}"
    with httpx.Client(timeout=TIMEOUT_SECONDS) as client:
        response = client.request(method=method, url=url, params=params, json=payload)
    if response.status_code >= 400:
        try:
            data = response.json()
        except ValueError:
            data = {"error": response.text}
        raise ApiError(f"MCP White Label API request failed ({response.status_code}): {data}")
    if not response.content:
        return None
    return response.json()


@mcp.tool(name="mcp_wl_spawn")
def mcp_wl_spawn(box_id: str, box_name: str, api_key: str = "") -> dict[str, Any]:
    """Spawn a new MCP White Label server instance for a Box.
    Each Box gets its own isolated MCP server with dedicated tools and resources.
    
    box_id: The Box UUID.
    box_name: Human-readable name for the Box.
    api_key: Optional API key for authenticating the Box MCP.
    """
    payload: dict[str, Any] = {"box_name": box_name}
    if api_key:
        payload["api_key"] = api_key
    return _wl_request("POST", f"/api/boxes/{box_id}/mcp/spawn", payload=payload)


@mcp.tool(name="mcp_wl_stop")
def mcp_wl_stop(box_id: str) -> dict[str, Any]:
    """Stop the MCP White Label server instance for a Box.
    
    box_id: The Box UUID whose MCP should be stopped.
    """
    return _wl_request("DELETE", f"/api/boxes/{box_id}/mcp/stop")


@mcp.tool(name="mcp_wl_status")
def mcp_wl_status(box_id: str) -> dict[str, Any]:
    """Get status of a Box's MCP White Label instance.
    Returns: port, PID, endpoint, tools_count, resources_count, health.
    
    box_id: The Box UUID to check.
    """
    return _wl_request("GET", f"/api/boxes/{box_id}/mcp/status")


@mcp.tool(name="mcp_wl_registry")
def mcp_wl_registry() -> dict[str, Any]:
    """List all active MCP White Label instances across all Boxes.
    Returns instances with their status, ports, and health states.
    """
    return _wl_request("GET", "/api/mcp-registry")


@mcp.tool(name="mcp_wl_registry_stats")
def mcp_wl_registry_stats() -> dict[str, Any]:
    """Get aggregate statistics for the MCP White Label registry.
    Returns: total_instances, running, stopped, failed, unhealthy, ports_allocated.
    """
    return _wl_request("GET", "/api/mcp-registry/stats")


# ============================================================
# BOX1: Sandbox Engine MCP Tools (ST-08)
# ============================================================

SANDBOX_API_BASE = os.getenv("PANDORA_SANDBOX_API_BASE_URL", API_BASE_URL)


def _sandbox_request(method: str, path: str, *, payload: dict | None = None, params: dict | None = None) -> Any:
    """Route sandbox requests to the backend API."""
    url = f"{SANDBOX_API_BASE.rstrip('/')}{path}"
    with httpx.Client(timeout=TIMEOUT_SECONDS) as client:
        response = client.request(method=method, url=url, params=params, json=payload)
    if response.status_code >= 400:
        try:
            data = response.json()
        except ValueError:
            data = {"error": response.text}
        raise ApiError(f"Sandbox API request failed ({response.status_code}): {data}")
    if not response.content:
        return None
    return response.json()


@mcp.tool(name="sandbox_create")
def sandbox_create(
    box_id: str,
    image: str,
    cpu: float = 2.0,
    mem: int = 512,
    timeout: int = 30,
    network_mode: str = "Restricted",
    task_id: str | None = None,
    work_dir: str = "/app",
) -> dict[str, Any]:
    """ST-08: Create a sandboxed Docker container for a Box.

    image       — alias ('node', 'python', 'dotnet') or full image name
    cpu         — CPU cores limit (0.5–4, default 2)
    mem         — RAM limit in MB (128–2048, default 512)
    timeout     — TTL in minutes before auto-destroy (1–120, default 30)
    network_mode — 'Restricted' (whitelist), 'Offline' (no network), 'Full'
    task_id     — optional work item ID that triggered this sandbox
    work_dir    — working directory inside container (default /app)

    Returns: sandbox entity with id, status, containerId
    """
    network_map = {"restricted": 0, "offline": 1, "full": 2}
    network_int = network_map.get(network_mode.lower(), 0)

    return _sandbox_request(
        "POST",
        "/api/sandbox",
        payload={
            "boxId": box_id,
            "imageName": image,
            "cpuCores": cpu,
            "memoryMb": mem,
            "timeoutMinutes": timeout,
            "networkMode": network_int,
            "taskId": task_id,
            "workDir": work_dir,
        },
    )


@mcp.tool(name="sandbox_start")
def sandbox_start(sandbox_id: str) -> dict[str, Any]:
    """ST-08: Start a created sandbox container (transitions Creating → Running)."""
    return _sandbox_request("POST", f"/api/sandbox/{sandbox_id}/start")


@mcp.tool(name="sandbox_exec")
def sandbox_exec(
    sandbox_id: str,
    command: str,
    work_dir: str | None = None,
    timeout_seconds: int = 60,
) -> dict[str, Any]:
    """ST-08: Execute a shell command inside a running sandbox.

    Returns: {exitCode, stdout, stderr, durationMs}
    """
    payload: dict[str, Any] = {
        "command": command,
        "timeoutSeconds": timeout_seconds,
    }
    if work_dir:
        payload["workDir"] = work_dir

    return _sandbox_request("POST", f"/api/sandbox/{sandbox_id}/exec", payload=payload)


@mcp.tool(name="sandbox_status")
def sandbox_status(sandbox_id: str) -> dict[str, Any]:
    """ST-08: Get status and metadata of a sandbox container.

    Returns: {id, status, imageName, cpuCores, memoryMb, timeoutAt, createdAt, ...}
    """
    result = _sandbox_request("GET", f"/api/sandbox/{sandbox_id}")
    if result is None:
        raise ApiError(f"Sandbox {sandbox_id} not found.")
    return result


@mcp.tool(name="sandbox_stop")
def sandbox_stop(sandbox_id: str) -> dict[str, Any]:
    """ST-08: Gracefully stop a running sandbox (Running → Stopped)."""
    return _sandbox_request("POST", f"/api/sandbox/{sandbox_id}/stop")


@mcp.tool(name="sandbox_destroy")
def sandbox_destroy(sandbox_id: str) -> dict[str, Any]:
    """ST-08: Force-remove a sandbox container and mark it as Destroyed."""
    _sandbox_request("DELETE", f"/api/sandbox/{sandbox_id}")
    return {"sandboxId": sandbox_id, "destroyed": True}


@mcp.tool(name="sandbox_list")
def sandbox_list(box_id: str) -> list[dict[str, Any]]:
    """ST-08: List all sandboxes for a given Box."""
    return _sandbox_request("GET", "/api/sandbox", params={"boxId": box_id}) or []


@mcp.tool(name="sandbox_stats")
def sandbox_stats() -> dict[str, Any]:
    """ST-08: Get aggregate sandbox statistics (total active, by image, by status, avg lifetime)."""
    return _sandbox_request("GET", "/api/sandbox/stats")


# ============================================================
# BOX4: OpenClaw Gateway MCP Tools (ST-46)
# ============================================================


@mcp.tool(name="channel_send")
def channel_send(
    channel_type: str,
    recipient_id: str,
    message: str,
    box_id: str | None = None,
) -> dict[str, Any]:
    """ST-46: Send a message to a recipient on a messaging channel.

    channel_type — 'whatsapp' | 'slack' | 'telegram'
    recipient_id — phone number, Slack user ID, or Telegram chat ID
    message      — text to send (plain text)
    box_id       — optional box context (informational)

    Returns: {delivery_id, channel, recipient_id, sent_at}
    """
    result = _request(
        "POST",
        "/api/openclaw/send",
        payload={
            "channel": channel_type,
            "recipientId": recipient_id,
            "message": message,
        },
    )
    return {
        "delivery_id": result.get("deliveryId") if isinstance(result, dict) else None,
        "channel": channel_type,
        "recipient_id": recipient_id,
        "box_id": box_id,
        "sent": True,
    }


@mcp.tool(name="channel_status")
def channel_status() -> list[dict[str, Any]]:
    """ST-46: List connection status of all configured OpenClaw channels.

    Returns list of: {channel, connected, last_msg_at}
    """
    result = _request("GET", "/api/openclaw/channels")
    if isinstance(result, list):
        return [
            {
                "channel": ch.get("channel"),
                "connected": ch.get("connected"),
                "last_msg_at": ch.get("lastMessageAt"),
            }
            for ch in result
        ]
    return []


@mcp.tool(name="openclaw_register_user")
def openclaw_register_user(
    channel_type: str,
    external_id: str,
    box_id: str,
) -> dict[str, Any]:
    """ST-46: Register a channel user to a Briefapp Box for message routing.

    channel_type — 'whatsapp' | 'slack' | 'telegram'
    external_id  — user's external identifier (phone number, Slack user ID, Telegram chat ID)
    box_id       — Briefapp Box (Project) UUID to route messages to

    Returns: {id, box_id, channel, external_id, registered_at}
    """
    return _request(
        "POST",
        "/api/openclaw/register",
        payload={
            "channelType": channel_type,
            "externalId": external_id,
            "boxId": box_id,
        },
    )


@mcp.tool(name="openclaw_inbound_stats")
def openclaw_inbound_stats(box_id: str | None = None) -> dict[str, Any]:
    """ST-46: Get OpenClaw inbound statistics — registered users by channel and box.

    box_id — optional: filter stats to a specific Box
    """
    params = {"boxId": box_id} if box_id else {}
    stats = _request("GET", "/api/openclaw/stats", params=params or None)
    users = _request("GET", "/api/openclaw/users", params=params or None)

    return {
        "registered_users": stats.get("registeredUsers") if isinstance(stats, dict) else 0,
        "by_channel": stats.get("byChannel") if isinstance(stats, dict) else [],
        "users": users if isinstance(users, list) else [],
    }


@mcp.tool(name="openclaw_list_users")
def openclaw_list_users(box_id: str | None = None) -> list[dict[str, Any]]:
    """ST-46: List all registered channel users, optionally filtered by Box.

    Returns list of: {id, box_id, channel, external_id, registered_at}
    """
    params = {"boxId": box_id} if box_id else None
    result = _request("GET", "/api/openclaw/users", params=params)
    return result if isinstance(result, list) else []




# ============================================================
# BOX2: Transactional Queue & Lock Protocol MCP Tools (ST-21/28)
# ============================================================


@mcp.tool(name="task_publish")
def task_publish(
    box_id: str,
    payload: dict,
    source: str = "mcp",
    work_item_id: str | None = None,
    max_retries: int = 3,
) -> dict[str, Any]:
    """ST-21: Publish a task to the Box queue (Tansu.io topic briefapp.{boxId}.tasks).

    box_id       — UUID of the target Box
    payload      — task data dict (will be JSON-serialized)
    source       — origin label, e.g. 'mcp', 'api', 'openclaw'
    work_item_id — optional Briefapp work item ID this task is linked to
    max_retries  — max retry attempts before DLQ (default 3)

    Returns: {id, boxId, topic, status, createdAt}
    """
    body: dict[str, Any] = {
        "boxId": box_id,
        "payload": payload,
        "source": source,
        "maxRetries": max_retries,
    }
    if work_item_id:
        body["workItemId"] = work_item_id
    return _request("POST", "/api/tasks", payload=body)


@mcp.tool(name="queue_status")
def queue_status(box_id: str | None = None) -> list[dict[str, Any]]:
    """ST-21: Get queue statistics (pending, processing, completed) per Tansu topic.

    box_id — optional Box UUID to filter; omit for all topics.
    Returns: [{topic, pending, processing, completed}]
    """
    params = {"boxId": box_id} if box_id else None
    result = _request("GET", "/api/tasks/status", params=params)
    return result if isinstance(result, list) else []


@mcp.tool(name="task_lock")
def task_lock(
    task_id: str,
    worker_id: str,
    timeout_minutes: int = 30,
) -> dict[str, Any]:
    """ST-28: Acquire a distributed lock on a task (pessimistic locking).

    task_id         — UUID string of the task to lock
    worker_id       — unique identifier of the agent/worker acquiring the lock
    timeout_minutes — lock TTL in minutes (default 30); use heartbeat to renew

    Returns: {lockId, acquiredAt, expiresAt} or raises on conflict.
    """
    return _request("POST", "/api/locks", payload={
        "taskId": task_id,
        "workerId": worker_id,
        "timeoutMinutes": timeout_minutes,
    })


@mcp.tool(name="task_heartbeat")
def task_heartbeat(lock_id: str, worker_id: str) -> dict[str, Any]:
    """ST-28: Renew a task lock to prevent expiry.

    Call every ~60s while working. Returns {success, newExpiresAt}.
    """
    return _request("POST", f"/api/locks/{lock_id}/heartbeat", payload={"workerId": worker_id})


@mcp.tool(name="task_unlock")
def task_unlock(lock_id: str, worker_id: str = "", force: bool = False) -> dict[str, Any]:
    """ST-28: Release a task lock after completing or abandoning a task.

    lock_id   — UUID of the lock to release
    worker_id — must match the lock owner (unless force=True for admin)
    force     — admin bypass (releases any lock regardless of owner)
    """
    params: dict[str, Any] = {}
    if force:
        params["force"] = "true"
    elif worker_id:
        params["workerId"] = worker_id
    _request("DELETE", f"/api/locks/{lock_id}", params=params or None)
    return {"lockId": lock_id, "released": True}


@mcp.tool(name="lock_status")
def lock_status(task_id: str) -> dict[str, Any]:
    """ST-28: Check if a task is currently locked and by whom.

    Returns: {locked, lockId?, lockedBy?, expiresAt?, heartbeatAgeSeconds?}
    """
    result = _request("GET", f"/api/locks/{task_id}")
    if result is None:
        return {"locked": False}
    return result


@mcp.tool(name="task_ack")
def task_ack(
    task_id: str,
    worker_id: str,
    commit_hash: str | None = None,
    files_changed: int | None = None,
    tests_passed: bool | None = None,
    tokens_used: int | None = None,
    duration_ms: int | None = None,
    model_used: str | None = None,
) -> dict[str, Any]:
    """ST-30: Send ACK (success) for a completed task.

    Returns: {success, action, retryCount}
    """
    result_data: dict[str, Any] = {}
    metrics_data: dict[str, Any] = {}

    if commit_hash: result_data["commitHash"] = commit_hash
    if files_changed is not None: result_data["filesChanged"] = files_changed
    if tests_passed is not None: result_data["testsPassed"] = tests_passed
    if tokens_used is not None: metrics_data["tokensUsed"] = tokens_used
    if duration_ms is not None: metrics_data["durationMs"] = duration_ms
    if model_used: metrics_data["modelUsed"] = model_used

    payload: dict[str, Any] = {
        "taskId": task_id,
        "workerId": worker_id,
        "status": "ack",
    }
    if result_data: payload["result"] = result_data
    if metrics_data: payload["metrics"] = metrics_data

    return _request("POST", f"/api/tasks/{task_id}/ack", payload=payload)


@mcp.tool(name="task_nack")
def task_nack(
    task_id: str,
    worker_id: str,
    category: str = "unknown",
    message: str | None = None,
    stack_trace: str | None = None,
    retry_hint: str | None = None,
) -> dict[str, Any]:
    """ST-30: Send NACK (failure) for a task. Triggers retry or DLQ routing.

    category — one of: compilation_error, test_failure, api_timeout,
               hallucination, dependency_error, resource_exhaustion, unknown
    Returns: {success, action, retryCount, message}
    """
    category_map = {
        "compilation_error": 0, "test_failure": 1, "api_timeout": 2,
        "hallucination": 3, "dependency_error": 4, "resource_exhaustion": 5,
        "unknown": 99,
    }
    error_data: dict[str, Any] = {"category": category_map.get(category.lower(), 99)}
    if message: error_data["message"] = message
    if stack_trace: error_data["stackTrace"] = stack_trace
    if retry_hint: error_data["retryHint"] = retry_hint

    return _request("POST", f"/api/tasks/{task_id}/ack", payload={
        "taskId": task_id,
        "workerId": worker_id,
        "status": "nack",
        "error": error_data,
    })



# ============================================================
# BOX1-02: File System, Network Policy & Metrics MCP Tools (ST-35)
# ============================================================


@mcp.tool(name="sandbox_workspace_prepare")
def sandbox_workspace_prepare(
    sandbox_id: str,
    git_repo_url: str,
    branch: str = "main",
) -> dict[str, Any]:
    """ST-35: Clone a git repo into a sandbox workspace (OverlayFS copy-on-write).

    sandbox_id   — UUID of an existing Running sandbox
    git_repo_url — HTTPS or SSH URL of the repository to clone
    branch       — git branch to checkout (default: 'main')

    Returns: {sandboxId, taskId, hostPath, containerPath, gitBranch, createdAt}
    Raises on clone failure or if symlinks are detected (security policy).
    """
    return _request("POST", f"/api/sandbox/{sandbox_id}/workspace", payload={
        "gitRepoUrl": git_repo_url,
        "branch": branch,
    })


@mcp.tool(name="sandbox_workspace_cleanup")
def sandbox_workspace_cleanup(sandbox_id: str) -> dict[str, Any]:
    """ST-35: Remove the workspace directory for a sandbox.

    Call after sandbox_destroy or when workspace is no longer needed.
    Returns: {deleted: true}
    """
    _request("DELETE", f"/api/sandbox/{sandbox_id}/workspace")
    return {"sandboxId": sandbox_id, "deleted": True}


@mcp.tool(name="sandbox_metrics")
def sandbox_metrics(sandbox_id: str, limit: int = 10) -> list[dict[str, Any]]:
    """ST-35: Get resource utilization metrics for a sandbox.

    Returns the last {limit} snapshots (collected every 30s):
    [{cpuPercent, memoryMb, memoryPercent, networkRxBytes, networkTxBytes,
      diskReadBytes, diskWriteBytes, uptimeSeconds, capturedAt}]
    """
    result = _request("GET", f"/api/sandbox/{sandbox_id}/metrics", params={"limit": limit})
    return result if isinstance(result, list) else []


@mcp.tool(name="sandbox_network_info")
def sandbox_network_info() -> dict[str, Any]:
    """ST-35: Get available network isolation policies for sandboxes.

    Returns: {policies, allowedDomains, restrictedNetworkName}
    - policies: ['Offline', 'Restricted', 'Full']
    - allowedDomains: list of whitelisted package registry domains
    """
    return _request("GET", "/api/sandbox/networks") or {}


# ============================================================
# BOX2-02: Dead Letter Queue & Dashboard MCP Tools (ST-38/47)
# ============================================================


@mcp.tool(name="dlq_list")
def dlq_list(
    box_id: str | None = None,
    status: str | None = None,
    page: int = 1,
    size: int = 20,
) -> dict[str, Any]:
    """ST-38: List Dead Letter Queue entries (paged).

    status — one of: Pending, Retrying, Resolved, Quarantined (or omit for all)
    Returns: {page, pageSize, totalCount, items[{id, boxId, failureReason, retryCount, status, ...}]}
    """
    params: dict[str, Any] = {"page": page, "size": size}
    if box_id:  params["boxId"]  = box_id
    if status:  params["status"] = status
    return _request("GET", "/api/queue/dlq", params=params) or {}


@mcp.tool(name="dlq_retry")
def dlq_retry(dlq_id: str) -> dict[str, Any]:
    """ST-38: Retry a single DLQ entry — resubmits to original Tansu topic.

    dlq_id — UUID of the DLQ entry to retry
    Returns: {dlqId, status: 'Retrying'} or error if quarantined.
    """
    return _request("POST", f"/api/queue/dlq/{dlq_id}/retry") or {}


@mcp.tool(name="dlq_quarantine")
def dlq_quarantine(dlq_id: str) -> dict[str, Any]:
    """ST-38: Quarantine a DLQ entry — marks as poison message, prevents retry.

    dlq_id — UUID of the DLQ entry to quarantine
    """
    return _request("POST", f"/api/queue/dlq/{dlq_id}/quarantine") or {}


@mcp.tool(name="dlq_drain")
def dlq_drain(box_id: str | None = None) -> dict[str, Any]:
    """ST-38: Drain DLQ — retry all Pending entries.

    box_id — optional: filter to a specific Box (or omit for all boxes)
    Returns: {resubmitted: N}
    """
    params = {"boxId": box_id} if box_id else {}
    return _request("POST", "/api/queue/dlq/drain", params=params) or {}


@mcp.tool(name="dlq_stats")
def dlq_stats(box_id: str | None = None) -> dict[str, Any]:
    """ST-38: Get DLQ statistics.

    Returns: {total, pending, retrying, resolved, quarantined, oldestEntryAge}
    """
    params = {"boxId": box_id} if box_id else {}
    return _request("GET", "/api/queue/dlq/stats", params=params) or {}


@mcp.tool(name="queue_dashboard")
def queue_dashboard(box_id: str | None = None) -> dict[str, Any]:
    """ST-47: Get live queue dashboard metrics (snapshot).

    Returns: {pendingCount, processingCount, completedToday, failedToday,
              dlqSize, avgProcessingMs, throughputPerMin, activeLocks,
              boxStats[{boxId, pending, processing}], capturedAt}
    """
    params = {"boxId": box_id} if box_id else {}
    return _request("GET", "/api/queue/stats", params=params) or {}



# ── BOX3: Circuit Breaker MCP Tools (ST-52) ───────────────────────────────────

@mcp.tool(name="circuit_breaker_status")
def circuit_breaker_status(box_id: str) -> dict[str, Any]:
    """ST-52: Get the current state of a circuit breaker for a specific Box.

    Returns: {boxId, state, stateLabel, failureCount, failureThreshold,
              cooldownSeconds, halfOpenMaxCalls, halfOpenCallCount,
              trippedAt, lastFailureAt, lastTransitionAt, cooldownExpired}
    """
    return _request("GET", f"/api/breaker/{box_id}")


@mcp.tool(name="circuit_breaker_all")
def circuit_breaker_all() -> list[dict[str, Any]]:
    """ST-52: Get circuit breaker status for all known Boxes.

    Returns a list of circuit breaker snapshots.
    """
    return _request("GET", "/api/breaker/all") or []


@mcp.tool(name="circuit_breaker_reset")
def circuit_breaker_reset(box_id: str) -> dict[str, Any]:
    """ST-52: Manually reset a circuit breaker to Closed state.

    Use this when you are certain the underlying failure has been resolved
    and want to restore normal operation without waiting for the cooldown.
    Returns the updated circuit breaker snapshot.
    """
    return _request("POST", f"/api/breaker/{box_id}/reset")


@mcp.tool(name="circuit_breaker_config")
def circuit_breaker_config(
    box_id: str,
    failure_threshold: int | None = None,
    cooldown_seconds: int | None = None,
    half_open_max_calls: int | None = None,
) -> dict[str, Any]:
    """ST-52: Update per-Box circuit breaker configuration.

    failure_threshold    — consecutive failures before tripping (default: 3)
    cooldown_seconds     — seconds to stay Open before probing (default: 300)
    half_open_max_calls  — probe requests allowed in Half-Open (default: 1)

    Only supplied fields are changed. Returns the updated snapshot.
    """
    payload: dict[str, Any] = {}
    if failure_threshold is not None:
        payload["failureThreshold"] = failure_threshold
    if cooldown_seconds is not None:
        payload["cooldownSeconds"] = cooldown_seconds
    if half_open_max_calls is not None:
        payload["halfOpenMaxCalls"] = half_open_max_calls
    if not payload:
        raise ApiError("At least one config field must be provided (failure_threshold, cooldown_seconds or half_open_max_calls).")
    return _request("POST", f"/api/breaker/{box_id}/config", payload=payload)


@mcp.tool(name="circuit_breaker_history")
def circuit_breaker_history(box_id: str, limit: int = 50) -> list[dict[str, Any]]:
    """ST-52: Get the FSM transition history for a circuit breaker.

    limit — max number of transitions to return (default: 50)
    Returns a list of {id, boxId, fromState, toState, category, reason, triggeredAt}.
    """
    return _request("GET", f"/api/breaker/{box_id}/history", params={"limit": limit}) or []


# ── BOX5: Prompy Cache ─────────────────────────────────────────────────────────

@mcp.tool(name="prompt_cache_stats")
def prompt_cache_stats(box_id: str | None = None) -> dict[str, Any]:
    """ST-91: Get cache hit/miss stats. If box_id is provided, gets stats for that specific agent box."""
    if box_id:
        return _request("GET", f"/api/prompt-cache/{box_id}/stats")
    return _request("GET", "/api/prompt-cache/stats")


@mcp.tool(name="prompt_cache_warm")
def prompt_cache_warm(box_id: str) -> dict[str, Any]:
    """ST-91: Triggers a warmup of all cacheable segments for a specific agent box."""
    return _request("POST", f"/api/prompt-cache/{box_id}/warm")


@mcp.tool(name="prompt_cache_clear")
def prompt_cache_clear(box_id: str, segment_type: str | None = None) -> dict[str, Any]:
    """ST-91: Invalidates the cache for a specific agent box. If segment_type is provided, invalidates only that segment."""
    params = {}
    if segment_type:
        params["segmentType"] = segment_type
    return _request("POST", f"/api/prompt-cache/{box_id}/invalidate", params=params)


@mcp.tool(name="prompt_cache_configure")
def prompt_cache_configure(box_id: str, segment_type: str, content: str) -> dict[str, Any]:
    """ST-91: Upserts a cacheable segment (SystemPrompt, ToolDefinitions, or ProjectContext) for an agent box."""
    return _request("PUT", f"/api/prompt-cache/{box_id}/segments/{segment_type}", payload={"content": content})


@mcp.tool(name="prompt_cache_get_prefix")
def prompt_cache_get_prefix(box_id: str) -> list[dict[str, Any]]:
    """ST-91: Gets all cacheable segments for a specific agent box."""
    return _request("GET", f"/api/prompt-cache/{box_id}/segments") or []


@mcp.tool(name="gemini_acp_steer")
def gemini_acp_steer(project_id: str, message: str, severity: str = "info") -> dict[str, Any]:
    """Send an ACP (Agent Control Protocol) steering message to the active Gemini CLI subagent for a project.
    
    project_id: The UUID of the project
    message: The steering instruction or feedback
    severity: 'info', 'warning', or 'error'
    """
    projects = _request("GET", "/api/projects", params={"includeArchived": False})
    project = next((p for p in projects if str(p.get("id")) == project_id), None)
    if not project or not project.get("localPath"):
        raise ApiError(f"Project {project_id} not found or localPath not configured.")
        
    steering_file = os.path.join(project.get("localPath"), ".gemini", "steering.jsonl")
    os.makedirs(os.path.dirname(steering_file), exist_ok=True)
    
    payload = {
        "timestamp": date.today().isoformat(),
        "type": "steering",
        "severity": severity,
        "message": message
    }
    
    with open(steering_file, "a", encoding="utf-8") as f:
        f.write(json.dumps(payload) + "\\n")
        
    return {"status": "Steering message delivered via steering.jsonl", "payload": payload}

# ── BOX6: Agentic Planner Tools ───────────────────────────────────────────────

@mcp.tool(name="agent_planner_generate")
def agent_planner_generate(
    project_id: str,
    user_prompt: str,
    project_context: str = "",
    complexity_multiplier: float = 1.0
) -> dict[str, Any]:
    """Trigger the Planner Agent to generate an Atomic Flow plan for the project.
    
    This will evaluate the user_prompt, project_id, and generate a structured JSON payload
    containing the proposed backlogs, wiki, docs, and checkpoint.
    """
    url = f"{AGENT_PLANNER_URL.rstrip('/')}/api/agent/plan/stream"
    payload = {
        "project_id": project_id,
        "order": user_prompt,
        "complexity_multiplier": complexity_multiplier
    }
    events = _request_sse("POST", url, payload=payload)
    
    # We want to extract the final status and the generated markdown text.
    # The events stream streams dicts with 'text' and finally 'done' and 'meta'
    full_text = ""
    for ev in events:
        if "text" in ev:
            full_text += ev["text"]
        if ev.get("meta", {}).get("status") == "complete":
            break
            
    # Try to extract the JSON payload
    json_payload = {}
    import re
    # We'll use the robust search
    start_idx = full_text.find("```json_payload")
    if start_idx != -1:
        after_start = full_text[start_idx + len("```json_payload"):]
        end_idx = after_start.rfind("```")
        raw_json = after_start[:end_idx].strip() if end_idx != -1 else after_start.strip()
        try:
            json_payload = json.loads(raw_json)
        except Exception:
            pass
            
    return {
        "status": "success",
        "events_count": len(events),
        "plan_text": full_text[:1000] + "... (truncated)",
        "plan_payload": json_payload
    }


@mcp.tool(name="agent_planner_execute")
def agent_planner_execute(
    project_id: str,
    plan_payload: dict[str, Any],
    complexity_multiplier: float = 1.0
) -> dict[str, Any]:
    """Execute/Materialize a previously generated Atomic Flow plan.
    
    The plan_payload should be the dictionary generated by agent_planner_generate.
    This will create all backlogs, loop through sprints/tasks/subtasks, and add wikis/docs.
    """
    url = f"{AGENT_PLANNER_URL.rstrip('/')}/api/agent/execute"
    payload = {
        "project_id": project_id,
        "plan_payload": plan_payload,
        "complexity_multiplier": complexity_multiplier
    }
    events = _request_sse("POST", url, payload=payload)
    
    final_results = {}
    for ev in events:
        if ev.get("done") and "results" in ev:
            final_results = ev["results"]
            
    return {
        "status": "executed",
        "events_streamed": len(events),
        "results": final_results
    }


# ─────────────────────────────────────────────────────────────────────────────
# Knowledge Graph Tools (Rastreabilidade 100%)
# ─────────────────────────────────────────────────────────────────────────────

@mcp.tool(name="graph_node_upsert")
def graph_node_upsert(
    project_id: str,
    node_type: str,
    external_id: str,
    label: str,
    properties: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """Cria ou atualiza um nó no grafo de rastreabilidade do projeto.

    node_type — tipo do nó: task | commit | file | business_rule |
                acceptance_criteria | sprint | backlog | agent
    external_id — identificador único e estável do nó (UUID para tasks,
                  hash para commits, caminho relativo para files)
    label       — texto legível exibido na visualização do grafo
    properties  — dict com metadados adicionais (status, branch, author, etc.)

    Upsert baseado em (project_id, node_type, external_id).
    Retorna o nó criado/atualizado com seu Id UUID.
    """
    payload: dict[str, Any] = {
        "nodeType": node_type,
        "externalId": external_id,
        "label": label,
    }
    if properties:
        payload["properties"] = properties
    return _request(
        "POST",
        f"/api/projects/{project_id}/graph/nodes",
        payload=payload,
    )


@mcp.tool(name="graph_edge_upsert")
def graph_edge_upsert(
    project_id: str,
    source_node_id: str,
    target_node_id: str,
    edge_type: str,
    weight: float = 1.0,
    metadata: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """Cria ou atualiza uma aresta direcional entre dois nós do grafo.

    source_node_id — Id UUID do nó de origem (obtido via graph_node_upsert)
    target_node_id — Id UUID do nó de destino
    edge_type      — tipo da aresta: implements | satisfies | produced |
                     modifies | belongs_to | executed_by | depends_on |
                     references | related_to
    weight         — relevância/frequência da relação (padrão 1.0)
    metadata       — dict com metadados adicionais da aresta

    Upsert baseado em (project_id, source_node_id, target_node_id, edge_type).
    """
    payload: dict[str, Any] = {
        "sourceNodeId": source_node_id,
        "targetNodeId": target_node_id,
        "edgeType": edge_type,
        "weight": weight,
    }
    if metadata:
        payload["metadata"] = metadata
    return _request(
        "POST",
        f"/api/projects/{project_id}/graph/edges",
        payload=payload,
    )


@mcp.tool(name="graph_get_task")
def graph_get_task(project_id: str, work_item_id: str) -> dict[str, Any]:
    """Retorna o grafo de rastreabilidade centrado em uma task específica.

    Inclui todos os nós conectados (commits, arquivos, regras de negócio,
    requisitos de aceite, agentes) e suas arestas — até profundidade 2.

    Ideal para: visualização ao clicar no ícone de grafo de uma task.

    Retorna: { nodes: [...], edges: [...] }
    """
    return _request("GET", f"/api/projects/{project_id}/graph/task/{work_item_id}")


@mcp.tool(name="graph_get_backlog")
def graph_get_backlog(project_id: str, backlog_item_id: str) -> dict[str, Any]:
    """Retorna a visão macro do grafo de rastreabilidade de um backlog item.

    Inclui todos os grafos de tasks do backlog interconectados, mostrando
    como commits, arquivos, regras de negócio e agentes se relacionam
    em nível de backlog/sprint completo.

    Ideal para: visualização ao clicar no ícone de grafo de um backlog item.

    Retorna: { nodes: [...], edges: [...] }
    """
    return _request("GET", f"/api/projects/{project_id}/graph/backlog/{backlog_item_id}")


# ============================================================
# BOX5: Connectors - Google Drive & OneDrive
# ============================================================

@mcp.tool(name="gdrive_list_files")
def gdrive_list_files(folder_id: str | None = None, page_size: int = 10) -> list[dict[str, Any]]:
    """Lista arquivos do Google Drive. Requer credentials.json na raiz do projeto."""
    return list_gdrive_files(folder_id=folder_id, page_size=page_size)


@mcp.tool(name="onedrive_list_files")
def onedrive_list_files_tool(folder_id: str | None = None) -> list[dict[str, Any]]:
    """Lista arquivos do OneDrive via Microsoft Graph. Requer ONEDRIVE_CLIENT_ID configurado."""
    return list_onedrive_files(folder_id=folder_id)


@mcp.tool(name="connector_auth_status")
def connector_auth_status() -> dict[str, Any]:
    """Verifica o status de autenticação dos conectores externos."""
    gdrive_token = os.path.exists(".token_cache/token.json")
    onedrive_token = os.path.exists(".onedrive_token.json")
    return {
        "gdrive": {
            "authenticated": gdrive_token,
            "setup_ready": os.path.exists("credentials.json") or os.path.exists("../credentials.json")
        },
        "onedrive": {
            "authenticated": onedrive_token,
            "setup_ready": bool(os.getenv("ONEDRIVE_CLIENT_ID"))
        }
    }


if __name__ == "__main__":
    import logging
    import uvicorn
    from starlette.applications import Starlette
    from starlette.routing import Mount

    logger = logging.getLogger("briefapp.mcp")

    # ── Build combined app: /sse (legacy) + /mcp (Streamable HTTP) ──────────────
    # streamable_http_app() has its own lifespan that initializes the task group.
    # We must pass that lifespan to the combined app so /mcp works correctly.

    sse_app = mcp.sse_app(mount_path=MCP_MOUNT_PATH)

    try:
        streamable_app = mcp.streamable_http_app()
        # Merge routes from both apps; use streamable's lifespan for task group init
        combined_routes = list(streamable_app.routes) + list(sse_app.routes)
        combined = Starlette(
            routes=combined_routes,
            lifespan=streamable_app.router.lifespan_context,
        )
        logger.info("Starting with DUAL transport: /sse (SSE) + /mcp (Streamable HTTP)")
    except AttributeError:
        combined = sse_app
        logger.warning("streamable_http_app() not available — running SSE only (/sse)")

    uvicorn.run(
        combined,
        host=MCP_HOST,
        port=MCP_PORT,
        log_level="info",
    )



