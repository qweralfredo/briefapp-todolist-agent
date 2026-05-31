import argparse
import json
import sys
import uuid
from datetime import date, timedelta
from typing import Any

import httpx

EXPECTED_TOOLS = {
    "project_list",
    "project_create",
    "project_delete",
    "backlog_add",
    "backlog_list",
    "sprint_create",
    "workitem_list",
    "workitem_update",
    "knowledge_checkpoint",
    "knowledge_list",
    "wiki_add",
    "wiki_list",
    "documentation_add",
    "documentation_list",
    "checkpoint_list",
    "search",
}

EXPECTED_PROMPTS = {
    "briefapp_project_create",
    "briefapp_sprint_create",
    "briefapp_resources_guide",
    "briefapp_search",
}

EXPECTED_RESOURCES = {
    "briefapp://about",
    "briefapp://projects/active",
    "briefapp://projects/all",
}

EXPECTED_RESOURCE_TEMPLATES = {
    "briefapp://projects/{project_id}/context",
    "briefapp://projects/{project_id}/dashboard",
    "briefapp://projects/{project_id}/backlog",
    "briefapp://projects/{project_id}/sprints",
    "briefapp://projects/{project_id}/workitems",
    "briefapp://projects/{project_id}/workitems/status/{status}",
    "briefapp://projects/{project_id}/sprints/{sprint_id}/workitems",
    "briefapp://projects/{project_id}/tasks/overview",
    "briefapp://projects/{project_id}/tasks/triage",
    "briefapp://projects/{project_id}/knowledge",
}


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def _extract_sse_json(response_text: str) -> dict[str, Any]:
    lines = response_text.splitlines()
    data_lines = [line[5:].strip() for line in lines if line.startswith("data:")]
    _require(len(data_lines) > 0, f"Unexpected MCP response format: {response_text}")
    return json.loads("\n".join(data_lines))


def _unwrap_result(payload: Any) -> Any:
    if isinstance(payload, dict) and "result" in payload and len(payload) == 1:
        return payload["result"]
    return payload


