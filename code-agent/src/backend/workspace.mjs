import fs from "node:fs";
import path from "node:path";
import { spawn } from "node:child_process";
import {
  COMMAND_OUTPUT_LIMIT,
  DEFAULT_WORKSPACE_NAME,
  DEFAULT_WORKSPACE_PATH,
  FILE_PREVIEW_LIMIT,
  TREE_ITEM_LIMIT,
  WORKSPACE_ROOT,
} from "./config.mjs";

// ByPath variants — used by project-aware sessions
export function listFilesByPath({ workspacePath, target = ".", depth = 3 }) {
  fs.mkdirSync(workspacePath, { recursive: true });
  const resolved = resolveTargetRaw(workspacePath, target);
  const items = [];
  buildTree(workspacePath, resolved, depth, items);
  return { workspacePath, target, items };
}

export function readFileByPath({ workspacePath, target }) {
  fs.mkdirSync(workspacePath, { recursive: true });
  const resolved = resolveTargetRaw(workspacePath, target);
  const content = fs.readFileSync(resolved, "utf8");
  return {
    path: target,
    content: content.slice(0, FILE_PREVIEW_LIMIT),
    truncated: content.length > FILE_PREVIEW_LIMIT,
  };
}

export function writeFileByPath({ workspacePath, target, content }) {
  fs.mkdirSync(workspacePath, { recursive: true });
  const resolved = resolveTargetRaw(workspacePath, target);
  fs.mkdirSync(path.dirname(resolved), { recursive: true });
  fs.writeFileSync(resolved, content, "utf8");
  return { path: target, bytesWritten: Buffer.byteLength(content, "utf8") };
}

export async function runCommandByPath({ workspacePath, command, timeoutMs = 15000 }) {
  fs.mkdirSync(workspacePath, { recursive: true });
  return runCommandInDir(workspacePath, command, timeoutMs);
}

export async function executeToolActionByPath(action, workspacePath) {
  switch (action.type) {
    case "list_files":
      return listFilesByPath({ workspacePath, target: action.path ?? ".", depth: action.depth ?? 3 });
    case "read_file":
      return readFileByPath({ workspacePath, target: action.path });
    case "write_file":
      return writeFileByPath({ workspacePath, target: action.path, content: action.content ?? "" });
    case "run_command":
      return runCommandByPath({ workspacePath, command: action.command ?? "" });
    default:
      throw new Error(`Unsupported action type: ${action.type}`);
  }
}

export function getWorkspaceSummaryByPath(workspacePath) {
  return { workspacePath, root: WORKSPACE_ROOT };
}

function resolveTargetRaw(workspacePath, relativeTarget = ".") {
  const fullPath = path.resolve(workspacePath, relativeTarget);
  const relative = path.relative(workspacePath, fullPath);
  if (relative.startsWith("..") || path.isAbsolute(relative)) {
    throw new Error("Target path is outside the allowed workspace.");
  }
  return fullPath;
}

async function runCommandInDir(cwd, command, timeoutMs) {
  return new Promise((resolve) => {
    const child = spawn("cmd.exe", ["/d", "/s", "/c", command], {
      cwd,
      env: process.env,
      windowsHide: true,
    });

    let stdout = "";
    let stderr = "";
    let killedByTimeout = false;

    const timer = setTimeout(() => {
      killedByTimeout = true;
      child.kill();
    }, timeoutMs);

    child.stdout.on("data", (chunk) => { stdout += chunk.toString(); });
    child.stderr.on("data", (chunk) => { stderr += chunk.toString(); });

    child.on("close", (code) => {
      clearTimeout(timer);
      resolve({
        command,
        code: killedByTimeout ? -1 : code ?? 0,
        timedOut: killedByTimeout,
        stdout: stdout.slice(0, COMMAND_OUTPUT_LIMIT),
        stderr: stderr.slice(0, COMMAND_OUTPUT_LIMIT),
      });
    });
  });
}

const IGNORED_NAMES = new Set(["node_modules", ".git", ".DS_Store"]);

function ensureWorkspace(name = DEFAULT_WORKSPACE_NAME) {
  const safeName = path.basename(name);
  const workspacePath = path.join(WORKSPACE_ROOT, safeName);
  fs.mkdirSync(workspacePath, { recursive: true });
  return workspacePath;
}

export function resolveWorkspacePath(name = DEFAULT_WORKSPACE_NAME) {
  return ensureWorkspace(name);
}

function resolveTarget(workspacePath, relativeTarget = ".") {
  const fullPath = path.resolve(workspacePath, relativeTarget);
  const relative = path.relative(workspacePath, fullPath);
  if (relative.startsWith("..") || path.isAbsolute(relative)) {
    throw new Error("Target path is outside the allowed workspace.");
  }
  return fullPath;
}

