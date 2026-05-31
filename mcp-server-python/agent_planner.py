"""
Briefapp Atomic Planner — Agentic Planning API

Converts high-level user orders into fully decomposed Briefapp Atomic Flow plans
following the official skill specifications (briefapp-atomic-flow + briefapp-todo-list-v2).
Streams the plan as SSE to the frontend.
"""
import os
import json
import logging
from typing import Optional, List
from datetime import date, timedelta

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from starlette.responses import StreamingResponse
import httpx
from google import genai

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("briefapp.agent_planner")

app = FastAPI(title="Briefapp Agentic Planner API")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

API_BASE_URL = os.getenv("PANDORA_API_BASE_URL", "http://127.0.0.1:8480")
GEMINI_API_KEY = os.getenv("GEMINI_API_KEY")

if not GEMINI_API_KEY:
    try:
        from dotenv import load_dotenv
        env_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), ".env")
        load_dotenv(env_path, override=True)
        GEMINI_API_KEY = os.getenv("GEMINI_API_KEY")
    except ImportError:
        pass

if GEMINI_API_KEY:
    client = genai.Client(api_key=GEMINI_API_KEY)
else:
    logger.warning("GEMINI_API_KEY not found. Agent Planner might fail.")
    client = genai.Client()

# ---------------------------------------------------------------------------
# System Prompt — Briefapp Atomic Flow Architect
# ---------------------------------------------------------------------------
SYSTEM_PROMPT = """Você é o **Briefapp Atomic Flow Architect**, um agente especializado em decompor
intenções de alto nível em planos de engenharia de software completos, rastreáveis e executáveis.

Você segue RIGOROSAMENTE a metodologia **Atomic-Agent Flow** e o **Briefapp Todo List v2 MVP**.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
REGRA #0 — MULTIPLICADOR DE COMPLEXIDADE (C)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

O C define a DENSIDADE da decomposição. Use a fórmula:
  Backlogs = ceil(5 × C)
  Sprints/Backlog = ceil(2 × C)
  Tasks/Sprint = ceil(3 × C)
  Subtasks/Task = ceil(4 × C)

Tabela de referência:
| C   | Cenário                                  | Backlogs | Sprints/BL | Tasks/Sprint | Subtasks/Task | Max subtasks |
|-----|------------------------------------------|----------|------------|--------------|---------------|--------------|
| 0.2 | Bug fix / correção pontual              | 1        | 1          | 1            | 1             | ~1           |
| 0.5 | Feature pequena / melhoria simples      | 3        | 1          | 2            | 2             | ~12          |
| 1.0 | Novo módulo / iniciativa de médio porte | 5        | 2          | 3            | 4             | ~120         |
| 2.0 | Feature complexa / múltiplos domínios   | 10       | 4          | 6            | 8             | ~1.920       |
| 3.0 | Refactor estrutural / grande épico      | 15       | 6          | 9            | 12            | ~9.720       |

IMPORTANTE: Para C baixos (0.2, 0.5) não gere mais itens do que o necessário. Ajuste inteligentemente.
Para C altos (2, 3) decomponha com profundidade máxima — cada subtask deve ser atômica e executável
por um agente autônomo em 15-30 minutos.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
REGRA #1 — HIERARQUIA OBRIGATÓRIA
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

A hierarquia completa que DEVE ser respeitada é:
  Box/Epic → Backlog Items → Sprints → Tasks (Work Items) → Subtasks

Cada nível tem um propósito:
- **Backlog Item**: Área funcional ou domínio (ex: "Autenticação", "API de Notificações")
  - Campos: title, description, storyPoints (1-21), priority (0=Low, 1=Medium, 2=High, 3=Critical)
  - Tags/wikiRefs/constraints via backlog_context_update
- **Sprint**: Iteração temporal com objetivo claro (ex: "Sprint 1 — Scaffold e Auth Base")
  - Campos: name, goal, startDate (YYYY-MM-DD), endDate (YYYY-MM-DD), backlogItemIds[]
  - Duração típica: 1-2 semanas
- **Task (Work Item)**: Unidade de trabalho concreta dentro do sprint
  - Campos: title, description, assignee, branch, tags
  - Deve ter descrição técnica detalhada: o que implementar, quais arquivos criar/modificar,
    quais dependências, critérios de aceitação
- **Subtask**: Átomo de trabalho — a menor unidade executável
  - Campos: title, description, assignee, tags (Subtasks não possuem branch própria e trabalham na branch da task pai)
  - Cada subtask segue TDD: RED (teste falha) → GREEN (implementação mínima) → REFACTOR
  - Executável em 15-30 min por um agente autônomo
  - Quando TODOS os filhos estão Done, o pai é auto-completado

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
REGRA #2 — DETALHAMENTO DE TASKS E SUBTASKS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Cada Task DEVE conter:
1. Título claro e acionável (ex: "Implementar endpoint POST /api/auth/login")
2. Descrição técnica com:
   - Arquivos a criar/modificar
   - Dependências externas necessárias (pacotes, libs)
   - Padrão de design a seguir (Repository, Service, Controller, etc.)
   - Critérios de aceitação claros
   - Cobertura mínima de testes: ≥ 80%
3. Tags relevantes (ex: "backend,auth,api")
4. Branch sugerida seguindo a hierarquia: task/{task_id}

Cada Subtask DEVE conter:
1. Título ultra-específico (ex: "Criar teste unitário para validação de JWT expirado")
2. Descrição com:
   - Exatamente o que fazer (arquivo, classe, método)
   - Input/Output esperado
   - Qual passo TDD representa (🔴 RED, 🟢 GREEN, ou 🔵 REFACTOR)
3. Sem branch: Subtasks trabalham diretamente na branch da task pai.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
REGRA #3 — ARQUITETURA DE BRANCHES (3 NÍVEIS)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

A arquitetura de branches reflete a hierarquia com apenas 3 níveis:
  develop → feature/{backlog_id} → task/{task_id}

Merge simplificado:
  task/{task_id} → feature/{backlog_id} → develop

Subtasks NÃO criam branches próprias e trabalham diretamente na branch da task pai (task/{task_id}).
O próximo backlog/feature SEMPRE inicia da develop atualizada após o merge da feature anterior.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
REGRA #4 — TDD OBRIGATÓRIO POR SUBTASK
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Cada subtask produz 3 commits:
| Passo     | Ação                              | Commit format                        |
|-----------|-----------------------------------|--------------------------------------|
| 🔴 RED     | Teste que deve falhar             | test: add failing test for <feature> |
| 🟢 GREEN   | Implementação mínima para passar  | feat: implement <feature>            |
| 🔵 REFACTOR| Limpar código, testes devem passar| refactor: clean up <feature>         |

Todo commit DEVE ter o footer de rastreabilidade:
```
Refs: backlog/<backlog_id> | sprint/<sprint_id> | task/<task_id>
```

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
REGRA #5 — FORMATO DE SAÍDA
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Produza um plano em Markdown rico com estas seções OBRIGATÓRIAS:

## 📋 Resumo Executivo
Breve visão geral do épico/feature.

## 🔢 Parâmetros do Plano
C = X → Backlogs: N | Sprints gerados por Backlog em background: N

## 🗂️ Backlog Items
Para cada backlog:
### B1 — [Título]
- **Descrição**: ...
- **Story Points**: N
- **Priority**: High/Medium/Low/Critical
- **Tags**: tag1, tag2
- **Constraints**: pré-condições se houver

## ✅ Visão Geral de Execução
O plano de execução conterá o design arquitetural e a lista de Backlogs. Quando este plano for APROVADO, uma segunda fase de LLM em background detalhará cirurgicamente cada um dos Sprints, Tasks e Subtasks DENTRO de cada Backlog, criando-os na plataforma.

## 🔀 Diagrama de Dependências (Mermaid)
⚠️ IMPORTANTE: Use APENAS sintaxe simples `graph TD`. Evite colchetes [] ou parênteses () nos nomes dos nós se contiverem espaços ou caracteres especiais, use aspas " " (ex: B1["Nome do nó"]). NÃO use diagramas complexos, foque no formato genérico:
```mermaid
graph TD
  B1["Login"] --> B2["Dashboard"]
```

## 📚 Knowledge Base Inicial
O plano DEVE incluir Knowledge completo para que o projeto já nasça documentado:

### Wiki Pages (decisões de arquitetura e padrões técnicos)
Para cada domínio/backlog, gere uma wiki page com:
- Visão geral da arquitetura
- Padrões de design escolhidos e justificativas
- Diagramas de componentes (Mermaid quando aplicável)
- Dependências externas e versões
- Convenções de código e nomenclatura

### Documentation Pages (documentação voltada a desenvolvedores/equipe)
Gere documentação prática:
- Guia de Setup do ambiente de desenvolvimento
- Referência de API (endpoints, payloads, responses)
- Guia de contribuição e padrões de commit
- Mapa de variáveis de ambiente necessárias

### Knowledge Checkpoint (snapshot inicial do projeto)
Gere um checkpoint que capture:
- Estado atual do projeto antes da execução
- Decisões técnicas tomadas no planejamento
- Riscos identificados e mitigações propostas
- Próximas ações imediatas

## 📊 Plano CSV (briefapp_plan.csv)
Gerar o CSV completo no formato (apenas backlogs nesta fase, separado por backlog):
```csv
Type,ID,Title,Description,ParentID,Status
Box,box1,"Epic Title","...",,todo
Backlog,B1,"Backlog Title","...",box1,todo
Backlog,B2,"Outro Backlog","...",box1,todo
```

## 🚀 JSON Payload de Materialização
No FINAL do documento, inclua o payload JSON completo dentro de um bloco.
O payload DEVE incluir as seções `backlogs`, `wiki`, `documentation` e `checkpoint`:
```json_payload
{
  "backlogs": [
    {
      "title": "B1 — Título",
      "description": "Descrição completa do backlog",
      "storyPoints": 5,
      "priority": 2,
      "tags": "tag1,tag2"
    }
  ],
  "wiki": [
    {
      "title": "Arquitetura — Nome do Módulo",
      "content_markdown": "# Arquitetura\n\n## Visão Geral\n...\n## Stack\n...\n## Diagrama\n```mermaid\n...\n```",
      "tags": "arquitetura,backend",
      "category": "Architecture"
    }
  ],
  "documentation": [
    {
      "title": "Setup do Ambiente de Desenvolvimento",
      "content_markdown": "# Setup\n\n## Pré-requisitos\n...\n## Instalação\n...\n## Variáveis de Ambiente\n...",
      "category": "Setup",
      "tags": "setup,onboarding"
    },
    {
      "title": "Referência de API — Nome do Módulo",
      "content_markdown": "# API Reference\n\n## Endpoints\n...\n## Payloads\n...\n## Exemplos\n...",
      "category": "API",
      "tags": "api,reference"
    }
  ],
  "checkpoint": {
    "name": "Checkpoint Inicial — Nome do Épico",
    "context_snapshot": "Estado atual do projeto: ...",
    "decisions": "Decisões tomadas no planejamento: ...",
    "risks": "Riscos identificados: ...",
    "next_actions": "Próximas ações: ..."
  }
}
```

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
REGRA #6 — CONTEXT-FIRST EXECUTION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Antes de sugerir tasks, ANALISE o contexto do projeto fornecido:
- Dashboard: sprints ativos, work items pendentes, métricas
- Knowledge: wiki, docs, checkpoints
- Backlog existente: evite duplicar itens já existentes

Se houver backlog items, sprints ou work items existentes no contexto, referencie-os e construa 
o plano complementarmente — não duplique trabalho já planejado ou feito.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
REGRA #7 — CHECKLIST DE FINALIZAÇÃO POR BACKLOG
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Ao final de cada backlog, o agente executor deve:
1. backlog_context_update — tags finais, wikiRefs, constraints
2. wiki_add — documentar decisões técnicas e padrões de arquitetura
3. knowledge_checkpoint — salvar context_snapshot, decisions, risks, next_actions
4. Commit de fechamento com Refs: completo + git push
5. Merge: feature/{backlog_id} → develop

Inclua este checklist no plano para cada backlog.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
REGRA #8 — QUALIDADE DO PLANO
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

- NUNCA liste tasks e subtasks no markdown e json. Gere APENAS a visão de alto nível (Backlogs e Sprints). As tasks serão expandidas e injetadas de forma fracionada no backend após sua aprovação, para evitar alucinação.
- SEMPRE gere as datas dos sprints usando datas reais a partir da data atual.
- Responda SEMPRE em português brasileiro.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
REGRA #9 — KNOWLEDGE-FIRST: O PROJETO NASCE DOCUMENTADO
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

O plano DEVE incluir Knowledge completo no JSON payload, com 3 tipos de artefatos:

### Wiki Pages (wiki_add)
Gere pelo menos 1 wiki page por backlog item, cobrindo:
- Decisões de arquitetura e justificativas técnicas
- Padrões de design escolhidos (ex: Repository Pattern, CQRS, Event Sourcing)
- Diagramas Mermaid de componentes/fluxo de dados  
- Dependências externas e suas versões
- Trade-offs considerados e alternativas descartadas

Campos obrigatórios: title, content_markdown (Markdown rico), tags, category
Categorias recomendadas: Architecture, Design-Patterns, Integration, Security

### Documentation Pages (documentation_add)
Gere documentação prática orientada a desenvolvedores:
- **Setup**: pré-requisitos, instalação, variáveis de ambiente
- **API Reference**: endpoints, métodos, payloads, exemplos curl
- **Contributing Guide**: padrões de commit, branching, TDD flow
- **Data Model**: schema do banco, relações, migrations

Campos obrigatórios: title, content_markdown, category, tags
Categorias recomendadas: Setup, API, Architecture, Contributing, Database

### Knowledge Checkpoint (knowledge_checkpoint)
Gere um checkpoint inicial capturando:
- **context_snapshot**: Estado completo do projeto pré-execução (tech stack, módulos, integrações)
- **decisions**: Todas as decisões arquiteturais tomadas no planejamento, com justificativas
- **risks**: Riscos técnicos identificados, probabilidade e impacto, mitigações propostas
- **next_actions**: Primeiros passos concretos para iniciar a execução do plano

Campos obrigatórios: name, context_snapshot, decisions, risks, next_actions

Importante:
- Wiki/docs devem ter conteúdo REAL e detalhado em Markdown, não placeholders.
- Use diagramas Mermaid nos wikis para visualizar arquitetura.
- Documente TODAS as variáveis de ambiente necessárias no Setup.
- O checkpoint deve ser um snapshot genuíno do estado — não genérico.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
REGRA #10 — PARIDADE JSON ↔ MARKDOWN (ZERO DIVERGÊNCIA)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Esta é a regra MAIS CRÍTICA. O JSON payload NÃO É um resumo — é a RÉPLICA EXATA e COMPLETA
do plano descrito no Markdown. Sem exceções.

⚠️ IMPORTANTE PARA A SINTAXE JSON:
- Você NÃO PODE usar quebras de linha literais (ENTER real) dentro das strings do JSON. Se uma string (como `content_markdown`) precisar de quebra de linha, ESCREVA usar os caracteres "\\n" literalmente.
- Strings JSON não podem ter multilinhas.
- O formato deve ser perfeitamente processável por `JSON.parse()`.

### Regras de Paridade Obrigatória:

1. **TODOS os Backlogs listados no Markdown → TODOS no JSON `backlogs[]`**
   - Se o markdown menciona 18 backlogs, o JSON DEVE ter 18 objetos em `backlogs[]`.

2. **Wiki e Docs:** Devem ser cópias exatas do planejado, ricas em Markdown.

### Antes de fechar o bloco ```json_payload, VALIDE mentalmente:
✅ Contagem de backlogs no JSON == contagem no markdown?
✅ Wikis têm >500 chars de content_markdown com Mermaid?
✅ Docs têm >300 chars com comandos reais?
✅ Checkpoint tem >150 chars por campo?

Se QUALQUER validação falhar, CORRIJA antes de emitir o JSON.
"""


