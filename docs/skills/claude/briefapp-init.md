# briefapp-init — Inicialização de Sessão com Briefapp

Ao iniciar qualquer sessão de desenvolvimento, execute este fluxo obrigatório:

## 1. Carregar Contexto do Projeto

Use `mcp__local__project_list` para listar projetos e identificar o projeto ativo no workspace atual.

Com o `project_id` em mãos, confirme via `mcp__local__project_config_update`:
- `mainBranch` — branch de trabalho padrão
- `gitHubUrl` — repositório remoto
- `localPath` — caminho local do projeto
- `techStack` — stack tecnológica

Use essas informações em **todas** as decisões da sessão (branch de trabalho, caminhos, stack).

## 2. Verificar Backlog e Work Items Ativos

- `mcp__local__backlog_list` — listar itens de backlog
- `mcp__local__workitem_list` — listar work items e identificar o que está `in_progress`

Se houver backlog items livres sem tarefa ativa, **pergunte ao usuário** antes de executar.

## 3. Verificar Base de Conhecimento

- `mcp__local__wiki_list` — páginas de arquitetura e decisões
- `mcp__local__checkpoint_list` — checkpoints anteriores (decisões, riscos, próximos passos)

## 4. Confirmar com o Usuário

Apresente um resumo do estado atual:
- Projeto ativo e stack
- Tasks em progresso (se houver)
- Próximos itens do backlog

**Pergunte se é necessário atualizar bases de conhecimento** (wiki, docs, checkpoint, README) antes de começar.

---

**Regras inegociáveis:**
- Sem este init, não inicie codificação
- Se o projeto não existir no Briefapp, crie via `mcp__local__project_create`
- Evite duplicatas — sempre verifique antes de criar
- Ao iniciar **ou** concluir qualquer tarefa, pergunte ao usuário sobre atualização das bases de conhecimento
- Se o projeto não tiver repositório git, crie um localmente — commits são obrigatórios por task
- Se o repositório remoto não existir, crie via `gh repo create` antes do commit de fechamento do backlog
