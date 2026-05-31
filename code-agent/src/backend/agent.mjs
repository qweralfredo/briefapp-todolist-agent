import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { DEFAULT_MODEL, DEFAULT_WORKSPACE_PATH, MAX_ACTIONS, MAX_AGENT_CYCLES, SESSION_DIR, WORKSPACE_ROOT } from "./config.mjs";
import { chatJson } from "./ollama.mjs";
import { discoverSkills, loadSkills } from "./skills.mjs";
import { executeToolActionByPath, getWorkspaceSummaryByPath, listFilesByPath } from "./workspace.mjs";

const sessions = new Map();

function sessionFilePath(sessionId) {
  return path.join(SESSION_DIR, `${sessionId}.json`);
}

export function hydrateSessionsFromDisk() {
  if (!fs.existsSync(SESSION_DIR)) return;
  for (const file of fs.readdirSync(SESSION_DIR)) {
    if (!file.endsWith(".json")) continue;
    try {
      const session = JSON.parse(fs.readFileSync(path.join(SESSION_DIR, file), "utf8"));
      if (session?.id) sessions.set(session.id, session);
    } catch {
      // corrupt file — skip
    }
  }
}

function persistSession(session) {
  fs.writeFileSync(sessionFilePath(session.id), JSON.stringify(session, null, 2), "utf8");
}

function nowIso() {
  return new Date().toISOString();
}

function sanitizeAction(action) {
  if (!action || typeof action !== "object") {
    return null;
  }

  const type = String(action.type ?? "");
  if (!type) {
    return null;
  }

  const allowed = { type };
  if (typeof action.path === "string") {
    allowed.path = action.path;
  }
  if (typeof action.command === "string") {
    allowed.command = action.command;
  }
  if (typeof action.content === "string") {
    allowed.content = action.content;
  }
  if (typeof action.depth === "number") {
    allowed.depth = action.depth;
  }
  return allowed;
}

function buildHeuristicPlan(prompt, selectedSkills) {
  const steps = [
    "Inspect the available workspace context and constraints.",
    "Decide the smallest set of actions needed to satisfy the request.",
    "Apply safe changes or collect evidence with the allowed tools.",
    "Summarize results, touched files, commands and remaining risks.",
  ];

  if (selectedSkills.length > 0) {
    steps.splice(1, 0, `Use the selected skills: ${selectedSkills.map((skill) => skill.name).join(", ")}.`);
  }

  if (/test|validate|verify/i.test(prompt)) {
    steps.push("Include explicit verification notes for the user.");
  }

  return steps;
}

function buildSkillPrompt(selectedSkills) {
  if (selectedSkills.length === 0) {
    return "No additional skills were selected.";
  }

  return selectedSkills
    .map((skill) => {
      const references = skill.referenceContents
        .map((reference) => `Reference ${reference.name}:\n${reference.content}`)
        .join("\n\n");

      return [
        `Skill: ${skill.name}`,
        `Description: ${skill.description}`,
        skill.body,
        references,
      ]
        .filter(Boolean)
        .join("\n\n");
    })
    .join("\n\n---\n\n");
}

function buildToolContract() {
  return [
    "Return JSON only.",
    "Use this schema:",
    "{",
    '  "reply": "assistant response for the user",',
    '  "plan": ["short step", "short step"],',
    '  "actions": [',
    '    {"type":"list_files","path":".","depth":2},',
    '    {"type":"read_file","path":"README.md"},',
    '    {"type":"write_file","path":"notes.txt","content":"..."},',
    '    {"type":"run_command","command":"dir"}',
    "  ],",
    '  "done": true',
    "}",
    "Only request actions that are strictly necessary.",
    "Never reference files outside the sandbox workspace.",
    "If the task is complete, return done=true and actions=[].",
  ].join("\n");
}

function buildSystemPrompt(session, selectedSkills) {
  const workspace = getWorkspaceSummaryByPath(session.workspacePath);

  return [
    "You are Code Agent, a local coding assistant inspired by GitHub Copilot and Claude Code.",
    "Behave like a cautious senior engineer: inspect first, plan clearly, change minimally, verify explicitly.",
    "You have access only to a sandbox workspace and a constrained tool API.",
    `Workspace path: ${workspace.workspacePath}`,
    `Workspace root fence: ${workspace.root}`,
    `Selected model: ${session.model}`,
    "",
    "Operational rules:",
    "- Never attempt to access files outside the sandbox workspace.",
    "- Prefer listing or reading files before writing.",
    "- Keep plans short and actionable.",
    "- Mention risks when tool output is incomplete.",
    "- If you request tool actions, still provide a short operator-facing reply in the same JSON payload.",
    "",
    "Loaded skills:",
    buildSkillPrompt(selectedSkills),
    "",
    buildToolContract(),
  ].join("\n");
}

function buildLocalToolSummary(prompt, toolResults, session) {
  const lines = [`Completed a local coding-agent pass for: ${prompt}`];

  const successfulReads = toolResults.filter((entry) => entry.action?.type === "read_file" && entry.result?.path);
  const successfulWrites = toolResults.filter((entry) => entry.action?.type === "write_file" && entry.result?.path);
  const successfulCommands = toolResults.filter((entry) => entry.action?.type === "run_command" && entry.result);
  const failures = toolResults.filter((entry) => entry.error);

  if (successfulReads.length > 0) {
    lines.push(`Read files: ${successfulReads.map((entry) => entry.result.path).join(", ")}.`);
  }
  if (successfulWrites.length > 0) {
    lines.push(`Wrote files: ${successfulWrites.map((entry) => entry.result.path).join(", ")}.`);
  }
  if (successfulCommands.length > 0) {
    lines.push(`Executed commands: ${successfulCommands.map((entry) => entry.action.command).join(" | ")}.`);
  }
  if (session.touchedFiles.length > 0) {
    lines.push(`Touched files now tracked by the session: ${session.touchedFiles.join(", ")}.`);
  }
  if (failures.length > 0) {
    lines.push(`Some tool calls failed: ${failures.map((entry) => entry.error).join(" | ")}.`);
  }
  if (toolResults.length === 0) {
    lines.push("No tool actions were executed in this pass.");
  }

  lines.push("Review the session panels for the exact plan and artifacts.");
  return lines.join(" ");
}