class PlanRequest(BaseModel):
    project_id: str
    order: str
    complexity_multiplier: float = 1.0
    feedback_history: Optional[List[str]] = []
    max_tokens_budget: int = 200000  # Token budget limit for the continuation loop
    max_iterations: int = 15  # Max continuation rounds


# ---------------------------------------------------------------------------
# Constants for the continuation loop
# ---------------------------------------------------------------------------
PLAN_COMPLETION_MARKER = "```"  # End of the json_payload block
PLAN_START_MARKER = "```json_payload"
CHARS_PER_TOKEN = 4  # Approximate: 1 token ≈ 4 characters

CONTINUATION_PROMPT = """⚠️ Sua resposta anterior foi TRUNCADA antes de completar o plano.

Abaixo está o que você já gerou. CONTINUE EXATAMENTE de onde parou — NÃO repita o conteúdo já gerado.
Se você estava no meio do markdown, continue o markdown.
Se você ainda não gerou o bloco ```json_payload, gere-o COMPLETO ao chegar na seção correspondente.
O plano SÓ está completo quando o bloco ```json_payload``` estiver fechado.

--- CONTEÚDO JÁ GERADO (últimos 4000 chars como referência) ---
{tail}
--- FIM DO CONTEÚDO ANTERIOR ---
CONTINUE a partir daqui. NÃO repita nada. Apenas continue gerando o conteúdo restante."""

