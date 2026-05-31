"""
Unit tests for server.py MCP tools — token tracking and status normalization.
"""
import inspect
import json
import pytest
from unittest.mock import patch, MagicMock

from server import (
    _normalize_workitem_status,
    _json_resource,
    _flatten_workitems,
    workitem_update,
    project_list,
    project_create,
    project_config_update,
    project_delete,
    backlog_add,
    backlog_list,
    sprint_create,
    workitem_list,
    knowledge_checkpoint,
    knowledge_list,
    wiki_add,
    wiki_list,
    documentation_add,
    documentation_list,
    checkpoint_list,
    ApiError,
    resource_about,
    resource_projects_active,
    resource_projects_all,
    resource_project_config,
    resource_project_dashboard,
    resource_project_backlog,
    resource_project_sprints,
    resource_project_workitems,
    resource_project_workitems_by_status,
    resource_project_sprint_workitems,
    resource_project_knowledge,
    resource_project_tasks_overview,
    resource_project_tasks_triage,
    resource_project_context,
    briefapp_project_config,
    briefapp_project_create,
    briefapp_sprint_create,
    briefapp_resources_guide,
)


# ---------------------------------------------------------------------------
# _normalize_workitem_status
# ---------------------------------------------------------------------------

class TestNormalizeWorkitemStatus:
    def test_string_labels(self):
        assert _normalize_workitem_status("todo") == 0
        assert _normalize_workitem_status("in_progress") == 1
        assert _normalize_workitem_status("in-progress") == 1
        assert _normalize_workitem_status("in progress") == 1
        assert _normalize_workitem_status("inprogress") == 1
        assert _normalize_workitem_status("doing") == 1
        assert _normalize_workitem_status("review") == 2
        assert _normalize_workitem_status("qa") == 2
        assert _normalize_workitem_status("done") == 3
        assert _normalize_workitem_status("completed") == 3
        assert _normalize_workitem_status("blocked") == 4
        assert _normalize_workitem_status("impediment") == 4

    def test_integer_strings(self):
        for i, expected in enumerate([0, 1, 2, 3, 4]):
            assert _normalize_workitem_status(str(i)) == expected

    def test_case_insensitive(self):
        assert _normalize_workitem_status("TODO") == 0
        assert _normalize_workitem_status("DONE") == 3
        assert _normalize_workitem_status("Review") == 2

    def test_whitespace_trimmed(self):
        assert _normalize_workitem_status("  done  ") == 3

    def test_invalid_raises(self):
        with pytest.raises(ApiError):
            _normalize_workitem_status("invalid-status")

    def test_invalid_number_raises(self):
        with pytest.raises(ApiError):
            _normalize_workitem_status("99")


# ---------------------------------------------------------------------------
# workitem_update — tokens_used optional / anti-hallucination
# ---------------------------------------------------------------------------

class TestWorkitemUpdateTokens:
    """Verify that tokens_used is optional and defaults to None (not a guess)."""

    def test_tokens_used_defaults_to_none(self):
        """tokens_used must default to None so agents are NOT forced to fabricate a value."""
        sig = inspect.signature(workitem_update)
        default = sig.parameters["tokens_used"].default
        assert default is None, (
            "tokens_used must default to None, not 0 or any other value, "
            "to prevent agents from being prompted to estimate/hallucinate."
        )

    def test_docstring_anti_hallucination_guidance(self):
        """The tool docstring must contain explicit guidance against estimating tokens."""
        doc = workitem_update.__doc__ or ""
        assert "NOT" in doc.upper() or "do not" in doc.lower(), (
            "workitem_update docstring must explicitly warn agents NOT to estimate token counts."
        )

    def test_zero_tokens_accepted(self):
        """tokens_used=0 (unknown/not tracked) must be accepted without error."""
        mock_result = {"id": "wi-1", "status": 3}
        with patch("server._request", return_value=mock_result) as mock_req:
            result = workitem_update(
                work_item_id="wi-1",
                status="done",
                assignee="copilot",
                tokens_used=0,
            )
        payload = mock_req.call_args[1]["payload"]
        assert payload["tokensUsed"] == 0

    def test_none_tokens_sends_zero_to_backend(self):
        """When tokens_used is None (omitted), backend receives 0 — never a made-up value."""
        mock_result = {"id": "wi-1", "status": 3}
        with patch("server._request", return_value=mock_result) as mock_req:
            result = workitem_update(
                work_item_id="wi-1",
                status="done",
                assignee="copilot",
                tokens_used=None,
            )
        payload = mock_req.call_args[1]["payload"]
        assert payload["tokensUsed"] == 0, (
            "None tokens_used must map to 0 in backend payload, not a guessed value."
        )

    def test_tokens_tracked_false_when_none(self):
        """Response must include tokensTracked=False when no tokens were provided."""
        mock_result = {"id": "wi-1", "status": 3}
        with patch("server._request", return_value=mock_result):
            result = workitem_update(
                work_item_id="wi-1",
                status="done",
                assignee="copilot",
                tokens_used=None,
            )
        assert result.get("tokensTracked") is False

    def test_tokens_tracked_true_when_provided(self):
        """Response must include tokensTracked=True when actual token count is passed."""
        mock_result = {"id": "wi-1", "status": 3}
        with patch("server._request", return_value=mock_result):
            result = workitem_update(
                work_item_id="wi-1",
                status="done",
                assignee="copilot",
                tokens_used=2487,
            )
        assert result.get("tokensTracked") is True

    def test_status_label_echoed(self):
        """Response must always include statusLabel for agent self-verification."""
        mock_result = {"id": "wi-1", "status": 3}
        with patch("server._request", return_value=mock_result):
            result = workitem_update(
                work_item_id="wi-1",
                status="done",
                assignee="copilot",
            )
        assert result.get("statusLabel") == "Done"


