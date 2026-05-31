import fs from "node:fs";
import path from "node:path";
import { FILE_PREVIEW_LIMIT, SKILLS_DIR } from "./config.mjs";

function parseFrontmatter(raw) {
  const match = raw.match(/^---\n([\s\S]*?)\n---\n?([\s\S]*)$/);
  if (!match) {
    return { attributes: {}, body: raw.trim() };
  }

  const [, yamlBlock, body] = match;
  const attributes = {};
  for (const line of yamlBlock.split(/\r?\n/)) {
    const [key, ...rest] = line.split(":");
    if (!key || rest.length === 0) {
      continue;
    }
    attributes[key.trim()] = rest.join(":").trim().replace(/^"|"$/g, "");
  }

  return { attributes, body: body.trim() };
}

function readReferenceFiles(skillRoot) {
  const referencesDir = path.join(skillRoot, "references");
  if (!fs.existsSync(referencesDir)) {
    return [];
  }

  return fs
    .readdirSync(referencesDir, { withFileTypes: true })
    .filter((entry) => entry.isFile())
    .map((entry) => {
      const fullPath = path.join(referencesDir, entry.name);
      const content = fs.readFileSync(fullPath, "utf8").slice(0, FILE_PREVIEW_LIMIT);
      return {
        name: entry.name,
        path: fullPath,
        content,
      };
    });
}

export function discoverSkills() {
  if (!fs.existsSync(SKILLS_DIR)) {
    return [];
  }

  return fs
    .readdirSync(SKILLS_DIR, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => {
      const skillRoot = path.join(SKILLS_DIR, entry.name);
      const skillFile = path.join(skillRoot, "SKILL.md");
      if (!fs.existsSync(skillFile)) {
        return null;
      }

      const raw = fs.readFileSync(skillFile, "utf8");
      const { attributes, body } = parseFrontmatter(raw);
      return {
        id: entry.name,
        name: attributes.name ?? entry.name,
        description: attributes.description ?? "",
        body,
        references: readReferenceFiles(skillRoot).map((reference) => ({
          name: reference.name,
          path: reference.path,
        })),
      };
    })
    .filter(Boolean)
    .sort((a, b) => a.name.localeCompare(b.name));
}

export function loadSkills(selectedIds = []) {
  const skills = discoverSkills();
  const selected = selectedIds.length > 0
    ? skills.filter((skill) => selectedIds.includes(skill.id) || selectedIds.includes(skill.name))
    : [];

  return selected.map((skill) => {
    const skillRoot = path.join(SKILLS_DIR, skill.id);
    return {
      ...skill,
      referenceContents: readReferenceFiles(skillRoot),
    };
  });
}

export { parseFrontmatter };