EXPAND_BACKLOG_PROMPT = """Sua missão como Briefapp Atomic Flow Architect é pegar um único Backlog aprovado e extraí-lo em profunda minúcia de engenharia.
Você fará fracionamento detalhado, gerando Sprints, Tasks e Subtasks, evitando alucinações.

## Projeto Geral
{context_str}

## Expansão Atual
Expanda o seguinte Backlog: '{backlog_title}'
- Descrição do Backlog: '{backlog_desc}'

## Meta de Complexidade (C={c})
- Sprints obrigatórios para este Backlog: pelo menos {expected_sprints}
- Tasks obrigatórias por Sprint: pelo menos {expected_tasks}
- Subtasks TDD obrigatórias por Task: pelo menos {expected_subtasks}

## Instruções
Cada subtask gerada DEVE OBRIGATORIAMENTE seguir o fluxo TDD, incluindo a tag vermelha/verde/azul no title e nas tags (test,tdd-red | feat,tdd-green | refactor,tdd-refactor).
Gerar exclusivamente em Português Brasileiro.

RESPOSTA OBRIGATÓRIA (Forneça apenas este JSON encapsulado):
```json_payload
{{
  "sprints": [
    {{
      "name": "Sprint 1 — Fase de XYZ",
      "goal": "Objetivo detalhado",
      "startDate": "YYYY-MM-DD",
      "endDate": "YYYY-MM-DD",
      "tasks": [
        {{
          "title": "T1 — [Título Técnico]",
          "description": "Especificação altíssima detalhando arquitetura, DTOs e APIs...",
          "tags": "api,backend...",
          "branch": "task/t1-nome",
          "subtasks": [
              {{ "title": "ST1.1 — 🔴 RED — Falhar teste auth", "description": "Criar em src/tests..", "tags": "test,tdd-red" }}
           ]
         }}
       ]
     }}
   ]
}}
```

⚠️ REGRAS DE SINTAXE JSON:
Você DEVE usar o formato JSON estrito.
NUNCA introduza quebras de linha reais (ENTER) não-escapadas dentro dos valores das strings. 
Se você precisa pular linhas na `description` do backlog ou tests, escreva literalmente `\\n` (barra invertida + n).
Se houver aspas, escape com `\\"`.
⚠️ REGRAS DE SINTAXE JSON: Você DEVE usar o formato JSON estrito, sem quebras de linha unescaped (ENTER) dentro das strings! Substitua quebras de linha reais por `\\n`, e escape aspas duplas internas de markdown como `\\"`.
"""


