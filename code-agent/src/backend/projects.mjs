import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { RUNTIME_DIR, WORKSPACE_ROOT } from "./config.mjs";

const PROJECTS_FILE = path.join(RUNTIME_DIR, "projects.json");
const SLUG_PATTERN = /^[a-z0-9][a-z0-9-]{0,61}[a-z0-9]$|^[a-z0-9]$/;

function nowIso() {
  return new Date().toISOString();
}

function loadProjects() {
  if (!fs.existsSync(PROJECTS_FILE)) {
    return [];
  }
  try {
    return JSON.parse(fs.readFileSync(PROJECTS_FILE, "utf8"));
  } catch {
    return [];
  }
}

function saveProjects(projects) {
  fs.mkdirSync(path.dirname(PROJECTS_FILE), { recursive: true });
  fs.writeFileSync(PROJECTS_FILE, JSON.stringify(projects, null, 2), "utf8");
}

function toSlug(name) {
  return name
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 63);
}

export function ensureDefaultProject() {
  const projects = loadProjects();
  const hasDefault = projects.some((p) => p.slug === "default");
  if (!hasDefault) {
    const defaultProject = {
      id: crypto.randomUUID(),
      name: "Default",
      slug: "default",
      description: "Default workspace for sessions without a specific project.",
      createdAt: nowIso(),
      workspacePath: path.join(WORKSPACE_ROOT, "default"),
    };
    fs.mkdirSync(defaultProject.workspacePath, { recursive: true });
    projects.unshift(defaultProject);
    saveProjects(projects);
  }
  return loadProjects();
}

export function listProjects() {
  return loadProjects().sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}

export function getProject(id) {
  return loadProjects().find((p) => p.id === id) ?? null;
}

export function getProjectBySlug(slug) {
  return loadProjects().find((p) => p.slug === slug) ?? null;
}

export function createProject({ name, slug, description = "" }) {
  if (!name || typeof name !== "string" || !name.trim()) {
    throw new Error("Project name is required.");
  }

  const resolvedSlug = slug ? String(slug).trim() : toSlug(name.trim());

  if (!SLUG_PATTERN.test(resolvedSlug)) {
    throw new Error(
      "Slug must be lowercase alphanumeric with hyphens, 1-63 chars, and cannot start or end with a hyphen.",
    );
  }

  const projects = loadProjects();
  if (projects.some((p) => p.slug === resolvedSlug)) {
    throw new Error(`A project with slug "${resolvedSlug}" already exists.`);
  }

  const workspacePath = path.join(WORKSPACE_ROOT, resolvedSlug);
  fs.mkdirSync(workspacePath, { recursive: true });

  const project = {
    id: crypto.randomUUID(),
    name: name.trim(),
    slug: resolvedSlug,
    description: typeof description === "string" ? description.trim() : "",
    createdAt: nowIso(),
    workspacePath,
  };

  projects.push(project);
  saveProjects(projects);
  return project;
}

export function updateProject(id, { name, description }) {
  const projects = loadProjects();
  const index = projects.findIndex((p) => p.id === id);
  if (index === -1) {
    throw new Error("Project not found.");
  }

  if (name !== undefined) {
    if (!name || typeof name !== "string" || !name.trim()) {
      throw new Error("Project name cannot be empty.");
    }
    projects[index].name = name.trim();
  }
  if (description !== undefined) {
    projects[index].description = typeof description === "string" ? description.trim() : "";
  }

  saveProjects(projects);
  return projects[index];
}

export function deleteProject(id) {
  const projects = loadProjects();
  const project = projects.find((p) => p.id === id);
  if (!project) {
    throw new Error("Project not found.");
  }
  if (project.slug === "default") {
    throw new Error("The default project cannot be deleted.");
  }

  const filtered = projects.filter((p) => p.id !== id);
  saveProjects(filtered);
  return project;
}
