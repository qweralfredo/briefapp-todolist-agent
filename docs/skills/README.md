# Briefapp Skills — Multi-IDE Reference

Este diretório contém as skills (instruções contextuais para agentes) do Briefapp Todo List, adaptadas para cada IDE suportada.

## IDEs Suportadas

| IDE | Diretório | Formato | Onde instalar |
|-----|-----------|---------|---------------|
| **VS Code (Copilot)** | `claude/` | `.md` | `.github/copilot-instructions.md` ou commands |
| **Antigravity (Gemini)** | `antigravity/` | `.md` (YAML frontmatter) | `~/.gemini/antigravity/skills/` |
| **Cursor** | `cursor/` | `.mdc` (MDC frontmatter) | `.cursor/rules/` |
| **Windsurf** | `windsurf/` | `.md` | `.windsurf/rules/` |

## Skills Disponíveis

| Skill | Propósito |
|-------|-----------|
| **briefapp-todo-list-v2** | Gestão completa de projetos via MCP: backlog, sprints, work items, wiki, checkpoints, RAG, sandboxes |
| **briefapp-atomic-flow** | Orquestração fractal hierárquica (multiplicador C) para épicos e features complexas |

## Instalação por IDE

### VS Code (Copilot)

As skills estão em `claude/` como comandos:
```
docs/skills/claude/briefapp-init.md
docs/skills/claude/briefapp-plan.md
docs/skills/claude/briefapp-execute.md
docs/skills/claude/briefapp-done.md
docs/skills/claude/briefapp-checkpoint.md
docs/skills/claude/briefapp-atomic-flow.md
```

Para usar, adicione como custom instructions ou comandos do GitHub PR Agent.

### Antigravity (Gemini)

```powershell
# Copiar skills para o diretório global do Antigravity
Copy-Item docs\skills\antigravity\briefapp-todo-list-v2.md "$env:USERPROFILE\.gemini\antigravity\skills\briefapp-todo-list-v2\SKILL.md"
Copy-Item docs\skills\antigravity\briefapp-atomic-flow.md "$env:USERPROFILE\.gemini\antigravity\skills\briefapp-atomic-flow\SKILL.md"
```

### Cursor

```powershell
# Copiar regras para o projeto
Copy-Item docs\skills\cursor\*.mdc .cursor\rules\
```

Ou instalar globalmente em `~/.cursor/rules/`.

### Windsurf

```powershell
# Copiar regras para o projeto
New-Item -ItemType Directory -Path .windsurf\rules -Force
Copy-Item docs\skills\windsurf\*.md .windsurf\rules\
```

## Configuração MCP por IDE

Todas as IDEs precisam do servidor MCP configurado. Use o [instalador cross-platform](../../installers/README.md) ou configure manualmente:

### VS Code
`.vscode/mcp.json`:
```json
{ "servers": { "briefapp-todo-list-mcp": { "type": "http", "url": "http://127.0.0.1:8481/mcp" } } }
```

### Cursor
`.cursor/mcp.json`:
```json
{ "mcpServers": { "briefapp-todo-list-mcp": { "url": "http://127.0.0.1:8481/mcp" } } }
```

### Windsurf
`.windsurf/mcp.json`:
```json
{ "mcpServers": { "briefapp-todo-list-mcp": { "serverUrl": "http://127.0.0.1:8481/mcp" } } }
```

### Claude Desktop
`claude_desktop_config.json`:
```json
{ "mcpServers": { "briefapp-todo-list-mcp": { "command": "npx", "args": ["-y", "mcp-remote", "http://127.0.0.1:8481/mcp"] } } }
```
