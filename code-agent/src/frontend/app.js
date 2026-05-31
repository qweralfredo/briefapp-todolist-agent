const state = {
  sessions: [],
  activeSessionId: null,
  models: [],
  skills: [],
  selectedSkillIds: new Set(),
  eventSource: null,
};

const els = {
  newSession: document.querySelector("#new-session"),
  sessionList: document.querySelector("#session-list"),
  workspaceSummary: document.querySelector("#workspace-summary"),
  sessionTitle: document.querySelector("#session-title"),
  sessionMeta: document.querySelector("#session-meta"),
  modelSelect: document.querySelector("#model-select"),
  conversation: document.querySelector("#conversation"),
  composer: document.querySelector("#composer"),
  promptInput: document.querySelector("#prompt-input"),
  planList: document.querySelector("#plan-list"),
  filesList: document.querySelector("#files-list"),
  commandsList: document.querySelector("#commands-list"),
  eventsFeed: document.querySelector("#events-feed"),
  skillsGrid: document.querySelector("#skills-grid"),
  skillPills: document.querySelector("#skill-pills"),
};

async function getJson(url, init) {
  const response = await fetch(url, init);
  if (!response.ok) {
    const error = await response.json().catch(() => ({ error: response.statusText }));
    throw new Error(error.error || "Request failed");
  }
  return response.json();
}

function pushEvent(type, payload) {
  const entry = document.createElement("div");
  entry.className = "event-entry";
  entry.innerHTML = `
    <strong>${type}</strong>
    <pre>${JSON.stringify(payload, null, 2)}</pre>
  `;
  els.eventsFeed.prepend(entry);
}

function renderWorkspaceSummary() {
  els.workspaceSummary.innerHTML = `
    <p>Sandbox root</p>
    <code>runtime/workspace/default</code>
    <p class="muted">The runtime agent cannot escape this area.</p>
  `;
}

function renderModels() {
  const current = els.modelSelect.value;
  els.modelSelect.innerHTML = "";
  for (const model of state.models) {
    const option = document.createElement("option");
    option.value = model.name;
    option.textContent = model.name;
    if (model.name === current) {
      option.selected = true;
    }
    els.modelSelect.append(option);
  }
}

function renderSessionList() {
  els.sessionList.innerHTML = "";
  for (const session of state.sessions) {
    const button = document.createElement("button");
    button.className = `session-chip ${session.id === state.activeSessionId ? "active" : ""}`;
    button.textContent = session.title;
    button.onclick = () => selectSession(session.id);
    els.sessionList.append(button);
  }
}

function renderSkills() {
  els.skillsGrid.innerHTML = "";
  els.skillPills.innerHTML = "";

  for (const skill of state.skills) {
    const active = state.selectedSkillIds.has(skill.id);
    const card = document.createElement("button");
    card.className = `skill-card ${active ? "active" : ""}`;
    card.innerHTML = `
      <strong>${skill.name}</strong>
      <p>${skill.description}</p>
    `;
    card.onclick = () => {
      if (state.selectedSkillIds.has(skill.id)) {
        state.selectedSkillIds.delete(skill.id);
      } else {
        state.selectedSkillIds.add(skill.id);
      }
      renderSkills();
    };
    els.skillsGrid.append(card);

    if (active) {
      const pill = document.createElement("span");
      pill.className = "pill";
      pill.textContent = skill.name;
      els.skillPills.append(pill);
    }
  }
}

function renderConversation(session) {
  els.conversation.innerHTML = "";
  if (!session) {
    els.conversation.innerHTML = `<p class="muted">No session selected.</p>`;
    return;
  }

  for (const message of session.messages ?? []) {
    const block = document.createElement("article");
    block.className = `message ${message.role}`;
    block.innerHTML = `
      <header>${message.role}</header>
      <pre>${message.content}</pre>
    `;
    els.conversation.append(block);
  }
  els.conversation.scrollTop = els.conversation.scrollHeight;
}

