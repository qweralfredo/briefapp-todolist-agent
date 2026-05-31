import fs from "node:fs";
import path from "node:path";

const GITIGNORE_NODE = [
  "node_modules/",
  "dist/",
  ".env",
  "*.log",
  "coverage/",
  ".DS_Store",
].join("\n");

const GITIGNORE_PYTHON = [
  "__pycache__/",
  "*.pyc",
  ".venv/",
  "venv/",
  "dist/",
  "*.egg-info/",
  ".env",
  "*.log",
  ".DS_Store",
].join("\n");

const GITIGNORE_DOTNET = [
  "bin/",
  "obj/",
  "*.user",
  ".vs/",
  ".env",
  "*.log",
  ".DS_Store",
].join("\n");

const GITIGNORE_GENERIC = [
  ".env",
  "*.log",
  "dist/",
  "build/",
  ".DS_Store",
].join("\n");

function detectStack(workspacePath) {
  const entries = fs.existsSync(workspacePath)
    ? fs.readdirSync(workspacePath).map((f) => f.toLowerCase())
    : [];

  if (entries.includes("package.json")) return "node";
  if (entries.includes("requirements.txt") || entries.includes("pyproject.toml") || entries.includes("setup.py")) {
    return "python";
  }
  if (entries.some((f) => f.endsWith(".csproj") || f.endsWith(".sln") || f.endsWith(".slnx"))) {
    return "dotnet";
  }
  if (entries.includes("go.mod")) return "go";
  if (entries.includes("cargo.toml")) return "rust";
  return "generic";
}

function scaffoldForStack(stack) {
  switch (stack) {
    case "node":
      return {
        dirs: ["src", "tests", "docs"],
        gitignore: GITIGNORE_NODE,
      };
    case "python":
      return {
        dirs: ["src", "tests", "docs"],
        gitignore: GITIGNORE_PYTHON,
      };
    case "dotnet":
      return {
        dirs: ["src", "tests", "docs"],
        gitignore: GITIGNORE_DOTNET,
      };
    default:
      return {
        dirs: ["src", "tests", "docs"],
        gitignore: GITIGNORE_GENERIC,
      };
  }
}

export function scaffoldWorkspace(workspacePath) {
  fs.mkdirSync(workspacePath, { recursive: true });

  const stack = detectStack(workspacePath);
  const { dirs, gitignore } = scaffoldForStack(stack);

  const created = [];

  for (const dir of dirs) {
    const dirPath = path.join(workspacePath, dir);
    if (!fs.existsSync(dirPath)) {
      fs.mkdirSync(dirPath, { recursive: true });
      created.push(dir + "/");
    }
  }

  const gitignorePath = path.join(workspacePath, ".gitignore");
  if (!fs.existsSync(gitignorePath)) {
    fs.writeFileSync(gitignorePath, gitignore, "utf8");
    created.push(".gitignore");
  }

  return { stack, created };
}