async def fetch_project_context(project_id: str) -> dict:
    """Fetch all relevant project context from Briefapp API."""
    context = {"dashboard": {}, "knowledge": {}, "backlog": [], "sprints": [], "workitems": []}
    async with httpx.AsyncClient(timeout=30.0) as http:
        endpoints = {
            "dashboard": f"{API_BASE_URL}/api/projects/{project_id}/dashboard",
            "knowledge": f"{API_BASE_URL}/api/projects/{project_id}/knowledge",
            "backlog": f"{API_BASE_URL}/api/projects/{project_id}/backlog",
            "sprints": f"{API_BASE_URL}/api/projects/{project_id}/sprints",
            "workitems": f"{API_BASE_URL}/api/projects/{project_id}/workitems",
        }
        for key, url in endpoints.items():
            try:
                res = await http.get(url)
                if res.status_code == 200:
                    context[key] = res.json()
            except Exception as e:
                logger.warning(f"Failed to fetch {key}: {e}")
    return context


def estimate_tokens(text: str) -> int:
    """Estimate token count from character count."""
    return len(text) // CHARS_PER_TOKEN


def is_plan_complete(accumulated_text: str) -> bool:
    """Check if the plan has been fully generated (json_payload block is closed)."""
    # The plan is complete when we find the json_payload opening AND a closing ``` after it
    start_idx = accumulated_text.rfind(PLAN_START_MARKER)
    if start_idx == -1:
        return False
    # Look for the closing ``` after the json_payload opening
    after_start = accumulated_text[start_idx + len(PLAN_START_MARKER):]
    # Need at least some JSON content and a closing ```
    closing_idx = after_start.rfind("\n```")
    if closing_idx == -1:
        # Also check for ``` at the very end
        if after_start.rstrip().endswith("```"):
            return True
        return False
    # Verify there's actual content between opening and closing
    content_between = after_start[:closing_idx].strip()
    return len(content_between) > 100  # At least some JSON content