class HttpMcpClient:
    def __init__(self, mcp_url: str, timeout_seconds: float):
        self.mcp_url = mcp_url
        self.timeout = timeout_seconds
        self.session_id = ""
        self._next_id = 1

    def _rpc(self, method: str, params: dict[str, Any] | None = None) -> dict[str, Any]:
        request_id = self._next_id
        self._next_id += 1
        payload = {
            "jsonrpc": "2.0",
            "id": request_id,
            "method": method,
            "params": params or {},
        }

        headers = {
            "Content-Type": "application/json",
            "Accept": "application/json, text/event-stream",
        }
        if self.session_id:
            headers["mcp-session-id"] = self.session_id

        with httpx.Client(timeout=self.timeout) as client:
            response = client.post(self.mcp_url, headers=headers, json=payload)
            response.raise_for_status()
            if not self.session_id and response.headers.get("mcp-session-id"):
                self.session_id = response.headers["mcp-session-id"]

        message = _extract_sse_json(response.text)
        if "error" in message:
            raise RuntimeError(f"MCP {method} error: {message['error']}")
        return message["result"]

    def initialize(self) -> dict[str, Any]:
        return self._rpc(
            "initialize",
            {
                "protocolVersion": "2025-03-26",
                "capabilities": {},
                "clientInfo": {"name": "briefapp-mcp-smoke", "version": "1.0.0"},
            },
        )

    def list_tools(self) -> list[dict[str, Any]]:
        return self._rpc("tools/list").get("tools", [])

    def call_tool(self, name: str, arguments: dict[str, Any]) -> Any:
        result = self._rpc("tools/call", {"name": name, "arguments": arguments})
        structured = result.get("structuredContent")
        if structured is not None:
            return _unwrap_result(structured)

        content = result.get("content") or []
        for block in content:
            text = block.get("text")
            if text:
                try:
                    return _unwrap_result(json.loads(text))
                except json.JSONDecodeError:
                    return text
        return None

    def list_prompts(self) -> list[dict[str, Any]]:
        return self._rpc("prompts/list").get("prompts", [])

    def get_prompt(self, name: str, arguments: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._rpc("prompts/get", {"name": name, "arguments": arguments or {}})

    def list_resources(self) -> list[dict[str, Any]]:
        return self._rpc("resources/list").get("resources", [])

    def list_resource_templates(self) -> list[dict[str, Any]]:
        return self._rpc("resources/templates/list").get("resourceTemplates", [])

    def read_resource(self, uri: str) -> dict[str, Any]:
        return self._rpc("resources/read", {"uri": uri})


def _check_api_health(api_base_url: str, timeout_seconds: float) -> None:
    url = f"{api_base_url.rstrip('/')}/health"
    with httpx.Client(timeout=timeout_seconds) as client:
        response = client.get(url)
    _require(response.status_code == 200, f"Briefapp API is not healthy at {url} (status={response.status_code}).")


def _run_smoke_test(api_base_url: str, mcp_url: str, timeout_seconds: float) -> None:
    _check_api_health(api_base_url, timeout_seconds)

    client = HttpMcpClient(mcp_url=mcp_url, timeout_seconds=timeout_seconds)
    init_result = client.initialize()
    capabilities = init_result.get("capabilities", {})
    _require("tools" in capabilities, "MCP initialize missing tools capability.")
    _require("prompts" in capabilities, "MCP initialize missing prompts capability.")
    _require("resources" in capabilities, "MCP initialize missing resources capability.")
    print("Initialize OK")

    tools = client.list_tools()
    tool_names = {tool.get("name") for tool in tools}
    missing_tools = EXPECTED_TOOLS - tool_names
    _require(not missing_tools, f"Missing expected tools: {sorted(missing_tools)}")
    _require(all("." not in name for name in tool_names if isinstance(name, str)), "Legacy dotted tool name found.")
    print(f"Tools OK ({len(tool_names)} available)")

    prompts = client.list_prompts()
    prompt_names = {prompt.get("name") for prompt in prompts}
    missing_prompts = EXPECTED_PROMPTS - prompt_names
    _require(not missing_prompts, f"Missing expected prompts: {sorted(missing_prompts)}")
    print(f"Prompts OK ({len(prompt_names)} available)")

    resources = client.list_resources()
    resource_uris = {resource.get("uri") for resource in resources}
    missing_resources = EXPECTED_RESOURCES - resource_uris
    _require(not missing_resources, f"Missing expected resources: {sorted(missing_resources)}")
    print(f"Resources OK ({len(resource_uris)} direct)")

    templates = client.list_resource_templates()
    template_uris = {template.get("uriTemplate") for template in templates}
    missing_templates = EXPECTED_RESOURCE_TEMPLATES - template_uris
    _require(not missing_templates, f"Missing expected resource templates: {sorted(missing_templates)}")
    print(f"Resource templates OK ({len(template_uris)} templates)")

    suffix = uuid.uuid4().hex[:8]
    project_name = f"Smoke Test {suffix}"
    backlog_title = f"Story {suffix}"
    sprint_name = f"Sprint {suffix}"
    project_id = ""

    try:
        created_project = client.call_tool(
            "project_create",
            {
                "name": project_name,
                "description": "Projeto criado por mcp_client_smoke_test",
            },
        )
        _require(isinstance(created_project, dict), "project_create did not return an object.")
        project_id = created_project.get("id", "")
        _require(isinstance(project_id, str) and project_id, "project_create did not return a valid id.")
        print(f"Created project: {project_id}")

        backlog_item = client.call_tool(
            "backlog_add",
            {
                "project_id": project_id,
                "title": backlog_title,
                "description": "Story de validacao do MCP",
                "story_points": 3,
                "priority": 1,
            },
        )
        _require(isinstance(backlog_item, dict), "backlog_add did not return an object.")
        backlog_item_id = backlog_item.get("id", "")
        _require(isinstance(backlog_item_id, str) and backlog_item_id, "backlog_add did not return a valid id.")
        print(f"Added backlog item: {backlog_item_id}")

        sprint = client.call_tool(
            "sprint_create",
            {
                "project_id": project_id,
                "name": sprint_name,
                "goal": "Validar pipeline MCP",
                "start_date": date.today().strftime("%Y-%m-%d"),
                "end_date": (date.today() + timedelta(days=14)).strftime("%Y-%m-%d"),
                "backlog_item_ids": [backlog_item_id],
            },
        )
        _require(isinstance(sprint, dict), "sprint_create did not return an object.")
        sprint_id = sprint.get("id", "")
        _require(isinstance(sprint_id, str) and sprint_id, "sprint_create did not return a valid id.")
        print(f"Created sprint: {sprint_id}")

        workitems = client.call_tool("workitem_list", {"project_id": project_id, "sprint_id": sprint_id})
        _require(isinstance(workitems, list), "workitem_list did not return a list.")
        print(f"Work items listed ({len(workitems)} found)")

        if len(workitems) > 0:
            work_item_id = workitems[0].get("id")
            _require(isinstance(work_item_id, str) and work_item_id, "workitem_list missing work item id.")

            updated = client.call_tool(
                "workitem_update",
                {
                    "work_item_id": work_item_id,
                    "status": 2,
                    "assignee": "briefapp-mcp-smoke",
                    "feedback": "Status atualizado no smoke test",
                },
            )
            _require(isinstance(updated, dict), "workitem_update did not return an object.")
            print("Work item update OK")
        else:
            print("Work item update skipped (no work items generated for sprint)")

        checkpoint = client.call_tool(
            "knowledge_checkpoint",
            {
                "project_id": project_id,
                "name": f"Checkpoint {suffix}",
                "context_snapshot": "Contexto de validacao pos-deploy",
                "decisions": "Manter resources completos",
                "risks": "Nenhum risco critico identificado",
                "next_actions": "Executar validacao automatica apos cada deploy",
            },
        )
        _require(isinstance(checkpoint, dict), "knowledge_checkpoint did not return an object.")
        print("Knowledge checkpoint OK")

        for prompt_name, args in [
            ("briefapp_project_create", {"name": "Demo", "description": "Teste"}),
            (
                "briefapp_sprint_create",
                {
                    "project_id": project_id,
                    "name": "Sprint Demo",
                    "goal": "Teste de prompt",
                    "start_date": date.today().strftime("%Y-%m-%d"),
                    "end_date": (date.today() + timedelta(days=14)).strftime("%Y-%m-%d"),
                },
            ),
            ("briefapp_resources_guide", {"project_id": project_id}),
        ]:
            prompt_payload = client.get_prompt(prompt_name, args)
            messages = prompt_payload.get("messages", [])
            _require(isinstance(messages, list) and len(messages) > 0, f"prompts/get for {prompt_name} returned no messages.")
        print("Prompts get OK")

        direct_resource = client.read_resource("briefapp://about")
        _require("contents" in direct_resource, "resources/read briefapp://about missing contents.")

        for uri in [
            f"briefapp://projects/{project_id}/context",
            f"briefapp://projects/{project_id}/tasks/overview",
            f"briefapp://projects/{project_id}/tasks/triage",
            f"briefapp://projects/{project_id}/workitems/status/review",
        ]:
            resource_payload = client.read_resource(uri)
            _require("contents" in resource_payload, f"resources/read failed for {uri}")
        print("Resources read OK")

    finally:
        if project_id:
            try:
                archived = client.call_tool("project_delete", {"project_id": project_id})
                _require(isinstance(archived, dict) and archived.get("archived") is True,
                         "project_delete did not archive smoke test project.")
                print("Cleanup OK (project archived)")
            except Exception as cleanup_error:
                raise RuntimeError(f"Cleanup failed for smoke project {project_id}: {cleanup_error}") from cleanup_error

    print("SMOKE TEST PASSED")


def main() -> int:
    parser = argparse.ArgumentParser(description="Post-deploy smoke test client for Briefapp MCP (HTTP).")
    parser.add_argument("--api-base-url", default="http://127.0.0.1:8480", help="Briefapp backend base URL.")
    parser.add_argument("--mcp-url", default="http://127.0.0.1:8481/mcp", help="Briefapp MCP endpoint URL.")
    parser.add_argument("--timeout-seconds", type=float, default=30.0, help="HTTP timeout for API/MCP calls.")
    args = parser.parse_args()

    try:
        _run_smoke_test(args.api_base_url, args.mcp_url, args.timeout_seconds)
        return 0
    except Exception as ex:
        print(f"SMOKE TEST FAILED: {ex}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
