import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawn } from "node:child_process";

const projectDir = process.cwd();
const stdoutPath = path.join(os.tmpdir(), "code-agent-live-test-stdout.log");
const stderrPath = path.join(os.tmpdir(), "code-agent-live-test-stderr.log");
const runtimeWorkspace = path.join(os.tmpdir(), "code-agent-runtime", "workspace", "default");

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function requestJson(url, init = {}, timeoutMs = 90000) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const response = await fetch(url, { ...init, signal: controller.signal });
    const text = await response.text();
    if (!response.ok) {
      throw new Error(`${response.status} ${response.statusText}: ${text}`);
    }
    return text ? JSON.parse(text) : {};
  } finally {
    clearTimeout(timer);
  }
}

async function main() {
  fs.mkdirSync(runtimeWorkspace, { recursive: true });
  fs.writeFileSync(path.join(runtimeWorkspace, "README.md"), "# Tool Test Workspace\n\nThis file exists for a live Ollama tool-action smoke test.\n", "utf8");
  fs.writeFileSync(path.join(runtimeWorkspace, "notes.txt"), "original note\n", "utf8");

  const env = {
    ...process.env,
    PORT: process.env.PORT || "8787",
    OLLAMA_MODEL: process.env.OLLAMA_MODEL || "gemma2:2b",
    CODE_AGENT_MAX_CYCLES: process.env.CODE_AGENT_MAX_CYCLES || "2",
    CODE_AGENT_MAX_ACTIONS: process.env.CODE_AGENT_MAX_ACTIONS || "2",
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
    await sleep(2500);

    const models = await requestJson("http://127.0.0.1:8787/api/models", {}, 30000);
    const projectsPayload = await requestJson("http://127.0.0.1:8787/api/projects", {}, 5000);
    const defaultProject = (projectsPayload.projects || []).find((p) => p.slug === "default");
    if (!defaultProject) throw new Error("Default project not found.");
    const sessionPayload = await requestJson(
      "http://127.0.0.1:8787/api/sessions",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          title: "Live tool-action smoke test",
          model: env.OLLAMA_MODEL,
          projectId: defaultProject.id,
        }),
      },
      30000,
    );

    const messagePayload = await requestJson(
      `http://127.0.0.1:8787/api/sessions/${sessionPayload.session.id}/messages`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          prompt: "Use the available tools to read README.md in the sandbox workspace, then create a file named report.txt containing exactly the line TOOL_ACTION_CONFIRMED. After the write succeeds, tell the operator what you found.",
          model: env.OLLAMA_MODEL,
          skillIds: ["core-coding"],
        }),
      },
      180000,
    );

    const reportPath = path.join(runtimeWorkspace, "report.txt");
    const reportExists = fs.existsSync(reportPath);
    const reportContent = reportExists ? fs.readFileSync(reportPath, "utf8") : null;

    const output = {
      models: models.models,
      sessionId: sessionPayload.session.id,
      assistantMessage: messagePayload.session.messages.at(-1)?.content ?? "",
      touchedFiles: messagePayload.session.touchedFiles,
      commands: messagePayload.session.commands,
      reportExists,
      reportContent,
      stdoutPath,
      stderrPath,
    };

    console.log(JSON.stringify(output, null, 2));

    if (!reportExists || reportContent.trim() !== "TOOL_ACTION_CONFIRMED") {
      process.exitCode = 1;
    }
  } finally {
    child.kill();
    fs.closeSync(stdout);
    fs.closeSync(stderr);
  }
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
