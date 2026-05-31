# Code Agent & Sandboxes (V3)

## Code Agent (legado)

O diretório `code-agent` é um **projeto isolado** que roda em Node e oferece uma UI inspirada em fluxos tipo Copilot/Claude Code, com **Ollama** para modelos locais e ferramentas restritas a um workspace de runtime.

### Relação com o Briefapp Todo List

- **Não** é necessário para subir `docker compose` do núcleo.
- **Não** depende do restante do repositório em runtime.
- Útil para experimentação local com modelos via Ollama.

## V3 — Sandbox System (recomendado)

A partir da V3, o Briefapp oferece um **sistema de sandboxes nativo** gerenciado via MCP, que substitui/complementa o code-agent local para execução isolada de código.

### Funcionalidades

| Feature | Descrição |
|---------|-----------|
| `sandbox_create` | Cria container Docker isolado (node, python, dotnet) com CPU/RAM/rede configuráveis |
| `sandbox_start` / `sandbox_stop` | Controle de lifecycle |
| `sandbox_exec` | Executa comandos shell dentro do container |
| `sandbox_workspace_prepare` | Clona repo git com OverlayFS copy-on-write |
| `sandbox_metrics` | Métricas de CPU, memória, rede, disco em tempo real |
| `sandbox_network_info` | Políticas de rede disponíveis (Offline, Restricted, Full) |

### Exemplo de uso

```
1. sandbox_create(box_id, image="python", cpu=2, mem=512, timeout=30)
2. sandbox_start(sandbox_id)
3. sandbox_workspace_prepare(sandbox_id, git_repo_url="https://github.com/...")
4. sandbox_exec(sandbox_id, command="pytest --cov=src")
5. sandbox_destroy(sandbox_id)
```

### Vantagens sobre o Code Agent local

- **Isolamento por box**: cada box tem seus próprios sandboxes
- **Controle de recursos**: CPU, RAM e rede configuraveis
- **TTL automático**: containers são destruídos após timeout
- **Métricas**: monitoramento em tempo real via `sandbox_metrics`
- **Workspace seguro**: OverlayFS + detecção de symlinks

## Quando documentar no Briefapp

Se uma equipa usar sandboxes para tarefas, registar no wiki do projeto: imagem base, limites de recursos, e política de rede — para alinhar com `techStack` no config do projeto Briefapp.