# ---------------------------------------------------------------------------
# _json_resource
# ---------------------------------------------------------------------------

class TestJsonResource:
    def test_returns_valid_json_string(self):
        result = _json_resource({"key": "value"})
        assert json.loads(result) == {"key": "value"}

    def test_ensure_ascii(self):
        result = _json_resource({"name": "Projeto Briefapp"})
        # ensure_ascii=True means non-ASCII chars are escaped
        assert isinstance(result, str)
        parsed = json.loads(result)
        assert parsed["name"] == "Projeto Briefapp"

    def test_list_payload(self):
        result = _json_resource([1, 2, 3])
        assert json.loads(result) == [1, 2, 3]


# ---------------------------------------------------------------------------
# _flatten_workitems
# ---------------------------------------------------------------------------

class TestFlattenWorkitems:
    _SPRINTS = [
        {
            "id": "sprint-1",
            "name": "Sprint 1",
            "workItems": [
                {"id": "wi-a", "title": "Task A"},
                {"id": "wi-b", "title": "Task B"},
            ],
        },
        {
            "id": "sprint-2",
            "name": "Sprint 2",
            "workItems": [
                {"id": "wi-c", "title": "Task C"},
            ],
        },
    ]

    def test_flatten_all(self):
        items = _flatten_workitems(self._SPRINTS)
        assert len(items) == 3
        titles = {i["title"] for i in items}
        assert titles == {"Task A", "Task B", "Task C"}

    def test_flatten_injects_sprint_metadata(self):
        items = _flatten_workitems(self._SPRINTS)
        for item in items:
            assert "sprintId" in item
            assert "sprintName" in item

    def test_filter_by_sprint_id(self):
        items = _flatten_workitems(self._SPRINTS, sprint_id="sprint-2")
        assert len(items) == 1
        assert items[0]["title"] == "Task C"
        assert items[0]["sprintId"] == "sprint-2"

    def test_empty_sprints(self):
        assert _flatten_workitems([]) == []

    def test_sprint_with_no_work_items(self):
        sprints = [{"id": "s-1", "name": "Empty", "workItems": []}]
        assert _flatten_workitems(sprints) == []


# ---------------------------------------------------------------------------
# project_list / project_create / project_config_update / project_delete
# ---------------------------------------------------------------------------