@app.post("/api/agent/plan/stream")
async def stream_plan(req: PlanRequest):
    context = await fetch_project_context(req.project_id)
    context_str = json.dumps(context, indent=2, default=str)

    # Calculate dates for sprints
    today = date.today()
    today_str = today.isoformat()

    # Pre-calculate expected counts from C
    import math
    c = req.complexity_multiplier
    expected_backlogs = max(1, math.ceil(5 * c))
    expected_sprints_per_bl = max(1, math.ceil(2 * c))
    expected_tasks_per_sprint = max(1, math.ceil(3 * c))
    expected_subtasks_per_task = max(1, math.ceil(4 * c))

    user_prompt = f"""## Ordem do Usuário
{req.order}

## Multiplicador de Complexidade
C = {c}

Contagens OBRIGATÓRIAS calculadas pela fórmula:
- Backlogs no JSON: EXATAMENTE {expected_backlogs} objetos em `backlogs[]`

⚠️ ATENÇÃO DE PARIDADE (REGRA #10):
O JSON payload DEVE conter A LISTA COMPLETA de Backlogs.
As Sprints, Tasks e Subtasks NÃO devem ser detalhadas nesta fase, elas ocorrerão em background individualmente por backlog na fase de Materialização.

## Histórico de Feedback (iterações anteriores)
{json.dumps(req.feedback_history, ensure_ascii=False) if req.feedback_history else "Nenhum — este é o primeiro rascunho."}

## Data Atual
{today_str}

## Contexto do Projeto (Briefapp API)
{context_str[:45000]}

---

## INSTRUÇÕES FINAIS DE GERAÇÃO

1. Gere o plano completo seguindo TODAS as 10 regras do system prompt.
2. Seja MÁXIMO-DETALHISTA nas tasks e subtasks — nada de stubs ou placeholders.
3. O JSON payload `json_payload` DEVE ser focado apenas em Backlogs e recursos do projeto de alto nível.
4. Inclua TODOS os {expected_backlogs} backlogs no JSON.
5. Sprints e Tasks serão delegados para o stream secundário, NÃO as adicione no JSON.
6. Wikis com Mermaid real. Docs com comandos reais. Checkpoint com dados reais do projeto.
7. Responda em português brasileiro.

⚠️ IMPORTANTE — MODO DE CONTINUAÇÃO ATIVO:
Se sua resposta for truncada por limite de tokens, você será chamado novamente com o contexto
acumulado para CONTINUAR de onde parou. Portanto, NÃO tente comprimir ou resumir o conteúdo
para caber em uma única resposta. Gere TUDO com máximo detalhamento — a continuação é automática."""

    async def event_generator():
        accumulated_text = ""
        total_tokens_used = 0
        total_prompt_tokens = 0
        total_candidate_tokens = 0
        iteration = 0
        max_iterations = req.max_iterations
        token_budget = req.max_tokens_budget

        # Build the initial conversation history
        conversation = [
            {"role": "user", "parts": [{"text": SYSTEM_PROMPT}]},
            {"role": "model", "parts": [{"text": "Entendido. Sou o Briefapp Atomic Flow Architect. Estou pronto para gerar planos completos seguindo rigorosamente a metodologia Atomic-Agent Flow com decomposição hierárquica, TDD obrigatório, branching em cascata e rastreabilidade total. Aguardando a ordem do usuário e o contexto do projeto."}]},
            {"role": "user", "parts": [{"text": user_prompt}]},
        ]

        while iteration < max_iterations and total_tokens_used < token_budget:
            iteration += 1
            iteration_text = ""

            # Send iteration metadata to frontend
            yield f"data: {json.dumps({'meta': {'iteration': iteration, 'tokens_used': total_tokens_used, 'budget': token_budget, 'status': 'generating'}})}\n\n"

            try:
                logger.info(f"[Loop {iteration}/{max_iterations}] Generating... (tokens so far: {total_tokens_used}/{token_budget})")

                response = client.models.generate_content_stream(
                    model="gemini-3-flash-preview",
                    contents=conversation,
                )

                last_usage = None
                for chunk in response:
                    if chunk.text:
                        iteration_text += chunk.text
                        yield f"data: {json.dumps({'text': chunk.text})}\n\n"

                    # Capture usage_metadata from each chunk (the last one has final counts)
                    if hasattr(chunk, 'usage_metadata') and chunk.usage_metadata:
                        last_usage = chunk.usage_metadata

                    # Check token budget mid-stream using estimate as fallback
                    current_tokens = estimate_tokens(iteration_text)
                    if total_tokens_used + current_tokens > token_budget:
                        logger.warning(f"[Loop {iteration}] Token budget exhausted mid-stream")
                        break

            except Exception as e:
                logger.error(f"[Loop {iteration}] Generate stream failed: {e}")
                yield f"data: {json.dumps({'error': str(e), 'iteration': iteration})}\n\n"
                break

            # Accumulate and count — prefer real usage_metadata over estimation
            accumulated_text += iteration_text
            if last_usage:
                prompt_tk = getattr(last_usage, 'prompt_token_count', 0) or 0
                candidate_tk = getattr(last_usage, 'candidates_token_count', 0) or 0
                tokens_this_iteration = prompt_tk + candidate_tk
                total_prompt_tokens += prompt_tk
                total_candidate_tokens += candidate_tk
                logger.info(f"[Loop {iteration}] 📊 REAL usage_metadata: prompt={prompt_tk}, candidates={candidate_tk}")
            else:
                tokens_this_iteration = estimate_tokens(iteration_text)
                logger.info(f"[Loop {iteration}] ⚠️ No usage_metadata — using estimate: {tokens_this_iteration}")
            total_tokens_used += tokens_this_iteration

            logger.info(f"[Loop {iteration}] Generated ~{tokens_this_iteration} tokens this round. Total: ~{total_tokens_used}")

            # Check if plan is complete
            if is_plan_complete(accumulated_text):
                logger.info(f"[Loop {iteration}] ✅ Plan COMPLETE — json_payload block detected and closed.")
                yield f"data: {json.dumps({'meta': {'iteration': iteration, 'tokens_used': total_tokens_used, 'budget': token_budget, 'status': 'complete'}})}\n\n"
                break

            # Check if budget is exhausted
            if total_tokens_used >= token_budget:
                logger.warning(f"[Loop {iteration}] ⛔ Token budget exhausted ({total_tokens_used}/{token_budget})")
                yield f"data: {json.dumps({'meta': {'iteration': iteration, 'tokens_used': total_tokens_used, 'budget': token_budget, 'status': 'budget_exhausted'}})}\n\n"
                break

            # Check if this iteration produced almost no output (model thinks it's done but plan isn't complete)
            if tokens_this_iteration < 50:
                logger.warning(f"[Loop {iteration}] ⚠️ Model produced very little output ({tokens_this_iteration} tokens). Breaking to avoid infinite loop.")
                yield f"data: {json.dumps({'meta': {'iteration': iteration, 'tokens_used': total_tokens_used, 'budget': token_budget, 'status': 'stalled'}})}\n\n"
                break

            # Plan is NOT complete — prepare continuation
            logger.info(f"[Loop {iteration}] Plan incomplete — preparing continuation prompt...")
            yield f"data: {json.dumps({'meta': {'iteration': iteration, 'tokens_used': total_tokens_used, 'budget': token_budget, 'status': 'continuing'}})}\n\n"

            # Add the model's partial response and a continuation prompt to the conversation
            tail = accumulated_text[-4000:]  # Last 4000 chars for context
            continuation = CONTINUATION_PROMPT.format(tail=tail)

            conversation.append({"role": "model", "parts": [{"text": iteration_text}]})
            conversation.append({"role": "user", "parts": [{"text": continuation}]})

        # Final done signal with real token breakdown
        yield f"data: {json.dumps({'done': True, 'total_iterations': iteration, 'total_tokens': total_tokens_used, 'prompt_tokens': total_prompt_tokens, 'candidate_tokens': total_candidate_tokens, 'source': 'usage_metadata' if total_prompt_tokens > 0 else 'estimate'})}\n\n"

    return StreamingResponse(event_generator(), media_type="text/event-stream")


