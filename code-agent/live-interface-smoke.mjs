import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawn } from "node:child_process";

const projectDir = process.cwd();
const stdoutPath = path.join(os.tmpdir(), "code-agent-live-interface-stdout.log");
const stderrPath = path.join(os.tmpdir(), "code-agent-live-interface-stderr.log");
const baseUrl = "http://127.0.0.1:8787";
const chatTimeoutMs = Number.parseInt(process.env.LIVE_CHAT_TIMEOUT_MS || "90000", 10);

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function request(url, init = {}, timeoutMs = 15000) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const response = await fetch(url, { ...init, signal: controller.signal });
    const text = await response.text();
    return { ok: response.ok, status: response.status, text };
  } finally {
    clearTimeout(timer);
  }
}

async function waitForHealth(retries = 20) {
  for (let attempt = 1; attempt <= retries; attempt += 1) {
    try {
      const health = await request(`${baseUrl}/api/health`, {}, 4000);
      if (health.ok) {
        return;
      }
    } catch {
      // Server can still be booting.
    }
    await sleep(500);
  }
  throw new Error("Server did not become healthy in time.");
}

function parseJson(text) {
  try {
    return text ? JSON.parse(text) : {};
  } catch (error) {
    throw new Error(`Invalid JSON response: ${error instanceof Error ? error.message : String(error)}\n${text}`);
  }
}

async function main() {
  const env = {
    ...process.env,
    PORT: process.env.PORT || "8787",
    CODE_AGENT_MAX_CYCLES: process.env.CODE_AGENT_MAX_CYCLES || "1",
    CODE_AGENT_MAX_ACTIONS: process.env.CODE_AGENT_MAX_ACTIONS || "1",
  };

  const stdout = fs.openSync(stdoutPath, "w");
  const stderr = fs.openSync(stderrPath, "w");
  const child = spawn("node", ["src/backend/server.mjs"], {
    cwd: projectDir,
    env,
    stdio: ["ignore", stdout, stderr],
    windowsHide: true,
  });

  try {
    await waitForHealth();

    const page = await request(`${baseUrl}/`, {}, 8000);
    if (!page.ok || !page.text.includes("<title>Code Agent</title>")) {
      throw new Error(`Frontend shell did not load correctly: HTTP ${page.status}`);
    }

    const modelsResponse = await request(`${baseUrl}/api/models`, {}, 12000);
    if (!modelsResponse.ok) {
      throw new Error(`Model discovery failed: HTTP ${modelsResponse.status} ${modelsResponse.text}`);
    }

    const modelsPayload = parseJson(modelsResponse.text);
    const models = Array.isArray(modelsPayload.models) ? modelsPayload.models : [];
    if (models.length === 0) {
      throw new Error("No Ollama models available.");
    }

    const chosenModel = (process.env.OLLAMA_MODEL || "").trim() || String(models[0].name);

    // Get default project id for session creation
    const projectsRes = await request(`${baseUrl}/api/projects`, {}, 5000);
    const projectsPayload = parseJson(projectsRes.text);
    const defaultProject = (projectsPayload.projects || []).find((p) => p.slug === "default");
    if (!defaultProject) {
      throw new Error("Default project not found in /api/projects.");
    }

    const createSession = await request(
      `${baseUrl}/api/sessions`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          title: "Fast interface smoke test",
          model: chosenModel,
          projectId: defaultProject.id,
        }),
      },
      12000,
    );
    if (!createSession.ok) {
      throw new Error(`Session creation failed: HTTP ${createSession.status} ${createSession.text}`);
    }

    const sessionPayload = parseJson(createSession.text);
    const sessionId = sessionPayload.session?.id;
    if (!sessionId) {
      throw new Error("Session API returned no session id.");
    }

    let assistantMessage = "";
    const runChatStep = process.env.LIVE_INCLUDE_CHAT === "1";
    if (runChatStep) {
      const messageResponse = await request(
        `${baseUrl}/api/sessions/${sessionId}/messages`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            prompt: "Respond in one short sentence confirming the workspace is isolated.",
            model: chosenModel,
            skillIds: [],
          }),
        },
        chatTimeoutMs,
      );
      if (!messageResponse.ok) {
        throw new Error(`Message processing failed: HTTP ${messageResponse.status} ${messageResponse.text}`);
      }

      const messagePayload = parseJson(messageResponse.text);
      assistantMessage = messagePayload.session?.messages?.at(-1)?.content ?? "";
    }

    const output = {
      status: "ok",
      modelCount: models.length,
      chosenModel,
      sessionId,
      runChatStep,
      chatTimeoutMs,
      assistantMessage,
      stdoutPath,
      stderrPath,
    };
    console.log(JSON.stringify(output, null, 2));
  } finally {
    child.kill();
    fs.closeSync(stdout);
    fs.closeSync(stderr);
  }
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
});
