# Code Agent

`code-agent` is an isolated coding agent project that lives inside `c:\projetos\todolist\code-agent` but does not depend on the rest of the repository at runtime.

## Goals

- Provide a web UI inspired by GitHub Copilot and Claude Code workflows.
- Run local models through Ollama.
- Support skills with `SKILL.md`, frontmatter and optional references.
- Restrict agent file and shell access to a dedicated sandbox workspace.
- Stay runnable with zero external runtime dependencies.

## Isolation model

The agent never reads or writes outside its dedicated runtime workspace when using built-in tools.

- Default runtime root on Windows: `%LOCALAPPDATA%\Temp\code-agent-runtime`
- Runtime root in Docker: `/app/runtime`
- Skills root: `code-agent/skills`
- Session persistence: isolated inside the runtime root

This isolation applies to the agent runtime, not to the human developer editing this project.

## Features in this MVP

- Session-based coding UI with prompt composer
- Streaming event feed via Server-Sent Events
- Ollama model discovery and chat execution
- Agent planning loop with controlled tools:
  - `list_files`
  - `read_file`
  - `write_file`
  - `run_command`
- Local skill discovery and prompt assembly
- Session panels for plan, touched files, commands and skills
- Docker and local run workflow
- Node built-in test coverage for critical logic paths

## Run locally

```powershell
cd c:\projetos\todolist\code-agent
node src\backend\server.mjs
```

Open `http://localhost:8787`.

## Background server commands

```powershell
cd c:\projetos\todolist\code-agent
npm run start:bg
npm run status:bg
npm run stop:bg
```

The daemon stores PID in `%LOCALAPPDATA%\Temp\code-agent-runtime\server.pid` and logs in `%TEMP%`.

## Ollama discovery

If `OLLAMA_BASE_URL` is not set, the agent tries these endpoints in order:

1. `http://127.0.0.1:11434`
2. `http://127.0.0.1:9434`
3. `http://host.docker.internal:11434`
4. `http://host.docker.internal:9434`

In this machine, the active container is exposed on `http://127.0.0.1:9434` and currently serves `gemma2:2b`.

## Environment variables

- `PORT` default: `8787`
- `OLLAMA_BASE_URL` optional override
- `OLLAMA_MODEL` default: `qwen2.5-coder:7b`
- `CODE_AGENT_RUNTIME_DIR` default: `%LOCALAPPDATA%\Temp\code-agent-runtime`
- `CODE_AGENT_MAX_CYCLES` default: `3`
- `CODE_AGENT_MAX_ACTIONS` default: `6`

## Docker

Use the local compose file in this folder:

```powershell
docker compose -f c:\projetos\todolist\code-agent\docker-compose.yml up --build
```

The Docker compose file is preconfigured for `http://host.docker.internal:9434`.

## Validation checklist

- `node tests/run-tests.mjs`
- Open the UI and create a session
- Confirm models load from Ollama
- Ask the agent to inspect files inside the sandbox workspace
- Confirm it cannot escape the sandbox

## Known MVP limitations

- Tool use depends on the selected Ollama model following a JSON tool contract.
- Small local models can be slow or inconsistent on heavier tool-planning prompts.
- The agent uses conservative tool limits and does not delete files.
- No parallel agent workers yet.
- No Git integration yet inside the runtime agent tools.
