"""
Remove do Briefapp apenas backlogs criados pelo import-git-history-to-briefapp.py
(descrição contém a marca '**Commit:**').

Requer API com DELETE /api/backlog-items/{id} (backend atualizado).

Uso: python delete-import-backlogs.py
"""
from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.request

API_BASE = os.environ.get("PANDORA_API_BASE_URL", "http://127.0.0.1:8480")
PROJECT_NAME = os.environ.get("PANDORA_PROJECT_NAME", "Todolist")
MARKER = "**Commit:**"


def get_json(url: str) -> object:
    with urllib.request.urlopen(url, timeout=120) as r:
        return json.loads(r.read().decode("utf-8"))


def delete_url(url: str) -> None:
    req = urllib.request.Request(url, method="DELETE")
    with urllib.request.urlopen(req, timeout=120) as r:
        r.read()


def resolve_project_id() -> str:
    env_id = os.environ.get("PANDORA_PROJECT_ID", "").strip()
    if env_id:
        return env_id
    projects = get_json(f"{API_BASE}/api/projects")
    if not isinstance(projects, list):
        print("Resposta inválida de /api/projects.", file=sys.stderr)
        sys.exit(1)
    for p in projects:
        if p.get("name") == PROJECT_NAME:
            pid = p.get("id")
            if pid:
                print(f"Projeto: {PROJECT_NAME} ({pid})")
                return str(pid)
    print(f"Projeto '{PROJECT_NAME}' não encontrado.", file=sys.stderr)
    sys.exit(1)


def main() -> int:
    project_id = resolve_project_id()
    backlog = get_json(f"{API_BASE}/api/projects/{project_id}/backlog")
    if not isinstance(backlog, list):
        print("Resposta inválida de /backlog.", file=sys.stderr)
        return 1

    to_delete = [
        str(b["id"])
        for b in backlog
        if MARKER in (b.get("description") or "")
    ]
    print(f"Itens de import encontrados: {len(to_delete)}")

    for bid in to_delete:
        try:
            delete_url(f"{API_BASE}/api/backlog-items/{bid}")
            print(f"  removido {bid}")
        except urllib.error.HTTPError as e:
            body = e.read().decode("utf-8", errors="replace")
            print(f"Erro DELETE {bid}: {e.code} {body}", file=sys.stderr)
            return 1

    print("Concluído.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