class ExecuteRequest(BaseModel):
    project_id: str
    plan_payload: dict
    complexity_multiplier: float = 1.0


async def expand_backlog_to_sprints_and_tasks(http: httpx.AsyncClient, project_id: str, backlog: dict, c: float) -> list:
    import math
    import re
    expected_sprints = max(1, math.ceil(2 * c))
    expected_tasks = max(1, math.ceil(3 * c))
    expected_subtasks = max(1, math.ceil(4 * c))

    context = await fetch_project_context(project_id)
    context_str = json.dumps(context, indent=2, default=str)[:15000]

    prompt = EXPAND_BACKLOG_PROMPT.format(
        context_str=context_str,
        backlog_title=backlog.get("title", ""),
        backlog_desc=backlog.get("description", ""),
        c=c,
        expected_sprints=expected_sprints,
        expected_tasks=expected_tasks,
        expected_subtasks=expected_subtasks
    )

    try:
        response = client.models.generate_content(
            model="gemini-3-flash-preview",
            contents=prompt,
        )

        # Log real token usage from the SDK
        if hasattr(response, 'usage_metadata') and response.usage_metadata:
            um = response.usage_metadata
            prompt_tk = getattr(um, 'prompt_token_count', 0) or 0
            candidate_tk = getattr(um, 'candidates_token_count', 0) or 0
            logger.info(f"📊 expand_backlog '{backlog.get('title', '?')}' usage: prompt={prompt_tk}, candidates={candidate_tk}, total={prompt_tk + candidate_tk}")
        
        text = response.text
        start_idx = text.find("```json_payload")
        if start_idx != -1:
            after_start = text[start_idx + len("```json_payload"):]
            end_idx = after_start.rfind("```")
            raw_json = after_start[:end_idx].strip() if end_idx != -1 else after_start.strip()
        else:
            # Fallback block matching
            block_match = re.search(r"```json(?:_payload)?(.*?)```", text, re.DOTALL)
            if block_match:
                raw_json = block_match.group(1).strip()
            else:
                raw_json = text.strip()
                if raw_json.startswith("{") and raw_json.endswith("}"):
                    pass
                else:
                    return []
        
        parsed = json.loads(raw_json)
        return parsed.get("sprints", [])
    except json.decoder.JSONDecodeError as e:
        logger.error(f"JSON Parse Error when expanding backlog {backlog.get('title')}: {e} | Raw={raw_json[:200]}")
        return []
    except Exception as e:
        logger.error(f"Failed to expand backlog {backlog.get('title')}: {e}")
        return []


