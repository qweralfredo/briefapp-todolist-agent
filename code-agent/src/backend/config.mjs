import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export const PROJECT_ROOT = path.resolve(__dirname, "..", "..");
export const RUNTIME_DIR = process.env.CODE_AGENT_RUNTIME_DIR ?? path.join(PROJECT_ROOT, "runtime");
export const SESSION_DIR = path.join(RUNTIME_DIR, "sessions");
export const WORKSPACE_ROOT = path.join(RUNTIME_DIR, "workspace");
export const SKILLS_DIR = path.join(PROJECT_ROOT, "skills");
// In production, serve the Vite build output; fall back to legacy vanilla frontend
const FRONTEND_DIST = path.join(PROJECT_ROOT, "frontend", "dist");
const FRONTEND_LEGACY = path.join(PROJECT_ROOT, "src", "frontend");
export const FRONTEND_DIR = fs.existsSync(FRONTEND_DIST) ? FRONTEND_DIST : FRONTEND_LEGACY;

export const PORT = Number.parseInt(process.env.PORT ?? "8787", 10);
export const OLLAMA_BASE_URL = process.env.OLLAMA_BASE_URL?.trim() || null;
export const OLLAMA_BASE_URL_CANDIDATES = OLLAMA_BASE_URL
  ? [OLLAMA_BASE_URL]
  : [
      "http://127.0.0.1:11434",
      "http://127.0.0.1:9434",
      "http://host.docker.internal:11434",
      "http://host.docker.internal:9434",
    ];
export const DEFAULT_MODEL = process.env.OLLAMA_MODEL ?? "qwen2.5-coder:7b";
export const MAX_AGENT_CYCLES = Number.parseInt(process.env.CODE_AGENT_MAX_CYCLES ?? "3", 10);
export const MAX_ACTIONS = Number.parseInt(process.env.CODE_AGENT_MAX_ACTIONS ?? "6", 10);
export const DEFAULT_WORKSPACE_NAME = "default";
export const DEFAULT_WORKSPACE_PATH = path.join(WORKSPACE_ROOT, DEFAULT_WORKSPACE_NAME);

export const FILE_PREVIEW_LIMIT = 16000;
export const COMMAND_OUTPUT_LIMIT = 12000;
export const TREE_ITEM_LIMIT = 200;

export function getWorkspacePath(slug) {
  return path.join(WORKSPACE_ROOT, slug);
}

export function ensureRuntimeLayout() {
  for (const dir of [RUNTIME_DIR, SESSION_DIR, WORKSPACE_ROOT, DEFAULT_WORKSPACE_PATH, SKILLS_DIR]) {
    fs.mkdirSync(dir, { recursive: true });
  }
}
