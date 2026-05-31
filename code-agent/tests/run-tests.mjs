import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createSession, getSession, listAvailableSkills } from "../src/backend/agent.mjs";
import { discoverSkills, parseFrontmatter } from "../src/backend/skills.mjs";
import { readFile, resolveTarget, writeFile } from "../src/backend/workspace.mjs";

const results = [];
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const frontendDir = path.join(__dirname, "..", "src", "frontend");

function run(name, fn) {
  try {
    fn();
    results.push({ name, ok: true });
  } catch (error) {
    results.push({ name, ok: false, error });
  }
}

run("parseFrontmatter extracts attributes and body", () => {
  const parsed = parseFrontmatter(`---\nname: demo\ndescription: sample\n---\n\n# Body`);
  assert.equal(parsed.attributes.name, "demo");
  assert.equal(parsed.attributes.description, "sample");
  assert.match(parsed.body, /# Body/);
});

run("discoverSkills returns bundled local skills", () => {
  const skills = discoverSkills();
  const names = skills.map((skill) => skill.name);
  assert.ok(names.includes("core-coding"));
  assert.ok(names.includes("web-ui"));
});

run("workspace write and read stay inside the sandbox", () => {
  const writeResult = writeFile({
    workspaceName: "test-sandbox",
    target: "notes/todo.txt",
    content: "sandboxed content",
  });
  assert.equal(writeResult.path, "notes/todo.txt");
  const readResult = readFile({
    workspaceName: "test-sandbox",
    target: "notes/todo.txt",
  });
  assert.equal(readResult.content, "sandboxed content");
});

run("resolveTarget rejects traversal outside workspace", () => {
  assert.throws(
    () => resolveTarget("c:\\safe-root", "..\\escape.txt"),
    /outside the allowed workspace/i,
  );
});

run("createSession initializes an isolated coding session", () => {
  const session = createSession({
    title: "Unit Test Session",
    model: "demo-model",
    workspaceName: "default",
  });
  const loaded = getSession(session.id);
  assert.equal(loaded.title, "Unit Test Session");
  assert.equal(loaded.model, "demo-model");
  assert.deepEqual(loaded.plan, []);
});

run("listAvailableSkills exposes skill metadata", () => {
  const skills = listAvailableSkills();
  assert.ok(skills.every((skill) => typeof skill.description === "string"));
  assert.ok(skills.length >= 2);
});

run("frontend shell exposes the core operator panels", () => {
  const html = fs.readFileSync(path.join(frontendDir, "index.html"), "utf8");
  for (const marker of [
    'id="new-session"',
    'id="session-list"',
    'id="conversation"',
    'id="prompt-input"',
    'id="plan-list"',
    'id="files-list"',
    'id="commands-list"',
    'id="skills-grid"',
    'id="events-feed"',
  ]) {
    assert.ok(html.includes(marker), `Missing frontend marker: ${marker}`);
  }
});

run("frontend app wires session, streaming and skill behaviors", () => {
  const appJs = fs.readFileSync(path.join(frontendDir, "app.js"), "utf8");
  assert.match(appJs, /new EventSource\(/);
  assert.match(appJs, /state\.selectedSkillIds/);
  assert.match(appJs, /loadModels\(\)/);
  assert.match(appJs, /loadSkills\(\)/);
  assert.match(appJs, /loadSessions\(\)/);
  assert.match(appJs, /"Content-Type": "application\/json"/);
});

const failed = results.filter((result) => !result.ok);
for (const result of results) {
  console.log(`${result.ok ? "PASS" : "FAIL"} ${result.name}`);
  if (!result.ok) {
    console.error(result.error);
  }
}

if (failed.length > 0) {
  process.exit(1);
}