class TestProjectTools:
    def test_project_list(self):
        mock_data = [{"id": "p-1", "name": "Briefapp"}]
        with patch("server._request", return_value=mock_data) as mock_req:
            result = project_list()
        mock_req.assert_called_once_with("GET", "/api/projects", params={"includeArchived": False})
        assert result == mock_data

    def test_project_list_include_archived(self):
        with patch("server._request", return_value=[]) as mock_req:
            project_list(include_archived=True)
        mock_req.assert_called_with("GET", "/api/projects", params={"includeArchived": True})

    def test_project_create_minimal(self):
        mock_data = {"id": "p-new", "name": "My Project"}
        with patch("server._request", return_value=mock_data) as mock_req:
            result = project_create("My Project", "An example project")
        payload = mock_req.call_args[1]["payload"]
        assert payload["name"] == "My Project"
        assert payload["description"] == "An example project"
        assert result == mock_data

    def test_project_create_with_optional_fields(self):
        with patch("server._request", return_value={}) as mock_req:
            project_create("P", "D", github_url="https://github.com/x/y", local_path="/p", tech_stack=".NET", main_branch="main")
        payload = mock_req.call_args[1]["payload"]
        assert payload["gitHubUrl"] == "https://github.com/x/y"
        assert payload["localPath"] == "/p"
        assert payload["techStack"] == ".NET"
        assert payload["mainBranch"] == "main"

    def test_project_config_update_single_field(self):
        with patch("server._request", return_value={}) as mock_req:
            project_config_update("p-1", github_url="https://github.com/x/y")
        payload = mock_req.call_args[1]["payload"]
        assert payload == {"gitHubUrl": "https://github.com/x/y"}

    def test_project_config_update_no_fields_raises(self):
        with pytest.raises(ApiError):
            project_config_update("p-1")

    def test_project_delete(self):
        with patch("server._request", return_value={"deleted": True}) as mock_req:
            result = project_delete("p-1")
        mock_req.assert_called_once_with("DELETE", "/api/projects/p-1")
        assert result == {"deleted": True}


# ---------------------------------------------------------------------------
# backlog_add / backlog_list
# ---------------------------------------------------------------------------

class TestBacklogTools:
    def test_backlog_add(self):
        with patch("server._request", return_value={"id": "b-1"}) as mock_req:
            backlog_add("p-1", "Story A", "Description", story_points=3, priority=1)
        payload = mock_req.call_args[1]["payload"]
        assert payload["title"] == "Story A"
        assert payload["storyPoints"] == 3
        assert payload["priority"] == 1

    def test_backlog_list(self):
        mock_data = [{"id": "b-1"}]
        with patch("server._request", return_value=mock_data) as mock_req:
            result = backlog_list("p-1")
        mock_req.assert_called_once_with("GET", "/api/projects/p-1/backlog")
        assert result == mock_data


# ---------------------------------------------------------------------------
# sprint_create / workitem_list
# ---------------------------------------------------------------------------

class TestSprintTools:
    def test_sprint_create(self):
        with patch("server._request", return_value={"id": "s-1"}) as mock_req:
            sprint_create("p-1", "Sprint 1", "Goal text", "2026-03-20", "2026-04-03", ["b-1", "b-2"])
        payload = mock_req.call_args[1]["payload"]
        assert payload["name"] == "Sprint 1"
        assert payload["goal"] == "Goal text"
        assert payload["backlogItemIds"] == ["b-1", "b-2"]

    def test_workitem_list_all(self):
        sprints = [{"id": "s-1", "name": "S1", "workItems": [{"id": "wi-1", "title": "T"}]}]
        with patch("server._request", return_value=sprints):
            result = workitem_list("p-1")
        assert len(result) == 1
        assert result[0]["sprintId"] == "s-1"

    def test_workitem_list_filtered_sprint(self):
        sprints = [
            {"id": "s-1", "name": "S1", "workItems": [{"id": "wi-1"}]},
            {"id": "s-2", "name": "S2", "workItems": [{"id": "wi-2"}]},
        ]
        with patch("server._request", return_value=sprints):
            result = workitem_list("p-1", sprint_id="s-2")
        assert len(result) == 1
        assert result[0]["id"] == "wi-2"


# ---------------------------------------------------------------------------
# knowledge tools
# ---------------------------------------------------------------------------