GENERATE_CSV_PROMPT = """Você é um especialista em estruturação de dados.
Sua única missão é transformar a lista de backlogs recém-aprovados em um formato CSV estrito.

Backlogs Originais em JSON:
{plan_json}

Regras:
1. O formato CSV DEVE ter exatamente este cabeçalho OBRIGATÓRIO (use vírgulas):
Type,ID,Title,Description,StoryPoints,Priority,Tags
2. O Type sempre será "Backlog".
3. O ID será sequencial (B1, B2, B3...).
4. Se um campo não existir, deixe vazio, mas mantenha a vírgula. StoryPoints padrao é 3, Priority padrao é 1.
5. Forneça APENAS o CSV puro. SEM formatação markdown (```csv). Não adicione saudações.

Gere o CSV:
"""

async def generate_csv_from_plan(plan_payload: dict) -> str:
    prompt = GENERATE_CSV_PROMPT.format(plan_json=json.dumps(plan_payload.get("backlogs", []), indent=2, ensure_ascii=False))
    
    try:
        response = client.models.generate_content(
            model="gemini-3-flash-preview",
            contents=prompt,
        )
        text = response.text.strip()
        if text.startswith("```csv"):
            text = text[6:]
        elif text.startswith("```"):
            text = text[3:]
        if text.endswith("```"):
            text = text[:-3]
        return text.strip()
    except Exception as e:
        logger.error(f"Error generating CSV: {e}")
        return ""

from fastapi.responses import StreamingResponse

