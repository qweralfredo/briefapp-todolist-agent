import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawn, spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const command = (process.argv[2] || "").trim().toLowerCase();
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectDir = path.resolve(__dirname, "..");
const runtimeDir = path.join(os.tmpdir(), "code-agent-runtime");
const pidFile = path.join(runtimeDir, "server.pid");
const stdoutPath = path.join(os.tmpdir(), "code-agent-daemon-stdout.log");
const stderrPath = path.join(os.tmpdir(), "code-agent-daemon-stderr.log");
const url = "http://127.0.0.1:8787";

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function ensureRuntimeDir() {
  fs.mkdirSync(runtimeDir, { recursive: true });
}

function readPid() {
  if (!fs.existsSync(pidFile)) {
    return null;
  }
  const raw = fs.readFileSync(pidFile, "utf8").trim();
  const value = Number.parseInt(raw, 10);
  return Number.isFinite(value) ? value : null;
}

function writePid(pid) {
  ensureRuntimeDir();
  fs.writeFileSync(pidFile, String(pid), "utf8");
}

function clearPid() {
  if (fs.existsSync(pidFile)) {
    fs.unlinkSync(pidFile);
  }
}

function isPidAlive(pid) {
  if (!pid || pid <= 0) {
    return false;
  }
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

async function healthStatus(timeoutMs = 4000) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const response = await fetch(`${url}/api/health`, {
      method: "GET",
      signal: controller.signal,
    });
    return response.status;
  } catch {
    return 0;
  } finally {
    clearTimeout(timer);
  }
}

async function waitForHealth(maxMs = 10000) {
  const startedAt = Date.now();
  while (Date.now() - startedAt < maxMs) {
    const status = await healthStatus(1500);
    if (status === 200) {
      return true;
    }
    await sleep(400);
  }
  return false;
}

function print(payload) {
  console.log(JSON.stringify(payload, null, 2));
}

async function startServer() {
  const currentHealth = await healthStatus();
  if (currentHealth === 200) {
    clearPid();
    print({
      action: "start",
      started: false,
      reason: "already_running_on_port",
      pid: null,
      url,
      healthStatus: currentHealth,
      stdoutPath,
      stderrPath,
    });
    return;
  }

  const existingPid = readPid();
  if (existingPid && isPidAlive(existingPid)) {
    const status = await healthStatus();
    print({
      action: "start",
      started: false,
      reason: "already_running",
      pid: existingPid,
      url,
      healthStatus: status,
      stdoutPath,
      stderrPath,
    });
    return;
  }

  clearPid();
  const stdoutFd = fs.openSync(stdoutPath, "a");
  const stderrFd = fs.openSync(stderrPath, "a");
  const child = spawn("node", ["src/backend/server.mjs"], {
    cwd: projectDir,
    detached: true,
    stdio: ["ignore", stdoutFd, stderrFd],
    windowsHide: true,
  });
  child.unref();
  writePid(child.pid);

  const ready = await waitForHealth(10000);
  print({
    action: "start",
    started: true,
    pid: child.pid,
    url,
    healthStatus: ready ? 200 : 0,
    stdoutPath,
    stderrPath,
  });
}

async function statusServer() {
  const pid = readPid();
  const pidAlive = pid ? isPidAlive(pid) : false;
  const status = await healthStatus();
  const reachable = status === 200;
  print({
    action: "status",
    pid,
    alive: pidAlive || reachable,
    pidAlive,
    reachable,
    healthStatus: status,
    url,
    stdoutPath,
    stderrPath,
    pidFile,
  });
}

async function stopServer() {
  const pid = readPid();
  if (!pid) {
    const status = await healthStatus();
    print({
      action: "stop",
      stopped: false,
      reason: status === 200 ? "no_pid_file_service_still_running" : "no_pid_file",
      healthStatus: status,
      pidFile,
    });
    return;
  }

  let stopped = false;
  if (process.platform === "win32") {
    const result = spawnSync("taskkill", ["/PID", String(pid), "/T", "/F"], { windowsHide: true });
    stopped = result.status === 0 || !isPidAlive(pid);
  } else {
    try {
      process.kill(pid, "SIGTERM");
      stopped = true;
    } catch {
      stopped = !isPidAlive(pid);
    }
  }

  if (!isPidAlive(pid)) {
    clearPid();
  }

  print({
    action: "stop",
    stopped,
    pid,
    pidFile,
  });
}

async function main() {
  if (command === "start") {
    await startServer();
    return;
  }
  if (command === "status") {
    await statusServer();
    return;
  }
  if (command === "stop") {
    await stopServer();
    return;
  }

  print({
    error: "invalid_command",
    usage: "node scripts/server-control.mjs <start|status|stop>",
  });
  process.exitCode = 1;
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
});