function renderPlan(session) {
  els.planList.innerHTML = "";
  for (const step of session?.plan ?? []) {
    const item = document.createElement("li");
    item.textContent = step;
    els.planList.append(item);
  }
}

function renderFiles(session) {
  els.filesList.innerHTML = "";
  for (const file of session?.touchedFiles ?? []) {
    const item = document.createElement("li");
    item.textContent = file;
    els.filesList.append(item);
  }
}

function renderCommands(session) {
  els.commandsList.innerHTML = "";
  for (const command of session?.commands ?? []) {
    const item = document.createElement("li");
    item.innerHTML = `
      <strong>${command.command}</strong>
      <span>exit ${command.code}${command.timedOut ? " • timeout" : ""}</span>
      <pre>${[command.stdout, command.stderr].filter(Boolean).join("\n")}</pre>
    `;
    els.commandsList.append(item);
  }
}

function renderSession(session) {
  els.sessionTitle.textContent = session?.title ?? "No session selected";
  els.sessionMeta.textContent = session
    ? `Workspace: ${session.workspaceName} • Model: ${session.model}`
    : "Create a session to start coding inside the sandbox workspace.";
  renderConversation(session);
  renderPlan(session);
  renderFiles(session);
  renderCommands(session);
}

async function loadSessions() {
  const payload = await getJson("/api/sessions");
  state.sessions = payload.sessions;
  renderSessionList();
}

async function loadSkills() {
  const payload = await getJson("/api/skills");
  state.skills = payload.skills;
  renderSkills();
}

async function loadModels() {
  try {
    const payload = await getJson("/api/models");
    state.models = payload.models;
  } catch (error) {
    state.models = [{ name: "qwen2.5-coder:7b" }];
    pushEvent("models:error", { message: error.message });
  }
  renderModels();
}

async function selectSession(sessionId) {
  state.activeSessionId = sessionId;
  renderSessionList();

  const payload = await getJson(`/api/sessions/${sessionId}`);
  renderSession(payload.session);

  if (state.eventSource) {
    state.eventSource.close();
  }

  state.eventSource = new EventSource(`/api/sessions/${sessionId}/events`);
  const eventTypes = ["ready", "session:update", "agent:status", "tool:start", "tool:result", "tool:error", "agent:error", "assistant:message"];
  for (const type of eventTypes) {
    state.eventSource.addEventListener(type, (event) => {
      const payloadData = JSON.parse(event.data);
      pushEvent(type, payloadData);
      if (payloadData.session) {
        renderSession(payloadData.session);
      }
      if (type === "session:update") {
        renderSession(payloadData);
      }
      loadSessions().catch(() => {});
    });
  }
}

async function createSession() {
  const title = prompt("Session title", "Code Agent Session") || "Code Agent Session";
  const payload = await getJson("/api/sessions", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      title,
      model: els.modelSelect.value || state.models[0]?.name,
      workspaceName: "default",
    }),
  });
  await loadSessions();
  await selectSession(payload.session.id);
}

async function sendPrompt(event) {
  event.preventDefault();
  if (!state.activeSessionId) {
    await createSession();
  }

  const promptValue = els.promptInput.value.trim();
  if (!promptValue) {
    return;
  }

  const skillIds = Array.from(state.selectedSkillIds);
  pushEvent("user:prompt", { prompt: promptValue, skillIds });
  els.promptInput.value = "";

  await getJson(`/api/sessions/${state.activeSessionId}/messages`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      prompt: promptValue,
      model: els.modelSelect.value,
      skillIds,
    }),
  });

  await loadSessions();
}

async function boot() {
  renderWorkspaceSummary();
  await Promise.all([loadModels(), loadSkills(), loadSessions()]);
  if (state.sessions.length > 0) {
    await selectSession(state.sessions[0].id);
  }
}

els.newSession.addEventListener("click", createSession);
els.composer.addEventListener("submit", sendPrompt);

boot().catch((error) => pushEvent("boot:error", { message: error.message }));
