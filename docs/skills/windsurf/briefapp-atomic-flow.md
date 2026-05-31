# Briefapp Atomic Flow — Windsurf Skill

> Fractal orchestration methodology for converting high-level intentions into traceable,
> safe, executable work units via Briefapp MCP. Activate when the user mentions: atomic flow,
> complexity multiplier, large epic, structural refactor, "C=2", "C=3", or any variant.

## Complexity Multiplier (C)

| C   | Scenario                                    | Backlogs | Sprints/BL | Tasks/Sprint | Subtasks/Task |
|-----|---------------------------------------------|----------|------------|--------------|---------------|
| 0.2 | Point fix / isolated bug                    | 1        | 1          | 1            | 1             |
| 0.5 | Small feature                               | 3        | 1          | 2            | 2             |
| 1   | New module / medium initiative              | 5        | 2          | 3            | 4             |
| 2   | Complex feature / multiple domains          | 10       | 4          | 6            | 8             |
| 3   | Structural refactor / large epic            | 15       | 6          | 9            | 12            |

## CSV Planning

Before MCP mass creation, draft plan in `briefapp_plan.csv`:
```csv
Type,ID,Title,Description,ParentID,Status
Box,box1,"Module","Description",,todo
Backlog,B1,"Area","Description",box1,todo
Sprint,S1,"Sprint 1","",B1,todo
Task,T1,"Task 1","Description",S1,todo
Subtask,ST1,"Subtask 1","Do X",T1,todo
```

## 3-Level Branch Architecture

`develop` → `feature/{backlog_id}` → `task/{task_id}`

Subtasks DO NOT create branches and are worked directly on the task branch. Merge bubble-up on completion. Each new backlog/feature branches from updated `develop`.

## TDD per Subtask

| Step | Commit |
|------|--------|
| 🔴 RED | `test: add failing test for <feature>` |
| 🟢 GREEN | `feat: implement <feature> to pass tests` |
| 🔵 REFACTOR | `refactor: clean up <feature>` |

**Minimum coverage: ≥ 80%**

## Commit Footer

```
<type>: <description>

Refs: backlog/<id> | sprint/<id> | task/<id>
```

## Non-Negotiable Rules

- Never generate code without prior test
- Never skip CSV checkpoint before MCP mass creation
- Never omit `Refs:` block in any commit
- Coverage < 80% = subtask not completed
- No silent mock/fallback in runtime