async function executeActions(session, actions, emit) {
  const results = [];
  for (const action of actions.slice(0, MAX_ACTIONS)) {
    const safeAction = sanitizeAction(action);
    if (!safeAction) {
      continue;
    }

    emit("tool:start", safeAction);
    try {
      const result = await executeToolActionByPath(safeAction, session.workspacePath);
      results.push({ action: safeAction, result });

      if ((safeAction.type === "write_file" || safeAction.type === "read_file") && typeof safeAction.path === "string") {
        session.touchedFiles = Array.from(new Set([...session.touchedFiles, safeAction.path]));
      }
      if (safeAction.type === "run_command" && typeof safeAction.command === "string") {
        session.commands.push({
          command: safeAction.command,
          code: result.code,
          timedOut: result.timedOut,
          stdout: result.stdout,
          stderr: result.stderr,
          createdAt: nowIso(),
        });
      }

      emit("tool:result", { action: safeAction, result });
    } catch (error) {
      const failure = {
        action: safeAction,
        error: error instanceof Error ? error.message : String(error),
      };
      results.push(failure);
      emit("tool:error", failure);
    }
  }
  return results;
}

async function askModel(session, prompt, selectedSkills, toolResults) {
  const workspaceTree = listFilesByPath({ workspacePath: session.workspacePath, target: ".", depth: 3 });
  const messages = [
    {
      role: "system",
      content: buildSystemPrompt(session, selectedSkills),
    },
    {
      role: "user",
      content: [
        `User request:\n${prompt}`,
        "",
        `Current plan:\n${session.plan.map((step, index) => `${index + 1}. ${step}`).join("\n")}`,
        "",
        `Workspace tree snapshot:\n${JSON.stringify(workspaceTree.items, null, 2)}`,
        "",
        `Previous tool results:\n${JSON.stringify(toolResults, null, 2)}`,
      ].join("\n"),
    },
  ];

  return chatJson({ model: session.model, messages });
}

function makeFallbackReply(prompt, session) {
  return [
    "I could not get a structured tool response from the selected Ollama model.",
    "The session is still available and you can retry with another model.",
    `Current request: ${prompt}`,
    `Current workspace: ${session.workspacePath}`,
  ].join(" ");
}

export function listAvailableSkills() {
  return discoverSkills();
}

export function createSession({
  title,
  model = DEFAULT_MODEL,
  projectId = null,
  projectName = "Default",
  workspacePath = DEFAULT_WORKSPACE_PATH,
} = {}) {
  const session = {
    id: crypto.randomUUID(),
    title: title?.trim() || "New coding session",
    model,
    projectId,
    projectName,
    workspacePath,
    createdAt: nowIso(),
    updatedAt: nowIso(),
    messages: [],
    plan: [],
    touchedFiles: [],
    commands: [],
    selectedSkillIds: [],
  };
  sessions.set(session.id, session);
  persistSession(session);
  return session;
}

export function getSession(sessionId) {
  return sessions.get(sessionId) ?? null;
}

export function listSessions() {
  return Array.from(sessions.values()).sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
}

export async function processUserMessage(sessionId, { prompt, model, skillIds = [] }, emit) {
  const session = getSession(sessionId);
  if (!session) {
    throw new Error("Session not found.");
  }

  session.updatedAt = nowIso();
  session.model = model || session.model || DEFAULT_MODEL;
  session.selectedSkillIds = skillIds;
  session.messages.push({
    role: "user",
    content: prompt,
    createdAt: nowIso(),
  });

  const selectedSkills = loadSkills(skillIds);
  session.plan = buildHeuristicPlan(prompt, selectedSkills);
  emit("session:update", session);

  let toolResults = [];
  let finalReply = "";

  for (let cycle = 0; cycle < MAX_AGENT_CYCLES; cycle += 1) {
    emit("agent:status", { phase: "thinking", cycle: cycle + 1 });
    try {
      const response = await askModel(session, prompt, selectedSkills, toolResults);
      const payload = response.json;

      if (Array.isArray(payload.plan) && payload.plan.length > 0) {
        session.plan = payload.plan.slice(0, 8).map((step) => String(step));
        emit("session:update", session);
      }

      const actions = Array.isArray(payload.actions) ? payload.actions : [];
      finalReply = typeof payload.reply === "string" ? payload.reply : finalReply;

      if (actions.length === 0 || payload.done === true) {
        break;
      }

      const results = await executeActions(session, actions, emit);
      toolResults = [...toolResults, ...results];
      session.updatedAt = nowIso();
      persistSession(session);

      if (finalReply && cycle + 1 >= MAX_AGENT_CYCLES) {
        break;
      }
    } catch (error) {
      const errMsg = error instanceof Error ? error.message : String(error);
      finalReply = `⚠️ Agent error: ${errMsg}`;
      emit("agent:error", { message: errMsg });
      break;
    }
  }

  if (!finalReply) {
    finalReply = buildLocalToolSummary(prompt, toolResults, session);
  }

  session.messages.push({
    role: "assistant",
    content: finalReply,
    createdAt: nowIso(),
  });
  session.updatedAt = nowIso();
  persistSession(session);
  emit("assistant:message", {
    content: finalReply,
    session,
  });

  return session;
}