function buildTree(rootPath, currentPath, depth, items) {
  if (items.length >= TREE_ITEM_LIMIT || depth < 0) {
    return;
  }

  const entries = fs.readdirSync(currentPath, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name));
  for (const entry of entries) {
    if (IGNORED_NAMES.has(entry.name)) {
      continue;
    }

    const fullPath = path.join(currentPath, entry.name);
    const relativePath = path.relative(rootPath, fullPath) || ".";
    items.push({
      path: relativePath.replaceAll("\\", "/"),
      type: entry.isDirectory() ? "directory" : "file",
    });

    if (items.length >= TREE_ITEM_LIMIT) {
      return;
    }

    if (entry.isDirectory()) {
      buildTree(rootPath, fullPath, depth - 1, items);
      if (items.length >= TREE_ITEM_LIMIT) {
        return;
      }
    }
  }
}

export function listFiles({ workspaceName, target = ".", depth = 3 } = {}) {
  const workspacePath = ensureWorkspace(workspaceName);
  const resolved = resolveTarget(workspacePath, target);
  const items = [];
  buildTree(workspacePath, resolved, depth, items);
  return {
    workspacePath,
    target,
    items,
  };
}

export function readFile({ workspaceName, target }) {
  const workspacePath = ensureWorkspace(workspaceName);
  const resolved = resolveTarget(workspacePath, target);
  const content = fs.readFileSync(resolved, "utf8");
  return {
    path: target,
    content: content.slice(0, FILE_PREVIEW_LIMIT),
    truncated: content.length > FILE_PREVIEW_LIMIT,
  };
}

export function writeFile({ workspaceName, target, content }) {
  const workspacePath = ensureWorkspace(workspaceName);
  const resolved = resolveTarget(workspacePath, target);
  fs.mkdirSync(path.dirname(resolved), { recursive: true });
  fs.writeFileSync(resolved, content, "utf8");
  return {
    path: target,
    bytesWritten: Buffer.byteLength(content, "utf8"),
  };
}

export async function runCommand({ workspaceName, command, timeoutMs = 15000 }) {
  const workspacePath = ensureWorkspace(workspaceName);

  return new Promise((resolve) => {
    const child = spawn("cmd.exe", ["/d", "/s", "/c", command], {
      cwd: workspacePath,
      env: process.env,
      windowsHide: true,
    });

    let stdout = "";
    let stderr = "";
    let killedByTimeout = false;

    const timer = setTimeout(() => {
      killedByTimeout = true;
      child.kill();
    }, timeoutMs);

    child.stdout.on("data", (chunk) => {
      stdout += chunk.toString();
    });

    child.stderr.on("data", (chunk) => {
      stderr += chunk.toString();
    });

    child.on("close", (code) => {
      clearTimeout(timer);
      resolve({
        command,
        code: killedByTimeout ? -1 : code ?? 0,
        timedOut: killedByTimeout,
        stdout: stdout.slice(0, COMMAND_OUTPUT_LIMIT),
        stderr: stderr.slice(0, COMMAND_OUTPUT_LIMIT),
      });
    });
  });
}

export async function executeToolAction(action, workspaceName) {
  switch (action.type) {
    case "list_files":
      return listFiles({ workspaceName, target: action.path ?? ".", depth: action.depth ?? 3 });
    case "read_file":
      return readFile({ workspaceName, target: action.path });
    case "write_file":
      return writeFile({ workspaceName, target: action.path, content: action.content ?? "" });
    case "run_command":
      return runCommand({ workspaceName, command: action.command ?? "" });
    default:
      throw new Error(`Unsupported action type: ${action.type}`);
  }
}

export function createWorkspaceBootstrapFile() {
  const bootstrapPath = path.join(DEFAULT_WORKSPACE_PATH, "README.md");
  if (!fs.existsSync(bootstrapPath)) {
    fs.writeFileSync(
      bootstrapPath,
      [
        "# Code Agent Workspace",
        "",
        "This is the only area that the runtime agent is allowed to read, edit and execute commands in.",
        "",
        "Create test files here when using the web UI.",
      ].join("\n"),
      "utf8",
    );
  }
}

export function getWorkspaceSummary(workspaceName) {
  const workspacePath = ensureWorkspace(workspaceName);
  return {
    workspaceName: workspaceName ?? DEFAULT_WORKSPACE_NAME,
    workspacePath,
    root: WORKSPACE_ROOT,
  };
}

export { resolveTarget };