class TestKnowledgeTools:
    def test_knowledge_checkpoint(self):
        with patch("server._request", return_value={"id": "cp-1"}) as mock_req:
            knowledge_checkpoint("p-1", "Checkpoint 1", "context", "decisions", "risks", "next")
        payload = mock_req.call_args[1]["payload"]
        assert payload["name"] == "Checkpoint 1"
        assert payload["contextSnapshot"] == "context"

    def test_knowledge_list(self):
        mock_data = {"wikiPages": [], "checkpoints": []}
        with patch("server._request", return_value=mock_data) as mock_req:
            result = knowledge_list("p-1")
        mock_req.assert_called_once_with("GET", "/api/projects/p-1/knowledge")
        assert result == mock_data

    def test_wiki_add(self):
        with patch("server._request", return_value={"id": "w-1"}) as mock_req:
            wiki_add("p-1", "Onboarding", "# Steps", "onboarding", "How-To")
        payload = mock_req.call_args[1]["payload"]
        assert payload["title"] == "Onboarding"
        assert payload["category"] == "How-To"

    def test_wiki_list(self):
        mock_knowledge = {"wikiPages": [{"id": "w-1"}], "checkpoints": []}
        with patch("server._request", return_value=mock_knowledge):
            result = wiki_list("p-1")
        assert result == [{"id": "w-1"}]

    def test_documentation_add(self):
        with patch("server._request", return_value={"id": "d-1"}) as mock_req:
            documentation_add("p-1", "ADR-001", "# Content", "Architecture", "adr")
        payload = mock_req.call_args[1]["payload"]
        assert payload["title"] == "ADR-001"
        assert payload["category"] == "Architecture"

    def test_documentation_list(self):
        mock_knowledge = {"documentationPages": [{"id": "d-1"}]}
        with patch("server._request", return_value=mock_knowledge):
            result = documentation_list("p-1")
        assert result == [{"id": "d-1"}]

    def test_checkpoint_list(self):
        mock_knowledge = {"checkpoints": [{"id": "c-1"}]}
        with patch("server._request", return_value=mock_knowledge):
            result = checkpoint_list("p-1")
        assert result == [{"id": "c-1"}]


# ---------------------------------------------------------------------------
# project_config_update — remaining optional field branches
# ---------------------------------------------------------------------------

class TestProjectConfigUpdateBranches:
    def test_local_path_only(self):
        with patch("server._request", return_value={}) as mock_req:
            project_config_update("p-1", local_path="/home/user/project")
        payload = mock_req.call_args[1]["payload"]
        assert payload == {"localPath": "/home/user/project"}

    def test_tech_stack_only(self):
        with patch("server._request", return_value={}) as mock_req:
            project_config_update("p-1", tech_stack=".NET 10, React, PostgreSQL")
        payload = mock_req.call_args[1]["payload"]
        assert payload == {"techStack": ".NET 10, React, PostgreSQL"}

    def test_main_branch_only(self):
        with patch("server._request", return_value={}) as mock_req:
            project_config_update("p-1", main_branch="develop")
        payload = mock_req.call_args[1]["payload"]
        assert payload == {"mainBranch": "develop"}

    def test_all_fields_together(self):
        with patch("server._request", return_value={}) as mock_req:
            project_config_update(
                "p-1",
                github_url="https://github.com/x/repo",
                local_path="c:/projetos/repo",
                tech_stack=".NET",
                main_branch="main",
            )
        payload = mock_req.call_args[1]["payload"]
        assert payload["gitHubUrl"] == "https://github.com/x/repo"
        assert payload["localPath"] == "c:/projetos/repo"
        assert payload["techStack"] == ".NET"
        assert payload["mainBranch"] == "main"


# ---------------------------------------------------------------------------
# resource handlers — read-only context resources
# ---------------------------------------------------------------------------

