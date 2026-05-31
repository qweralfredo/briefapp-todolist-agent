"""
Importa o histórico Git para o Briefapp de forma idempotente:
- Nunca cria projeto (deve existir).
- Só cria backlog se não houver item com o mesmo commit hash.
- Só cria sprint para backlogs que ainda não estão em nenhuma sprint.
- Só atualiza work item para Done se ainda não estiver Done.

Variáveis de ambiente:
  PANDORA_API_BASE_URL   (default http://127.0.0.1:8480)
  PANDORA_PROJECT_ID     (opcional; se omitido, busca por PANDORA_PROJECT_NAME)
  PANDORA_PROJECT_NAME   (default Todolist)
  TODOLIST_ROOT          (raiz do repositório git)
  IMPORT_SPRINT_BATCH    (default 10)
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
import urllib.error
import urllib.request

API_BASE = os.environ.get("PANDORA_API_BASE_URL", "http://127.0.0.1:8480")
PROJECT_NAME = os.environ.get("PANDORA_PROJECT_NAME", "Todolist")
REPO_ROOT = os.path.normpath(
    os.environ.get(
        "TODOLIST_ROOT",
        os.path.join(os.path.dirname(__file__), "..", ".."),
    )
)
BATCH_SIZE = int(os.environ.get("IMPORT_SPRINT_BATCH", "10"))


def norm_hash(h: str) -> str:
    return (h or "").strip().lower()


def git_commits() -> list[dict]:
    out = subprocess.check_output(
        [
            "git",
            "-C",
            REPO_ROOT,
            "log",
            "--all",
            "--reverse",
            "--format=%H|%cI|%s",
        ],
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    rows: list[dict] = []
    for line in out.splitlines():
        line = line.strip()
        if not line:
            continue
        parts = line.split("|", 2)
        if len(parts) < 3:
            continue
        rows.append({"hash": parts[0], "iso": parts[1], "subject": parts[2]})
    return rows


def priority_story_points(subject: str) -> tuple[int, int]:
    s = subject.lower()
    if s.startswith("feat") or s.startswith("fix"):
        return 2, 3
    if s.startswith("test") or s.startswith("refactor"):
        return 1, 2
    return 0, 1


def post_json(url: str, payload: dict) -> dict:
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read().decode("utf-8"))


def get_json(url: str) -> object:
    with urllib.request.urlopen(url, timeout=120) as r:
        return json.loads(r.read().decode("utf-8"))


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
                print(f"Projeto encontrado: {PROJECT_NAME} ({pid})")
                return str(pid)
    print(
        f"Projeto '{PROJECT_NAME}' não encontrado. Crie manualmente ou defina PANDORA_PROJECT_ID.",
        file=sys.stderr,
    )
    sys.exit(1)


def backlog_commit_index(backlog: list) -> dict[str, str]:
    """commit hash (normalizado) -> backlog item id"""
    m: dict[str, str] = {}
    for bi in backlog:
        bid = bi.get("id")
        if not bid:
            continue
        for cid in bi.get("commitIds") or bi.get("commit_ids") or []:
            key = norm_hash(str(cid))
            if key and key not in m:
                m[key] = str(bid)
    return m


def backlog_ids_in_sprints(sprints: list) -> set[str]:
    seen: set[str] = set()
    for sp in sprints:
        for wi in sp.get("workItems") or []:
            bid = wi.get("backlogItemId") or wi.get("backlog_item_id")
            if bid:
                seen.add(str(bid))
    return seen


def main() -> int:
    project_id = resolve_project_id()

    commits = git_commits()
    if not commits:
        print("Nenhum commit encontrado no repositório.", file=sys.stderr)
        return 1

    print(f"Commits no Git (todas as branches, mais antigo primeiro): {len(commits)}")

    backlog_raw = get_json(f"{API_BASE}/api/projects/{project_id}/backlog")
    if not isinstance(backlog_raw, list):
        print("Resposta inválida de /backlog.", file=sys.stderr)
        return 1

    by_commit = backlog_commit_index(backlog_raw)
    created_backlog = 0
    skipped_backlog = 0

    backlog_ids_ordered: list[str] = []
    for c in commits:
        key = norm_hash(c["hash"])
        if key in by_commit:
            backlog_ids_ordered.append(by_commit[key])
            skipped_backlog += 1
            continue
        pri, sp = priority_story_points(c["subject"])
        title = c["subject"][:200]
        desc = (
            f"**Commit:** `{c['hash']}`\n"
            f"**Data (autor):** {c['iso']}\n\n"
            f"{c['subject']}"
        )
        payload = {
            "title": title,
            "description": desc,
            "storyPoints": sp,
            "priority": pri,
            "commitIds": [c["hash"]],
        }
        try:
            r = post_json(f"{API_BASE}/api/projects/{project_id}/backlog", payload)
            new_id = str(r["id"])
            backlog_ids_ordered.append(new_id)
            by_commit[key] = new_id
            created_backlog += 1
        except urllib.error.HTTPError as e:
            body = e.read().decode("utf-8", errors="replace")
            print(f"Erro backlog {c['hash'][:7]}: {e.code} {body}", file=sys.stderr)
            return 1

    print(f"Backlog: {created_backlog} criado(s), {skipped_backlog} já existente(s).")

    commit_by_backlog = {backlog_ids_ordered[i]: commits[i] for i in range(len(commits))}

    sprints_raw = get_json(f"{API_BASE}/api/projects/{project_id}/sprints")
    if not isinstance(sprints_raw, list):
        print("Resposta inválida de /sprints.", file=sys.stderr)
        return 1

    in_sprint = backlog_ids_in_sprints(sprints_raw)
    pending = [bid for bid in backlog_ids_ordered if bid not in in_sprint]

    if not pending:
        print("Nenhum backlog pendente de sprint — sprints já cobrem todos os itens.")
    else:
        sprint_base = len(sprints_raw)
        batch_idx = 0
        index_of = {backlog_ids_ordered[i]: i for i in range(len(backlog_ids_ordered))}
        for start in range(0, len(pending), BATCH_SIZE):
            batch_ids = pending[start : start + BATCH_SIZE]
            batch_idx += 1
            # datas pelos commits correspondentes aos backlogs deste lote
            idxs = [index_of[b] for b in batch_ids]
            batch_commits = [commits[i] for i in idxs]
            d0 = batch_commits[0]["iso"][:10]
            d1 = batch_commits[-1]["iso"][:10]
            sprint_num = sprint_base + batch_idx
            name = f"Sprint {sprint_num} - Git {d0} -> {d1}"
            goal = (
                f"Reconstrução do histórico Git ({len(batch_ids)} commits), "
                f"do mais antigo ao mais novo neste lote."
            )
            sprint_payload = {
                "name": name,
                "goal": goal,
                "startDate": d0,
                "endDate": d1,
                "backlogItemIds": batch_ids,
                "commitIds": [c["hash"] for c in batch_commits],
            }
            try:
                post_json(f"{API_BASE}/api/projects/{project_id}/sprints", sprint_payload)
                print(f"  Sprint criada: {name} ({len(batch_ids)} itens)")
            except urllib.error.HTTPError as e:
                body = e.read().decode("utf-8", errors="replace")
                print(f"Erro sprint '{name}': {e.code} {body}", file=sys.stderr)
                return 1

    # Atualizar work items para Done (somente se não estiverem Done)
    sprints_after = get_json(f"{API_BASE}/api/projects/{project_id}/sprints")
    assert isinstance(sprints_after, list)
    updated = 0
    skipped_wi = 0
    for sp in sprints_after:
        for wi in sp.get("workItems") or []:
            bid = wi.get("backlogItemId")
            if not bid or str(bid) not in commit_by_backlog:
                continue
            status = wi.get("status")
            if status == 3:
                skipped_wi += 1
                continue
            c = commit_by_backlog[str(bid)]
            status_payload = {
                "status": 3,
                "assignee": "git-history-import",
                "tokensUsed": 0,
                "agentName": "import-git-history",
                "modelUsed": "n/a",
                "ideUsed": "Cursor",
                "feedback": f"Histórico Git importado — {c['subject'][:200]}",
                "metadataJson": "{}",
                "branch": "",
                "commitIds": [c["hash"]],
            }
            try:
                post_json(
                    f"{API_BASE}/api/work-items/{wi['id']}/status",
                    status_payload,
                )
                updated += 1
            except urllib.error.HTTPError as e:
                body = e.read().decode("utf-8", errors="replace")
                print(f"Erro work item {wi['id']}: {e.code} {body}", file=sys.stderr)
                return 1

    print(f"Work items: {updated} atualizado(s) para Done, {skipped_wi} já estavam Done.")
    print("Concluído.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