@app.post("/api/agent/execute")
async def execute_plan(req: ExecuteRequest):
    """
    Materializes a plan by creating backlogs, sprints, and work items
    via the Briefapp API. Streams progress back to the client.
    """
    logger.info(f"Executing plan for project {req.project_id}...")

    async def generate_execution_stream():
        results = {
            "backlogs_created": 0, "sprints_created": 0, "tasks_created": 0,
            "subtasks_created": 0, "wiki_created": 0, "docs_created": 0,
            "checkpoints_created": 0, "errors": [],
        }

        def yield_msg(msg: str, done: bool = False):
            payload = {"message": msg, "done": done}
            if done: payload["results"] = results
            return f"data: {json.dumps(payload)}\n\n"

        async with httpx.AsyncClient(timeout=60.0) as http:
            # ── Phase 0: Agente Gera CSV ──────────────
            yield yield_msg("🔄 Agente Data-Structurer gerando planialha CSV do projeto aprovado...")
            csv_text = await generate_csv_from_plan(req.plan_payload)
            if not csv_text:
                yield yield_msg("⚠️ Falha ao gerar CSV. Abortando execução.", done=True)
                return
                
            yield yield_msg(f"📊 CSV gerado com sucesso. Lendo {len(csv_text.splitlines()) - 1} backlogs inseridos.")
            
            import csv
            import io
            reader = csv.DictReader(io.StringIO(csv_text))
            
            # ── Phase 1: Backlogs → Sprints → Tasks → Subtasks ──────────────
            for row in reader:
                # Caso alguma coluna não venha mapeada corretamente, podemos fazer fallbacks seguros
                b_title = row.get("Title", "Untitled Backlog")
                if not b_title or b_title.strip() == "":
                    continue

                yield yield_msg(f"📦 Criando Backlog (via CSV): '{b_title}'...")
                
                try:
                    sp = int(row.get("StoryPoints", 3))
                except:
                    sp = 3
                    
                try:
                    prio = int(row.get("Priority", 1))
                except:
                    prio = 1
                
                b_payload = {
                    "title": b_title,
                    "description": row.get("Description", ""),
                    "storyPoints": sp,
                    "priority": prio,
                }
                try:
                    res_b = await http.post(f"{API_BASE_URL}/api/projects/{req.project_id}/backlog", json=b_payload)
                    if res_b.status_code not in (200, 201):
                        results["errors"].append(f"Backlog '{b_title}': {res_b.status_code}")
                        continue
                    b_data = res_b.json()
                    b_id = b_data.get("id")
                    results["backlogs_created"] += 1

                    # Update tags/context if provided
                    b_tags = row.get("Tags", "")
                    if b_tags:
                        await http.patch(
                            f"{API_BASE_URL}/api/backlog-items/{b_id}/context",
                            json={"tags": b_tags},
                        )

                    # Expand Backlog into Sprints and Tasks
                    yield yield_msg(f"🧠 Expandindo Backlog '{b_title}' em Sprints, Tasks e Subtasks (Pode demorar vários segundos)...")
                    # Para expandir, precisamos passar um dict, usamos o b_payload original + tags
                    b_payload["tags"] = b_tags
                    expanded_sprints = await expand_backlog_to_sprints_and_tasks(http, req.project_id, b_payload, req.complexity_multiplier)
                    
                    if not expanded_sprints:
                        yield yield_msg(f"⚠️ Atenção: Nenhum sprint/task gerado retornado para o Backlog '{b_title}'.")

                    # Create sprints
                    for s_item in expanded_sprints:
                        s_name = s_item.get("name")
                        yield yield_msg(f"🏃 Criando Sprint: {s_name}...")
                        s_payload = {
                            "name": s_name,
                            "goal": s_item.get("goal", ""),
                            "startDate": s_item.get("startDate", date.today().isoformat()),
                            "endDate": s_item.get("endDate", (date.today() + timedelta(days=14)).isoformat()),
                            "backlogItemIds": [b_id]
                        }
                        try:
                            res_s = await http.post(f"{API_BASE_URL}/api/projects/{req.project_id}/sprints", json=s_payload)
                            if res_s.status_code not in (200, 201):
                                results["errors"].append(f"Sprint '{s_name}': {res_s.status_code}")
                                continue
                            s_data = res_s.json()
                            s_id = s_data.get("id")
                            results["sprints_created"] += 1

                            expanded_tasks = s_item.get("tasks", [])
                            yield yield_msg(f"✅ Inserindo {len(expanded_tasks)} Tasks no Sprint '{s_name}'...")

                            for t_item in expanded_tasks:
                                wi_payload = {
                                    "title": t_item.get("title"),
                                    "description": t_item.get("description", ""),
                                    "assignee": "pending",
                                    "branch": t_item.get("branch", ""),
                                    "tags": t_item.get("tags", "")
                                }
                                try:
                                    res_wi = await http.post(f"{API_BASE_URL}/api/projects/{req.project_id}/workitems?sprintId={s_id}", json=wi_payload)
                                    if res_wi.status_code not in (200, 201):
                                        results["errors"].append(f"Task '{wi_payload['title']}': {res_wi.status_code}")
                                        continue
                                    target_wi = res_wi.json()
                                except Exception as e:
                                    results["errors"].append(f"Task error: {e}")
                                    continue

                                results["tasks_created"] += 1

                                if target_wi:
                                    wi_id = target_wi.get("id")
                                    subtasks_list = t_item.get("subtasks", [])
                                    if subtasks_list:
                                        yield yield_msg(f"  ↳ Inserindo {len(subtasks_list)} Subtasks na Task: '{wi_payload['title'][:30]}...'")
                                    for st_item in subtasks_list:
                                        st_payload = {
                                            "title": st_item.get("title"),
                                            "description": st_item.get("description", ""),
                                            "tags": st_item.get("tags", ""),
                                        }
                                        try:
                                            res_st = await http.post(
                                                f"{API_BASE_URL}/api/work-items/{wi_id}/sub-tasks",
                                                json=st_payload,
                                            )
                                            if res_st.status_code in (200, 201):
                                                results["subtasks_created"] += 1
                                            else:
                                                results["errors"].append(
                                                    f"Subtask '{st_payload['title']}': {res_st.status_code}"
                                                )
                                        except Exception as e:
                                            results["errors"].append(f"Subtask error: {e}")
                        except Exception as e:
                            results["errors"].append(f"Sprint error: {e}")
                except Exception as e:
                    import traceback
                    logger.error(f"Backlog exception: {traceback.format_exc()}")
                    results["errors"].append(f"Backlog error: {e}")

        # ── Phase 2: Wiki Pages ─────────────────────────────────────────
        for wiki_item in req.plan_payload.get("wiki", []):
            wiki_payload = {
                "title": wiki_item.get("title"),
                "contentMarkdown": wiki_item.get("content_markdown", ""),
                "tags": wiki_item.get("tags", ""),
                "category": wiki_item.get("category", "General"),
            }
            try:
                res_w = await http.post(
                    f"{API_BASE_URL}/api/projects/{req.project_id}/wiki",
                    json=wiki_payload,
                )
                if res_w.status_code in (200, 201):
                    results["wiki_created"] += 1
                else:
                    results["errors"].append(f"Wiki '{wiki_payload['title']}': {res_w.status_code}")
            except Exception as e:
                results["errors"].append(f"Wiki error: {e}")

        # ── Phase 3: Documentation Pages ────────────────────────────────
        for doc_item in req.plan_payload.get("documentation", []):
            doc_payload = {
                "title": doc_item.get("title"),
                "contentMarkdown": doc_item.get("content_markdown", ""),
                "category": doc_item.get("category", "General"),
                "tags": doc_item.get("tags", ""),
            }
            try:
                res_d = await http.post(
                    f"{API_BASE_URL}/api/projects/{req.project_id}/documentation",
                    json=doc_payload,
                )
                if res_d.status_code in (200, 201):
                    results["docs_created"] += 1
                else:
                    results["errors"].append(f"Doc '{doc_payload['title']}': {res_d.status_code}")
            except Exception as e:
                results["errors"].append(f"Documentation error: {e}")

        # ── Phase 4: Knowledge Checkpoint ───────────────────────────────
        cp = req.plan_payload.get("checkpoint")
        if cp:
            cp_payload = {
                "name": cp.get("name", "Checkpoint Inicial"),
                "contextSnapshot": cp.get("context_snapshot", ""),
                "decisions": cp.get("decisions", ""),
                "risks": cp.get("risks", ""),
                "nextActions": cp.get("next_actions", ""),
            }
            try:
                res_cp = await http.post(
                    f"{API_BASE_URL}/api/projects/{req.project_id}/checkpoints",
                    json=cp_payload,
                )
                if res_cp.status_code in (200, 201):
                    results["checkpoints_created"] += 1
                else:
                    results["errors"].append(f"Checkpoint: {res_cp.status_code}")
            except Exception as e:
                results["errors"].append(f"Checkpoint error: {e}")

        # Final yield
        yield yield_msg("🎉 Plano materializado com sucesso!", done=True)
        
    return StreamingResponse(generate_execution_stream(), media_type="text/event-stream")


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8483)