class TestResourceHandlers:
    _SPRINTS = [
        {
            "id": "s-1",
            "name": "Sprint 1",
            "workItems": [
                {"id": "wi-1", "title": "Task A", "status": 3},
                {"id": "wi-2", "title": "Task B", "status": 1},
            ],
        }
    ]

    def test_resource_about(self):
        result = resource_about()
        data = json.loads(result)
        assert data["name"] == "briefapp-todo-list-mcp"

    def test_resource_projects_active(self):
        mock_projects = [{"id": "p-1"}]
        with patch("server._request", return_value=mock_projects):
            result = resource_projects_active()
        assert json.loads(result) == mock_projects

    def test_resource_projects_all(self):
        mock_projects = [{"id": "p-1"}, {"id": "p-archived"}]
        with patch("server._request", return_value=mock_projects):
            result = resource_projects_all()
        assert json.loads(result) == mock_projects

    def test_resource_project_config_found(self):
        mock_projects = [{"id": "p-1", "name": "Briefapp", "gitHubUrl": "https://github.com/x/y",
                          "localPath": None, "techStack": ".NET", "mainBranch": "main"}]
        with patch("server._request", return_value=mock_projects):
            result = resource_project_config("p-1")
        data = json.loads(result)
        assert data["projectId"] == "p-1"
        assert data["techStack"] == ".NET"

    def test_resource_project_config_not_found(self):
        with patch("server._request", return_value=[]):
            with pytest.raises(ApiError):
                resource_project_config("missing-id")

    def test_resource_project_dashboard(self):
        mock_dash = {"backlogTotal": 5}
        with patch("server._request", return_value=mock_dash):
            result = resource_project_dashboard("p-1")
        assert json.loads(result) == mock_dash

    def test_resource_project_backlog(self):
        mock_backlog = [{"id": "b-1"}]
        with patch("server._request", return_value=mock_backlog):
            result = resource_project_backlog("p-1")
        assert json.loads(result) == mock_backlog

    def test_resource_project_sprints(self):
        with patch("server._request", return_value=self._SPRINTS):
            result = resource_project_sprints("p-1")
        assert json.loads(result) == self._SPRINTS

    def test_resource_project_workitems(self):
        with patch("server._request", return_value=self._SPRINTS):
            result = resource_project_workitems("p-1")
        items = json.loads(result)
        assert len(items) == 2

    def test_resource_project_workitems_by_status_done(self):
        with patch("server._request", return_value=self._SPRINTS):
            result = resource_project_workitems_by_status("p-1", "done")
        data = json.loads(result)
        assert data["statusCode"] == 3
        assert data["count"] == 1
        assert data["items"][0]["id"] == "wi-1"

    def test_resource_project_sprint_workitems(self):
        with patch("server._request", return_value=self._SPRINTS):
            result = resource_project_sprint_workitems("p-1", "s-1")
        items = json.loads(result)
        assert len(items) == 2

    def test_resource_project_knowledge(self):
        mock_k = {"wikiPages": []}
        with patch("server._request", return_value=mock_k):
            result = resource_project_knowledge("p-1")
        assert json.loads(result) == mock_k

    def test_resource_project_tasks_overview(self):
        mock_backlog = [{"id": "b-1", "status": 0}, {"id": "b-2", "status": 3}]
        with patch("server._request", side_effect=[mock_backlog, self._SPRINTS]):
            result = resource_project_tasks_overview("p-1")
        data = json.loads(result)
        assert data["summary"]["backlogTotal"] == 2
        assert data["summary"]["backlogDone"] == 1

    def test_resource_project_tasks_triage(self):
        mock_backlog = [{"id": "b-1", "status": 4}]
        with patch("server._request", side_effect=[mock_backlog, self._SPRINTS]):
            result = resource_project_tasks_triage("p-1")
        data = json.loads(result)
        assert data["summary"]["backlogBlocked"] == 1
        assert data["summary"]["workItemsInReview"] == 0

    def test_resource_project_context(self):
        mock_dash = {"metric": 1}
        mock_backlog = [{"id": "b-1"}]
        mock_knowledge = {"wikiPages": []}
        with patch("server._request", side_effect=[mock_dash, mock_backlog, self._SPRINTS, mock_knowledge]):
            result = resource_project_context("p-1")
        data = json.loads(result)
        assert data["projectId"] == "p-1"
        assert data["dashboard"] == mock_dash
        assert len(data["workItems"]) == 2


# ---------------------------------------------------------------------------
# prompt handlers — return guided string instructions
# ---------------------------------------------------------------------------

class TestPromptHandlers:
    def test_briefapp_project_config_returns_string(self):
        result = briefapp_project_config(project_id="p-1")
        assert isinstance(result, str)
        assert "project_config_update" in result
        assert "p-1" in result

    def test_briefapp_project_config_shows_field_values(self):
        result = briefapp_project_config(
            project_id="p-1",
            github_url="https://github.com/x/y",
            tech_stack=".NET",
        )
        assert "https://github.com/x/y" in result
        assert ".NET" in result

    def test_briefapp_project_create_returns_string(self):
        result = briefapp_project_create(name="My App", description="Test app")
        assert isinstance(result, str)
        assert "My App" in result
        assert "project_create" in result

    def test_briefapp_sprint_create_returns_string(self):
        result = briefapp_sprint_create(project_id="p-1", name="Sprint 2")
        assert isinstance(result, str)
        assert "sprint_create" in result
        assert "p-1" in result
        assert "Sprint 2" in result

    def test_briefapp_resources_guide_returns_string(self):
        result = briefapp_resources_guide(project_id="p-1")
        assert isinstance(result, str)
        assert "p-1" in result
        assert "briefapp://" in result
